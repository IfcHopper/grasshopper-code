using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class DeconstructFacilityPartComponent : DeconstructComponentBase
    {
        public DeconstructFacilityPartComponent()
          : base("Deconstruct Facility Part", "DePart",
              "Deconstructs any IFC facility part. Nested parts and elements are loaded on demand.",
              ComponentCategory.FacilityPart)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new FacilityPartParam(), "Part", "FP", "IfcHopper facility part.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            RegisterCommonOutputs(pManager);
            pManager.AddTextParameter("Class", "C", "IFC class, e.g. IfcBridgePart.", GH_ParamAccess.item);
            pManager.AddTextParameter("Type", "T", "Predefined type, or the object type when user defined.", GH_ParamAccess.item);
            pManager.AddTextParameter("Usage", "U", "How the part divides the facility.", GH_ParamAccess.item);
            pManager.AddParameter(new FacilityPartParam(), "Parts", "FP", "Nested parts.", GH_ParamAccess.list);
            pManager.AddParameter(new SpaceParam(), "Spaces", "Sp", "Spaces in the part.", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "Elements contained in the part.", GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.OutputDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterOutput(this);
            RegisterPlacementOutput(pManager);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            FacilityPartGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var part = goo.Value;
            SetCommonOutputs(DA, part);
            DA.SetData(3, part.IfcClass);
            DA.SetData(4, TypeOf(part));
            DA.SetData(5, part.Usage.ToString());
            DA.SetDataList(6, part.Parts.Select(p => new FacilityPartGoo(p)));
            DA.SetDataList(7, part.Spaces.Select(s => new SpaceGoo(s)));
            DA.SetDataList(8, part.Elements.Select(e => new ElementGoo(e)));
            ComponentPropertySets.SetOutput(DA, 9, part);
            ComponentClassifications.SetOutput(this, DA, part);
            SetPlacement(DA, 11, part);
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructFacilityPart;

        public override Guid ComponentGuid => new Guid("1b5f8d2e-c63a-4f97-8e0b-d4a9c7e2f158");
    }
}
