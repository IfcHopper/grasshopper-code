using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Components;

namespace IfcHopper.Types
{
    public class ModelParam : GH_Param<ModelGoo>
    {
        public ModelParam()
          : base("Model", "M", "IfcHopper model.", ComponentCategory.Tab, ComponentCategory.Params, GH_ParamAccess.item)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override Bitmap Icon => Properties.Resources.ModelParam;

        public override Guid ComponentGuid => new Guid("8d14b6c9-2e7a-4f3b-a5d8-61c0e9f4b237");
    }
}
