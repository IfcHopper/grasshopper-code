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
        /// <summary>IFC classes to include, e.g. IfcWall or IfcBuiltElement; prefix ! to exclude a class, e.g. !IfcWall.</summary>
        /// <remarks>An object must match an inclusion when any are supplied and must not match any exclusion. Exclusion-only lists start
        /// from all objects. Subclasses match both inclusion and exclusion unless <see cref="ExactClass"/> is true.
        /// Blank entries are ignored; surrounding whitespace is trimmed. Excluding a parent does not exclude its descendants.</remarks>
        public List<string> Classes { get; } = new List<string>();

        public bool ExactClass { get; set; }

        /// <summary>Case-insensitive .NET regular expression on the object name; null/blank matches every name.</summary>
        /// <remarks>Matching searches anywhere in the name; use ^ and $ for a whole-name match. Pattern whitespace is significant.
        /// Null names are treated as empty strings. Negative lookaheads can exclude names, e.g. ^(?!.*temporary).*wall.
        /// This replaces the beta wildcard syntax: use .* instead of * and . instead of ?.</remarks>
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
        /// elements, element parts and openings.
        /// </summary>
        /// <param name="root">Search root; included in the results when it matches. Descendants of non-matching objects are still searched.</param>
        /// <param name="query">All supplied criteria must match; name expressions use culture-independent, case-insensitive matching.</param>
        /// <returns>Lazy results in tree order, preserving object references, source links and ancestor paths.</returns>
        /// <exception cref="ArgumentException">A class, GlobalId or name expression is invalid.</exception>
        /// <exception cref="RegexMatchTimeoutException">A name match exceeds 100 ms during enumeration.</exception>
        /// <remarks>Reading objects and property filters can load data from the source file. Geometry is not loaded by the search.
        /// Name matching has a 100 ms limit per object, following Microsoft's guidance to bound regex matching:
        /// https://learn.microsoft.com/dotnet/standard/base-types/best-practices-regex#use-time-out-values .</remarks>
        public static IEnumerable<SearchResult> Find(ModelObject root, ObjectQuery query)
        {
            var classes = query.Classes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c =>
            {
                var text = c.Trim();
                bool exclude = text.StartsWith("!", StringComparison.Ordinal);
                var className = (exclude ? text.Substring(1) : text).Trim();
                if (className.Length == 0)
                    throw new ArgumentException("Classes exclusion needs an IFC class after !, e.g. !IfcWall.");
                var resolved = IfcBackend.Schema.FindClass(className)
                    ?? throw new ArgumentException($"'{className}' is not an IFC class. Use a class name, optionally prefixed with ! to exclude it.");
                return (Name: resolved, Exclude: exclude);
            }).ToArray();
            var includedClasses = classes.Where(c => !c.Exclude).Select(c => c.Name).ToArray();
            var excludedClasses = classes.Where(c => c.Exclude).Select(c => c.Name).ToArray();
            var ids = new HashSet<string>(query.GlobalIds.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i =>
                Model.GlobalIds.TryParse(i, out var id) ? id : throw new ArgumentException($"'{i.Trim()}' is not a valid GlobalId (22 IFC characters or a GUID).")));
            Regex name = null;
            if (!string.IsNullOrWhiteSpace(query.Name))
            {
                try
                {
                    name = new Regex(query.Name, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException("Name is not a valid .NET regular expression. Check grouping, brackets and escapes. " + ex.Message,
                        nameof(query), ex);
                }
            }

            bool Matches(ModelObject obj)
            {
                var ifcClass = SpatialRules.IfcClassOf(obj);
                bool IsClass(string wanted) => query.ExactClass ? string.Equals(ifcClass, wanted, StringComparison.OrdinalIgnoreCase)
                    : IfcBackend.Schema.IsClassOf(ifcClass, wanted);
                if (includedClasses.Length > 0 && !includedClasses.Any(IsClass))
                    return false;
                if (excludedClasses.Any(IsClass)) return false;
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
