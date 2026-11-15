using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Components;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace SampleBuilder.Samples
{
    internal sealed class S17ClassColours : Sample
    {
        public override int Number => 17;
        public override string Title => "Class colours";
        public override string Summary => "Class Colours supplies one stable colour per IFC class to native Custom Preview; it does not edit IFC materials or geometry.\n" +
            "The manual palette overrides only IfcWall (with transparency); the slab keeps its automatic colour. Enable one Custom Preview at a time to compare.";

        protected override void BuildBody(Canvas c)
        {
            var wall = Object(c, 0, Top, null, "IfcWall", "Wall A");
            Canvas.Set(wall, "Geometry", Brep.CreateFromBox(new BoundingBox(0, 0, 0, 4, .2, 3)));
            var otherWall = Object(c, 0, Canvas.Below(wall, 50), null, "IfcWall", "Wall B");
            Canvas.Set(otherWall, "Geometry", Brep.CreateFromBox(new BoundingBox(0, 0, 0, .2, 3, 3)));
            var slab = Object(c, 0, Canvas.Below(otherWall, 50), null, "IfcSlab", "Slab");
            Canvas.Set(slab, "Geometry", Brep.CreateFromBox(new BoundingBox(0, 0, -.2, 4, 3, 0)));
            foreach (var obj in new[] { wall, otherWall, slab }) ((IGH_PreviewObject)obj).Hidden = true;
            c.Group("1. Two walls and a slab (standard previews hidden)", Canvas.Colours.Ifc, wall, otherWall, slab);

            var objects = c.Add(new Param_GenericObject { Name = "Objects", NickName = "Objects" }, Canvas.RightOf(wall, 90), Top);
            foreach (var obj in new[] { wall, otherWall, slab }) objects.AddSource(Canvas.Output(obj));
            var automatic = c.Ifc("Class Colours", Canvas.RightOf(objects, 90), Top);
            c.Wire(objects, automatic, "Objects");
            var automaticPreview = (GH_CustomPreviewComponent)c.Native("Custom Preview", Canvas.RightOf(automatic, 70), Top);
            c.Wire(objects, automaticPreview, "Geometry");
            c.Wire(automatic, automaticPreview, "Material");
            automaticPreview.Hidden = true;
            var autoSwatches = Show(c, automatic, "Colours", Canvas.RightOf(automaticPreview, 50), Top, 220, 70);
            c.Group("2. Automatic colours (preview hidden; enable to compare)", Canvas.Colours.Read, automatic, automaticPreview, autoSwatches);

            var manual = c.Ifc("Class Colours", automatic.Attributes.Bounds.X, Canvas.Below(automaticPreview, 140));
            c.Wire(objects, manual, "Objects");
            Canvas.Set(manual, "Classes", "IfcWall");
            var requested = Color.FromArgb(160, 220, 70, 90);
            Canvas.Set(manual, "Colours", requested);
            var manualPreview = (GH_CustomPreviewComponent)c.Native("Custom Preview", Canvas.RightOf(manual, 70), manual.Attributes.Bounds.Y);
            c.Wire(objects, manualPreview, "Geometry");
            c.Wire(manual, manualPreview, "Material");
            var manualSwatches = Show(c, manual, "Colours", Canvas.RightOf(manualPreview, 50), manual.Attributes.Bounds.Y, 220, 70);
            c.Group("3. Partial override: translucent walls, automatic slab", Canvas.Colours.Output, manual, manualPreview, manualSwatches);
            var fallback = c.Ifc("Class Colours", automatic.Attributes.Bounds.X, Canvas.Below(manualPreview, 140));
            c.Wire(objects, fallback, "Objects");
            Canvas.Set(fallback, "Classes", "*", "IfcWall");
            Canvas.Set(fallback, "Colours", Color.Gray, requested);
            var fallbackPreview = (GH_CustomPreviewComponent)c.Native("Custom Preview", Canvas.RightOf(fallback, 70), fallback.Attributes.Bounds.Y);
            c.Wire(objects, fallbackPreview, "Geometry");
            c.Wire(fallback, fallbackPreview, "Material");
            fallbackPreview.Hidden = true;
            var fallbackSwatches = Show(c, fallback, "Colours", Canvas.RightOf(fallbackPreview, 50), fallback.Attributes.Bounds.Y, 220, 70);
            c.Group("4. * colours all other classes grey; exact IfcWall override wins", Canvas.Colours.Output, fallback, fallbackPreview, fallbackSwatches);
            c.BeforeSave.Add(() =>
            {
                var baseline = Colours(automatic);
                for (int solve = 0; solve < 2; solve++)
                {
                    if (solve > 0) c.Document.NewSolution(true, GH_SolutionMode.Silent);
                    var defaults = Colours(automatic);
                    var overrides = Colours(manual);
                    var fallbackColours = Colours(fallback);
                    if (defaults.Length != 3 || !defaults.SequenceEqual(baseline) || defaults[0] != defaults[1]
                        || overrides[0] != requested || overrides[1] != requested || overrides[2] != defaults[2]
                        || fallbackColours[0] != requested || fallbackColours[1] != requested || fallbackColours[2].ToArgb() != Color.Gray.ToArgb())
                        throw new InvalidOperationException("Class Colours sample lost class correspondence, stable defaults or partial overrides.");
                    var autoItems = automaticPreview.DisplayItems.ToList();
                    var manualItems = manualPreview.DisplayItems.ToList();
                    if (autoItems.Count != 3 || manualItems.Count != 3
                        || manualItems[0].Shader.Diffuse.ToArgb() != Color.FromArgb(255, requested.R, requested.G, requested.B).ToArgb()
                        || Math.Abs(manualItems[0].Shader.Transparency - (1 - requested.A / 255.0)) > .001)
                        throw new InvalidOperationException("Native Custom Preview did not receive the class colours/transparency.");
                }
                ClassColourChecks.Run();
            });
        }

        private static Color[] Colours(IGH_DocumentObject component) =>
            Canvas.Output(component).VolatileData.AllData(true).OfType<GH_Colour>().Select(c => c.Value).ToArray();
    }
}
