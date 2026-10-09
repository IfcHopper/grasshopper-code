using System;
using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.Model;

namespace IfcHopper.Core.IO
{
    /// <summary>
    /// An attribute of an element or type class beyond the common ones (e.g. IfcDoor.OverallHeight) whose value is simple: text, a number,
    /// a boolean or an enumeration value.
    /// </summary>
    public sealed class AttributeInfo
    {
        /// <summary>Attribute name, e.g. "OverallHeight".</summary>
        public string Name { get; }

        /// <summary>IFC value type, e.g. "IfcPositiveLengthMeasure", or the enumeration, e.g. "IfcDoorTypeOperationEnum".</summary>
        public string ValueType { get; }

        /// <summary>CLR form of the value; null for enumerations.</summary>
        public ValueKind? Kind { get; }

        /// <summary>Values of an enumeration; null for other attributes.</summary>
        public IReadOnlyList<string> EnumValues { get; }

        public bool Optional { get; }

        /// <summary>Power of length of the value (1 lengths, 2 areas, 3 volumes, 0 others); values are stored in metres, m² and m³.</summary>
        public int Dimension => Units.MeasureDimension(ValueType);

        public AttributeInfo(string name, string valueType, ValueKind? kind, IReadOnlyList<string> enumValues, bool optional)
        {
            Name = name;
            ValueType = valueType;
            Kind = kind;
            EnumValues = enumValues;
            Optional = optional;
        }

        /// <summary>
        /// The value in its stored form: string, double, long, bool or (logical) bool?; for enumerations the enumeration value (any case).
        /// Null stays null. Throws <see cref="ArgumentException"/> when the value does not convert.
        /// </summary>
        public object Convert(object value)
        {
            if (value == null) return null;
            if (Kind != null) return PropertySet.Convert(value, Kind.Value, Name, ValueType);
            var text = Property.ValueText(value).Trim();
            return EnumValues.FirstOrDefault(v => v.Equals(text, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"'{text}' is not a value of {Name} ({string.Join(", ", EnumValues)}).");
        }

        public override string ToString() => $"{Name} ({ValueType})";
    }
}
