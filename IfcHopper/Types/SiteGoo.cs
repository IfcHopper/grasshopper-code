using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace IfcHopper.Types
{
    public class SiteGoo : IfcHopperGoo<Site>
    {
        public SiteGoo() { }

        public SiteGoo(Site value) : base(value) { }

        public override string TypeName => "IFC Site";

        public override string TypeDescription => "IfcHopper site (IfcSite).";

        public override IGH_Goo Duplicate() => new SiteGoo(Value);

        public override string ToString() => Value == null ? "Null Site" : $"IfcSite: {Value.Name}";
    }
}
