namespace IfcHopper.Core.Model
{
    public class Site : ModelObject
    {
        /// <summary>Nested sites, e.g. a building site within an environment site.</summary>
        public ChildView<Site> Sites => new ChildView<Site>(this);
        public ChildView<Facility> Facilities => new ChildView<Facility>(this);
        public ChildView<Space> Spaces => new ChildView<Space>(this);

        /// <summary>Elements contained directly in the site, e.g. terrain or landscaping.</summary>
        public ChildView<Element> Elements => new ChildView<Element>(this);

        public const string DefaultName = "Hopper Site";

        public Site(string name = DefaultName) : base(name) { }
    }
}
