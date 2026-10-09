using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IfcHopper.Core.IO;

namespace IfcHopper.Core.Model
{
    /// <summary>What <see cref="ModelSearch.Find"/> looks for; empty criteria match everything. All given criteria must match.</summary>
    public sealed class ObjectQuery
    {
        /// <summary>IFC classes, e.g. IfcWall or IfcBuiltElement; an object matches one of them. Subclasses match unless <see cref="ExactClass"/>.</summary>
        public List<string> Classes { get; } = new List<string>();

        public bool ExactClass { get; set; }

        /// <summary>Name pattern with * and ? wildcards, ignoring case; null matches every name.</summary>
        public string Name { get; set; }

        /// <summary>GlobalIds (22 characters or GUIDs); an object matches one of them.</summary>
        public List<string> GlobalIds { get; } = new List<string>();

        /// <summary>Property and quantity filters; an object matches all of them.</summary>
        public List<PropertyFilter> Properties { get; } = new List<PropertyFilter>();

        /// <summary>Converts a number of a filter to SI for a measure dimension (1 length, 2 area, 3 volume), e.g. from document units.</summary>
        public Func<double, int, double> NumberToSi { get; set; } = (value, dimension) => value;
    }

    /// <summary>
    /// A filter on a property or quantity, written "Set.Property" (the property exists) or "Set.Property op value" with op one of
    /// =, !=, &lt;, &lt;=, &gt;, &gt;=, e.g. "Pset_WallCommon.IsExternal = true" or "Qto_WallBaseQuantities.Length &gt; 5".
    /// Names ignore case; sets of the object's type count.
    /// </summary>
    public sealed class PropertyFilter
    {
        public string SetName { get; }
        public string PropertyName { get; }

        /// <summary>Comparison operator; null when the filter only asks for the property.</summary>
        public string Operator { get; }

        public string Value { get; }

        private static readonly Regex Syntax = new Regex(@"^\s*(?<set>[^.=!<>]+)\.(?<property>[^=!<>]+?)\s*(?:(?<op>!=|<=|>=|=|<|>)\s*(?<value>.*?))?\s*$");

        public PropertyFilter(string setName, string propertyName, string op = null, string value = null)
        {
            SetName = setName.Trim();
            PropertyName = propertyName.Trim();
            Operator = op;
            Value = value?.Trim();
        }

        /// <summary>Parses "Set.Property [op value]"; throws ArgumentException for other text.</summary>
        public static PropertyFilter Parse(string text)
        {
            var match = Syntax.Match(text ?? "");
            if (!match.Success)
                throw new ArgumentException($"'{text}' is not a property filter: write Set.Property, or Set.Property = value (also !=, <, <=, >, >=).");
            return new PropertyFilter(match.Groups["set"].Value, match.Groups["property"].Value,
                match.Groups["op"].Success ? match.Groups["op"].Value : null, match.Groups["value"].Success ? match.Groups["value"].Value : null);
        }

        internal bool Matches(ModelObject obj, Func<double, int, double> numberToSi)
        {
            foreach (var set in obj.PropertySets.Where(s => string.Equals(s.Name, SetName, StringComparison.OrdinalIgnoreCase)))
            {
                switch (set)
                {
                    case PropertySet properties:
                        foreach (var property in properties.Properties.Where(p => string.Equals(p.Name, PropertyName, StringComparison.OrdinalIgnoreCase)))
                            if (Compare(property.Value, Units.MeasureDimension(property.ValueType ?? ""), numberToSi)) return true;
                        break;
                    case QuantitySet quantities:
                        foreach (var quantity in quantities.Quantities.Where(q => string.Equals(q.Name, PropertyName, StringComparison.OrdinalIgnoreCase)))
                            if (Compare(quantity.Value, Quantity.Dimension(quantity.Kind), numberToSi)) return true;
                        break;
                }
            }
            return false;
        }

        /// <summary>Compares a value (or any item of a list value) with the filter: numbers numerically, booleans and text ignoring case.</summary>
        private bool Compare(object value, int dimension, Func<double, int, double> numberToSi)
        {
            if (Operator == null) return true;
            if (value is IEnumerable<object> items && !(value is string)) return items.Any(i => Compare(i, dimension, numberToSi));

            int? order = null;
            if (IsNumber(value) && double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                var wanted = dimension == 0 ? number : numberToSi(number, dimension);
                var actual = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                order = Math.Abs(actual - wanted) <= 1e-9 * Math.Max(1, Math.Abs(wanted)) ? 0 : actual.CompareTo(wanted);
            }
            else if (value is bool flag && bool.TryParse(Value, out var wantedFlag)) order = flag == wantedFlag ? 0 : 1;
            else if (value != null) order = string.Compare(Property.ValueText(value), Value, StringComparison.OrdinalIgnoreCase);

            switch (Operator)
            {
                case "=": return order == 0;
                case "!=": return order != 0;
                case "<": return order < 0;
                case "<=": return order <= 0;
                case ">": return order > 0;
                case ">=": return order >= 0;
                default: return false;
            }
        }

