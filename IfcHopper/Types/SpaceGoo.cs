using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace IfcHopper.Types
{
    public class SpaceGoo : IfcHopperGoo<Space>
    {
        public SpaceGoo() { }

        public SpaceGoo(Space value) : base(value) { }

        public override string TypeName => "IFC Space";

        public override string TypeDescription => "IfcHopper space (IfcSpace).";

        public override IGH_Goo Duplicate() => new SpaceGoo(Value);

        public override string ToString() => Value == null ? "Null Space" : $"IfcSpace: {Value.Name}";
    }
}
