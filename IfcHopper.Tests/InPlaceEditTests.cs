using System;
using System.IO;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xunit;

namespace IfcHopper.Tests
{
    /// <summary>Edits of models read from a file, written back onto a copy of that file.</summary>
    public class InPlaceEditTests
    {
        /// <summary>Project > Site > (Building > Storey "L1" with a wall and a slab) + (Bridge > Substructure > Pier with a column).</summary>
        private static string WriteSource()
        {
            var wall = new Element("Wall", "IfcWall");
            wall.SetType("SOLIDWALL");
            var storey = new Storey("L1", 3.0);
            storey.Elements.AddRange(new[] { wall, new Element("Slab", "IfcSlab") });
            var building = new Building("Building");
            building.Storeys.Add(storey);

            var pier = new FacilityPart(FacilityType.Bridge, "Pier");
            pier.Elements.Add(new Element("Column", "IfcColumn"));
            var substructure = new FacilityPart(FacilityType.Bridge, "Substructure");
            substructure.Parts.Add(pier);
            var bridge = new Facility(FacilityType.Bridge, "Bridge");
            bridge.Parts.Add(substructure);

            var site = new Site("Site");
            site.Facilities.AddRange(new Facility[] { building, bridge });
            var project = new Project("Project");
            project.Sites.Add(site);

            var path = TestFiles.NewPath("source.ifc");
            IfcBackend.Writer.Write(new IfcHopperModel(project), path);
            return path;
        }

        /// <summary>Copies of the project and its first site, with the site list replaced so edits to the site are written.</summary>
        private static (Project project, Site site) EditSite(IfcHopperModel model)
        {
            var project = (Project)model.Project.Copy();
            var site = (Site)project.Sites[0].Copy();
            project.Sites.Clear();
            project.Sites.Add(site);
            return (project, site);
        }

        private static IfcHopperModel WriteAndRead(Project project, out string path)
        {
            path = TestFiles.NewPath("edited.ifc");
            IfcBackend.Writer.Write(new IfcHopperModel(project), path);
            return IfcBackend.Reader.Read(path);
        }

        [Fact]
        public void Copy_DoesNotChangeTheOriginal()
        {
            var original = IfcBackend.Reader.Read(WriteSource()).Project.Sites[0];
            var copy = (Site)original.Copy();
            copy.Name = "Renamed";
            copy.Facilities.RemoveAt(0);

            Assert.Equal("Site", original.Name);
            Assert.Equal(2, original.Facilities.Count);
            Assert.Equal(original.GlobalId, copy.GlobalId);
            Assert.Same(original.Source, copy.Source);
        }

        [Fact]
        public void Rename_ChangesOnlyTheName()
        {
            var source = WriteSource();
            var (project, site) = EditSite(IfcBackend.Reader.Read(source));
            site.Name = "Renamed";

            var read = WriteAndRead(project, out var path).Project.Sites[0];

            Assert.Equal("Renamed", read.Name);
            Assert.Equal(project.Sites[0].GlobalId, read.GlobalId);
            Assert.Equal(File.ReadLines(source).Count(), File.ReadLines(path).Count());
        }

        [Fact]
        public void RemovingAChild_DeletesItsSubtree()
        {
            var (project, site) = EditSite(IfcBackend.Reader.Read(WriteSource()));
            site.Facilities.RemoveAll(f => f.Type == FacilityType.Bridge);

            WriteAndRead(project, out var path);
            var lines = File.ReadAllLines(path);

            Assert.DoesNotContain(lines, l => l.Contains("IFCBRIDGE") || l.Contains("IFCCOLUMN"));
            Assert.Contains(lines, l => l.Contains("IFCWALL"));
            var readSite = IfcBackend.Reader.Read(path).Project.Sites[0];
            Assert.Equal("Building", Assert.Single(readSite.Facilities).Name);
        }

        [Fact]
        public void AddingAChild_CreatesIt()
        {
            var (project, site) = EditSite(IfcBackend.Reader.Read(WriteSource()));
            var building = (Building)site.Facilities[0].Copy();
            site.Facilities[0] = building;
            building.Storeys.Add(new Storey("L2", 6.0) { GlobalId = GlobalIds.FromSeed("new storey") });

            var readBuilding = (Building)WriteAndRead(project, out _).Project.Sites[0].Facilities[0];

            Assert.Equal(new[] { "L1", "L2" }, readBuilding.Storeys.Select(s => s.Name));
            Assert.Equal(6.0, readBuilding.Storeys[1].Elevation, 9);
            Assert.Equal(6.0, readBuilding.Storeys[1].Placement.Origin[2], 9);
        }

