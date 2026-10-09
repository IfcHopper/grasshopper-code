using System;
using System.Collections.Generic;
using System.Linq;

namespace IfcHopper.Core.Model
{
    public enum UnitKind
    {
        Length,
        Area,
        Volume,
        Angle,
    }

    /// <summary>
    /// A unit IfcHopper can write: either an SI unit (with optional prefix) or a conversion-based unit (e.g. INCH).
    /// </summary>
    public sealed class UnitDefinition
    {
        public UnitKind Kind { get; }
        public string Name { get; }
        public string Symbol { get; }

        /// <summary>Factor to the SI base unit (m, m², m³, rad).</summary>
        public double ToSi { get; }

        /// <summary>IfcSIPrefix (e.g. "MILLI"); null for unprefixed SI units and conversion-based units.</summary>
        public string SiPrefix { get; }

        /// <summary>IfcConversionBasedUnit name (e.g. "INCH"); null for SI units.</summary>
        public string ConversionName { get; }

        public bool IsSi => ConversionName == null;

        internal UnitDefinition(UnitKind kind, string name, string symbol, double toSi, string siPrefix = null, string conversionName = null)
        {
            Kind = kind;
            Name = name;
            Symbol = symbol;
            ToSi = toSi;
            SiPrefix = siPrefix;
            ConversionName = conversionName;
        }

        public override string ToString() => Name;
    }

    /// <summary>Units of a project (IfcUnitAssignment). The Core model itself always stores lengths in metres.</summary>
    public class Units
    {
        private static readonly UnitDefinition[] All =
        {
            new UnitDefinition(UnitKind.Length, "Millimetre", "mm", 1e-3, "MILLI"),
            new UnitDefinition(UnitKind.Length, "Centimetre", "cm", 1e-2, "CENTI"),
            new UnitDefinition(UnitKind.Length, "Decimetre", "dm", 1e-1, "DECI"),
            new UnitDefinition(UnitKind.Length, "Metre", "m", 1.0),
            new UnitDefinition(UnitKind.Length, "Kilometre", "km", 1e3, "KILO"),
            new UnitDefinition(UnitKind.Length, "Inch", "in", 0.0254, conversionName: "INCH"),
            new UnitDefinition(UnitKind.Length, "Foot", "ft", 0.3048, conversionName: "FOOT"),
            new UnitDefinition(UnitKind.Area, "Square millimetre", "mm2", 1e-6, "MILLI"),
            new UnitDefinition(UnitKind.Area, "Square centimetre", "cm2", 1e-4, "CENTI"),
            new UnitDefinition(UnitKind.Area, "Square metre", "m2", 1.0),
            new UnitDefinition(UnitKind.Area, "Square inch", "in2", 0.00064516, conversionName: "SQUARE INCH"),
            new UnitDefinition(UnitKind.Area, "Square foot", "ft2", 0.09290304, conversionName: "SQUARE FOOT"),
            new UnitDefinition(UnitKind.Volume, "Cubic millimetre", "mm3", 1e-9, "MILLI"),
            new UnitDefinition(UnitKind.Volume, "Cubic centimetre", "cm3", 1e-6, "CENTI"),
            new UnitDefinition(UnitKind.Volume, "Cubic metre", "m3", 1.0),
            new UnitDefinition(UnitKind.Volume, "Cubic inch", "in3", 1.6387064e-5, conversionName: "CUBIC INCH"),
            new UnitDefinition(UnitKind.Volume, "Cubic foot", "ft3", 0.028316846592, conversionName: "CUBIC FOOT"),
            new UnitDefinition(UnitKind.Angle, "Radian", "rad", 1.0),
            new UnitDefinition(UnitKind.Angle, "Degree", "deg", Math.PI / 180.0, conversionName: "DEGREE"),
        };

        public UnitDefinition Length { get; set; } = Find(UnitKind.Length, "m");
        public UnitDefinition Area { get; set; } = Find(UnitKind.Area, "m2");
        public UnitDefinition Volume { get; set; } = Find(UnitKind.Volume, "m3");
        public UnitDefinition Angle { get; set; } = Find(UnitKind.Angle, "rad");

        public static IEnumerable<UnitDefinition> Of(UnitKind kind) => All.Where(u => u.Kind == kind);

        /// <summary>Factor from the unit of a length dimension (1 length, 2 area, 3 volume) to SI; 1 for other dimensions.</summary>
        public double ToSi(int dimension) => dimension == 1 ? Length.ToSi : dimension == 2 ? Area.ToSi : dimension == 3 ? Volume.ToSi : 1.0;

        /// <summary>Power of length in an IFC measure type: 1 for length measures, 2 for areas, 3 for volumes, otherwise 0.</summary>
        public static int MeasureDimension(string valueType)
        {
            switch (valueType)
            {
                case "IfcLengthMeasure":
                case "IfcPositiveLengthMeasure":
                case "IfcNonNegativeLengthMeasure":
                    return 1;
                case "IfcAreaMeasure": return 2;
                case "IfcVolumeMeasure": return 3;
                default: return 0;
            }
        }

        /// <summary>Finds a unit by name, symbol or IFC conversion name, ignoring case and spaces (e.g. "mm", "metre", "square foot").</summary>
        public static UnitDefinition Find(UnitKind kind, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var key = Normalize(text);
            return Of(kind).FirstOrDefault(u =>
                Normalize(u.Symbol) == key || Normalize(u.Name) == key || Normalize(u.Name.Replace("metre", "meter")) == key ||
                (u.ConversionName != null && Normalize(u.ConversionName) == key));
        }

        /// <summary>Finds the unit with the given factor to SI, or null when IfcHopper has no such unit.</summary>
        public static UnitDefinition FromFactor(UnitKind kind, double toSi) =>
            Of(kind).FirstOrDefault(u => Math.Abs(u.ToSi - toSi) <= 1e-9 * Math.Max(u.ToSi, toSi));

        private static string Normalize(string text) => new string(text.Where(c => !char.IsWhiteSpace(c) && c != '_').ToArray()).ToLowerInvariant();
    }
}
