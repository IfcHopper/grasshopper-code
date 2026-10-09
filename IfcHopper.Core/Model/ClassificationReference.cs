using System;
using System.Linq;

namespace IfcHopper.Core.Model
{
    /// <summary>
    /// Reference to an item of a classification system (IfcClassificationReference), e.g. Uniclass 2015 Pr_20_93_52, associated with
    /// objects and types by IfcRelAssociatesClassification. References read from a file whose parent is another reference take the system
    /// of the top of the hierarchy; the parent references are kept in the file but not modelled. Immutable, so a reference read from a file
    /// always matches its source.
    /// </summary>
    public sealed class ClassificationReference
    {
        /// <summary>Name of the classification system (IfcClassification.Name), e.g. "Uniclass 2015"; null when the reference has none.</summary>
        public string System { get; }

        /// <summary>Edition of the classification system, e.g. "v1.30".</summary>
        public string Edition { get; }

        /// <summary>Identifier of the item in the system (Identification; ItemReference in IFC2X3), e.g. "Pr_20_93_52".</summary>
        public string Code { get; }

        /// <summary>Name of the item, e.g. "Steel beams".</summary>
        public string Name { get; }

        /// <summary>URI of the item, e.g. a bSDD or web page.</summary>
        public string Location { get; }

        /// <summary>True for a reference of the object's type, shown on the object (not written for it).</summary>
        public bool FromType { get; internal set; }

        /// <summary>Backend entity this reference was read from (IfcClassificationReference, or IfcClassification for a whole system).</summary>
        public object Source { get; internal set; }

        public ClassificationReference(string system, string code, string name = null, string edition = null, string location = null)
        {
            System = Clean(system);
            Code = Clean(code);
            Name = Clean(name);
            Edition = Clean(edition);
            Location = Clean(location);
        }

        private static string Clean(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        /// <summary>True for the same system name, ignoring case; null is a system of its own.</summary>
        public bool SameSystem(ClassificationReference other) => string.Equals(System, other.System, StringComparison.OrdinalIgnoreCase);

        /// <summary>True when both have the same values (not <see cref="FromType"/> or <see cref="Source"/>).</summary>
        public bool SameAs(ClassificationReference other) =>
            other != null && System == other.System && Edition == other.Edition && Code == other.Code && Name == other.Name && Location == other.Location;

        public override string ToString() => string.Join(" ", new[] { System, Code, Name }.Where(s => s != null));
    }
}
