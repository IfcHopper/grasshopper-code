using System.IO;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Ifc4.Interfaces;
using Xbim.IO.Memory;
using Xunit;
using Xunit.Abstractions;

namespace IfcHopper.Tests
{
    public class OpeningTests
    {
        private readonly ITestOutputHelper _output;

        public OpeningTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Openings_AreReadWithTheElementTheyVoidAndTheirFills()
        {
            var storey = Storey(IfcBackend.Reader.Read(WriteOpeningSample()));

            Assert.Equal(new[] { "Wall", "Door" }, storey.Elements.Select(e => e.Name));
            var wall = storey.Elements[0];
            var opening = Assert.Single(wall.Openings);
            Assert.IsType<Opening>(opening);
            Assert.Equal(("Hole", "IfcOpeningElement", "OPENING"), (opening.Name, opening.IfcClass, opening.PredefinedType));
            Assert.All(opening.Placement.Origin.Zip(new[] { 2.0, 0, 0 }), c => Assert.Equal(c.Second, c.First, 9));
            Assert.Single(opening.Geometry);
            Assert.Empty(opening.Openings);

            var fill = Assert.Single(opening.Fills);
            Assert.Equal(("Door", "IfcDoor", storey.Elements[1].GlobalId), (fill.Name, fill.IfcClass, fill.GlobalId));
            Assert.Empty(storey.Elements[1].Openings);

            var copy = (Element)wall.Copy();
            Assert.Same(opening, Assert.Single(copy.Openings));
            copy.Openings.Clear();
            Assert.Single(wall.Openings);
        }

        [Fact]
        public void UnchangedHosts_AreWrittenBackWithTheirOpenings()
        {
            var source = WriteOpeningSample();
            var model = IfcBackend.Reader.Read(source);
            var wall = (Element)Storey(model).Elements[0].Copy();
            wall.Name = "Renamed wall";
            _ = wall.Openings[0].Fills;
            var path = TestFiles.NewPath("edited.ifc");

            IfcBackend.Writer.Write(ModelEdits.Apply(model, new[] { wall }, out _), path);

            using (var ifc = MemoryModel.OpenRead(path))
            {
                var opening = ifc.Instances.OfType<IIfcOpeningElement>().Single();
                Assert.Equal("Renamed wall", (string)opening.VoidsElements.RelatingBuildingElement.Name);
                Assert.Equal("Door", (string)opening.HasFillings.Single().RelatedBuildingElement.Name);
            }
        }

        [Theory]
        [InlineData(IfcSchema.Ifc4x3)]
        [InlineData(IfcSchema.Ifc4)]
        [InlineData(IfcSchema.Ifc2x3)]
        public void Openings_AreWrittenWithTheirFills(IfcSchema schema)
        {
            var path = TestFiles.NewPath();
            var warnings = IfcBackend.Writer.Write(NewOpeningModel(), path, schema);
            Assert.DoesNotContain(warnings, w => w.Contains("fill") || w.Contains("void"));

            using (var ifc = MemoryModel.OpenRead(path))
            {
                var opening = ifc.Instances.OfType<IIfcOpeningElement>().Single();
                Assert.Equal("Wall", (string)opening.VoidsElements.RelatingBuildingElement.Name);
                Assert.Equal("Door", (string)opening.HasFillings.Single().RelatedBuildingElement.Name);
                Assert.Empty(opening.ContainedInStructure);
                // Placed relative to the wall, which is at x = 1 m (millimetre file).
                var local = (IIfcLocalPlacement)opening.ObjectPlacement;
                Assert.Same(((IIfcProduct)opening.VoidsElements.RelatingBuildingElement).ObjectPlacement, local.PlacementRelTo);
                Assert.Equal(1000.0, (double)((IIfcAxis2Placement3D)local.RelativePlacement).Location.Coordinates[0].Value, 6);
            }

            var wall = Storey(IfcBackend.Reader.Read(path)).Elements.Single(e => e.Name == "Wall");
            var read = Assert.Single(wall.Openings);
            Assert.Equal(("Hole", "RECESS"), (read.Name, read.PredefinedType ?? read.ObjectType));
            Assert.Equal(2.0, read.Placement.Origin[0], 9);
            Assert.Single(read.Geometry);
            Assert.Equal("Pset_OpeningElementCommon", Assert.Single(read.PropertySets).Name);
            Assert.Equal("Door", Assert.Single(read.Fills).Name);
        }

