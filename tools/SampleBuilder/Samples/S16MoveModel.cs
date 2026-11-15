using System.IO;
using Grasshopper.Kernel;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using IfcHopper.Tests;
using Rhino;

namespace SampleBuilder.Samples
{
    internal sealed class S16MoveModel : Sample
    {
        public override int Number => 16;
        public override string Title => "Move model near origin";
        public override string Summary => "A synthetic 20 mm wall starts at E=500000 m, N=5000000 m. Move Model shifts it by X=-500000, Y=-5000000.\n" +
            "Keep Map Location adjusts the existing georeference: local coordinates become small, projected map positions stay unchanged. Z and axes do not change.";
        public override System.Collections.Generic.IEnumerable<string> ExpectedMessages => new[]
        {
            "Large placement or geometry coordinates detected", "Model geometry and placement origins translated",
        };

        public override bool PrepareRhino(RhinoDoc doc)
        {
            var model = SyntheticLargeCoordinates.Create(true);
            model.Project.Georeference = new Georeference("EPSG:32632");
            IfcBackend.Writer.Write(model, SourcePath);
            return false;
        }

        private static string SourcePath => Path.Combine(TestFilesRoot(), "samples", "ifc", "16 Survey source.ifc");
        private static string TestFilesRoot()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "IfcHopper.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("IfcHopper repository root not found.");
        }

        protected override void BuildBody(Canvas c)
        {
            var read = c.Ifc("Read IFC", 0, Top);
            Canvas.Set(read, "Path", "ifc/16 Survey source.ifc");
            var move = c.Ifc("Move Model", Canvas.RightOf(read, 100), Top);
            c.Wire(read, move, "Model");
            Canvas.Set(move, "X", -500000.0);
            Canvas.Set(move, "Y", -5000000.0);
            Canvas.Set(move, "Keep Map Location", true);
            c.Group("1. Read survey coordinates; shift the complete model while keeping map positions", Canvas.Colours.Read, read, move);
            var find = c.Ifc("Find Objects", Canvas.RightOf(move, 100), Top);
            c.Wire(move, find, "Model");
            Canvas.Set(find, "Classes", "IfcWall");
            var deconstruct = c.Ifc("Deconstruct Object", Canvas.RightOf(find, 100), Top);
            c.Wire(find, deconstruct, "Object", "Elements");
            var placement = Show(c, deconstruct, "Placement", Canvas.RightOf(deconstruct, 40), Top, 260, 70);
            c.Group("2. Preview the wall at the origin; its placement moves with its geometry", Canvas.Colours.Geometry, find, deconstruct, placement);

            var write = c.Ifc("Write IFC", Canvas.RightOf(placement, 80), Top);
            c.Wire(move, write, "Model");
            Canvas.Set(write, "Name", IfcName);
            Canvas.Set(write, "Directory", "ifc");
            var toggle = c.Toggle("Write", true, write.Attributes.Bounds.X, Canvas.Below(write, 50));
            c.Wire(toggle, write, "Write");
            c.BeforeSave.Add(() => toggle.Value = false);
            var path = Show(c, write, "Path", Canvas.RightOf(write, 40), Top, 300, 70);
            c.Group("3. Save explicitly; the source file and upstream model are kept", Canvas.Colours.Output, write, toggle, path);
        }
    }
}
