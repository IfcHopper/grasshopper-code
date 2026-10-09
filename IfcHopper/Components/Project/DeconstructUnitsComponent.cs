using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class DeconstructUnitsComponent : GH_Component
    {
        public DeconstructUnitsComponent()
          : base("Deconstruct Units", "DeUnits",
              "Deconstructs IFC project units.",
              ComponentCategory.Tab, ComponentCategory.Project)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new UnitsParam(), "Units", "U", "IfcHopper project units.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Length", "L", "Length unit.", GH_ParamAccess.item);
            pManager.AddTextParameter("Area", "A", "Area unit.", GH_ParamAccess.item);
            pManager.AddTextParameter("Volume", "V", "Volume unit.", GH_ParamAccess.item);
            pManager.AddTextParameter("Angle", "An", "Plane angle unit.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            UnitsGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            DA.SetData(0, goo.Value.Length.Symbol);
            DA.SetData(1, goo.Value.Area.Symbol);
            DA.SetData(2, goo.Value.Volume.Symbol);
            DA.SetData(3, goo.Value.Angle.Symbol);
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructUnits;

        public override Guid ComponentGuid => new Guid("4f0a8d3c-e61b-47b9-a2d5-9c3e7f1b6a84");
    }
}
