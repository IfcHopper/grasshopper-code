using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class DeconstructFacilityComponent : DeconstructComponentBase
    {
        public DeconstructFacilityComponent()
          : base("Deconstruct Facility", "DeFacility",
              "Deconstructs any IFC facility (building, road, bridge, railway, marine facility, facility). Children are loaded on demand.",
              ComponentCategory.Facility)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new FacilityParam(), "Facility", "F", "IfcHopper facility.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            RegisterCommonOutputs(pManager);
            pManager.AddTextParameter("Class", "C", "IFC class, e.g. IfcRoad.", GH_ParamAccess.item);
            pManager.AddTextParameter("Type", "T", "Predefined type, or the object type when user defined.", GH_ParamAccess.item);
            pManager.AddParameter(new StoreyParam(), "Storeys", "St", "Storeys (buildings only).", GH_ParamAccess.list);
            pManager.AddParameter(new FacilityPartParam(), "Parts", "FP", "Facility parts.", GH_ParamAccess.list);
            pManager.AddParameter(new SpaceParam(), "Spaces", "Sp", "Spaces directly in the facility.", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "Elements contained directly in the facility.", GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.OutputDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterOutput(this);
            RegisterPlacementOutput(pManager);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            FacilityGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var facility = goo.Value;
            SetCommonOutputs(DA, facility);
            DA.SetData(3, facility.IfcClass);
            DA.SetData(4, facility.HasPredefinedType ? TypeOf(facility) : null);
            if (facility is Building building) DA.SetDataList(5, building.Storeys.Select(s => new StoreyGoo(s)));
            DA.SetDataList(6, facility.Parts.Select(p => new FacilityPartGoo(p)));
            DA.SetDataList(7, facility.Spaces.Select(s => new SpaceGoo(s)));
            DA.SetDataList(8, facility.Elements.Select(e => new ElementGoo(e)));
            ComponentPropertySets.SetOutput(DA, 9, facility);
            ComponentClassifications.SetOutput(this, DA, facility);
            SetPlacement(DA, 11, facility);
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructFacility;

        public override Guid ComponentGuid => new Guid("f6b0a3d9-7c25-4e18-a9d1-5e2c8b4f7036");
    }
}
