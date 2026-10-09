using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace SampleBuilder.Samples
{
    /// <summary>Spaces in a storey and on the site, nested spaces, objects in a space and space property sets.</summary>
    internal sealed class S05Spaces : Sample
    {
        public override int Number => 5;
        public override string Title => "Spaces";
        public override string Summary =>
            "Spaces (IfcSpace) belong to a storey, a facility part or the site; they can nest (partial spaces) and contain objects, e.g. furniture.\n" +
            "Pset_SpaceCommon gives a space its standard properties, typed from the IFC 4.3 templates. Spaces have a placement but no body geometry yet.";

        protected override void BuildBody(Canvas c)
        {
            var tableBox = Box(c, 0, Top, new Point3d(3.5, 3, 0.375), new Vector3d(0.8, 0.45, 0.375));
            var table = Object(c, Canvas.RightOf(tableBox), Top, tableBox, "IfcFurniture", "Table", "TABLE");
            var pset = c.Ifc("Pset", Canvas.RightOf(tableBox), Canvas.Below(table));
            Canvas.Set(pset, "Name", "Pset_SpaceCommon");
            Canvas.Set(pset, "Names", "Reference", "IsExternal", "PubliclyAccessible");
            Canvas.Set(pset, "Values", new GH_String("R01"), new GH_Boolean(false), new GH_Boolean(false));
            c.Group("1. Contents and properties of the living room", Canvas.Colours.Ifc, tableBox, table, pset);

            var x = Canvas.RightOf(table, 80);
            var corner = Space(c, x, Top, "Reading corner", "SPACE", new Point3d(0.2, 0.2, 0));
            var room = Space(c, Canvas.RightOf(corner), Top, "Living room", "INTERNAL", new Point3d(0.1, 0.1, 0));
            c.Wire(corner, room, "Spaces");
            c.Wire(table, room, "Elements");
            c.Wire(pset, room, "Property Sets");
            var garden = Space(c, x, Canvas.Below(corner, 60), "Garden", "EXTERNAL", new Point3d(-1, -9, 0));
            c.Group("2. Spaces: the reading corner is part of the living room", Canvas.Colours.Ifc, corner, room, garden);

            var storey = c.Ifc("Storey", Canvas.RightOf(room, 80), Top);
            c.Wire(room, storey, "Spaces");
            var building = c.Ifc("Building", Canvas.RightOf(storey), Top);
            c.Wire(storey, building, "Storeys");
            var site = c.Ifc("Site", Canvas.RightOf(building), Top);
            c.Wire(building, site, "Facilities");
            c.Wire(garden, site, "Spaces");
            var project = c.Ifc("Project", Canvas.RightOf(site), Top);
            Canvas.Set(project, "Name", "Spaces");
            c.Wire(site, project, "Sites");
            c.Group("3. The living room is in the storey, the garden on the site", Canvas.Colours.Ifc, storey, building, site, project);
            WriteFile(c, Canvas.RightOf(project, 80), Top, project, IfcName);
        }

        private static IGH_DocumentObject Space(Canvas c, float x, float y, string name, string type, Point3d origin)
        {
            var space = c.Ifc("Space", x, y);
            Canvas.Set(space, "Name", name);
            Canvas.Set(space, "Type", type);
            Canvas.Set(space, "Placement", new Plane(origin, Vector3d.ZAxis));
            return space;
        }
    }
}
