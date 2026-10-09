using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class DeconstructStoreyComponent : DeconstructComponentBase
    {
        public DeconstructStoreyComponent()
          : base("Deconstruct Storey", "DeStorey",
              "Deconstructs an IFC building storey. Children of a storey read from a file are loaded on demand.",
              ComponentCategory.FacilityPart)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new StoreyParam(), "Storey", "St", "IfcHopper building storey.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            RegisterCommonOutputs(pManager);
            pManager.AddNumberParameter("Elevation", "El", "Storey elevation in document units.", GH_ParamAccess.item);
            pManager.AddParameter(new SpaceParam(), "Spaces", "Sp", "Spaces in the storey.", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "Elements contained in the storey.", GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.OutputDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterOutput(this);
            RegisterPlacementOutput(pManager);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            StoreyGoo storey = null;
            if (!DA.GetData(0, ref storey) || !storey.IsValid) return;

            SetCommonOutputs(DA, storey.Value);
            DA.SetData(3, DocumentUnits.FromMetres(storey.Value.Elevation));
            DA.SetDataList(4, storey.Value.Spaces.Select(s => new SpaceGoo(s)));
            DA.SetDataList(5, storey.Value.Elements.Select(e => new ElementGoo(e)));
            ComponentPropertySets.SetOutput(DA, 6, storey.Value);
            ComponentClassifications.SetOutput(this, DA, storey.Value);
            SetPlacement(DA, 8, storey.Value);
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructStorey;

        public override Guid ComponentGuid => new Guid("8e2d6b1c-f94a-4307-b5e8-a1c7d3f9e264");
    }
}
