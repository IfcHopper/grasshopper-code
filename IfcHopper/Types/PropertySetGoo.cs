using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace IfcHopper.Types
{
    /// <summary>Property or quantity set wrapper (IfcPropertySet or IfcElementQuantity).</summary>
    public class PropertySetGoo : IfcHopperGoo<PropertySetDefinition>
    {
        public PropertySetGoo() { }

        public PropertySetGoo(PropertySetDefinition value) : base(value) { }

        public override string TypeName => "IFC Property Set";

        public override string TypeDescription => "IfcHopper property set (IfcPropertySet) or quantity set (IfcElementQuantity).";

        public override IGH_Goo Duplicate() => new PropertySetGoo(Value);

        public override string ToString() => Value == null ? "Null Property Set" : $"{Value.IfcClass}: {Value}{(Value.FromType ? " from type" : "")}";
    }
}
