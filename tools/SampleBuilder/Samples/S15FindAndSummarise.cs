using Grasshopper.Kernel;

namespace SampleBuilder.Samples
{
    /// <summary>Model Info and Find Objects on the files of samples 11 and 09: classes, names, openings and property filters.</summary>
    internal sealed class S15FindAndSummarise : Sample
    {
        public override int Number => 15;
        public override string Title => "Find and summarise";
        public override string Summary =>
            "Model Info gives the schema, the number of objects per class and the spatial tree. Find Objects searches by IFC class (subclasses included),\n" +
            "name wildcards, GlobalIds and property filters such as Pset_WallCommon.FireRating = REI 60; its objects go on to Deconstruct or Modify components.";

        protected override void BuildBody(Canvas c)
        {
            var openingsPath = c.Panel(0, Top, 260, 30, "ifc/11 Openings.ifc");
            var read = c.Ifc("Read IFC", 0, Canvas.Below(openingsPath, 40));
            c.Wire(openingsPath, read, "Path");
            var info = c.Ifc("Model Info", Canvas.RightOf(read), read.Attributes.Bounds.Y);
            c.Wire(read, info, "Model");
            var schema = Show(c, info, "Schema", Canvas.RightOf(info, 30), read.Attributes.Bounds.Y);
            var tree = Show(c, info, "Tree", Canvas.RightOf(info, 30), Canvas.Below(schema, 20), 380, 100);
            var classes = Show(c, info, "Classes", Canvas.RightOf(info, 30), Canvas.Below(tree, 20), 180, 120);
            var counts = Show(c, info, "Counts", Canvas.RightOf(classes, 20), classes.Attributes.Bounds.Y, 80, 120);
            c.Group("1. Model Info: what the file holds", Canvas.Colours.Read, openingsPath, read, info, schema, tree, classes, counts);

            var x = Canvas.RightOf(tree, 100);
            var built = Find(c, x, Top, read, "Built elements (walls, doors, windows; subclasses of IfcBuiltElement)", "IfcBuiltElement", null, null);
            var openings = Find(c, x, Canvas.Below(built.Find, 60), read, "Openings, with the element each one voids as parent", "IfcOpeningElement", null, null);
            var named = Find(c, x, Canvas.Below(openings.Find, 60), read, "Any object whose name starts with W", null, "w*", null);

            var psetPath = c.Panel(0, Canvas.Below(classes, 120), 260, 30, "ifc/09 Property and quantity sets.ifc");
            var readSets = c.Ifc("Read IFC", 0, Canvas.Below(psetPath, 40));
            c.Wire(psetPath, readSets, "Path");
            var filtered = Find(c, Canvas.RightOf(readSets), readSets.Attributes.Bounds.Y, readSets, "Walls with a fire rating of REI 60 and longer than 5 (document units)", "IfcWall", null,
                "Pset_WallCommon.FireRating = REI 60", "Qto_WallBaseQuantities.Length > 5");
            c.Group(null, Canvas.Colours.Read, psetPath, readSets);
        }

        /// <summary>A Find Objects with its filters, and panels with the names (paths) and classes found.</summary>
        private static (IGH_DocumentObject Find, IGH_DocumentObject Paths) Find(Canvas c, float x, float y, IGH_DocumentObject model, string title,
            string ifcClass, string name, params string[] filters)
        {
            var find = c.Ifc("Find Objects", x, y);
            c.Wire(model, find, "Model");
            if (ifcClass != null) Canvas.Set(find, "Classes", ifcClass);
            if (name != null) Canvas.Set(find, "Name", name);
            if (filters != null && filters.Length > 0) Canvas.Set(find, "Properties", filters);
            var objects = Show(c, find, "Objects", Canvas.RightOf(find, 30), y, 260, 70);
            var paths = Show(c, find, "Paths", Canvas.RightOf(find, 30), Canvas.Below(objects, 20), 260, 70);
            c.Group(title, Canvas.Colours.Read, find, objects, paths);
            return (find, paths);
        }
    }
}
