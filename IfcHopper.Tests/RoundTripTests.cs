using System;
using System.IO;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xunit;

namespace IfcHopper.Tests
{
    /// <summary>Models written with the IFC writer and read back with the IFC reader.</summary>
    public class RoundTripTests
    {
        private static IfcHopperModel WriteAndRead(IfcHopperModel model, out string path)
        {
            path = TestFiles.NewPath();
            IfcBackend.Writer.Write(model, path);
            return IfcBackend.Reader.Read(path);
        }

        private static IfcHopperModel WriteAndRead(Project project) => WriteAndRead(new IfcHopperModel(project), out _);

        [Fact]
        public void Header_IsWrittenAndRead()
        {
            var model = new IfcHopperModel(new Project("My Project")) { Author = "Tester", Organization = "Org" };
            WriteAndRead(model, out var path);

            var header = IfcBackend.Reader.ReadHeader(path);
            Assert.Equal("My Project", header.ProjectName);
            Assert.Equal(new[] { "IFC4X3_ADD2" }, header.Schemas);
            Assert.Equal(Path.GetFileName(path), header.Name);
            Assert.Equal("IfcHopper", header.OriginatingSystem);
            Assert.Equal(new[] { "Tester" }, header.Author);
            Assert.Equal(new[] { "Org" }, header.Organization);
        }

        [Fact]
        public void SpatialHierarchy_RoundTrips()
        {
            var building = new Building("B1");
            building.Storeys.Add(new Storey("Ground", 0.0));
            building.Storeys.Add(new Storey("First", 3.2) { Description = "Level 1" });
            var site = new Site("S1");
            site.Facilities.Add(building);
            var project = new Project("P1");
            project.Sites.Add(site);

            var read = WriteAndRead(project).Project;

            Assert.Equal("P1", read.Name);
            var readSite = Assert.Single(read.Sites);
            Assert.Equal("S1", readSite.Name);
            var readBuilding = Assert.IsType<Building>(Assert.Single(readSite.Facilities));
            Assert.Equal(new[] { "Ground", "First" }, readBuilding.Storeys.Select(s => s.Name));
            Assert.Equal(3.2, readBuilding.Storeys[1].Elevation, 9);
            Assert.Equal("Level 1", readBuilding.Storeys[1].Description);
        }

        [Fact]
        public void FacilitiesAndParts_RoundTrip()
        {
            var bridge = new Facility(FacilityType.Bridge);
            bridge.SetType("arched");
            var substructure = new FacilityPart(FacilityType.Bridge, "Substructure") { Usage = FacilityUsage.Vertical };
            substructure.SetType("SUBSTRUCTURE");
            var pier = new FacilityPart(FacilityType.Bridge, "P1");
            pier.SetType("PIER");
            substructure.Parts.Add(pier);
            bridge.Parts.Add(substructure);

            var road = new Facility(FacilityType.Road);
            road.SetType("Motorway");
            var lane = new FacilityPart(FacilityType.Road) { Usage = FacilityUsage.Longitudinal };
            lane.SetType("Fast Lane");
            road.Parts.Add(lane);

            var site = new Site();
            site.Facilities.AddRange(new[] { bridge, road, new Facility(FacilityType.Railway), new Facility(FacilityType.MarineFacility), new Facility() });
            var project = new Project();
            project.Sites.Add(site);

            var facilities = WriteAndRead(project).Project.Sites[0].Facilities;

            Assert.Equal(new[] { "IfcBridge", "IfcRoad", "IfcRailway", "IfcMarineFacility", "IfcFacility" }, facilities.Select(f => f.IfcClass));
            Assert.Equal("ARCHED", facilities[0].PredefinedType);
            Assert.Equal("Motorway", facilities[1].ObjectType);

            var readSubstructure = Assert.Single(facilities[0].Parts);
            Assert.Equal(("SUBSTRUCTURE", FacilityUsage.Vertical), (readSubstructure.PredefinedType, readSubstructure.Usage));
            Assert.Equal("PIER", Assert.Single(readSubstructure.Parts).PredefinedType);

            var readLane = Assert.Single(facilities[1].Parts);
            Assert.Equal((TypedModelObject.UserDefined, "Fast Lane", FacilityUsage.Longitudinal), (readLane.PredefinedType, readLane.ObjectType, readLane.Usage));
        }

