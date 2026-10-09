namespace IfcHopper.Core.Model
{
    /// <summary>
    /// Which children each object accepts, following IFC spatial decomposition (aggregation) and containment:
    /// Project > Site or Facility; Site > Site, Facility, Space or Element; Facility > Facility of its kind, FacilityPart of its kind
    /// or common, Space or Element (buildings also Storey); Storey > Storey, Space or Element; FacilityPart > FacilityPart of its kind
    /// or common, Space or Element; Space > Space or Element; Element > Element (its parts).
    /// </summary>
    public static class SpatialRules
    {
        public static bool Accepts(ModelObject parent, ModelObject child)
        {
            switch (parent)
            {
                case Project _:
                    return child is Site || child is Facility;
                case Site _:
                    return child is Site || child is Facility || child is Space || child is Element;
                case Facility facility:
                    return child is Facility other && other.Type == facility.Type ||
                        child is Storey && facility.Type == FacilityType.Building ||
                        child is FacilityPart facilityPart && (facilityPart.Kind == facility.Type || facilityPart.Kind == FacilityType.Facility) ||
                        child is Space || child is Element;
                case Storey _:
                    return child is Storey || child is Space || child is Element;
                case FacilityPart part:
                    return child is FacilityPart nested && (nested.Kind == part.Kind || nested.Kind == FacilityType.Facility) ||
                        child is Space || child is Element;
                case Space _:
                    return child is Space || child is Element;
                case Element _:
                    return child is Element;
                default:
                    return false;
            }
        }

        /// <summary>Null when <paramref name="parent"/> accepts <paramref name="child"/>, otherwise the reason.</summary>
        public static string Check(ModelObject parent, ModelObject child) =>
            Accepts(parent, child) ? null : $"{IfcClassOf(child)} '{child.Name}' cannot be a child of {IfcClassOf(parent)} '{parent.Name}'.";

        /// <summary>IFC entity name of an object, e.g. "IfcBridgePart".</summary>
        public static string IfcClassOf(ModelObject obj)
        {
            switch (obj)
            {
                case Project _: return "IfcProject";
                case Site _: return "IfcSite";
                case Facility facility: return facility.IfcClass;
                case Storey _: return "IfcBuildingStorey";
                case FacilityPart part: return part.IfcClass;
                case Space _: return "IfcSpace";
                case Element element: return element.IfcClass;
                default: return obj?.GetType().Name;
            }
        }
    }
}
