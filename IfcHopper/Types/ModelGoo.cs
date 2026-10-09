using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace IfcHopper.Types
{
    public class ModelGoo : IfcHopperGoo<IfcHopperModel>
    {
        public ModelGoo() { }

        public ModelGoo(IfcHopperModel value) : base(value) { }

        public override bool IsValid => Value?.Project != null;

        public override string TypeName => "IFC Model";

        public override string TypeDescription => "IfcHopper model, ready to be written to IFC.";

        public override IGH_Goo Duplicate() => new ModelGoo(Value);

        public override string ToString() => Value == null ? "Null Model" : $"IfcHopper Model: {Value.Project?.Name}";
    }
}
