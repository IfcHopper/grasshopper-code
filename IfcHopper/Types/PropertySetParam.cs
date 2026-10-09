using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Components;

namespace IfcHopper.Types
{
    public class PropertySetParam : GH_Param<PropertySetGoo>
    {
        public PropertySetParam()
          : base("Property Set", "PSet", "IfcHopper property set (IfcPropertySet) or quantity set (IfcElementQuantity).", ComponentCategory.Tab, ComponentCategory.Params, GH_ParamAccess.item)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override Bitmap Icon => Properties.Resources.PropertySetParam;

        public override Guid ComponentGuid => new Guid("6f2d8c41-9b3e-4a57-8e1c-d4a0b7f25c96");
    }
}
