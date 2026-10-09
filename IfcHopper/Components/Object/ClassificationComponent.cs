using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class ClassificationComponent : GH_Component
    {
        public ClassificationComponent()
          : base("Classification", "Class",
              "Creates a classification reference (IfcClassificationReference), e.g. Uniclass 2015 Pr_20_93_52. Connect it to the Classifications " +
              "input of objects, types and spatial components; on write equal references are written once.",
              ComponentCategory.Tab, ComponentCategory.Object)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("System", "S", "Classification system (IfcClassification), e.g. Uniclass 2015, OmniClass, NL-SfB.", GH_ParamAccess.item);
            pManager.AddTextParameter("Edition", "E", "Edition of the system, e.g. v1.30.", GH_ParamAccess.item);
            pManager.AddTextParameter("Code", "C", "Identifier of the item in the system, e.g. Pr_20_93_52. Empty: the reference removes the references of its system on Modify components.", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "Name of the item, e.g. Concrete walls.", GH_ParamAccess.item);
            pManager.AddTextParameter("Location", "L", "URI of the item, e.g. its bSDD or web page.", GH_ParamAccess.item);
            for (int i = 1; i < 5; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new ClassificationParam(), "Classification", "Cl", "IfcHopper classification reference.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string system = null, edition = null, code = null, name = null, location = null;
            if (!DA.GetData(0, ref system)) return;
            if (string.IsNullOrWhiteSpace(system))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "System cannot be empty.");
                return;
            }
            DA.GetData(1, ref edition);
            DA.GetData(2, ref code);
            DA.GetData(3, ref name);
            DA.GetData(4, ref location);

            var reference = new ClassificationReference(system, code, name, edition, location);
            if (reference.Code == null)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Without a code this reference removes the references of {reference.System} from objects instead of adding one.");
            DA.SetData(0, new ClassificationGoo(reference));
        }

        protected override Bitmap Icon => Properties.Resources.Classification;

        public override Guid ComponentGuid => new Guid("b446c739-a091-4a44-8a3d-13839dafc900");
    }
}
