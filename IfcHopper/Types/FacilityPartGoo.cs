using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace IfcHopper.Types
{
    public class FacilityPartGoo : IfcHopperGoo<FacilityPart>
    {
        public FacilityPartGoo() { }

        public FacilityPartGoo(FacilityPart value) : base(value) { }

        public override string TypeName => "IFC Facility Part";

        public override string TypeDescription => "IfcHopper facility part (IfcRoadPart, IfcBridgePart, IfcRailwayPart, IfcMarinePart, IfcFacilityPartCommon).";

        public override IGH_Goo Duplicate() => new FacilityPartGoo(Value);

        public override string ToString() => Value == null ? "Null Facility Part" : $"{Value.IfcClass}: {Value.Name}";
    }
}
