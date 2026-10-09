using System;
using System.Drawing;
using IfcHopper.Core.Model;

namespace IfcHopper.Components
{
    public class FacilityComponent : FacilityComponentBase
    {
        public FacilityComponent()
          : base("Facility", "Creates an IFC facility (IfcFacility).")
        {
        }

        protected override FacilityType FacilityType => FacilityType.Facility;

        protected override string OutputNickName => "F";

        protected override Bitmap Icon => Properties.Resources.Facility;

        public override Guid ComponentGuid => new Guid("0e4a7c3b-d81f-4925-a6c2-b9f5e3d1a876");
    }
}
