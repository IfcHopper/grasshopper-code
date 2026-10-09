using System;
using System.Drawing;
using IfcHopper.Core.Model;

namespace IfcHopper.Components
{
    public class RoadComponent : FacilityComponentBase
    {
        public RoadComponent()
          : base("Road", "Creates an IFC road (IfcRoad).")
        {
        }

        protected override FacilityType FacilityType => FacilityType.Road;

        protected override string OutputNickName => "Rd";

        protected override Bitmap Icon => Properties.Resources.Road;

        public override Guid ComponentGuid => new Guid("3e9b5c72-a4d1-4f08-b6e3-58c2f1d7a094");
    }
}
