using System.Drawing;
using Rhino.Geometry;

namespace SampleBuilder.Samples
{
    /// <summary>Geometry kinds an object takes, colours with transparency and element assemblies.</summary>
    internal sealed class S06GeometryAndColour : Sample
    {
        public override int Number => 6;
        public override string Title => "Geometry and colour";
        public override string Summary =>
            "Objects take meshes, Breps, extrusions, surfaces and SubDs (faceted to meshes) and a surface colour, whose alpha sets the transparency.\n" +
            "An element assembly (IfcElementAssembly) is made of parts: objects in its Parts input. Deconstruct Object gives back the meshes and colours.";

        protected override void BuildBody(Canvas c)
        {
            var brep = Box(c, 0, Top, new Point3d(0, 0, 1), new Vector3d(0.5, 0.5, 1));
            var mesh = c.Native("Mesh Sphere", 0, Canvas.Below(brep));
            Canvas.Set(mesh, "Base", new Plane(new Point3d(3, 0, 1), Vector3d.ZAxis));
            Canvas.Set(mesh, "Radius", 0.8);
            var circle = new Circle(new Plane(new Point3d(6, 0, 0), Vector3d.ZAxis), 0.4).ToNurbsCurve();
            var extrusion = c.Native("Extrude", 0, Canvas.Below(mesh));
            Canvas.Set(extrusion, "Base", circle);
            Canvas.Set(extrusion, "Direction", new Vector3d(0, 0, 2));
            c.Group("1. Geometry: a Brep box, a mesh sphere, an extruded circle", Canvas.Colours.Geometry, brep, mesh, extrusion);

            var x = Canvas.RightOf(brep, 80);
            var column = Object(c, x, Top, brep, "IfcColumn", "Brep column", "COLUMN");
            Canvas.Set(column, "Colour", Color.FromArgb(255, 200, 80, 60));
            var sphere = Object(c, Canvas.RightOf(column), Top, mesh, "IfcBuildingElementProxy", "Glass sphere");
            Canvas.Set(sphere, "Colour", Color.FromArgb(90, 120, 190, 230));
            var post = Object(c, Canvas.RightOf(sphere), Top, extrusion, "IfcMember", "Round post", "POST");
            c.Group("2. Objects with a colour (the sphere is 65 % transparent); the post has none", Canvas.Colours.Ifc, column, sphere, post);

            var plateBox = Box(c, x, Canvas.Below(column, 60), new Point3d(9, 0, 2.05), new Vector3d(1, 0.5, 0.05));
            var legBox = Box(c, x, Canvas.Below(plateBox), new Point3d(9, 0, 1), new Vector3d(0.05, 0.05, 1));
            var plate = Object(c, Canvas.RightOf(plateBox), plateBox.Attributes.Bounds.Y, plateBox, "IfcPlate", "Top plate");
            var leg = Object(c, Canvas.RightOf(plate), plateBox.Attributes.Bounds.Y, legBox, "IfcMember", "Leg");
            var assembly = Object(c, Canvas.RightOf(leg), plateBox.Attributes.Bounds.Y, null, "IfcElementAssembly", "Stand");
            c.Wire(plate, assembly, "Parts");
            c.Wire(leg, assembly, "Parts");
            c.Group("3. An assembly made of two parts (no geometry of its own)", Canvas.Colours.Ifc, plateBox, legBox, plate, leg, assembly);

            var project = House(c, Canvas.RightOf(post, 120), Top, "Geometry and colour", column, sphere, post, assembly);
            var write = WriteFile(c, Canvas.RightOf(project, 80), Top, project, IfcName);

            var deconstruct = c.Ifc("Deconstruct Object", Canvas.RightOf(assembly, 120), Canvas.Below(write, 400));
            c.Wire(sphere, deconstruct, "Object");
            var colours = Show(c, deconstruct, "Colour", Canvas.RightOf(deconstruct, 30), deconstruct.Attributes.Bounds.Y);
            c.Group("4. Deconstruct Object: meshes in world coordinates and their colours", Canvas.Colours.Read, deconstruct, colours);
        }
    }
}
