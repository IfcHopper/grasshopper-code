using System;
using System.Drawing;
using IfcHopper.Core.Model;

namespace IfcHopper.Components
{
    public class RailwayPartComponent : FacilityPartComponentBase
    {
        public RailwayPartComponent()
          : base("Railway Part", "Creates an IFC railway part (IfcRailwayPart).")
        {
        }

        protected override FacilityType PartKind => FacilityType.Railway;

        protected override Bitmap Icon => Properties.Resources.RailwayPart;

        public override Guid ComponentGuid => new Guid("2f6b8d4e-a1c7-4e93-b5d2-9c0e7f3a1b58");
    }
}
