namespace SampleBuilder.Samples
{
    /// <summary>Reads the file of sample 01 and walks its spatial structure with the Deconstruct components.</summary>
    internal sealed class S02ReadAndExplore : Sample
    {
        public override int Number => 2;
        public override string Title => "Read and explore";
        public override string Summary =>
            "Reads the IFC file written by sample 01 and walks the spatial structure with the Deconstruct components: only what you deconstruct is loaded.\n" +
            "Element outputs preview the objects in their IFC colours; right-click an Element output and Bake to bake them, named after the object.";

        protected override void BuildBody(Canvas c)
        {
            var path = c.Panel(0, Top, 240, 30, "ifc/01 First building.ifc");
            var header = c.Ifc("Read IFC Header", 0, Canvas.Below(path, 60));
            c.Wire(path, header, "Path");
            var schema = Show(c, header, "Schema", Canvas.RightOf(header, 30), header.Attributes.Bounds.Y);
            var system = Show(c, header, "Originating System", Canvas.RightOf(header, 30), Canvas.Below(schema, 20));
            c.Group("1. The header, without reading the model", Canvas.Colours.Read, header, schema, system);

            var read = c.Ifc("Read IFC", Canvas.RightOf(schema, 80), Top);
            c.Wire(path, read, "Path");
            var model = c.Ifc("Deconstruct Model", Canvas.RightOf(read), Top);
            c.Wire(read, model, "Model");
            var project = c.Ifc("Deconstruct Project", Canvas.RightOf(model), Top);
            c.Wire(model, project, "Project", "Project");
            var site = c.Ifc("Deconstruct Site", Canvas.RightOf(project), Top);
            c.Wire(project, site, "Site", "Sites");
            var facility = c.Ifc("Deconstruct Facility", Canvas.RightOf(site), Top);
            c.Wire(site, facility, "Facility", "Facilities");
            var storey = c.Ifc("Deconstruct Storey", Canvas.RightOf(facility), Top);
            c.Wire(facility, storey, "Storey", "Storeys");
            var obj = c.Ifc("Deconstruct Object", Canvas.RightOf(storey), Top);
            c.Wire(storey, obj, "Object", "Elements");
            c.Group("2. Read IFC, then deconstruct project > site > facility > storey > object", Canvas.Colours.Read, read, model, project, site, facility, storey, obj);

            var x = Canvas.RightOf(obj, 40);
            var names = Show(c, obj, "Name", x, Top, 200, 70);
            var classes = Show(c, obj, "Class", x, Canvas.Below(names, 20), 200, 70);
            var ids = Show(c, obj, "GlobalId", x, Canvas.Below(classes, 20), 200, 70);
            c.Group("3. Object values (one per object)", Canvas.Colours.Output, names, classes, ids);
        }
    }
}
