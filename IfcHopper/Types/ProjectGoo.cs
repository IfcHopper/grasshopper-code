using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace IfcHopper.Types
{
    public class ProjectGoo : IfcHopperGoo<Project>
    {
        public ProjectGoo() { }

        public ProjectGoo(Project value) : base(value) { }

        public override string TypeName => "IFC Project";

        public override string TypeDescription => "IfcHopper project (IfcProject).";

        public override IGH_Goo Duplicate() => new ProjectGoo(Value);

        public override string ToString() => Value == null ? "Null Project" : $"IfcProject: {Value.Name}";
    }
}
