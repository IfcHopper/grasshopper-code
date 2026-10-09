using Grasshopper.Kernel;
using Rhino.Geometry;

namespace SampleBuilder.Samples
{
    /// <summary>Classification references on objects and types, inherited from the type, replaced and removed by system.</summary>
    internal sealed class S10Classification : Sample
    {
        public override int Number => 10;
        public override string Title => "Classification";
        public override string Summary =>
            "Classification references (e.g. Uniclass 2015 Pr_20_93_52) on objects and types; an object shows its type's references in systems it has none in.\n" +
            "Modify Object puts a reference in place of the references in the same system; a reference without a code removes them.";

        public override System.Collections.Generic.IEnumerable<string> ExpectedMessages => new[] { "Without a code this reference removes the references of Uniclass 2015" };

        protected override void BuildBody(Canvas c)
        {
            var wallCode = Reference(c, 0, Top, "Uniclass 2015", "v1.30", "Pr_20_93_52", "Concrete walls", "https://uniclass.thenbs.com/taxon/pr_20_93_52");
            var typeCode = Reference(c, 0, Canvas.Below(wallCode), "Uniclass 2015", "v1.30", "Pr_20_93", "Wall and barrier units");
            var omniClass = Reference(c, 0, Canvas.Below(typeCode), "OmniClass", "2012", "23-13 35 11", "Walls");
            c.Group("1. Classification references", Canvas.Colours.Ifc, wallCode, typeCode, omniClass);

            var x = Canvas.RightOf(wallCode, 80);
            var type = c.Ifc("Element Type", x, Top);
            Canvas.Set(type, "Class", "IfcWallType");
            Canvas.Set(type, "Name", "Concrete wall 200");
            c.Wire(typeCode, type, "Classifications");
            c.Wire(omniClass, type, "Classifications");
            var wall = Object(c, Canvas.RightOf(type, 80), Top, null, "IfcWall", "Wall");
            Canvas.Set(wall, "Geometry", Brep.CreateFromBox(new BoundingBox(0, 0, 0, 6, 0.2, 3)));
            c.Wire(type, wall, "Element Type");
            c.Wire(wallCode, wall, "Classifications");
            c.Group("2. The type has Uniclass and OmniClass, the wall its own Uniclass", Canvas.Colours.Ifc, type, wall);

            var deconstruct = c.Ifc("Deconstruct Object", Canvas.RightOf(wall, 80), Top);
            c.Wire(wall, deconstruct, "Object");
            var classification = c.Ifc("Deconstruct Classification", Canvas.RightOf(deconstruct, 50), Top);
            c.Wire(deconstruct, classification, "Classification", "Classifications");
            var systems = Show(c, classification, "System", Canvas.RightOf(classification, 30), Top, 200, 60);
            var codes = Show(c, classification, "Code", Canvas.RightOf(classification, 30), Canvas.Below(systems, 20), 200, 60);
            var fromType = Show(c, classification, "From Type", Canvas.RightOf(classification, 30), Canvas.Below(codes, 20), 200, 60);
            c.Group("3. The wall's own Uniclass reference, then OmniClass from its type", Canvas.Colours.Read, deconstruct, classification, systems, codes, fromType);

            var y = Canvas.Below(omniClass, 80);
            var sfb = Reference(c, 0, y, "NL-SfB", null, "21.1", "Exterior walls, load-bearing");
            var remove = Reference(c, 0, Canvas.Below(sfb), "Uniclass 2015", null, null, null);
            var modify = c.Ifc("Modify Object", Canvas.RightOf(sfb, 80), y);
            c.Wire(wall, modify, "Object");
            c.Wire(sfb, modify, "Classifications");
            c.Wire(remove, modify, "Classifications");
            var deModified = c.Ifc("Deconstruct Object", Canvas.RightOf(modify, 80), y);
            c.Wire(modify, deModified, "Object");
            var references = Show(c, deModified, "Classifications", Canvas.RightOf(deModified, 30), y, 300, 80);
            c.Group("4. Modify: NL-SfB is added and Uniclass removed (also the type's, on this object)", Canvas.Colours.Read, sfb, remove, modify, deModified, references);

            var project = House(c, Canvas.RightOf(systems, 120), Top, "Classification", wall);
            WriteFile(c, Canvas.RightOf(project, 80), Top, project, IfcName);
        }

        private static IGH_DocumentObject Reference(Canvas c, float x, float y, string system, string edition, string code, string name, string location = null)
        {
            var reference = c.Ifc("Classification", x, y);
            Canvas.Set(reference, "System", system);
            if (edition != null) Canvas.Set(reference, "Edition", edition);
            if (code != null) Canvas.Set(reference, "Code", code);
            if (name != null) Canvas.Set(reference, "Name", name);
            if (location != null) Canvas.Set(reference, "Location", location);
            return reference;
        }
    }
}
