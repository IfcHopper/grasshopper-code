using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace IfcHopper.Types
{
    /// <summary>Classification reference wrapper (IfcClassificationReference).</summary>
    public class ClassificationGoo : IfcHopperGoo<ClassificationReference>
    {
        public ClassificationGoo() { }

        public ClassificationGoo(ClassificationReference value) : base(value) { }

        public override string TypeName => "IFC Classification";

        public override string TypeDescription => "IfcHopper classification reference (IfcClassificationReference).";

        public override IGH_Goo Duplicate() => new ClassificationGoo(Value);

        public override string ToString()
        {
            if (Value == null) return "Null Classification";
            var text = $"{Value.System ?? "(no system)"}: {Value.Code ?? "(no code)"}";
            return Value.Name == null ? text : $"{text} {Value.Name}";
        }
    }
}
