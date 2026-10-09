using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class ModelComponent : GH_Component
    {
        public ModelComponent()
          : base("Model", "Model",
              "Assembles an IfcHopper model from a project, ready to be written to IFC.",
              ComponentCategory.Tab, ComponentCategory.File)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new ProjectParam(), "Project", "Pr", "IfcHopper project.", GH_ParamAccess.item);
            pManager.AddTextParameter("Author", "A", "Author, written to the IFC header.", GH_ParamAccess.item);
            pManager.AddTextParameter("Organization", "O", "Organization, written to the IFC header.", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new ModelParam(), "Model", "M", "IfcHopper model.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            ProjectGoo project = null;
            string author = null, organization = null;
            if (!DA.GetData(0, ref project) || !project.IsValid) return;
            DA.GetData(1, ref author);
            DA.GetData(2, ref organization);

            var model = new IfcHopperModel(project.Value) { Author = author, Organization = organization };
            DA.SetData(0, new ModelGoo(model));
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override Bitmap Icon => Properties.Resources.Model;

        public override Guid ComponentGuid => new Guid("4a9c3e17-b6d2-4f85-9c0a-e7b2d5f1a368");
    }
}