        private static bool IsNumber(object value) => value is double || value is long || value is int || value is float;

        public override string ToString() => Operator == null ? $"{SetName}.{PropertyName}" : $"{SetName}.{PropertyName} {Operator} {Value}";
    }

    /// <summary>An object found by <see cref="ModelSearch.Find"/>, with the objects above it from the search root down to its parent.</summary>
    public sealed class SearchResult
    {
        public ModelObject Object { get; }
        public IReadOnlyList<ModelObject> Ancestors { get; }

        /// <summary>The direct parent (a spatial object, the element of a part or the host of an opening); null for the search root.</summary>
        public ModelObject Parent => Ancestors.Count == 0 ? null : Ancestors[Ancestors.Count - 1];

        /// <summary>Names of the ancestors, e.g. "Project / Site / House / Ground floor".</summary>
        public string Path => string.Join(" / ", Ancestors.Select(ModelSearch.Label));

        internal SearchResult(ModelObject obj, IReadOnlyList<ModelObject> ancestors)
        {
            Object = obj;
            Ancestors = ancestors;
        }
    }

    /// <summary>Searches and summarises a model tree. Objects read from a file are loaded as the walk reaches them (not their geometry).</summary>
    public static class ModelSearch
    {
        /// <summary>
        /// The objects matching <paramref name="query"/> in <paramref name="root"/> and below, depth first in child order: spatial objects,
        /// elements, element parts and openings. Throws ArgumentException for class names the schema does not know.
        /// </summary>
        public static IEnumerable<SearchResult> Find(ModelObject root, ObjectQuery query)
        {
            var classes = query.Classes.Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => IfcBackend.Schema.FindClass(c) ?? throw new ArgumentException($"'{c.Trim()}' is not an IFC class.")).ToList();
            var ids = new HashSet<string>(query.GlobalIds.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i =>
                Model.GlobalIds.TryParse(i, out var id) ? id : throw new ArgumentException($"'{i.Trim()}' is not a valid GlobalId (22 IFC characters or a GUID).")));
            var name = string.IsNullOrWhiteSpace(query.Name) ? null
                : new Regex("^" + Regex.Escape(query.Name.Trim()).Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase);

            bool Matches(ModelObject obj)
            {
                var ifcClass = SpatialRules.IfcClassOf(obj);
                if (classes.Count > 0 && !classes.Any(c => query.ExactClass ? string.Equals(ifcClass, c, StringComparison.OrdinalIgnoreCase) : IfcBackend.Schema.IsClassOf(ifcClass, c)))
                    return false;
                if (ids.Count > 0 && (obj.GlobalId == null || !ids.Contains(obj.GlobalId))) return false;
                if (name != null && !name.IsMatch(obj.Name ?? "")) return false;
                return query.Properties.All(p => p.Matches(obj, query.NumberToSi));
            }

            return Walk(root, new List<ModelObject>()).Where(r => Matches(r.Object));
        }

        private static IEnumerable<SearchResult> Walk(ModelObject obj, List<ModelObject> ancestors)
        {
            yield return new SearchResult(obj, ancestors.ToList());
            ancestors.Add(obj);
            foreach (var child in Below(obj))
                foreach (var result in Walk(child, ancestors)) yield return result;
            ancestors.RemoveAt(ancestors.Count - 1);
        }

        /// <summary>Children, then the openings of an element.</summary>
        private static IEnumerable<ModelObject> Below(ModelObject obj) =>
            obj is Element element ? obj.Children.Concat(element.Openings) : obj.Children;

        /// <summary>Number of objects per IFC class in <paramref name="root"/> and below, by class name.</summary>
        public static IReadOnlyList<(string IfcClass, int Count)> CountByClass(ModelObject root) =>
            Walk(root, new List<ModelObject>()).GroupBy(r => SpatialRules.IfcClassOf(r.Object)).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => (g.Key, g.Count())).ToList();

        /// <summary>
        /// The spatial tree as indented lines ("Name [IfcClass]"); the elements of each spatial object are summarised per class unless
        /// <paramref name="elements"/>, which lists them (with their parts and openings).
        /// </summary>
        public static string Tree(ModelObject root, bool elements = false)
        {
            var text = new StringBuilder();
            void Add(ModelObject obj, int depth)
            {
                text.Append(' ', depth * 2).Append(Label(obj)).Append(" [").Append(SpatialRules.IfcClassOf(obj)).Append(']');
                var children = Below(obj).ToList();
                var shown = elements ? children : children.Where(c => !(c is Element)).ToList();
                var hidden = children.Except(shown).GroupBy(SpatialRules.IfcClassOf).Select(g => $"{g.Count()} {g.Key}").ToList();
                if (hidden.Count > 0) text.Append(": ").Append(string.Join(", ", hidden));
                text.AppendLine();
                foreach (var child in shown) Add(child, depth + 1);
            }
            Add(root, 0);
            return text.ToString().TrimEnd();
        }

        internal static string Label(ModelObject obj) => string.IsNullOrWhiteSpace(obj.Name) ? "(unnamed)" : obj.Name;
    }
}
