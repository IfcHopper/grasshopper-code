using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class BuildingComponent : FacilityComponentBase
    {
        public BuildingComponent()
          : base("Building", "Creates an IFC building (IfcBuilding) from storeys.")
        {
        }

        protected override FacilityType FacilityType => FacilityType.Building;

        protected override string OutputNickName => "Bu";

        protected override void RegisterChildrenInput(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new StoreyParam(), "Storeys", "St", "Storeys of the building.", GH_ParamAccess.list);
        }

        protected override Facility CreateFacility(IGH_DataAccess DA, string name)
        {
            var storeys = new List<StoreyGoo>();
            DA.GetDataList(ChildrenIndex, storeys);

            var building = new Building(name);
            building.Storeys.AddRange(storeys.Where(s => s != null && s.IsValid).Select(s => s.Value));
            return building;
        }

        protected override Bitmap Icon => Properties.Resources.Building;

        public override Guid ComponentGuid => new Guid("f1b8e4a7-6d29-4c3e-b5f0-93a2d6e8c715");
    }
}
