using System;
using System.Drawing;
using IfcHopper.Core.Model;

namespace IfcHopper.Components
{
    public class MarinePartComponent : FacilityPartComponentBase
    {
        public MarinePartComponent()
          : base("Marine Part", "Creates an IFC marine part (IfcMarinePart).")
        {
        }

        protected override FacilityType PartKind => FacilityType.MarineFacility;

        protected override Bitmap Icon => Properties.Resources.MarinePart;

        public override Guid ComponentGuid => new Guid("e7d3a9b1-4c58-4f2e-8a6d-15b0c9e4f372");
    }
}