        [Fact]
        public void ReadOpenings_AreRebuiltInAnotherSchema()
        {
            var path = TestFiles.NewPath("ifc2x3.ifc");
            IfcBackend.Writer.Write(IfcBackend.Reader.Read(WriteOpeningSample()), path, IfcSchema.Ifc2x3);

            var wall = Storey(IfcBackend.Reader.Read(path)).Elements.Single(e => e.Name == "Wall");
            Assert.Equal("Door", Assert.Single(Assert.Single(wall.Openings).Fills).Name);
        }

        [Fact]
        public void FillsOutsideTheModel_AreLeftOutWithAWarning()
        {
            var model = NewOpeningModel();
            var storey = Storey(model);
            storey.Elements.RemoveAll(e => e.Name == "Door");
            var path = TestFiles.NewPath();

            var warnings = IfcBackend.Writer.Write(model, path);

            Assert.Contains(warnings, w => w.Contains("'Door' filling opening 'Hole'"));
            using (var ifc = MemoryModel.OpenRead(path)) Assert.Empty(ifc.Instances.OfType<IIfcRelFillsElement>());
        }

        [Fact]
        public void NewElementsWithOpenings_AreAddedInPlace()
        {
            var model = IfcBackend.Reader.Read(WriteOpeningSample());
            var storey = (Storey)Storey(model).Copy();
            var wall = new Element("New wall", "IfcWall") { GlobalId = GlobalIds.New() };
            wall.Geometry.Add(GeometryTests.Cube());
            var opening = new Opening("New hole");
            opening.Geometry.Add(GeometryTests.Cube());
            opening.Fills.Add(storey.Elements.Single(e => e.Name == "Door"));
            wall.Openings.Add(opening);
            storey.Elements.Add(wall);
            var path = TestFiles.NewPath("added.ifc");

            IfcBackend.Writer.Write(ModelEdits.Apply(model, new[] { storey }, out _), path);

            var read = Storey(IfcBackend.Reader.Read(path)).Elements.ToDictionary(e => e.Name);
            Assert.Equal("Door", Assert.Single(Assert.Single(read["New wall"].Openings).Fills).Name);
            Assert.Equal("Door", Assert.Single(Assert.Single(read["Wall"].Openings).Fills).Name);
        }

        [Fact]
        public void OpeningsAndFills_AreEditedInPlace()
        {
            var source = WriteOpeningSample();
            var model = IfcBackend.Reader.Read(source);
            var wall = (Element)Storey(model).Elements[0].Copy();
            var edited = (Opening)wall.Openings[0].Copy();
            edited.Name = "Wider hole";
            edited.ReplaceGeometry(new[] { GeometryTests.Cube().Transform(p => new[] { p[0] * 2, p[1], p[2] }) });
            edited.Fills.Clear();
            var added = new Opening("Window hole");
            added.SetType("RECESS");
            added.Geometry.Add(GeometryTests.Cube());
            added.Fills.Add(Storey(model).Elements[1]);
            wall.Openings.Clear();
            wall.Openings.AddRange(new[] { edited, added });
            var path = TestFiles.NewPath("edited.ifc");

            IfcBackend.Writer.Write(ModelEdits.Apply(model, new[] { wall }, out _), path);

            var openings = Storey(IfcBackend.Reader.Read(path)).Elements[0].Openings.ToDictionary(o => o.Name);
            Assert.Equal(new[] { "Wider hole", "Window hole" }, openings.Keys);
            Assert.Equal(edited.GlobalId, openings["Wider hole"].GlobalId);
            Assert.Empty(openings["Wider hole"].Fills);
            Assert.Equal(2.0, openings["Wider hole"].Geometry[0].Vertices.Max(v => v[0]) - openings["Wider hole"].Geometry[0].Vertices.Min(v => v[0]), 6);
            Assert.Equal("RECESS", openings["Window hole"].PredefinedType);
            Assert.Equal("Door", Assert.Single(openings["Window hole"].Fills).Name);
            using (var ifc = MemoryModel.OpenRead(path)) Assert.Single(ifc.Instances.OfType<IIfcRelFillsElement>());
        }

