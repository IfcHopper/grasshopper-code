using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xunit;

namespace IfcHopper.Tests
{
    public class ElementTests
    {
        [Theory]
        [InlineData("IfcWall", "IfcWall")]
        [InlineData("ifcwall", "IfcWall")]
        [InlineData(" IfcColumn ", "IfcColumn")]
        [InlineData("IfcBuildingElementProxy", "IfcBuildingElementProxy")]
        [InlineData("IfcWallFoo", null)]
        [InlineData("IfcProduct", null)]
        [InlineData("IfcBuilding", null)]
        public void Schema_FindsInstantiableElementClasses(string name, string expected)
        {
            Assert.Equal(expected, IfcBackend.Schema.FindElementClass(name));
        }

        [Fact]
        public void Schema_ListsPredefinedTypesPerClass()
        {
            Assert.Contains("SOLIDWALL", IfcBackend.Schema.GetPredefinedTypes("IfcWall"));
            Assert.DoesNotContain("USERDEFINED", IfcBackend.Schema.GetPredefinedTypes("IfcWall"));
            Assert.Contains("PIERSTEM", IfcBackend.Schema.GetPredefinedTypes("IfcColumn"));
            Assert.True(IfcBackend.Schema.ElementClasses.Count > 50);
        }

        [Fact]
        public void Element_TypeFollowsItsClass()
        {
            var wall = new Element("W", "IfcWall");
            Assert.True(wall.SetType("solidwall"));
            Assert.Equal("SOLIDWALL", wall.PredefinedType);
            Assert.False(new Element("X", "IfcWallFoo").HasPredefinedType);
        }

        [Fact]
        public void Elements_RoundTripWithTypesAndPlacements()
        {
            var wall = new Element("W1", "IfcWall") { Placement = Placement.Translation(1, 2, 0) };
            wall.SetType("SOLIDWALL");
            var column = new Element("C1", "IfcColumn");
            column.SetType("My Column");
            var storey = new Storey();
            storey.Elements.AddRange(new[] { wall, column, new Element() });
            var building = new Building();
            building.Storeys.Add(storey);

            var deck = new Element("Deck slab", "IfcSlab");
            var part = new FacilityPart(FacilityType.Bridge);
            part.Elements.Add(deck);
            var bridge = new Facility(FacilityType.Bridge);
            bridge.Parts.Add(part);

            var site = new Site();
            site.Facilities.AddRange(new Facility[] { building, bridge });
            var project = new Project();
            project.Sites.Add(site);

            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(project), path);
            var readSite = IfcBackend.Reader.Read(path).Project.Sites[0];
            var elements = ((Building)readSite.Facilities[0]).Storeys[0].Elements;

            Assert.Equal(new[] { "IfcWall", "IfcColumn", "IfcBuildingElementProxy" }, elements.Select(e => e.IfcClass));
            Assert.Equal("SOLIDWALL", elements[0].PredefinedType);
            Assert.Equal(new[] { 1.0, 2.0, 0.0 }, elements[0].Placement.Origin);
            Assert.Equal((TypedModelObject.UserDefined, "My Column"), (elements[1].PredefinedType, elements[1].ObjectType));
            Assert.Equal(Element.DefaultName, elements[2].Name);
            Assert.Equal("Deck slab", Assert.Single(readSite.Facilities[1].Parts[0].Elements).Name);
        }
    }
}
