using System;
using System.Drawing;
using IfcHopper.Core.Model;

namespace IfcHopper.Components
{
    public class MarineFacilityComponent : FacilityComponentBase
    {
        public MarineFacilityComponent()
          : base("Marine Facility", "Creates an IFC marine facility (IfcMarineFacility).")
        {
        }

        protected override FacilityType FacilityType => FacilityType.MarineFacility;

        protected override string OutputNickName => "MF";

        protected override Bitmap Icon => Properties.Resources.MarineFacility;

        public override Guid ComponentGuid => new Guid("6b2d9f81-c5e4-4a7b-9e36-1f8a0d4c7b25");
    }
}
