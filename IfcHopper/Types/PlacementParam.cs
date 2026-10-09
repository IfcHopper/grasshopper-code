using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using IfcHopper.Components;
using Rhino.Geometry;
using Rhino.Input;

namespace IfcHopper.Types
{
    /// <summary>Placement parameter of the Placement inputs; takes planes. Hidden: only used as a component input.</summary>
    public class PlacementParam : GH_PersistentParam<PlacementGoo>, IGH_PreviewObject
    {
        public PlacementParam()
          : base("Placement", "Pl", "IfcHopper placement: a world plane in document units.", ComponentCategory.Tab, ComponentCategory.Params)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.hidden;

        protected override GH_GetterResult Prompt_Singular(ref PlacementGoo value)
        {
            if (RhinoGet.GetPlane(out Plane plane) != Rhino.Commands.Result.Success) return GH_GetterResult.cancel;
            value = new PlacementGoo(plane);
            return GH_GetterResult.success;
        }

        protected override GH_GetterResult Prompt_Plural(ref List<PlacementGoo> values)
        {
            PlacementGoo value = null;
            if (Prompt_Singular(ref value) != GH_GetterResult.success) return GH_GetterResult.cancel;
            values = new List<PlacementGoo> { value };
            return GH_GetterResult.success;
        }

        public bool Hidden { get; set; }

        public bool IsPreviewCapable => true;

        public BoundingBox ClippingBox => Preview_ComputeClippingBox();

        public void DrawViewportWires(IGH_PreviewArgs args) => Preview_DrawWires(args);

        public void DrawViewportMeshes(IGH_PreviewArgs args) { }

        public override Guid ComponentGuid => new Guid("4e9a7c21-b3d8-4f56-9e1a-6c2d8b5f0a93");
    }
}
