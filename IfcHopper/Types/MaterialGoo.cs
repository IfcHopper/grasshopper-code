using System.Linq;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace IfcHopper.Types
{
    /// <summary>Material definition wrapper: a single material or a layer, constituent or profile set.</summary>
    public class MaterialGoo : IfcHopperGoo<MaterialDefinition>
    {
        public MaterialGoo() { }

        public MaterialGoo(MaterialDefinition value) : base(value) { }

        public override string TypeName => "IFC Material";

        public override string TypeDescription => "IfcHopper material (IfcMaterial or a material set).";

        public override IGH_Goo Duplicate() => new MaterialGoo(Value);

        public override string ToString()
        {
            if (Value == null) return "Null Material";
            var text = $"{Value.IfcClass}: {Value.Name ?? "(unnamed)"}";
            return Value is Material ? text : $"{text} ({Value.Materials.Count()} parts)";
        }
    }
}
