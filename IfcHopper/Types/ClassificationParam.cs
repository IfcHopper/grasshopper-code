using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Components;

namespace IfcHopper.Types
{
    public class ClassificationParam : GH_Param<ClassificationGoo>
    {
        public ClassificationParam()
          : base("Classification", "Cl", "IfcHopper classification reference (IfcClassificationReference).", ComponentCategory.Tab, ComponentCategory.Params, GH_ParamAccess.item)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override Bitmap Icon => Properties.Resources.ClassificationParam;

        public override Guid ComponentGuid => new Guid("7332e1b5-4fc7-4e78-aaf8-c187d360404d");
    }
}