        [Fact]
        public void TypeAndElevation_AreUpdated()
        {
            var (project, site) = EditSite(IfcBackend.Reader.Read(WriteSource()));
            var building = (Building)site.Facilities[0].Copy();
            site.Facilities[0] = building;
            var storey = (Storey)building.Storeys[0].Copy();
            building.Storeys[0] = storey;
            storey.Elevation = 4.0;
            var wall = (Element)storey.Elements[0].Copy();
            storey.Elements[0] = wall;
            wall.SetType("PARAPET");

            var readStorey = ((Building)WriteAndRead(project, out _).Project.Sites[0].Facilities[0]).Storeys[0];

            Assert.Equal(4.0, readStorey.Elevation, 9);
            Assert.Equal(4.0, readStorey.Placement.Origin[2], 9);
            Assert.Equal("PARAPET", readStorey.Elements[0].PredefinedType);
            Assert.Equal(4.0, readStorey.Elements[0].Placement.Origin[2], 9);
        }

        [Fact]
        public void MovingAParent_MovesItsChildren()
        {
            var (project, site) = EditSite(IfcBackend.Reader.Read(WriteSource()));
            var building = (Building)site.Facilities[0].Copy();
            site.Facilities[0] = building;
            building.Placement = Placement.Translation(10, 0, 0);

            var readBuilding = (Building)WriteAndRead(project, out _).Project.Sites[0].Facilities[0];

            Assert.Equal(new[] { 10.0, 0.0, 0.0 }, readBuilding.Placement.Origin);
            Assert.Equal(new[] { 10.0, 0.0, 3.0 }, readBuilding.Storeys[0].Placement.Origin);
        }

        [Fact]
        public void Georeference_IsAdded()
        {
            var project = (Project)IfcBackend.Reader.Read(WriteSource()).Project.Copy();
            project.Georeference = new Georeference("EPSG:25832") { Eastings = 500000, Northings = 5000000 };

            var read = WriteAndRead(project, out _).Project.Georeference;

            Assert.Equal("EPSG:25832", read.CrsName);
            Assert.Equal(500000, read.Eastings, 6);
        }

        [Fact]
        public void ChangingUnits_IsRejected()
        {
            var project = (Project)IfcBackend.Reader.Read(WriteSource()).Project.Copy();
            project.Units = new Units { Length = Units.Find(UnitKind.Length, "mm") };

            Assert.Throws<NotSupportedException>(() => IfcBackend.Writer.Write(new IfcHopperModel(project), TestFiles.NewPath()));
        }

        [Fact]
        public void LoadedButUnchangedModel_IsWrittenUnchanged()
        {
            var source = WriteSource();
            var model = IfcBackend.Reader.Read(source);
            foreach (var facility in model.Project.Sites.SelectMany(s => s.Facilities))
            {
                foreach (var storey in (facility as Building)?.Storeys ?? Enumerable.Empty<Storey>()) _ = storey.Elements.Count;
                foreach (var part in facility.Parts) _ = part.Parts.SelectMany(p => p.Elements).Count();
            }

            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(model, path);

            Assert.Equal(File.ReadLines(source).Skip(4), File.ReadLines(path).Skip(4));
        }

        [Fact]
        public void NewProjectWithReadObjects_IsReportedAsRebuild()
        {
            var read = IfcBackend.Reader.Read(WriteSource());
            var project = new Project();
            project.Sites.Add(read.Project.Sites[0]);

            Assert.True(new IfcHopperModel(project).RebuildsReadObjects);
            Assert.False(read.RebuildsReadObjects);
        }

        [SampleModelFact]
        public void BridgeSample_RenameAndDeleteKeepOtherData()
        {
            var model = IfcBackend.Reader.Read(TestFiles.BridgeSample);
            var (project, site) = EditSite(model);
            var bridge = site.Facilities[0].Copy();
            site.Facilities[0] = (Facility)bridge;
            bridge.Name = "Renamed bridge";
            var superstructure = (FacilityPart)((Facility)bridge).Parts[1].Copy();
            ((Facility)bridge).Parts[1] = superstructure;
            superstructure.Parts.Clear();

            var read = WriteAndRead(project, out var path).Project.Sites[0].Facilities[0];
            var lines = File.ReadAllLines(path);

            Assert.Equal("Renamed bridge", read.Name);
            Assert.Empty(read.Parts[1].Parts);
            Assert.Equal(6, read.Parts[0].Parts.Count);
            Assert.Contains(lines, l => l.Contains("IFCALIGNMENT"));
            Assert.True(lines.Length < File.ReadLines(TestFiles.BridgeSample).Count());
        }
    }
}
