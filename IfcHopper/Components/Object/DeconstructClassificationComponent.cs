using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class DeconstructClassificationComponent : GH_Component
    {
        public DeconstructClassificationComponent()
          : base("Deconstruct Classification", "DeClass",
              "Deconstructs a classification reference (IfcClassificationReference). References nested in other references give the system at the top.",
              ComponentCategory.Tab, ComponentCategory.Object)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new ClassificationParam(), "Classification", "Cl", "IfcHopper classification reference.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("System", "S", "Classification system (IfcClassification name).", GH_ParamAccess.item);
            pManager.AddTextParameter("Edition", "E", "Edition of the system.", GH_ParamAccess.item);
            pManager.AddTextParameter("Code", "C", "Identifier of the item in the system.", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "Name of the item.", GH_ParamAccess.item);
            pManager.AddTextParameter("Location", "L", "URI of the item.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("From Type", "FT", "True when the reference is inherited from the object's type; such references are not written for the object.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            ClassificationGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var reference = goo.Value;
            DA.SetData(0, reference.System);
            DA.SetData(1, reference.Edition);
            DA.SetData(2, reference.Code);
            DA.SetData(3, reference.Name);
            DA.SetData(4, reference.Location);
            DA.SetData(5, reference.FromType);
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructClassification;

        public override Guid ComponentGuid => new Guid("ecb82637-e21c-4b74-8a25-8c636bd8c387");
    }
}
