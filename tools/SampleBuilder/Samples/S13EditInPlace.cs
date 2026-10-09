using System.Drawing;
using Grasshopper.Kernel.Types;

namespace SampleBuilder.Samples
{
    /// <summary>Reads a file, edits a wall and the storey, puts the edits back with Apply Edits and writes the file in place.</summary>
    internal sealed class S13EditInPlace : Sample
    {
        public override int Number => 13;
        public override string Title => "Edit in place";
        public override string Summary =>
            "Modify components output edited copies that keep their GlobalId; Apply Edits puts them back into the read model. Writing it changes only\n" +
            "what was edited and keeps everything else in the file as it was (entities IfcHopper does not model included).";

        protected override void BuildBody(Canvas c)
        {
            var path = c.Panel(0, Top, 240, 30, "ifc/01 First building.ifc");
            var y0 = Canvas.Below(path, 40);
            var read = c.Ifc("Read IFC", 0, y0);
            c.Wire(path, read, "Path");
            var pick = c.Ifc("Find Objects", Canvas.RightOf(read), y0);
            c.Wire(read, pick, "Model");
            Canvas.Set(pick, "Classes", "IfcWall");
            Canvas.Set(pick, "Name", "South*");
            var storey = c.Ifc("Find Objects", Canvas.RightOf(read), Canvas.Below(pick));
            c.Wire(read, storey, "Model");
            Canvas.Set(storey, "Classes", "IfcBuildingStorey");
            c.Group("1. Read the file of sample 01 and find the south wall and the storey", Canvas.Colours.Read, path, read, pick, storey);
            var x = Canvas.RightOf(pick, 80);
            var fire = c.Ifc("Pset", x, Canvas.Below(pick, 200));
            Canvas.Set(fire, "Name", "Pset_WallCommon");
            Canvas.Set(fire, "Names", "IsExternal", "FireRating");
            Canvas.Set(fire, "Values", new GH_Boolean(true), new GH_String("REI 120"));
            var wall = c.Ifc("Modify Object", Canvas.RightOf(fire, 50), Top);
            c.Wire(pick, wall, "Object", "Elements");
            Canvas.Set(wall, "Name", "South wall (renovated)");
            Canvas.Set(wall, "Colour", Color.FromArgb(255, 220, 140, 60));
            c.Wire(fire, wall, "Property Sets");
            var floor = c.Ifc("Modify Storey", Canvas.RightOf(fire, 50), Canvas.Below(wall));
            c.Wire(storey, floor, "Storey", "Objects");
            Canvas.Set(floor, "Name", "Ground floor (renovated)");
            c.Group("2. Edit: rename and recolour the wall, give it a property set; rename the storey", Canvas.Colours.Ifc, fire, wall, floor);

            var apply = c.Ifc("Apply Edits", Canvas.RightOf(wall, 80), Top);
            c.Wire(read, apply, "Model");
            c.Wire(wall, apply, "Edits");
            c.Wire(floor, apply, "Edits");
            var write = c.Ifc("Write IFC", Canvas.RightOf(apply), Top);
            c.Wire(apply, write, "Model");
            Canvas.Set(write, "Name", IfcName);
            Canvas.Set(write, "Directory", "ifc");
            var toggle = c.Toggle("Write", true, Canvas.RightOf(wall, 80), Canvas.Below(apply));
            c.Wire(toggle, write, "Write");
            var output = Show(c, write, "Path", Canvas.RightOf(write, 30), Top, 300, 40);
            c.Group("3. Apply Edits and write: only the edits change, the rest of the file is kept", Canvas.Colours.Output, apply, write, toggle, output);
            c.BeforeSave.Add(() => toggle.Value = false);
        }
    }
}
