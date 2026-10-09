using System;
using System.Drawing;
using IfcHopper.Core.Model;

namespace IfcHopper.Components
{
    public class FacilityPartComponent : FacilityPartComponentBase
    {
        public FacilityPartComponent()
          : base("Facility Part", "Creates a generic IFC facility part (IfcFacilityPartCommon), usable in any facility.")
        {
        }

        protected override FacilityType PartKind => FacilityType.Facility;

        protected override Bitmap Icon => Properties.Resources.FacilityPart;

        public override Guid ComponentGuid => new Guid("4b1e6c8a-f37d-4a95-9e2b-d6c0a8f5e914");
    }
}
