namespace IfcHopper.Core.Model
{
    public class Building : Facility
    {
        public ChildView<Storey> Storeys => new ChildView<Storey>(this);

        public const string DefaultName = "Hopper Building";

        public Building(string name = DefaultName) : base(FacilityType.Building, name) { }
    }
}
