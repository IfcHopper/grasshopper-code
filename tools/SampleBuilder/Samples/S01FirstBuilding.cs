using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace SampleBuilder.Samples
{
    /// <summary>The minimum IFC model: walls and a slab in a storey, building, site and project, written to an IFC file.</summary>
    internal sealed class S01FirstBuilding : Sample
    {
        public override int Number => 1;
        public override string Title => "First building";
        public override string Summary =>
            "The minimum IFC model: objects in a storey, in a building, on a site, in a project, written to a file.\n" +
            "Inputs left empty take defaults (names, placements, GlobalIds, units from the Rhino document).";

        protected override void BuildBody(Canvas c)
        {
            var specs = new[]
            {
                (Centre: new Point3d(3, 2.5, -0.1), Size: new Vector3d(3, 2.5, 0.1), Class: "IfcSlab", Name: "Floor slab", Type: "FLOOR"),
                (Centre: new Point3d(3, 0.1, 1.5), Size: new Vector3d(3, 0.1, 1.5), Class: "IfcWall", Name: "South wall", Type: "SOLIDWALL"),
                (Centre: new Point3d(3, 4.9, 1.5), Size: new Vector3d(3, 0.1, 1.5), Class: "IfcWall", Name: "North wall", Type: "SOLIDWALL"),
            };
            var boxes = new List<IGH_DocumentObject>();
            var objects = new List<IGH_DocumentObject>();
            var y = Top;
            foreach (var spec in specs)
            {
                var box = Box(c, 0, y, spec.Centre, spec.Size);
                var obj = Object(c, Canvas.RightOf(box), y, box, spec.Class, spec.Name, spec.Type);
                boxes.Add(box);
                objects.Add(obj);
                y = Canvas.Below(obj);
            }
            c.Group("1. Geometry (Center Box: half sizes, metres)", Canvas.Colours.Geometry, boxes.ToArray());
            c.Group("2. Objects: IFC class, name and predefined type", Canvas.Colours.Ifc, objects.ToArray());

            var project = House(c, Canvas.RightOf(objects[0], 120), Top + 300, "First building", objects.ToArray());
            WriteFile(c, Canvas.RightOf(project, 80), Top + 300, project, IfcName);
        }
    }
}
