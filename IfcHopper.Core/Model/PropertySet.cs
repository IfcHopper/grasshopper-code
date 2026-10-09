using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IfcHopper.Core.IO;

namespace IfcHopper.Core.Model
{
    /// <summary>
    /// A named set of properties or quantities attached to an object (IfcRelDefinesByProperties) or to its type.
    /// Sets read from a file are shared by every object that uses them; components edit copies.
    /// </summary>
    public abstract class PropertySetDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>Backend entity this set was read from; null for sets created in Grasshopper.</summary>
        public object Source { get; internal set; }

        /// <summary>True when the set is inherited from the object's type. Inherited sets are never written.</summary>
        public bool FromType { get; internal set; }

        /// <summary>IFC entity name of the set: "IfcPropertySet" or "IfcElementQuantity".</summary>
        public abstract string IfcClass { get; }

        /// <summary>Number of properties or quantities. A set without any deletes the set of the same name when merged (<see cref="ModelObject.MergePropertySets"/>).</summary>
        public abstract int Count { get; }

        protected PropertySetDefinition(string name)
        {
            Name = name;
        }

        /// <summary>True when both sets hold the same name, description and values; used to skip unchanged sets on write back.</summary>
        internal abstract bool SameAs(PropertySetDefinition other);

        internal static bool SameValue(object a, object b)
        {
            switch (a)
            {
                case null: return b == null;
                case double x when b is double y: return Math.Abs(x - y) <= 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(x), Math.Abs(y)));
                case IList x when b is IList y: return x.Count == y.Count && x.Cast<object>().Zip(y.Cast<object>(), SameValue).All(same => same);
                case Property x when b is Property y: return x.SameAs(y);
                case BoundedValue x when b is BoundedValue y: return SameValue(x.Lower, y.Lower) && SameValue(x.Upper, y.Upper) && SameValue(x.SetPoint, y.SetPoint);
                case TableValue x when b is TableValue y:
                    return x.DefiningValueType == y.DefiningValueType && SameValue(x.DefiningValues, y.DefiningValues) && SameValue(x.DefinedValues, y.DefinedValues);
                default: return a.Equals(b);
            }
        }
    }

    /// <summary>How a property holds its value (the IfcProperty subclass).</summary>
    public enum PropertyKind
    {
        /// <summary>One value (IfcPropertySingleValue).</summary>
        Single,
        /// <summary>One or more values from an enumeration (IfcPropertyEnumeratedValue); the value is a list.</summary>
        Enumerated,
        /// <summary>A list of values (IfcPropertyListValue); the value is a list.</summary>
        List,
        /// <summary>Upper, lower and set point values (IfcPropertyBoundedValue); the value is a <see cref="BoundedValue"/>.</summary>
        Bounded,
        /// <summary>Defining and defined values (IfcPropertyTableValue); the value is a <see cref="TableValue"/>.</summary>
        Table,
        /// <summary>A reference to another entity (IfcPropertyReferenceValue); read as text, not authored.</summary>
        Reference,
        /// <summary>Nested properties (IfcComplexProperty); the value is a list of properties.</summary>
        Complex,
    }

    /// <summary>CLR form of an IFC value type: text (string), boolean (bool), logical (bool?, null is UNKNOWN), integer (long) or real (double).</summary>
    public enum ValueKind
    {
        Text,
        Boolean,
        Logical,
        Integer,
        Real,
    }

    public sealed class Property
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public PropertyKind Kind { get; set; }

        /// <summary>
        /// IFC value type, e.g. "IfcLabel" or "IfcPositiveLengthMeasure"; null when unknown. Lengths, areas and volumes are in
        /// metres, m² and m³ (<see cref="Units.MeasureDimension"/>), other measures in SI units.
        /// </summary>
        public string ValueType { get; set; }

        /// <summary>
        /// The value: string, bool, long or double for single values (null when not set); a list of those for enumerated and
        /// list values; a <see cref="BoundedValue"/>, a <see cref="TableValue"/> or a list of the nested <see cref="Property"/> objects of a
        /// complex property; a text summary for reference properties, which can be read but not authored.
        /// </summary>
        public object Value { get; set; }

        /// <summary>What a complex property is used for (IfcComplexProperty.UsageName); null for other kinds.</summary>
        public string UsageName { get; set; }

        /// <summary>True for kinds that can be written: all but reference properties.</summary>
        public bool IsWritable => Kind != PropertyKind.Reference;

        /// <summary>
        /// A copy with every number converted by <paramref name="convert"/>, given the power of length of its value type (1 lengths, 2 areas,
        /// 3 volumes, 0 others), e.g. to show values in document units. Nested properties are converted too.
        /// </summary>
        public Property WithNumbers(Func<double, int, double> convert)
        {
            object Map(object value, string type) => value is double number ? convert(number, Units.MeasureDimension(type)) : value;
            var copy = (Property)MemberwiseClone();
            switch (Value)
            {
                case BoundedValue bounded:
                    copy.Value = new BoundedValue(Map(bounded.Lower, ValueType), Map(bounded.Upper, ValueType), Map(bounded.SetPoint, ValueType));
                    break;
                case TableValue table:
                    copy.Value = new TableValue(table.DefiningValues.Select(v => Map(v, table.DefiningValueType)), table.DefinedValues.Select(v => Map(v, ValueType)), table.DefiningValueType);
                    break;
                case IEnumerable<Property> nested:
                    copy.Value = nested.Select(p => p.WithNumbers(convert)).ToList();
                    break;
                case IList list:
                    copy.Value = list.Cast<object>().Select(v => Map(v, ValueType)).ToList();
                    break;
                default:
                    copy.Value = Map(Value, ValueType);
                    break;
            }
            return copy;
        }

        internal bool SameAs(Property other) =>
            Name == other.Name && Description == other.Description && Kind == other.Kind && ValueType == other.ValueType && UsageName == other.UsageName &&
            PropertySetDefinition.SameValue(Value, other.Value);

        public override string ToString() => $"{Name}: {ValueText(Value)}";

        /// <summary>Value as text; list items are separated by "; ".</summary>
        public static string ValueText(object value)
        {
            switch (value)
            {
                case null: return "";
                case string text: return text;
                case IList list: return string.Join("; ", list.Cast<object>().Select(ValueText));
                case IFormattable formattable: return formattable.ToString(null, CultureInfo.InvariantCulture);
                default: return value.ToString();
            }
        }
    }

    /// <summary>Value of a bounded property (IfcPropertyBoundedValue): lower and upper bounds and a set point, each optional.</summary>
    public sealed class BoundedValue
    {
        public object Lower { get; }
        public object Upper { get; }

        /// <summary>Set point within the bounds (IFC4 and later); null when not given.</summary>
        public object SetPoint { get; }

        public BoundedValue(object lower, object upper, object setPoint = null)
        {
            Lower = lower;
            Upper = upper;
            SetPoint = setPoint;
        }

        public override string ToString() =>
            $"{Property.ValueText(Lower)} .. {Property.ValueText(Upper)}" + (SetPoint != null ? $" (set point {Property.ValueText(SetPoint)})" : "");
    }

    /// <summary>Value of a table property (IfcPropertyTableValue): defining values and the values they define, pairwise.</summary>
    public sealed class TableValue
    {
        public IReadOnlyList<object> DefiningValues { get; }
        public IReadOnlyList<object> DefinedValues { get; }

        /// <summary>IFC value type of the defining values; the defined values have the property's value type.</summary>
        public string DefiningValueType { get; }

        public TableValue(IEnumerable<object> definingValues, IEnumerable<object> definedValues, string definingValueType)
        {
            DefiningValues = definingValues.ToList();
            DefinedValues = definedValues.ToList();
            DefiningValueType = definingValueType;
        }

        public override string ToString() => string.Join("; ", DefiningValues.Zip(DefinedValues, (a, b) => $"{Property.ValueText(a)}: {Property.ValueText(b)}"));
    }

    /// <summary>Properties of an object (IfcPropertySet), e.g. Pset_WallCommon.</summary>
    public sealed class PropertySet : PropertySetDefinition
    {
        public List<Property> Properties { get; } = new List<Property>();

        public override string IfcClass => "IfcPropertySet";

        public override int Count => Properties.Count;

        public PropertySet(string name) : base(name) { }

        public Property this[string name] => Properties.FirstOrDefault(p => p.Name == name);

        /// <summary>
        /// Adds (or replaces) a property of any kind but reference. The value type is <paramref name="valueType"/> when given, else the type of the
        /// property in the IFC template of this set (Annex A), else inferred from the value: bool as IfcBoolean, integers as IfcInteger,
        /// numbers as IfcReal and text as IfcLabel. Template enumerations give an enumerated value. Text is converted to the value type
        /// (e.g. "true" for IfcBoolean). Lengths, areas and volumes are taken in metres, m² and m³.
        /// Throws <see cref="ArgumentException"/> for an unknown value type or a value that does not convert.
        /// </summary>
        /// <param name="kind">
        /// Kind of the property; when null, a <see cref="BoundedValue"/> gives a bounded, a property set or properties a complex and a
        /// <see cref="TableValue"/> a table property, else the kind of the template (see TemplateKind), else single. Text is split for list ("a; b"), bounded
        /// ("lower .. upper") and table ("defining: defined; …") values. Reference properties cannot be added.
        /// </param>
        public Property Add(string name, object value, string valueType = null, PropertyKind? kind = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Property names cannot be empty.");
            name = name.Trim();
            var template = PropertyTemplates.Find(Name)?.Find(name);
            var resolved = kind ?? (value is BoundedValue ? PropertyKind.Bounded : value is TableValue ? PropertyKind.Table
                : value is PropertySet || value is IEnumerable<Property> ? PropertyKind.Complex : TemplateKind(template?.Kind, value, valueType));
            if (resolved == PropertyKind.Reference) throw new ArgumentException($"'{name}' is a reference property; these can be read but not authored.");
            var enumerated = resolved == PropertyKind.Enumerated && valueType == null;

            Property property;
            if (resolved == PropertyKind.Complex)
                property = Complex(name, value);
            else
            {
                string type;
                if (!string.IsNullOrWhiteSpace(valueType))
                    type = IfcBackend.Schema.FindValueType(valueType) ?? throw new ArgumentException($"'{valueType}' is not an IFC value type (e.g. IfcLabel, IfcBoolean, IfcLengthMeasure).");
                else if (enumerated)
                    type = "IfcLabel";
                else
                    type = IfcBackend.Schema.FindValueType(template?.ValueType) ?? Infer(Sample(value, resolved));

                object Converted(object item) => Convert(item, IfcBackend.Schema.GetValueKind(type).Value, name, type);
                object converted;
                switch (resolved)
                {
                    case PropertyKind.List:
                        converted = Items(value, ';').Select(Converted).ToList();
                        break;
                    case PropertyKind.Bounded:
                        var bounded = value as BoundedValue ?? Bounds(value, name);
                        converted = new BoundedValue(Converted(bounded.Lower), Converted(bounded.Upper), Converted(bounded.SetPoint));
                        break;
                    case PropertyKind.Table:
                        var table = value as TableValue ?? Table(value, name);
                        var definingType = table.DefiningValueType ?? Infer(table.DefiningValues.FirstOrDefault());
                        converted = new TableValue(table.DefiningValues.Select(v => Convert(v, IfcBackend.Schema.GetValueKind(definingType).Value, name, definingType)),
                            table.DefinedValues.Select(Converted), definingType);
                        break;
                    case PropertyKind.Enumerated:
                        converted = Items(value, ';').Select(Converted).Where(v => v != null)
                            .Select(v => (object)(template?.EnumItems.FirstOrDefault(i => i.Equals(v as string, StringComparison.OrdinalIgnoreCase)) ?? v)).ToList();
                        break;
                    default:
                        converted = Converted(value);
                        break;
                }
                property = new Property { Name = name, Kind = resolved, ValueType = type, Value = converted };
            }
            Properties.RemoveAll(p => p.Name == name);
            Properties.Add(property);
            return property;
        }

        /// <summary>
        /// The kind a template gives a plain value: enumerated without an explicit value type; list; bounded or table only for text in their
        /// form ("a .. b", "a: b"); single otherwise (also for template reference and complex properties).
        /// </summary>
        private static PropertyKind TemplateKind(PropertyKind? templateKind, object value, string valueType)
        {
            switch (templateKind)
            {
                case PropertyKind.Enumerated when string.IsNullOrWhiteSpace(valueType): return PropertyKind.Enumerated;
                case PropertyKind.List: return PropertyKind.List;
                case PropertyKind.Bounded when value is string text && text.Contains(".."): return PropertyKind.Bounded;
                case PropertyKind.Table when value is string text && text.Contains(":"): return PropertyKind.Table;
                default: return PropertyKind.Single;
            }
        }

        /// <summary>A complex property holding copies of the properties of a set (its name as usage name) or of the given properties.</summary>
        private static Property Complex(string name, object value)
        {
            var nested = value is PropertySet set ? set.Properties : value as IEnumerable<Property>
                ?? throw new ArgumentException($"'{name}' is a complex property; give a property set as its value.");
            return new Property
            {
                Name = name,
                Kind = PropertyKind.Complex,
                UsageName = (value as PropertySet)?.Name ?? name,
                Value = nested.Select(p => p.WithNumbers((v, _) => v)).ToList(),
            };
        }

        /// <summary>The items of a list value: a list, or text split at <paramref name="separator"/>.</summary>
        private static List<object> Items(object value, char separator)
        {
            switch (value)
            {
                case null: return new List<object>();
                case string text: return text.Split(separator).Select(t => t.Trim()).Where(t => t.Length > 0).Cast<object>().ToList();
                case IEnumerable items: return items.Cast<object>().ToList();
                default: return new List<object> { value };
            }
        }

        /// <summary>Bounds from text "lower .. upper", where either side may be empty.</summary>
        private static BoundedValue Bounds(object value, string name)
        {
            var parts = (value as string)?.Split(new[] { ".." }, StringSplitOptions.None);
            if (parts == null || parts.Length != 2) throw new ArgumentException($"'{name}' is a bounded property; give its value as \"lower .. upper\".");
            object Bound(string text) => string.IsNullOrWhiteSpace(text) ? null : Parse(text);
            return new BoundedValue(Bound(parts[0]), Bound(parts[1]));
        }

        /// <summary>A table from text "defining: defined; defining: defined".</summary>
        private static TableValue Table(object value, string name)
        {
            var rows = Items(value, ';').Select(r => (r as string)?.Split(':')).ToList();
            if (rows.Count == 0 || rows.Any(r => r == null || r.Length != 2))
                throw new ArgumentException($"'{name}' is a table property; give its value as \"defining: defined; defining: defined\".");
            return new TableValue(rows.Select(r => Parse(r[0])), rows.Select(r => Parse(r[1])), null);
        }

        /// <summary>Text of a bounded or table value: a number when it reads as one, so the value type is inferred from it.</summary>
        private static object Parse(string text) =>
            double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : (object)text.Trim();

        /// <summary>A value to infer the type of a list, bounded or table value from.</summary>
        private static object Sample(object value, PropertyKind kind)
        {
            switch (kind)
            {
                case PropertyKind.List:
                case PropertyKind.Enumerated: return Items(value, ';').FirstOrDefault();
                case PropertyKind.Bounded:
                    var bounded = value as BoundedValue ?? (value is string range && range.Contains("..") ? Bounds(range, null) : null);
                    return bounded?.Lower ?? bounded?.Upper;
                case PropertyKind.Table:
                    var table = value as TableValue ?? (value is string rows && rows.Contains(":") ? Table(rows, null) : null);
                    return table?.DefinedValues.FirstOrDefault();
                default: return value;
            }
        }

        private static string Infer(object value)
        {
            switch (value)
            {
                case bool _: return "IfcBoolean";
                case int _:
                case long _: return "IfcInteger";
                case double _:
                case float _: return "IfcReal";
                default: return "IfcLabel";
            }
        }

        internal static object Convert(object value, ValueKind kind, string name, string type)
        {
            if (value == null) return null;
            try
            {
                switch (kind)
                {
                    case ValueKind.Boolean: return ToBool(value) ?? throw new FormatException();
                    case ValueKind.Logical: return ToBool(value);
                    case ValueKind.Integer: return value is string s ? long.Parse(s.Trim(), CultureInfo.InvariantCulture) : System.Convert.ToInt64(value, CultureInfo.InvariantCulture);
                    case ValueKind.Real: return value is string t ? double.Parse(t.Trim(), CultureInfo.InvariantCulture) : System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    default: return value is string text ? text : Property.ValueText(value);
                }
            }
            catch (Exception e) when (e is FormatException || e is InvalidCastException || e is OverflowException)
            {
                throw new ArgumentException($"'{Property.ValueText(value)}' is not a valid {type} value for '{name}'.");
            }
        }

        /// <summary>bool, or text true/false/yes/no/1/0; "unknown" is null.</summary>
        private static bool? ToBool(object value)
        {
            if (value is bool b) return b;
            if (!(value is string text)) return System.Convert.ToDouble(value, CultureInfo.InvariantCulture) != 0;
            switch (text.Trim().ToUpperInvariant())
            {
                case "TRUE": case "T": case "YES": case "1": return true;
                case "FALSE": case "F": case "NO": case "0": return false;
                case "UNKNOWN": case "U": return null;
                default: throw new FormatException();
            }
        }

        internal override bool SameAs(PropertySetDefinition other) =>
            other is PropertySet set && Name == set.Name && Description == set.Description && Properties.Count == set.Properties.Count &&
            Properties.Zip(set.Properties, (a, b) => a.SameAs(b)).All(same => same);

        public override string ToString() => $"{Name} ({Properties.Count} properties)";
    }

    /// <summary>Kind of a physical quantity (IfcPhysicalSimpleQuantity subclass).</summary>
    public enum QuantityKind
    {
        Length,
        Area,
        Volume,
        Count,
        Weight,
        Time,
        Number,
    }

    public sealed class Quantity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public QuantityKind Kind { get; set; }

        /// <summary>Value in metres, m², m³, kilograms or seconds; counts and numbers as they are.</summary>
        public double Value { get; set; }

        /// <summary>How the value was calculated (IFC4 and later); null when not given.</summary>
        public string Formula { get; set; }

        /// <summary>Power of length in the unit of a quantity kind: 1 for lengths, 2 for areas, 3 for volumes, otherwise 0.</summary>
        public static int Dimension(QuantityKind kind) =>
            kind == QuantityKind.Length ? 1 : kind == QuantityKind.Area ? 2 : kind == QuantityKind.Volume ? 3 : 0;

        internal bool SameAs(Quantity other) =>
            Name == other.Name && Description == other.Description && Kind == other.Kind && Formula == other.Formula &&
            PropertySetDefinition.SameValue(Value, other.Value);

        public override string ToString() => $"{Name}: {Value.ToString(CultureInfo.InvariantCulture)} ({Kind})";
    }

    /// <summary>Quantities of an object (IfcElementQuantity), e.g. Qto_WallBaseQuantities.</summary>
    public sealed class QuantitySet : PropertySetDefinition
    {
        public List<Quantity> Quantities { get; } = new List<Quantity>();

        /// <summary>Name of the measurement method, e.g. "BaseQuantities"; null when not given.</summary>
        public string MethodOfMeasurement { get; set; }

        public override string IfcClass => "IfcElementQuantity";

        public override int Count => Quantities.Count;

        public QuantitySet(string name) : base(name) { }

        public Quantity this[string name] => Quantities.FirstOrDefault(q => q.Name == name);

        /// <summary>
        /// Adds (or replaces) a quantity in metres, m², m³, kilograms or seconds. The kind is <paramref name="kind"/> when given, else the
        /// kind of the quantity in the IFC template of this set (Annex A). Throws <see cref="ArgumentException"/> when neither gives one.
        /// </summary>
        public Quantity Add(string name, double value, QuantityKind? kind = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Quantity names cannot be empty.");
            name = name.Trim();
            var resolved = kind ?? PropertyTemplates.Find(Name)?.Find(name)?.QuantityKind
                ?? throw new ArgumentException($"'{name}' is not in a known quantity set template; give its kind ({string.Join(", ", Enum.GetNames(typeof(QuantityKind)))}).");

            var quantity = new Quantity { Name = name, Kind = resolved, Value = value };
            Quantities.RemoveAll(q => q.Name == name);
            Quantities.Add(quantity);
            return quantity;
        }

        internal override bool SameAs(PropertySetDefinition other) =>
            other is QuantitySet set && Name == set.Name && Description == set.Description && MethodOfMeasurement == set.MethodOfMeasurement &&
            Quantities.Count == set.Quantities.Count && Quantities.Zip(set.Quantities, (a, b) => a.SameAs(b)).All(same => same);

        public override string ToString() => $"{Name} ({Quantities.Count} quantities)";
    }
}
