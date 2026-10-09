using System;
using System.Linq;

namespace IfcHopper.Core.IO
{
    /// <summary>IFC schemas IfcHopper can write.</summary>
    public enum IfcSchema
    {
        Ifc2x3,
        Ifc4,
        /// <summary>IFC4X3_ADD2, the default for new models.</summary>
        Ifc4x3,
    }

    public static class IfcSchemas
    {
        /// <summary>Name as written in the file header, e.g. "IFC4X3_ADD2".</summary>
        public static string Name(this IfcSchema schema) =>
            schema == IfcSchema.Ifc2x3 ? "IFC2X3" : schema == IfcSchema.Ifc4 ? "IFC4" : "IFC4X3_ADD2";

        /// <summary>Parses "IFC2X3", "IFC4", "IFC4X3" or "IFC4X3_ADD2", ignoring case, spaces and underscores; null when unknown.</summary>
        public static IfcSchema? Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var key = new string(text.Where(c => !char.IsWhiteSpace(c) && c != '_').ToArray()).ToUpperInvariant();
            switch (key)
            {
                case "IFC2X3": return IfcSchema.Ifc2x3;
                case "IFC4": return IfcSchema.Ifc4;
                case "IFC4X3":
                case "IFC4X3ADD2": return IfcSchema.Ifc4x3;
                default: return null;
            }
        }

        public static string[] Names => Enum.GetValues(typeof(IfcSchema)).Cast<IfcSchema>().Select(Name).ToArray();
    }
}
