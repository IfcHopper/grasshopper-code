using System.Collections.Generic;

namespace IfcHopper.Core.Model
{
    /// <summary>A space (IfcSpace) in a site, facility, storey, facility part or another space.</summary>
    public class Space : TypedModelObject
    {
        public const string DefaultName = "Hopper Space";

        // IfcSpaceTypeEnum from the IFC4X3_ADD2 schema (USERDEFINED and NOTDEFINED excluded).
        private static readonly string[] SpaceTypes = { "BERTH", "EXTERNAL", "GFA", "INTERNAL", "PARKING", "SPACE" };

        public override IReadOnlyList<string> PredefinedTypes => SpaceTypes;

        /// <summary>Nested spaces (partial spaces).</summary>
        public ChildView<Space> Spaces => new ChildView<Space>(this);
        public ChildView<Element> Elements => new ChildView<Element>(this);

        public Space(string name = DefaultName) : base(name) { }
    }
}