        [Fact]
        public void RemovedOpeningsAndDeletedHosts_LeaveNoRelationships()
        {
            var model = IfcBackend.Reader.Read(WriteOpeningSample());
            var wall = (Element)Storey(model).Elements[0].Copy();
            wall.Openings.Clear();
            var removed = TestFiles.NewPath("removed.ifc");
            IfcBackend.Writer.Write(ModelEdits.Apply(model, new[] { wall }, out _), removed);

            var storey = (Storey)Storey(model).Copy();
            storey.Elements.RemoveAt(0);
            var deleted = TestFiles.NewPath("deleted.ifc");
            IfcBackend.Writer.Write(ModelEdits.Apply(model, new[] { storey }, out _), deleted);

            foreach (var path in new[] { removed, deleted })
                using (var ifc = MemoryModel.OpenRead(path))
                {
                    Assert.Empty(ifc.Instances.OfType<IIfcOpeningElement>());
                    Assert.Empty(ifc.Instances.OfType<IIfcRelVoidsElement>());
                    Assert.Empty(ifc.Instances.OfType<IIfcRelFillsElement>());
                    Assert.Single(ifc.Instances.OfType<IIfcDoor>());
                }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Openings_MoveWithTheirHost(bool placedOnStorey)
        {
            string source;
            if (placedOnStorey) source = WriteOpeningSample();
            else
            {
                source = TestFiles.NewPath();
                IfcBackend.Writer.Write(NewOpeningModel(), source);
            }
            var model = IfcBackend.Reader.Read(source);
            var wall = (Element)Storey(model).Elements.Single(e => e.Name == "Wall").Copy();
            var from = wall.Placement;
            var oldOrigin = wall.Openings[0].Placement.Origin;
            wall.Placement = Placement.Translation(from.Origin[0], from.Origin[1] + 3, from.Origin[2]);
            wall.MoveGeometry(from, wall.Placement);
            Assert.Equal(oldOrigin[1] + 3, wall.Openings[0].Placement.Origin[1], 9);
            var path = TestFiles.NewPath("moved.ifc");

            IfcBackend.Writer.Write(ModelEdits.Apply(model, new[] { wall }, out _), path);

            var opening = Storey(IfcBackend.Reader.Read(path)).Elements.Single(e => e.Name == "Wall").Openings.Single();
            Assert.Equal(new[] { oldOrigin[0], oldOrigin[1] + 3, oldOrigin[2] }, opening.Placement.Origin.Select(v => System.Math.Round(v, 6)));
        }

        [SampleModelFact("IFC Schependomlaan.ifc")]
        public void SchependomlaanSample_OpeningsAreRead()
        {
            var project = IfcBackend.Reader.Read(TestFiles.Sample("IFC Schependomlaan.ifc")).Project;
            var elements = Descendants(project).OfType<Element>().ToList();
            var openings = elements.SelectMany(e => e.Openings).ToList();
            _output.WriteLine($"{openings.Count} openings, {openings.Sum(o => o.Fills.Count)} fills, {openings.Count(o => o.Geometry.Count == 0)} without geometry");

            Assert.DoesNotContain(elements, e => e is Opening);
            Assert.True(openings.Count >= 70);
            Assert.True(openings.Sum(o => o.Fills.Count) >= 70);
            Assert.All(openings, o => Assert.NotEmpty(o.Geometry));
        }

        private static System.Collections.Generic.IEnumerable<ModelObject> Descendants(ModelObject parent) =>
            parent.Children.SelectMany(c => new[] { c }.Concat(Descendants(c)));

        private static Storey Storey(IfcHopperModel model) => ((Building)model.Project.Sites[0].Facilities[0]).Storeys[0];

        /// <summary>
        /// A millimetre model with Wall at x = 1 m voided by Hole (a recess at x = 2 m with a property set), which Door fills; Wall and Door are
        /// in the storey.
        /// </summary>
        private static IfcHopperModel NewOpeningModel()
        {
            var door = new Element("Door", "IfcDoor") { GlobalId = GlobalIds.New() };
            var hole = new Opening("Hole") { Placement = new Placement(new[] { 2.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0, 0, 1.0 }) };
            hole.SetType("RECESS");
            hole.Geometry.Add(GeometryTests.Cube());
            hole.Fills.Add(door);
            var set = new PropertySet("Pset_OpeningElementCommon");
            set.Add("Reference", "O1");
            hole.PropertySets.Add(set);
            var wall = new Element("Wall", "IfcWall") { Placement = new Placement(new[] { 1.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0, 0, 1.0 }) };
            wall.Geometry.Add(GeometryTests.Cube());
            wall.Openings.Add(hole);
            var storey = new Storey();
            storey.Elements.Add(wall);
            storey.Elements.Add(door);
            var building = new Building();
            building.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project { Units = new Units { Length = Units.Find(UnitKind.Length, "mm") } };
            project.Sites.Add(site);
            return new IfcHopperModel(project);
        }

        /// <summary>
        /// A storey with Wall, Door and Hole (an IfcOpeningElement with a unit cube at x = 2 m), relinked in the file so that Hole voids
        /// Wall and Door fills Hole.
        /// </summary>
        private static string WriteOpeningSample()
        {
            var wall = new Element("Wall", "IfcWall");
            wall.Geometry.Add(GeometryTests.Cube());
            var hole = new Element("Hole", "IfcOpeningElement") { Placement = new Placement(new[] { 2.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0, 0, 1.0 }) };
            hole.SetType("OPENING");
            hole.Geometry.Add(GeometryTests.Cube());
            var storey = new Storey();
            storey.Elements.Add(wall);
            storey.Elements.Add(new Element("Door", "IfcDoor"));
            storey.Elements.Add(hole);
            var building = new Building();
            building.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project();
            project.Sites.Add(site);
            var written = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(project), written);

            var path = TestFiles.NewPath("openings.ifc");
            using (var ifc = MemoryModel.OpenRead(written))
            {
                using (var txn = ifc.BeginTransaction("openings"))
                {
                    var opening = ifc.Instances.OfType<IIfcOpeningElement>().Single();
                    var ifcWall = ifc.Instances.OfType<IIfcWall>().Single();
                    var door = ifc.Instances.OfType<IIfcDoor>().Single();
                    foreach (var rel in ifc.Instances.OfType<IIfcRelContainedInSpatialStructure>()) rel.RelatedElements.Remove(opening);
                    ifc.Instances.New<Xbim.Ifc4x3.ProductExtension.IfcRelVoidsElement>(r =>
                    {
                        r.GlobalId = GlobalIds.New();
                        ((IIfcRelVoidsElement)r).RelatingBuildingElement = ifcWall;
                        ((IIfcRelVoidsElement)r).RelatedOpeningElement = opening;
                    });
                    ifc.Instances.New<Xbim.Ifc4x3.ProductExtension.IfcRelFillsElement>(r =>
                    {
                        r.GlobalId = GlobalIds.New();
                        ((IIfcRelFillsElement)r).RelatingOpeningElement = opening;
                        ((IIfcRelFillsElement)r).RelatedBuildingElement = door;
                    });
                    txn.Commit();
                }
                using (var stream = File.Create(path)) ifc.SaveAsStep21(stream);
            }
            return path;
        }
    }
}
