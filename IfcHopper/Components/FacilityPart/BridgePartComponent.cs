using System;
using System.Drawing;
using IfcHopper.Core.Model;

namespace IfcHopper.Components
{
    public class BridgePartComponent : FacilityPartComponentBase
    {
        public BridgePartComponent()
          : base("Bridge Part", "Creates an IFC bridge part (IfcBridgePart).")
        {
        }

        protected override FacilityType PartKind => FacilityType.Bridge;

        protected override Bitmap Icon => Properties.Resources.BridgePart;

        public override Guid ComponentGuid => new Guid("c8a2f5d1-6e94-4b37-9f0c-3d7e1b5a8c46");
    }
}
