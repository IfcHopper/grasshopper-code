using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace IfcHopper.Types
{
    public class StoreyGoo : IfcHopperGoo<Storey>
    {
        public StoreyGoo() { }

        public StoreyGoo(Storey value) : base(value) { }

        public override string TypeName => "IFC Storey";

        public override string TypeDescription => "IfcHopper building storey (IfcBuildingStorey).";

        public override IGH_Goo Duplicate() => new StoreyGoo(Value);

        public override string ToString() => Value == null ? "Null Storey" : $"IfcBuildingStorey: {Value.Name}";
    }
}
