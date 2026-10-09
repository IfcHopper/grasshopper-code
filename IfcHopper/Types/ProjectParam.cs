using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Components;

namespace IfcHopper.Types
{
    public class ProjectParam : GH_Param<ProjectGoo>
    {
        public ProjectParam()
          : base("Project", "Pr", "IfcHopper project.", ComponentCategory.Tab, ComponentCategory.Params, GH_ParamAccess.item)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override Bitmap Icon => Properties.Resources.ProjectParam;

        public override Guid ComponentGuid => new Guid("c3a7e2f1-58b4-4d09-9e1c-7f2b6a4d8e51");
    }
}
