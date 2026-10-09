using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Components;

namespace IfcHopper.Types
{
    public class SpaceParam : GH_Param<SpaceGoo>
    {
        public SpaceParam()
          : base("Space", "Sp", "IfcHopper space.", ComponentCategory.Tab, ComponentCategory.Params, GH_ParamAccess.item)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override Bitmap Icon => Properties.Resources.SpaceParam;

        public override Guid ComponentGuid => new Guid("211b05b1-5e98-4adb-97d1-3c1c99a166cc");
    }
}
