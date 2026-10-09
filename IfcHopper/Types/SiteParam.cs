using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Components;

namespace IfcHopper.Types
{
    public class SiteParam : GH_Param<SiteGoo>
    {
        public SiteParam()
          : base("Site", "Si", "IfcHopper site.", ComponentCategory.Tab, ComponentCategory.Params, GH_ParamAccess.item)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override Bitmap Icon => Properties.Resources.SiteParam;

        public override Guid ComponentGuid => new Guid("5b2e8f41-9c3d-4a76-b18e-0d7f3a6c2e95");
    }
}
