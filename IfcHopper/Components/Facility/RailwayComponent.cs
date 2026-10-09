using System;
using System.Drawing;
using IfcHopper.Core.Model;

namespace IfcHopper.Components
{
    public class RailwayComponent : FacilityComponentBase
    {
        public RailwayComponent()
          : base("Railway", "Creates an IFC railway (IfcRailway).")
        {
        }

        protected override FacilityType FacilityType => FacilityType.Railway;

        protected override string OutputNickName => "Rw";

        protected override Bitmap Icon => Properties.Resources.Railway;

        public override Guid ComponentGuid => new Guid("d5c8e2a1-9f37-4064-bb1e-7a4d2c6f9e83");
    }
}
