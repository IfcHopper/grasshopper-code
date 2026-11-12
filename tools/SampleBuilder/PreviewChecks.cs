using System;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace SampleBuilder
{
    /// <summary>Host regressions for object components that must preview their outputs without their uncut inputs.</summary>
    internal static class PreviewChecks
    {
        public static void OutputsOnly(string name)
        {
            var canvas = new Canvas();
            using (canvas.Document)
            {
                var component = (GH_Component)canvas.Ifc(name, 0, 0);
                // Replace parameters on this disposable test instance with probes; no display pipeline is needed.
                foreach (var param in component.Params.Input.ToList()) component.Params.UnregisterInputParameter(param);
                foreach (var param in component.Params.Output.ToList()) component.Params.UnregisterOutputParameter(param);
                var input = new PreviewProbe(new BoundingBox(-10, -10, -10, 10, 10, 10));
                var output = new PreviewProbe(new BoundingBox(0, 0, 0, 1, 1, 1));
                component.Params.RegisterInputParam(input);
                component.Params.RegisterOutputParam(output);
                component.Params.OnParametersChanged();

                component.DrawViewportMeshes(null);
                component.DrawViewportWires(null);
                if (input.MeshDraws != 0 || input.WireDraws != 0 || output.MeshDraws != 1 || output.WireDraws != 1)
                    throw new InvalidOperationException($"{name} must draw only output meshes and wires.");
                if (!component.ClippingBox.Equals(output.ClippingBox))
                    throw new InvalidOperationException($"{name} clipping bounds must exclude input geometry.");

                output.Hidden = true;
                component.DrawViewportMeshes(null);
                component.DrawViewportWires(null);
                if (output.MeshDraws != 1 || output.WireDraws != 1 || component.ClippingBox.IsValid)
                    throw new InvalidOperationException($"{name} must honor hidden outputs, including their bounds.");

                output.Hidden = false;
                component.Hidden = true;
                component.DrawViewportMeshes(null);
                component.DrawViewportWires(null);
                component.Hidden = false;
                component.Locked = true;
                component.DrawViewportMeshes(null);
                component.DrawViewportWires(null);
                if (output.MeshDraws != 1 || output.WireDraws != 1)
                    throw new InvalidOperationException($"{name} must not draw when hidden or disabled.");
                Console.WriteLine($"  {name}: output-only meshes, wires, bounds and visibility checks passed.");
            }
        }

        private sealed class PreviewProbe : GH_Param<GH_String>, IGH_PreviewObject
        {
            public PreviewProbe(BoundingBox box)
                : base("Preview probe", "Probe", "Host regression probe.", "Tests", "Tests", GH_ParamAccess.item)
            {
                ClippingBox = box;
            }

            public override Guid ComponentGuid => new Guid("e256c3ee-0c76-4e37-97dc-a79dbcb84a65");
            public bool Hidden { get; set; }
            public bool IsPreviewCapable => true;
            public BoundingBox ClippingBox { get; }
            public int MeshDraws { get; private set; }
            public int WireDraws { get; private set; }
            public void DrawViewportMeshes(IGH_PreviewArgs args) => MeshDraws++;
            public void DrawViewportWires(IGH_PreviewArgs args) => WireDraws++;
        }
    }
}
