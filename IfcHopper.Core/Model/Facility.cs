using System;
using System.Collections.Generic;

namespace IfcHopper.Core.Model
{
    /// <summary>IFC 4.3 facility kinds (IfcFacility and its subtypes).</summary>
    public enum FacilityType
    {
        Facility,
        Building,
        Bridge,
        Road,
        Railway,
        MarineFacility,
    }

    /// <summary>A facility on a site, written as IfcFacility or one of its subtypes.</summary>
    public class Facility : TypedModelObject
    {
        // Predefined types per facility, from the IFC4X3_ADD2 schema (USERDEFINED and NOTDEFINED excluded).
        // IfcBuilding and IfcFacility have no PredefinedType; IfcRoad and IfcRailway only allow USERDEFINED.
        private static readonly Dictionary<FacilityType, string[]> FacilityTypes = new Dictionary<FacilityType, string[]>
        {
            [FacilityType.Bridge] = new[] { "ARCHED", "CABLE_STAYED", "CANTILEVER", "CULVERT", "FRAMEWORK", "GIRDER", "SUSPENSION", "TRUSS" },
            [FacilityType.Road] = new string[0],
            [FacilityType.Railway] = new string[0],
            [FacilityType.MarineFacility] = new[] { "BARRIERBEACH", "BREAKWATER", "CANAL", "DRYDOCK", "FLOATINGDOCK", "HYDROLIFT", "JETTY", "LAUNCHRECOVERY", "MARINEDEFENCE", "NAVIGATIONALCHANNEL", "PORT", "QUAY", "REVETMENT", "SHIPLIFT", "SHIPLOCK", "SHIPYARD", "SLIPWAY", "WATERWAY", "WATERWAYSHIPLIFT" },
        };

        public FacilityType Type { get; }

        /// <summary>IFC entity name, e.g. "IfcRoad".</summary>
        public string IfcClass => "Ifc" + Type;

        public ChildView<FacilityPart> Parts => new ChildView<FacilityPart>(this);
        public ChildView<Space> Spaces => new ChildView<Space>(this);

        /// <summary>Elements contained directly in the facility.</summary>
        public ChildView<Element> Elements => new ChildView<Element>(this);

        /// <summary>Facilities of the same kind making up this one (complex facilities).</summary>
        public ChildView<Facility> Facilities => new ChildView<Facility>(this);

        public override IReadOnlyList<string> PredefinedTypes => GetPredefinedTypes(Type);

        public Facility(FacilityType type = FacilityType.Facility, string name = null) : base(name ?? GetDefaultName(type))
        {
            Type = type;
        }

        /// <summary>A facility accepts parts of its own kind and common facility parts.</summary>
        public bool Accepts(FacilityPart part) => SpatialRules.Accepts(this, part);

        /// <summary>Predefined types of a facility kind, or null when its IFC entity has no PredefinedType.</summary>
        public static IReadOnlyList<string> GetPredefinedTypes(FacilityType type) =>
            FacilityTypes.TryGetValue(type, out var types) ? types : null;

        public static string GetDefaultName(FacilityType type) =>
            type == FacilityType.MarineFacility ? "Hopper Marine Facility" : "Hopper " + type;
    }
}
