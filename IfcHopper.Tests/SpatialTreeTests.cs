using System;
using System.IO;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Ifc4x3.Kernel;
using Xbim.Ifc4x3.ProductExtension;
using Xbim.IO.Memory;
using Xunit;

namespace IfcHopper.Tests
{
    public class SpatialTreeTests
    {
        /// <summary>
        /// Project > environment site > (terrain element, house site > building > (storey > (wall, room > table), entrance element,
        /// assembly > two parts)).
        /// </summary>
        private static Project NestedProject()
        {
            var room = new Space("Room") { GlobalId = GlobalIds.New() };
            room.SetType("INTERNAL");
            room.Elements.Add(new Element("Table", "IfcFurniture"));
            var storey = new Storey("Ground", 0);
            storey.Elements.Add(new Element("Wall", "IfcWall"));
            storey.Spaces.Add(room);

            var assembly = new Element("Truss", "IfcElementAssembly");
            assembly.Parts.Add(new Element("Chord", "IfcMember"));
            assembly.Parts.Add(new Element("Diagonal", "IfcMember"));
            assembly.Parts[0].Geometry.Add(GeometryTests.Cube());

            var building = new Building("House");
            building.Storeys.Add(storey);
            building.Elements.Add(new Element("Entrance", "IfcDoor"));
            building.Elements.Add(assembly);

            var house = new Site("House site");
            house.Facilities.Add(building);
            var environment = new Site("Environment");
            environment.Elements.Add(new Element("Terrain", "IfcGeographicElement"));
            environment.Sites.Add(house);

            var project = new Project();
            project.Sites.Add(environment);
            return project;
        }

        private static string Write(Project project)
        {
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(project), path);
            return path;
        }

        [Fact]
        public void ChildView_FiltersAndEditsOnlyItsType()
        {
            var site = new Site();
            var facility = new Facility(FacilityType.Road);
            var element = new Element();
            site.Facilities.Add(facility);
            site.Elements.Add(element);

            Assert.Equal(new ModelObject[] { facility, element }, site.Children);
            Assert.Same(facility, Assert.Single(site.Facilities));
            site.Facilities.Clear();
            Assert.Same(element, Assert.Single(site.Children));

            var replacement = new Element("New");
            site.Elements[0] = replacement;
            Assert.Same(replacement, Assert.Single(site.Children));
        }

        [Fact]
        public void Rules_FollowIfcSpatialStructure()
        {
            Assert.True(SpatialRules.Accepts(new Site(), new Site()));
            Assert.True(SpatialRules.Accepts(new Site(), new Element()));
            Assert.True(SpatialRules.Accepts(new Project(), new Building()));
            Assert.True(SpatialRules.Accepts(new Building(), new Building()));
            Assert.True(SpatialRules.Accepts(new Element(), new Element()));
            Assert.False(SpatialRules.Accepts(new Facility(FacilityType.Bridge), new Storey()));
            Assert.False(SpatialRules.Accepts(new Facility(FacilityType.Bridge), new FacilityPart(FacilityType.Road)));
            Assert.False(SpatialRules.Accepts(new Project(), new Element()));
            Assert.False(SpatialRules.Accepts(new Element(), new Space()));

            var bridge = new Facility(FacilityType.Bridge);
            bridge.Children.Add(new Storey());
            var site = new Site();
            site.Facilities.Add(bridge);
            var project = new Project();
            project.Sites.Add(site);
            var error = Assert.Throws<ArgumentException>(() => Write(project));
            Assert.Contains("IfcBuildingStorey", error.Message);
        }

