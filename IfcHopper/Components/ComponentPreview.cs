using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace IfcHopper.Components
{
    /// <summary>Previews the result of an object operation without also drawing its uncut inputs and opening cutters.</summary>
    internal static class ComponentPreview
    {
        // GH_Component previews input and output parameters by default (Grasshopper 8 SDK,
        // GH_Component.DrawViewportMeshes/DrawViewportWires). Object operations must draw only their result;
        // otherwise the uncut Geometry/Object inputs obscure the openings in that result. See issue #6 and
        // https://developer.rhino3d.com/api/grasshopper/html/M_Grasshopper_Kernel_GH_Component_DrawViewportMeshes.htm.
        private static IEnumerable<IGH_PreviewObject> Outputs(GH_Component component) =>
            component.Params.Output.OfType<IGH_PreviewObject>().Where(p => p.IsPreviewCapable && !p.Hidden);

        public static BoundingBox ClippingBox(GH_Component component)
        {
            var box = BoundingBox.Empty;
            foreach (var output in Outputs(component)) box.Union(output.ClippingBox);
            return box;
        }

        public static void DrawMeshes(GH_Component component, IGH_PreviewArgs args)
        {
            if (component.Hidden || component.Locked) return;
            foreach (var output in Outputs(component)) output.DrawViewportMeshes(args);
        }

        public static void DrawWires(GH_Component component, IGH_PreviewArgs args)
        {
            if (component.Hidden || component.Locked) return;
            foreach (var output in Outputs(component)) output.DrawViewportWires(args);
        }
    }
}
