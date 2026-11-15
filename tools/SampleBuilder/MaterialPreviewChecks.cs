using System;
using System.Drawing;
using System.Linq;
using IfcHopper.Core.Model;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace SampleBuilder
{
    /// <summary>Material preview regressions run inside licensed Rhino when building sample 08.</summary>
    internal static class MaterialPreviewChecks
    {
        public static void Run()
        {
            // The duplicate input/output draw fixed for #6 also caused #5's default-colour overlap.
            PreviewChecks.OutputsOnly("Object");
            PreviewChecks.OutputsOnly("Modify Object");

            var canvas = new Canvas();
            using (canvas.Document)
            using (var body = Brep.CreateFromBox(new BoundingBox(0, 0, 0, 1, 1, 1)))
            {
                var first = Color.FromArgb(170, 85, 60);
                var second = Color.FromArgb(110, 115, 125);
                var surface = Color.FromArgb(50, 120, 210);
                var material = canvas.Ifc("Material", 0, 0);
                Canvas.Set(material, "Name", "Preview regression material");
                Canvas.Set(material, "Colour", first);
                var replacement = canvas.Ifc("Material", 0, 0);
                Canvas.Set(replacement, "Name", "Replacement material");
                Canvas.Set(replacement, "Colour", second);

                var unstyled = Object(canvas, body);
                var assigned = Object(canvas, body);
                canvas.Wire(material, assigned, "Material");
                var styled = Object(canvas, body);
                canvas.Wire(material, styled, "Material");
                Canvas.Set(styled, "Colour", surface);

                var type = canvas.Ifc("Element Type", 0, 0);
                Canvas.Set(type, "Name", "Preview regression type");
                canvas.Wire(material, type, "Material");
                var inherited = Object(canvas, body);
                canvas.Wire(type, inherited, "Element Type");

                var modified = canvas.Ifc("Modify Object", 0, 0);
                canvas.Wire(assigned, modified, "Object");
                canvas.Wire(replacement, modified, "Material");
                var modifiedStyle = canvas.Ifc("Modify Object", 0, 0);
                canvas.Wire(styled, modifiedStyle, "Object");
                canvas.Wire(replacement, modifiedStyle, "Material");

                canvas.Document.Enabled = true;
                for (int solve = 0; solve < 2; solve++)
                {
                    var assignedColour = solve == 0 ? first : Color.FromArgb(60, 170, 85);
                    Canvas.Set(material, "Colour", assignedColour);
                    canvas.Document.NewSolution(true, GH_SolutionMode.Silent);
                    foreach (var component in canvas.Document.Objects.OfType<IGH_Component>())
                        if (component.RuntimeMessages(GH_RuntimeMessageLevel.Error).Count > 0)
                            throw new InvalidOperationException($"Material preview setup failed: {component.Name}.");
                    CheckColour(unstyled, null);
                    CheckColour(assigned, assignedColour);
                    CheckColour(inherited, assignedColour);
                    CheckColour(styled, surface);
                    CheckColour(modified, second);
                    CheckColour(modifiedStyle, surface);
                }
                Console.WriteLine("  IFC style data: assigned/type colours, surface precedence and replacement preserved; implicit preview meshes remain colour-free across repeated solves.");
            }
        }

        private static IGH_DocumentObject Object(Canvas canvas, Brep body)
        {
            var component = canvas.Ifc("Object", 0, 0);
            Canvas.Set(component, "Geometry", body);
            return component;
        }

        internal static void CheckColour(IGH_DocumentObject component, Color? expected)
        {
            var goo = Canvas.Output(component).VolatileData.AllData(true).Single();
            if (!goo.CastTo(out GH_Mesh mesh) || mesh?.Value == null)
                throw new InvalidOperationException("The material preview must contain a mesh.");
            using (mesh.Value)
            {
                if (mesh.Value.VertexColors.Count != 0)
                    throw new InvalidOperationException("Implicit mesh casts must allow downstream preview materials to control colour.");
            }
            // Inspect source style data independently of the colour-free display mesh.
            // The runner loads the selected GHA at runtime, so unwrap its public typed Value.
            var element = (Element)goo.GetType().GetProperty("Value").GetValue(goo);
            foreach (var body in element.Geometry)
            {
                var colour = element.DisplayColour(body);
                if (expected.HasValue ? colour == null
                    || Math.Abs(colour.Red - expected.Value.R / 255.0) > 1e-8
                    || Math.Abs(colour.Green - expected.Value.G / 255.0) > 1e-8
                    || Math.Abs(colour.Blue - expected.Value.B / 255.0) > 1e-8
                    || Math.Abs(colour.Transparency - (1 - expected.Value.A / 255.0)) > 1e-8
                    : colour != null)
                    throw new InvalidOperationException($"IFC style data changed for {component.Name}.");
            }
        }
    }
}