        [Fact]
        public void NestedTree_RoundTripsWithAggregationAndContainment()
        {
            var path = Write(NestedProject());

            using (var ifc = MemoryModel.OpenRead(path))
            {
                var terrain = ifc.Instances.OfType<IfcElement>().Single(e => e.Name == "Terrain");
                Assert.IsType<IfcSite>(Assert.Single(terrain.ContainedInStructure).RelatingStructure);
                var chord = ifc.Instances.OfType<IfcElement>().Single(e => e.Name == "Chord");
                Assert.Empty(chord.ContainedInStructure);
                Assert.Equal("Truss", (string)Assert.Single(chord.Decomposes).RelatingObject.Name);
                var table = ifc.Instances.OfType<IfcElement>().Single(e => e.Name == "Table");
                Assert.IsType<IfcSpace>(Assert.Single(table.ContainedInStructure).RelatingStructure);
            }

            var environment = Assert.Single(IfcBackend.Reader.Read(path).Project.Sites);
            Assert.Equal("Terrain", Assert.Single(environment.Elements).Name);
            var building = (Building)Assert.Single(Assert.Single(environment.Sites).Facilities);
            Assert.Equal(new[] { "Entrance", "Truss" }, building.Elements.Select(e => e.Name));
            var truss = building.Elements[1];
            Assert.Equal(new[] { "Chord", "Diagonal" }, truss.Parts.Select(p => p.Name));
            Assert.Single(truss.Parts[0].Geometry);
            var room = Assert.Single(Assert.Single(building.Storeys).Spaces);
            Assert.Equal(("Room", "INTERNAL"), (room.Name, room.PredefinedType));
            Assert.Equal("Table", Assert.Single(room.Elements).Name);
            Assert.Equal("Wall", Assert.Single(building.Storeys[0].Elements).Name);

            var ancestors = IfcBackend.Reader.GetAncestorIds(truss.Parts[0]);
            Assert.Equal(truss.GlobalId, ancestors.Last());
        }

        [Fact]
        public void ProjectWithoutSite_RoundTrips()
        {
            var building = new Building();
            building.Storeys.Add(new Storey());
            var project = new Project();
            project.Facilities.Add(building);

            var read = IfcBackend.Reader.Read(Write(project)).Project;
            Assert.Empty(read.Sites);
            Assert.Single(((Building)Assert.Single(read.Facilities)).Storeys);
        }

        [Fact]
        public void InPlace_EditsSiteElementsSpacesAndParts()
        {
            var model = IfcBackend.Reader.Read(Write(NestedProject()));
            var environment = model.Project.Sites[0];
            environment.Elements.Clear();
            var building = (Building)environment.Sites[0].Facilities[0];
            building.Elements[1].Parts.Add(new Element("Brace", "IfcMember"));
            building.Storeys[0].Spaces.Add(new Space("Hall"));

            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(model, path);

            using (var ifc = MemoryModel.OpenRead(path))
            {
                Assert.DoesNotContain(ifc.Instances.OfType<IfcElement>(), e => e.Name == "Terrain");
                var brace = ifc.Instances.OfType<IfcElement>().Single(e => e.Name == "Brace");
                Assert.Equal("Truss", (string)Assert.Single(brace.Decomposes).RelatingObject.Name);
                Assert.Equal(new[] { "Hall", "Room" }, ifc.Instances.OfType<IfcSpace>().Select(s => (string)s.Name).OrderBy(n => n));
                Assert.Equal(1, ifc.Instances.OfType<IfcRelAggregates>().Count(r => (string)r.RelatingObject.Name == "Truss"));
            }
        }

        [Fact]
        public void PartAlsoContainedInStorey_IsLoadedOnceUnderItsElement()
        {
            var source = Write(NestedProject());
            var path = TestFiles.NewPath();
            using (var ifc = MemoryModel.OpenRead(source))
            {
                using (var txn = ifc.BeginTransaction("contain part"))
                {
                    var chord = ifc.Instances.OfType<IfcElement>().Single(e => e.Name == "Chord");
                    ifc.Instances.OfType<IfcRelContainedInSpatialStructure>().Single(r => r.RelatingStructure is IfcBuildingStorey).RelatedElements.Add(chord);
                    txn.Commit();
                }
                using (var stream = File.Create(path)) ifc.SaveAsStep21(stream);
            }

            var building = (Building)IfcBackend.Reader.Read(path).Project.Sites[0].Sites[0].Facilities[0];
            Assert.Equal("Wall", Assert.Single(building.Storeys[0].Elements).Name);
            Assert.Equal(2, building.Elements[1].Parts.Count);
        }

        [Fact]
        public void ApplyEdits_ReachesElementParts()
        {
            var model = IfcBackend.Reader.Read(Write(NestedProject()));
            var truss = ((Building)model.Project.Sites[0].Sites[0].Facilities[0]).Elements[1];
            var edit = truss.Parts[1].Copy();
            edit.Name = "Edited diagonal";

            var edited = ModelEdits.Apply(model, new[] { edit }, out var notFound);
            Assert.Empty(notFound);
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(edited, path);
            using (var ifc = MemoryModel.OpenRead(path))
                Assert.Contains(ifc.Instances.OfType<IfcElement>(), e => e.Name == "Edited diagonal");
        }
    }
}
