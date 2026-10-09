using IfcHopper.Core.Backends.Xbim;

namespace IfcHopper.Core.IO
{
    /// <summary>
    /// Entry point for the active IFC backend. Components use this instead of a concrete backend,
    /// so other backends (IfcOpenShell, ifc-lite) can be plugged in later.
    /// </summary>
    public static class IfcBackend
    {
        public static IIfcReader Reader { get; set; } = new XbimIfcReader();
        public static IIfcWriter Writer { get; set; } = new XbimIfcWriter();
        public static ISchemaInfo Schema { get; set; } = new XbimSchemaInfo();
    }
}
