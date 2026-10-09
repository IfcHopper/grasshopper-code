using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace SampleBuilder.Samples
{
    /// <summary>Standard and custom property sets, a quantity set, deconstructing them and replacing or deleting sets on Modify Object.</summary>
    internal sealed class S09PropertyAndQuantitySets : Sample
    {
        public override int Number => 9;
        public override string Title => "Property and quantity sets";
        public override string Summary =>
            "Standard sets (Pset_WallCommon, Qto_WallBaseQuantities) take the value types and kinds of their IFC 4.3 templates; custom sets infer them from the values.\n" +
            "Modify Object puts each set in place of the set with the same name; a set without properties deletes it.";

        public override System.Collections.Generic.IEnumerable<string> ExpectedMessages => new[] { "Without properties this set deletes the set Project_Data" };

        protected override void BuildBody(Canvas c)
        {
            var common = c.Ifc("Pset", 0, Top);
            Canvas.Set(common, "Name", "Pset_WallCommon");
            Canvas.Set(common, "Names", "IsExternal", "FireRating", "ThermalTransmittance", "Status");
            Canvas.Set(common, "Values", new GH_Boolean(true), new GH_String("REI 60"), new GH_Number(0.25), new GH_String("NEW"));
            var custom = c.Ifc("Pset", 0, Canvas.Below(common));
            Canvas.Set(custom, "Name", "Project_Data");
            Canvas.Set(custom, "Names", "Supplier", "Batch", "Tolerance");
            Canvas.Set(custom, "Values", new GH_String("ACME"), new GH_Integer(3), new GH_String("-0.005 .. 0.005"));
            Canvas.Set(custom, "Kinds", "", "", "Bounded");
            var quantities = c.Ifc("Qto", 0, Canvas.Below(custom));
            Canvas.Set(quantities, "Name", "Qto_WallBaseQuantities");
            Canvas.Set(quantities, "Names", "Length", "Height", "Width", "NetSideArea", "NetVolume");
            Canvas.Set(quantities, "Values", 6.0, 3.0, 0.2, 18.0, 3.6);
            Canvas.Set(quantities, "Method", "BaseQuantities");
            c.Group("1. A standard set, a custom set (a bounded tolerance) and a quantity set", Canvas.Colours.Ifc, common, custom, quantities);

            var x = Canvas.RightOf(common, 80);
            var wall = Object(c, x, Top, null, "IfcWall", "Wall");
            Canvas.Set(wall, "Geometry", Brep.CreateFromBox(new BoundingBox(0, 0, 0, 6, 0.2, 3)));
            foreach (var set in new[] { common, custom, quantities }) c.Wire(set, wall, "Property Sets");

            var deconstruct = c.Ifc("Deconstruct Object", Canvas.RightOf(wall, 80), Top);
            c.Wire(wall, deconstruct, "Object");
            var first = c.Native("List Item", Canvas.RightOf(deconstruct, 50), Top);
            c.Wire(deconstruct, first, "List", "Property Sets");
            var third = c.Native("List Item", Canvas.RightOf(deconstruct, 50), Canvas.Below(first));
            c.Wire(deconstruct, third, "List", "Property Sets");
            Canvas.Set(third, "Index", 2);
            var dePset = c.Ifc("Deconstruct Pset", Canvas.RightOf(first, 40), Top);
            c.Wire(first, dePset, "Pset");
            var deQto = c.Ifc("Deconstruct Qto", Canvas.RightOf(first, 40), Canvas.Below(dePset));
            c.Wire(third, deQto, "Qto");
            var values = Show(c, dePset, "Values", Canvas.RightOf(dePset, 30), Top, 200, 80);
            var types = Show(c, dePset, "Types", Canvas.RightOf(dePset, 30), Canvas.Below(values, 20), 200, 80);
            var quantityValues = Show(c, deQto, "Values", Canvas.RightOf(deQto, 30), System.Math.Max(deQto.Attributes.Bounds.Y, Canvas.Below(types, 20)), 200, 100);
            c.Group("2. Deconstruct the sets of the wall", Canvas.Colours.Read, deconstruct, first, third, dePset, deQto, values, types, quantityValues);

            var y = Canvas.Below(quantities, 80);
            var fire = c.Ifc("Pset", 0, y);
            Canvas.Set(fire, "Name", "Pset_WallCommon");
            Canvas.Set(fire, "Names", "IsExternal", "FireRating");
            Canvas.Set(fire, "Values", new GH_Boolean(true), new GH_String("REI 120"));
            var delete = c.Ifc("Pset", 0, Canvas.Below(fire));
            Canvas.Set(delete, "Name", "Project_Data");
            var modify = c.Ifc("Modify Object", Canvas.RightOf(fire, 80), y);
            c.Wire(wall, modify, "Object");
            c.Wire(fire, modify, "Property Sets");
            c.Wire(delete, modify, "Property Sets");
            var deModified = c.Ifc("Deconstruct Object", Canvas.RightOf(modify, 80), y);
            c.Wire(modify, deModified, "Object");
            var sets = Show(c, deModified, "Property Sets", Canvas.RightOf(deModified, 30), y, 260, 80);
            c.Group("3. Modify: Pset_WallCommon is replaced (only its two properties remain), Project_Data is deleted", Canvas.Colours.Read, fire, delete, modify, deModified, sets);

            var project = House(c, Canvas.RightOf(values, 120), Top, "Property and quantity sets", wall);
            WriteFile(c, Canvas.RightOf(project, 80), Top, project, IfcName);
        }
    }
}
