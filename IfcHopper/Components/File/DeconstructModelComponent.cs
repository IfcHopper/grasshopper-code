using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class DeconstructModelComponent : GH_Component
    {
        public DeconstructModelComponent()
          : base("Deconstruct Model", "DeModel",
              "Deconstructs an IfcHopper model.",
              ComponentCategory.Tab, ComponentCategory.File)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new ModelParam(), "Model", "M", "IfcHopper model.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new ProjectParam(), "Project", "Pr", "IfcHopper project.", GH_ParamAccess.item);
            pManager.AddTextParameter("Author", "A", "Author from the IFC header.", GH_ParamAccess.item);
            pManager.AddTextParameter("Organization", "O", "Organization from the IFC header.", GH_ParamAccess.item);
            pManager.AddTextParameter("Source", "S", "File the model was read from. Empty for models created in Grasshopper.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            ModelGoo model = null;
            if (!DA.GetData(0, ref model) || !model.IsValid) return;

            DA.SetData(0, new ProjectGoo(model.Value.Project));
            DA.SetData(1, model.Value.Author);
            DA.SetData(2, model.Value.Organization);
            DA.SetData(3, model.Value.SourcePath);
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructModel;

        public override Guid ComponentGuid => new Guid("5e8c1b4a-d7f2-4693-b0a5-2c9e6f3d8a17");
    }
}
