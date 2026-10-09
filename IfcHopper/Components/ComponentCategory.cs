namespace IfcHopper.Components
{
    /// <summary>
    /// Grasshopper tab and panel names. Numbered so Grasshopper sorts the panels in order.
    /// In each panel, create components go in the primary row and deconstruct components in the row below.
    /// </summary>
    internal static class ComponentCategory
    {
        public const string Tab = "IfcHopper";
        public const string File = "1 - File";
        public const string Project = "2 - Project";
        public const string Facility = "3 - Facility";
        public const string FacilityPart = "4 - Facility Part";
        public const string Object = "5 - Object";
        public const string Utilities = "6 - Utilities";
        public const string Params = "7 - Params";
    }
}
