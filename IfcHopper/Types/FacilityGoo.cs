using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace IfcHopper.Types
{
    public class FacilityGoo : IfcHopperGoo<Facility>
    {
        public FacilityGoo() { }

        public FacilityGoo(Facility value) : base(value) { }

        public override string TypeName => "IFC Facility";

        public override string TypeDescription => "IfcHopper facility (IfcFacility, IfcBuilding, IfcBridge, IfcRoad, IfcRailway, IfcMarineFacility).";

        public override IGH_Goo Duplicate() => new FacilityGoo(Value);

        public override string ToString() => Value == null ? "Null Facility" : $"{Value.IfcClass}: {Value.Name}";
    }
}
