using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Components;

namespace IfcHopper.Types
{
    public class ElementTypeParam : GH_Param<ElementTypeGoo>
    {
        public ElementTypeParam()
          : base("Element Type", "ET", "IfcHopper element type, shared by its occurrences.", ComponentCategory.Tab, ComponentCategory.Params, GH_ParamAccess.item)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override Bitmap Icon => Properties.Resources.ElementTypeParam;

        public override Guid ComponentGuid => new Guid("5d0c8f3e-7a21-4b96-9e4d-2f61b8c7a903");
    }
}
