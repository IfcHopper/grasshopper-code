using System;
using System.Drawing;
using IfcHopper.Core.Model;

namespace IfcHopper.Components
{
    public class RoadPartComponent : FacilityPartComponentBase
    {
        public RoadPartComponent()
          : base("Road Part", "Creates an IFC road part (IfcRoadPart).")
        {
        }

        protected override FacilityType PartKind => FacilityType.Road;

        protected override Bitmap Icon => Properties.Resources.RoadPart;

        public override Guid ComponentGuid => new Guid("51c9e7a3-d2b8-4f60-a4e1-8b3f6c0d9e27");
    }
}
