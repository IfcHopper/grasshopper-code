using System;
using System.Collections.Generic;

namespace IfcHopper.Core.Model
{
    /// <summary>IfcFacilityUsageEnum: how a facility part divides its facility.</summary>
    public enum FacilityUsage
    {
        NotDefined,
        Lateral,
        Longitudinal,
        Region,
        Vertical,
    }

    /// <summary>
    /// A part of a facility, written as IfcRoadPart, IfcBridgePart, IfcRailwayPart, IfcMarinePart
    /// or IfcFacilityPartCommon depending on <see cref="Kind"/>.
    /// </summary>
    public class FacilityPart : TypedModelObject
    {
        // Predefined types per kind, from the IFC4X3_ADD2 schema (USERDEFINED and NOTDEFINED excluded).
        private static readonly Dictionary<FacilityType, string[]> PartTypes = new Dictionary<FacilityType, string[]>
        {
            [FacilityType.Road] = new[] { "BICYCLECROSSING", "BUS_STOP", "CARRIAGEWAY", "CENTRALISLAND", "CENTRALRESERVE", "HARDSHOULDER", "INTERSECTION", "LAYBY", "PARKINGBAY", "PASSINGBAY", "PEDESTRIAN_CROSSING", "RAILWAYCROSSING", "REFUGEISLAND", "ROADSEGMENT", "ROADSIDE", "ROADSIDEPART", "ROADWAYPLATEAU", "ROUNDABOUT", "SHOULDER", "SIDEWALK", "SOFTSHOULDER", "TOLLPLAZA", "TRAFFICISLAND", "TRAFFICLANE" },
            [FacilityType.Bridge] = new[] { "ABUTMENT", "DECK", "DECK_SEGMENT", "FOUNDATION", "PIER", "PIER_SEGMENT", "PYLON", "SUBSTRUCTURE", "SUPERSTRUCTURE", "SURFACESTRUCTURE" },
            [FacilityType.Railway] = new[] { "ABOVETRACK", "DILATIONTRACK", "LINESIDE", "LINESIDEPART", "PLAINTRACK", "SUBSTRUCTURE", "TRACK", "TRACKPART", "TURNOUTTRACK" },
            [FacilityType.MarineFacility] = new[] { "ABOVEWATERLINE", "ANCHORAGE", "APPROACHCHANNEL", "BELOWWATERLINE", "BERTHINGSTRUCTURE", "CHAMBER", "CILL_LEVEL", "COPELEVEL", "CORE", "CREST", "GATEHEAD", "GUDINGSTRUCTURE", "HIGHWATERLINE", "LANDFIELD", "LEEWARDSIDE", "LOWWATERLINE", "MANUFACTURING", "NAVIGATIONALAREA", "PROTECTION", "SHIPTRANSFER", "STORAGEAREA", "VEHICLESERVICING", "WATERFIELD", "WEATHERSIDE" },
            [FacilityType.Facility] = new[] { "ABOVEGROUND", "BELOWGROUND", "JUNCTION", "LEVELCROSSING", "SEGMENT", "SUBSTRUCTURE", "SUPERSTRUCTURE", "TERMINAL" },
        };

        /// <summary>Facility kind this part belongs to. <see cref="FacilityType.Facility"/> means IfcFacilityPartCommon.</summary>
        public FacilityType Kind { get; }

        public FacilityUsage Usage { get; set; }

        public ChildView<Element> Elements => new ChildView<Element>(this);

        /// <summary>Nested parts, e.g. piers inside a substructure.</summary>
        public ChildView<FacilityPart> Parts => new ChildView<FacilityPart>(this);
        public ChildView<Space> Spaces => new ChildView<Space>(this);

        public string IfcClass => GetIfcClass(Kind);

        /// <summary>A part accepts nested parts of its own kind and common facility parts.</summary>
        public bool Accepts(FacilityPart part) => SpatialRules.Accepts(this, part);

        public override IReadOnlyList<string> PredefinedTypes => PartTypes[Kind];

        public FacilityPart(FacilityType kind, string name = null) : base(name ?? GetDefaultName(kind))
        {
            if (!PartTypes.ContainsKey(kind)) throw new ArgumentException($"{kind} has no facility parts.", nameof(kind));
            Kind = kind;
        }

        public static IReadOnlyList<string> GetPredefinedTypes(FacilityType kind) => PartTypes[kind];

        public static string GetIfcClass(FacilityType kind)
        {
            switch (kind)
            {
                case FacilityType.Road: return "IfcRoadPart";
                case FacilityType.Bridge: return "IfcBridgePart";
                case FacilityType.Railway: return "IfcRailwayPart";
                case FacilityType.MarineFacility: return "IfcMarinePart";
                case FacilityType.Facility: return "IfcFacilityPartCommon";
                default: throw new ArgumentException($"{kind} has no facility parts.", nameof(kind));
            }
        }

        public static string GetDefaultName(FacilityType kind) =>
            kind == FacilityType.MarineFacility ? "Hopper Marine Part" : $"Hopper {kind} Part";
    }
}
