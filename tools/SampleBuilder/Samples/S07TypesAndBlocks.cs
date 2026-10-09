using System.Drawing;
using Rhino;
using Rhino.Geometry;

namespace SampleBuilder.Samples
{
    /// <summary>Element types from Grasshopper geometry and from a Rhino block, placed by objects like block instances.</summary>
    internal sealed class S07TypesAndBlocks : Sample
    {
        public override int Number => 7;
        public override string Title => "Types and blocks";
        public override string Summary =>
            "An element type (e.g. IfcColumnType) holds geometry in its own coordinates, like a block definition; objects with a type and no geometry\n" +
            "are its instances at their placements. Open '07 Types and blocks.3dm' first: the chair type is made from its Chair block. Bake the objects as block instances.";

        public override bool PrepareRhino(RhinoDoc doc)
        {
            var parts = new GeometryBase[]
            {
                Brep.CreateFromBox(new BoundingBox(-0.22, -0.22, 0.42, 0.22, 0.22, 0.46)),
                Brep.CreateFromBox(new BoundingBox(-0.22, 0.18, 0.46, 0.22, 0.22, 0.9)),
                Brep.CreateFromBox(new BoundingBox(-0.22, -0.22, 0, -0.18, -0.18, 0.42)),
                Brep.CreateFromBox(new BoundingBox(0.18, -0.22, 0, 0.22, -0.18, 0.42)),
                Brep.CreateFromBox(new BoundingBox(-0.22, 0.18, 0, -0.18, 0.22, 0.42)),
                Brep.CreateFromBox(new BoundingBox(0.18, 0.18, 0, 0.22, 0.22, 0.42)),
            };
            doc.InstanceDefinitions.Add("Chair", "Dining chair", Point3d.Origin, parts);
            return true;
        }

        protected override void BuildBody(Canvas c)
        {
            var columnBox = Box(c, 0, Top, new Point3d(0, 0, 1.5), new Vector3d(0.15, 0.15, 1.5));
            var columnType = c.Ifc("Element Type", Canvas.RightOf(columnBox), Top);
            Canvas.Set(columnType, "Class", "IfcColumnType");
            Canvas.Set(columnType, "Name", "Column 300x300");
            Canvas.Set(columnType, "Type", "COLUMN");
            Canvas.Set(columnType, "Colour", Color.FromArgb(255, 160, 160, 165));
            c.Wire(columnBox, columnType, "Geometry");
            var columns = c.Ifc("Object", Canvas.RightOf(columnType, 80), Top);
            Canvas.Set(columns, "Name", "Column");
            Canvas.Set(columns, "Placement", Plane.WorldXY, new Plane(new Point3d(4, 0, 0), Vector3d.ZAxis), new Plane(new Point3d(8, 0, 0), Vector3d.ZAxis));
            c.Wire(columnType, columns, "Element Type");
            c.Group("1. A column type from Grasshopper geometry, placed three times (one placement per object; the class IfcColumn follows from the type)",
                Canvas.Colours.Ifc, columnBox, columnType, columns);

            var y = Canvas.Below(columns, 60);
            var query = c.Native("Query Model Block Definitions", 0, y);
            Canvas.Set(query, "Name", "Chair");
            var chairType = c.Ifc("Element Type", Canvas.RightOf(query), y);
            Canvas.Set(chairType, "Class", "IfcFurnitureType");
            Canvas.Set(chairType, "Type", "CHAIR");
            c.Wire(query, chairType, "Geometry");
            var chairs = c.Ifc("Object", Canvas.RightOf(chairType, 80), y);
            Canvas.Set(chairs, "Name", "Chair");
            Canvas.Set(chairs, "Placement", new Plane(new Point3d(2, 3, 0), Vector3d.ZAxis), new Plane(new Point3d(3, 3, 0), new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0)));
            c.Wire(chairType, chairs, "Element Type");
            c.Group("2. A furniture type from the Chair block of the Rhino document (named after the block), placed twice",
                Canvas.Colours.Ifc, query, chairType, chairs);

            var deconstruct = c.Ifc("Deconstruct Element Type", Canvas.RightOf(chairs, 80), y);
            c.Wire(chairType, deconstruct, "Element Type");
            var name = Show(c, deconstruct, "Name", Canvas.RightOf(deconstruct, 30), y);
            var cls = Show(c, deconstruct, "Class", Canvas.RightOf(deconstruct, 30), Canvas.Below(name, 20));
            c.Group("3. Deconstruct Element Type: geometry in type coordinates", Canvas.Colours.Read, deconstruct, name, cls);

            var project = House(c, Canvas.RightOf(columns, 80), Top, "Types and blocks", columns, chairs);
            WriteFile(c, Canvas.RightOf(project, 80), Top, project, IfcName);
        }
    }
}
