using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace IfcHopper.Types
{
    /// <summary>Element type wrapper. Its geometry is in type coordinates, so it is not previewed.</summary>
    public class ElementTypeGoo : IfcHopperGoo<ElementType>
    {
        public ElementTypeGoo() { }

        public ElementTypeGoo(ElementType value) : base(value) { }

        public override string TypeName => "IFC Element Type";

        public override string TypeDescription => "IfcHopper element type (IfcElementType subtype), shared by its occurrences.";

        public override IGH_Goo Duplicate() => new ElementTypeGoo(Value);

        public override string ToString() => Value == null ? "Null Element Type" : $"{Value.IfcClass}: {Value.Name}";
    }
}
