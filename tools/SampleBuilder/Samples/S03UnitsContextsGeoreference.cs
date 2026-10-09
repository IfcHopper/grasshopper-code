using Rhino.Geometry;

namespace SampleBuilder.Samples
{
    /// <summary>File units, representation contexts and a map conversion, written and read back.</summary>
    internal sealed class S03UnitsContextsGeoreference : Sample
    {
        public override int Number => 3;
        public override string Title => "Units, contexts and georeference";
        public override string Summary =>
            "Writes a millimetre file with a 3D context (Body, Axis and Box subcontexts, true north) and a map conversion to UTM, then reads it back.\n" +
            "Grasshopper values stay in Rhino units; IfcHopper converts on write and read. Read IFC offers 'Match Rhino units to file' in its context menu.";

        public override System.Collections.Generic.IEnumerable<string> ExpectedMessages => new[] { "File length unit: Millimetre" };

        protected override void BuildBody(Canvas c)
        {
            var units = c.Ifc("Units", 0, Top);
            Canvas.Set(units, "Length", "mm");
            var context = c.Ifc("Context", 0, Canvas.Below(units));
            Canvas.Set(context, "Identifiers", "Body", "Axis", "Box");
            Canvas.Set(context, "Target Views", "MODEL_VIEW", "GRAPH_VIEW", "MODEL_VIEW");
            Canvas.Set(context, "True North", new Vector3d(-0.1, 1, 0));
            var georeference = c.Ifc("Georeference", 0, Canvas.Below(context));
            Canvas.Set(georeference, "CRS", "EPSG:32632");
            Canvas.Set(georeference, "Description", "WGS 84 / UTM zone 32N");
            Canvas.Set(georeference, "Geodetic Datum", "WGS84");
            Canvas.Set(georeference, "Eastings", 500000.0);
            Canvas.Set(georeference, "Northings", 5000000.0);
            Canvas.Set(georeference, "Height", 120.0);
            Canvas.Set(georeference, "X Axis", new Vector3d(1, 0.05, 0));
            c.Group("1. Units, context and georeference", Canvas.Colours.Ifc, units, context, georeference);

            var box = Box(c, 0, Canvas.Below(georeference, 60), new Point3d(0, 0, 0.5), new Vector3d(0.5, 0.5, 0.5));
            var marker = Object(c, Canvas.RightOf(box), box.Attributes.Bounds.Y, box, "IfcBuildingElementProxy", "Survey marker");
            var site = c.Ifc("Site", Canvas.RightOf(marker), box.Attributes.Bounds.Y);
            c.Wire(marker, site, "Elements");
            c.Group("2. An object directly on the site", Canvas.Colours.Geometry, box, marker, site);

            var x = Canvas.RightOf(site, 100);
            var project = c.Ifc("Project", x, Top);
            Canvas.Set(project, "Name", "Georeferenced site");
            c.Wire(site, project, "Sites");
            c.Wire(units, project, "Units");
            c.Wire(context, project, "Contexts");
            c.Wire(georeference, project, "Georeference");
            var write = WriteFile(c, Canvas.RightOf(project, 80), Top, project, IfcName);

            var readY = Canvas.Below(write, 260);
            var read = c.Ifc("Read IFC", Canvas.RightOf(project, 80), readY);
            c.Wire(write, read, "Path", "Path");
            var model = c.Ifc("Deconstruct Model", Canvas.RightOf(read), readY);
            c.Wire(read, model, "Model");
            var deProject = c.Ifc("Deconstruct Project", Canvas.RightOf(model), readY);
            c.Wire(model, deProject, "Project", "Project");
            var deUnits = c.Ifc("Deconstruct Units", Canvas.RightOf(deProject), readY);
            c.Wire(deProject, deUnits, "Units", "Units");
            var deContext = c.Ifc("Deconstruct Context", Canvas.RightOf(deProject), Canvas.Below(deUnits));
            c.Wire(deProject, deContext, "Context", "Contexts");
            var deGeo = c.Ifc("Deconstruct Georeference", Canvas.RightOf(deProject), Canvas.Below(deContext));
            c.Wire(deProject, deGeo, "Georeference", "Georeference");
            var lengths = Show(c, deUnits, "Length", Canvas.RightOf(deUnits, 30), deUnits.Attributes.Bounds.Y);
            var identifiers = Show(c, deContext, "Identifiers", Canvas.RightOf(deContext, 30), deContext.Attributes.Bounds.Y, 220, 70);
            var crs = Show(c, deGeo, "CRS", Canvas.RightOf(deGeo, 30), deGeo.Attributes.Bounds.Y);
            var eastings = Show(c, deGeo, "Eastings", Canvas.RightOf(deGeo, 30), Canvas.Below(crs, 20));
            c.Group("3. Read back: values come back in Rhino units", Canvas.Colours.Read, read, model, deProject, deUnits, deContext, deGeo, lengths, identifiers, crs, eastings);
        }
    }
}
