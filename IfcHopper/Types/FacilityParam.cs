using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Components;

namespace IfcHopper.Types
{
    public class FacilityParam : GH_Param<FacilityGoo>
    {
        public FacilityParam()
          : base("Facility", "F", "IfcHopper facility.", ComponentCategory.Tab, ComponentCategory.Params, GH_ParamAccess.item)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override Bitmap Icon => Properties.Resources.FacilityParam;

        public override Guid ComponentGuid => new Guid("2c7f9a4e-b13d-4e68-95a2-d0e6b8f3c571");
    }
}
