namespace IfcHopper.Core.Model
{
    public class Storey : ModelObject
    {
        public const string DefaultName = "Hopper Storey";

        /// <summary>Elevation in metres.</summary>
        public double Elevation { get; set; }

        public ChildView<Element> Elements => new ChildView<Element>(this);
        public ChildView<Space> Spaces => new ChildView<Space>(this);

        /// <summary>Nested (partial) storeys.</summary>
        public ChildView<Storey> Storeys => new ChildView<Storey>(this);

        public Storey(string name = DefaultName, double elevation = 0.0) : base(name)
        {
            Elevation = elevation;
        }
    }
}