        [Fact]
        public void Contexts_RoundTrip()
        {
            var model = new RepresentationContext { TrueNorth = new[] { 0.6, 0.8 } };
            model.SubContexts.Add(new RepresentationSubContext("Body"));
            var box = new RepresentationSubContext("Box");
            box.SetTargetView("MY_VIEW");
            model.SubContexts.Add(box);
            var plan = new RepresentationContext(RepresentationContext.PlanType);
            plan.SubContexts.Add(new RepresentationSubContext("Annotation", "PLAN_VIEW") { TargetScale = 0.01 });
            var project = new Project();
            project.Contexts.AddRange(new[] { model, plan });

            var contexts = WriteAndRead(project).Project.Contexts;

            Assert.Equal(new[] { 3, 2 }, contexts.Select(c => c.Dimension));
            Assert.Equal(new[] { 0.6, 0.8 }, contexts[0].TrueNorth);
            Assert.Equal(new[] { "Body", "Box" }, contexts[0].SubContexts.Select(s => s.Identifier));
            Assert.Equal("MY_VIEW", contexts[0].SubContexts[1].UserDefinedTargetView);
            Assert.Equal(0.01, contexts[1].SubContexts[0].TargetScale);
        }

        [Fact]
        public void Contexts_DefaultModelContextWhenNoneGiven()
        {
            var context = Assert.Single(WriteAndRead(new Project()).Project.Contexts);
            Assert.Equal(RepresentationContext.ModelType, context.ContextType);
            Assert.Equal(3, context.Dimension);
        }

        [Fact]
        public void Units_MillimetreFileStoresScaledLengths()
        {
            var building = new Building();
            building.Storeys.Add(new Storey("L1", 3.2));
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project { Units = new Units { Length = Units.Find(UnitKind.Length, "mm") } };
            project.Sites.Add(site);

            var read = WriteAndRead(new IfcHopperModel(project), out var path).Project;

            Assert.Contains(File.ReadLines(path), l => l.Contains("IFCBUILDINGSTOREY") && l.EndsWith(",3200.);"));
            Assert.Equal("Millimetre", read.Units.Length.Name);
            Assert.Equal(3.2, ((Building)read.Sites[0].Facilities[0]).Storeys[0].Elevation, 9);
            Assert.Equal(RepresentationContext.DefaultPrecision, read.Contexts[0].Precision, 12);
        }

        [Fact]
        public void Units_ImperialUnitsRoundTrip()
        {
            var units = new Units
            {
                Length = Units.Find(UnitKind.Length, "ft"),
                Area = Units.Find(UnitKind.Area, "ft2"),
                Volume = Units.Find(UnitKind.Volume, "in3"),
                Angle = Units.Find(UnitKind.Angle, "deg"),
            };

            var read = WriteAndRead(new Project { Units = units }).Project.Units;

            Assert.Equal(new[] { "Foot", "Square foot", "Cubic inch", "Degree" }, new[] { read.Length.Name, read.Area.Name, read.Volume.Name, read.Angle.Name });
        }

        [Fact]
        public void Elements_RoundTripWithClass()
        {
            var storey = new Storey();
            storey.Elements.Add(new Element("W1", "IfcWall"));
            var building = new Building();
            building.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project();
            project.Sites.Add(site);

            var element = Assert.Single(((Building)WriteAndRead(project).Project.Sites[0].Facilities[0]).Storeys[0].Elements);
            Assert.Equal(("W1", "IfcWall"), (element.Name, element.IfcClass));
        }

        [Fact]
        public void Write_RejectsInvalidElementClass()
        {
            var storey = new Storey();
            storey.Elements.Add(new Element("bad", "IfcWallFoo"));
            var building = new Building();
            building.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project();
            project.Sites.Add(site);

            Assert.Throws<ArgumentException>(() => IfcBackend.Writer.Write(new IfcHopperModel(project), TestFiles.NewPath()));
        }

        [Fact]
        public void Write_RejectsPartOfAnotherFacilityKind()
        {
            var road = new Facility(FacilityType.Road);
            road.Parts.Add(new FacilityPart(FacilityType.Bridge));
            var site = new Site();
            site.Facilities.Add(road);
            var project = new Project();
            project.Sites.Add(site);

            Assert.Throws<ArgumentException>(() => IfcBackend.Writer.Write(new IfcHopperModel(project), TestFiles.NewPath()));
        }
    }
}
