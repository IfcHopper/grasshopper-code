using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Components;

namespace IfcHopper.Types
{
    public class FacilityPartParam : GH_Param<FacilityPartGoo>
    {
        public FacilityPartParam()
          : base("Facility Part", "FP", "IfcHopper facility part.", ComponentCategory.Tab, ComponentCategory.Params, GH_ParamAccess.item)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override Bitmap Icon => Properties.Resources.FacilityPartParam;

        public override Guid ComponentGuid => new Guid("94e1b7c5-3a6f-4d28-8b0e-f2c9d5a7e13b");
    }
}
