using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace SampleBuilder.Samples
{
    /// <summary>One model written as IFC4X3_ADD2, IFC4 and IFC2X3, with the downgrade warnings of the older schemas.</summary>
    internal sealed class S14SchemaExport : Sample
    {
        public override int Number => 14;
        public override string Title => "Schema export";
        public override string Summary =>
            "Write IFC writes IFC4X3_ADD2 by default, or IFC4 or IFC2X3. What an older schema lacks is downgraded where IFC allows, and listed as warnings:\n" +
            "here the road, its part and the pavement (IFC 4.3 only) and, in IFC2X3, the material description. The transparent wall keeps its colour in all three.";

        public override IEnumerable<string> ExpectedMessages => _expected;

        private static readonly string[] _expected =
        {
            "IfcRoad is written as IfcBuilding",
            "IfcRoadPart is written as IfcBuildingStorey",
            "IfcPavement is written as IfcBuildingElementProxy",
            "Material descriptions and categories are not saved",
        };

        protected override void BuildBody(Canvas c)
        {
            var pavement = Object(c, 0, Top, null, "IfcPavement", "Pavement");
            Canvas.Set(pavement, "Geometry", Brep.CreateFromBox(new BoundingBox(-10, -12, -0.3, 10, -5, 0)));
            var carriageway = c.Ifc("Road Part", Canvas.RightOf(pavement), Top);
            Canvas.Set(carriageway, "Name", "Carriageway");
            Canvas.Set(carriageway, "Type", "CARRIAGEWAY");
            c.Wire(pavement, carriageway, "Elements");
            var road = c.Ifc("Road", Canvas.RightOf(carriageway), Top);
            Canvas.Set(road, "Name", "Access road");
            c.Wire(carriageway, road, "Parts");
            c.Group("1. A road (IFC 4.3 only)", Canvas.Colours.Ifc, pavement, carriageway, road);

            var y = Canvas.Below(pavement, 60);
            var concrete = c.Ifc("Material", 0, y);
            Canvas.Set(concrete, "Name", "Concrete");
            Canvas.Set(concrete, "Category", "concrete");
            Canvas.Set(concrete, "Description", "C30/37");
            var wall = Object(c, Canvas.RightOf(concrete), y, null, "IfcWall", "Glass-topped wall");
            Canvas.Set(wall, "Geometry", Brep.CreateFromBox(new BoundingBox(0, 0, 0, 6, 0.2, 3)));
            Canvas.Set(wall, "Colour", Color.FromArgb(160, 120, 170, 200));
            c.Wire(concrete, wall, "Material");
            var storey = c.Ifc("Storey", Canvas.RightOf(wall), y);
            c.Wire(wall, storey, "Elements");
            var building = c.Ifc("Building", Canvas.RightOf(storey), y);
            c.Wire(storey, building, "Storeys");
            c.Group("2. A building with a transparent wall and a described material (IFC2X3 writes the colour as a rendering style)", Canvas.Colours.Ifc, concrete, wall, storey, building);

            var site = c.Ifc("Site", Canvas.RightOf(building, 80), Top);
            c.Wire(road, site, "Facilities");
            c.Wire(building, site, "Facilities");
            var project = c.Ifc("Project", Canvas.RightOf(site), Top);
            Canvas.Set(project, "Name", "Schema export");
            c.Wire(site, project, "Sites");
            c.Group("3. Site and project", Canvas.Colours.Ifc, site, project);

            var x = Canvas.RightOf(project, 80);
            var writes = new List<IGH_DocumentObject>();
            var nextY = Top;
            foreach (var schema in new[] { "IFC4X3_ADD2", "IFC4", "IFC2X3" })
            {
                var write = WriteFile(c, x, nextY, project, $"{IfcName} {schema}", schema, false);
                writes.Add(write);
                nextY = Canvas.Below(write, 160);
            }
            c.Group("4. The same model in three schemas: the warnings list what is downgraded", Canvas.Colours.Output, writes.ToArray());
        }
    }
}
