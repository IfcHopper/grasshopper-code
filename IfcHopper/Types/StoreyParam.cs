using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Components;

namespace IfcHopper.Types
{
    public class StoreyParam : GH_Param<StoreyGoo>
    {
        public StoreyParam()
          : base("Storey", "St", "IfcHopper building storey.", ComponentCategory.Tab, ComponentCategory.Params, GH_ParamAccess.item)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override Bitmap Icon => Properties.Resources.StoreyParam;

        public override Guid ComponentGuid => new Guid("1d8a6f3c-e25b-47c9-a03f-6c4e9b2d7f18");
    }
}
