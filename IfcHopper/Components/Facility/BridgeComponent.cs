using System;
using System.Drawing;
using IfcHopper.Core.Model;

namespace IfcHopper.Components
{
    public class BridgeComponent : FacilityComponentBase
    {
        public BridgeComponent()
          : base("Bridge", "Creates an IFC bridge (IfcBridge).")
        {
        }

        protected override FacilityType FacilityType => FacilityType.Bridge;

        protected override string OutputNickName => "Br";

        protected override Bitmap Icon => Properties.Resources.Bridge;

        public override Guid ComponentGuid => new Guid("8a1f4d6e-27c9-4b53-a8d0-c3e5b9f2160d");
    }
}
