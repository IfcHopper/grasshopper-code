using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Components;

namespace IfcHopper.Types
{
    public class MaterialParam : GH_Param<MaterialGoo>
    {
        public MaterialParam()
          : base("Material", "Mat", "IfcHopper material: a single material or a layer, constituent or profile set.", ComponentCategory.Tab, ComponentCategory.Params, GH_ParamAccess.item)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override Bitmap Icon => Properties.Resources.MaterialParam;

        public override Guid ComponentGuid => new Guid("cb01791b-97e4-4e9c-af07-a46f0a6c0ac1");
    }
}
