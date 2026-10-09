using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IfcHopper.Core.Geometry;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Common;
using Xbim.Ifc4x3.GeometryResource;
using Xbim.Ifc4x3.Kernel;
using Xbim.Ifc4x3.MeasureResource;
using Xbim.Ifc4x3.ProductExtension;
using Xbim.Ifc4x3.PropertyResource;
using Xbim.Ifc4x3.RepresentationResource;
using Xbim.Ifc4x3.SharedBldgElements;
using Xbim.IO.Memory;
using Xunit;
using Xunit.Abstractions;

namespace IfcHopper.Tests
{
    public class TypeTests
    {
        private readonly ITestOutputHelper _output;

        public TypeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Type_IsReadOnceWithItsSetsAndGeometry()
        {
            var elements = ReadElements(WriteTypeSample());

            var type = elements["Beam A"].Type;
            Assert.NotNull(type);
            Assert.Same(type, elements["Beam B"].Type);
            Assert.Same(type, elements["Beam C"].Type);
            Assert.Null(elements["Plain"].Type);
            Assert.Equal("Steel beam", type.Name);
            Assert.Equal("IfcBeamType", type.IfcClass);
            Assert.Equal("BEAM", type.PredefinedType);
            Assert.Null(type.Placement);

            var set = Assert.Single(type.PropertySets);
            Assert.Equal("Pset_BeamCommon", set.Name);
            Assert.False(set.FromType);
            Assert.True(elements["Beam A"].PropertySets.Single(s => s.Name == "Pset_BeamCommon").FromType);

            // The map holds the unit cube of Beam A, in its own (element) coordinates and converted from millimetres.
            var cube = Assert.Single(type.Geometry);
            Assert.Equal(GeometryTests.Cube().Vertices.OrderBy(Key), cube.Vertices.OrderBy(Key), new PointComparer());
        }

        [Fact]
        public void TypeTransform_PlacesTheTypeGeometryLikeABlockInstance()
        {
            var elements = ReadElements(WriteTypeSample());

            var a = elements["Beam A"].TypeTransform;
            Assert.Equal(new[] { 2.0, 0, 0 }, a.Origin, new PointComparer().Coordinate);
            Assert.Equal(new[] { 1.0, 0, 0 }, a.X, new PointComparer().Coordinate);

            // Beam B maps the type 500 mm further along X at twice the size.
            var b = elements["Beam B"].TypeTransform;
            Assert.Equal(new[] { 5.5, 0, 0 }, b.Origin, new PointComparer().Coordinate);
            Assert.Equal(new[] { 2.0, 0, 0 }, b.X, new PointComparer().Coordinate);
            Assert.Equal(new[] { 0, 0, 2.0 }, b.Z, new PointComparer().Coordinate);
            foreach (var name in new[] { "Beam A", "Beam B" }) AssertPlacedType(elements[name]);

            Assert.Null(elements["Beam C"].TypeTransform);
            Assert.Null(elements["Plain"].TypeTransform);
        }

        [Fact]
        public void TypeTransform_FollowsMovedGeometryAndIsDroppedByEdits()
        {
            var elements = ReadElements(WriteTypeSample());

            var moved = (Element)elements["Beam B"].Copy();
            var to = new Placement(new[] { 1.0, 2, 3 }, new[] { 0.0, 1, 0 }, new[] { 0.0, 0, 1 });
            moved.MoveGeometry(moved.Placement, to);
            moved.Placement = to;
            AssertPlacedType(moved);
            Assert.NotNull(elements["Beam B"].TypeTransform);

            var recoloured = (Element)elements["Beam A"].Copy();
            recoloured.SetColour(new Colour(1, 0, 0));
            Assert.Null(recoloured.TypeTransform);
            var replaced = (Element)elements["Beam A"].Copy();
            replaced.ReplaceGeometry(new[] { GeometryTests.Cube() });
            Assert.Null(replaced.TypeTransform);
            Assert.Same(elements["Beam A"].Type, replaced.Type);
        }

        [SampleModelFact("IFC Schependomlaan.ifc")]
        public void SchependomlaanSample_TypesAreRead()
        {
            var project = IfcBackend.Reader.Read(TestFiles.Sample("IFC Schependomlaan.ifc")).Project;
            var elements = Descendants(project).OfType<Element>().ToList();
            var typed = elements.Where(e => e.Type != null).ToList();
            var instances = typed.Where(e => e.TypeTransform != null).ToList();
            foreach (var group in typed.GroupBy(e => e.Type.IfcClass)) _output.WriteLine($"{group.Key}: {group.Count()} typed, {group.Count(e => e.TypeTransform != null)} instances");

            Assert.NotEmpty(typed);
            Assert.Contains(typed, e => e.Type.IfcClass == "IfcDoorStyle");
            foreach (var element in instances.Take(50)) AssertPlacedType(element);
        }

        [SampleModelFact]
        public void BridgeSample_TypesAreRead()
        {
            var project = IfcBackend.Reader.Read(TestFiles.BridgeSample).Project;
            var typed = Descendants(project).OfType<Element>().Where(e => e.Type != null).ToList();
            _output.WriteLine($"{typed.Count} typed, {typed.Count(e => e.TypeTransform != null)} instances, {typed.Select(e => e.Type).Distinct().Count()} types");

            Assert.NotEmpty(typed);
            foreach (var element in typed.Where(e => e.TypeTransform != null)) AssertPlacedType(element);
        }

        [Theory]
        [InlineData(IfcSchema.Ifc4x3)]
        [InlineData(IfcSchema.Ifc4)]
        [InlineData(IfcSchema.Ifc2x3)]
        public void WrittenType_IsSharedAndMappedByItsInstances(IfcSchema schema)
        {
            var path = TestFiles.NewPath();
            var warnings = IfcBackend.Writer.Write(TypedModel(out var type), path, schema);
            Assert.DoesNotContain(warnings, w => w.Contains("shear"));

            using (var ifc = MemoryModel.OpenRead(path))
            {
                Assert.Single(ifc.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcTypeProduct>());
                Assert.Single(ifc.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcRelDefinesByType>());
                Assert.Single(ifc.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcRepresentationMap>());
                Assert.Equal(2, ifc.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcMappedItem>().Count());
                Assert.Single(ifc.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcCartesianTransformationOperator3DnonUniform>());
            }

            var elements = ReadElements(path);
            var read = elements["Placed"].Type;
            Assert.Same(read, elements["Scaled"].Type);
            Assert.Same(read, elements["Own"].Type);
            Assert.Equal("IfcColumnType", read.IfcClass);
            Assert.Equal("COLUMN", read.PredefinedType);
            Assert.Equal("Steel", Assert.IsType<PropertySet>(Assert.Single(read.PropertySets))["Material"].Value);
            Assert.Equal(type.Geometry[0].Vertices.OrderBy(Key), Assert.Single(read.Geometry).Vertices.Distinct(new PointComparer()).OrderBy(Key), new PointComparer(1e-9));

            foreach (var name in new[] { "Placed", "Scaled" })
            {
                var original = TypedModelElement(type, name);
                var element = elements[name];
                Assert.Equal(original.TypeTransform.Origin, element.TypeTransform.Origin, new PointComparer(1e-9));
                foreach (var (a, b) in new[] { (original.TypeTransform.X, element.TypeTransform.X), (original.TypeTransform.Y, element.TypeTransform.Y), (original.TypeTransform.Z, element.TypeTransform.Z) })
                    Assert.Equal(a, b, new PointComparer(1e-9));
                AssertPlacedType(element);
            }
            Assert.Null(elements["Own"].TypeTransform);
            Assert.Equal(GeometryTests.Cube(9).Vertices.OrderBy(Key), elements["Own"].Geometry[0].Vertices.Distinct(new PointComparer()).OrderBy(Key), new PointComparer(1e-9));
        }

        [Fact]
        public void ShearedInstance_IsWrittenWithItsOwnGeometry()
        {
            var type = new ElementType("Box", "IfcBuildingElementProxyType");
            type.Geometry.Add(GeometryTests.Cube());
            var element = new Element("Sheared");
            element.PlaceType(type, new Affine(new[] { 1.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.5, 1, 0 }, new[] { 0, 0, 1.0 }));
            var path = TestFiles.NewPath();

            var warnings = IfcBackend.Writer.Write(Model(element), path);

            Assert.Contains(warnings, w => w.Contains("shear"));
            var read = ReadElements(path)["Sheared"];
            Assert.Equal("Box", read.Type.Name);
            Assert.Null(read.TypeTransform);
            Assert.Equal(element.Geometry[0].Vertices, read.Geometry[0].Vertices, new PointComparer(1e-9));
        }

        [Fact]
        public void DoorType_IsWrittenAsAStyleInIfc2x3()
        {
            var type = new ElementType("Door type", "IfcDoorType");
            type.SetType("DOOR");
            var door = new Element("Door", "IfcDoor");
            door.AssignType(type);
            var path = TestFiles.NewPath();

            var warnings = IfcBackend.Writer.Write(Model(door), path, IfcSchema.Ifc2x3);

            Assert.Contains(warnings, w => w.Contains("IfcDoorStyle"));
            Assert.Equal("IfcDoorStyle", ReadElements(path)["Door"].Type.IfcClass);
        }

        [Fact]
        public void NewElementWrittenInPlace_UsesTheTypeOfTheFile()
        {
            var source = WriteTypeSample();
            var model = IfcBackend.Reader.Read(source);
            var storey = ((Building)model.Project.Sites[0].Facilities[0]).Storeys[0];
            var type = storey.Elements.First(e => e.Name == "Beam A").Type;
            var added = new Element("Beam D", "IfcBeam") { Placement = Placement.Translation(14, 0, 0) };
            added.PlaceType(type, added.Placement.ToAffine());
            storey.Elements.Add(added);
            var path = TestFiles.NewPath();

            IfcBackend.Writer.Write(model, path);

            using (var ifc = MemoryModel.OpenRead(path))
            {
                Assert.Single(ifc.Instances.OfType<IfcBeamType>());
                Assert.Equal(4, Assert.Single(ifc.Instances.OfType<IfcRelDefinesByType>()).RelatedObjects.Count);
            }
            var elements = ReadElements(path);
            Assert.Same(elements["Beam A"].Type, elements["Beam D"].Type);
            Assert.Equal(new[] { 14.0, 0, 0 }, elements["Beam D"].TypeTransform.Origin, new PointComparer().Coordinate);
            AssertPlacedType(elements["Beam D"]);
        }

        [Fact]
        public void ReadTypes_AreCarriedOverWhenRebuiltInAnotherSchema()
        {
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(IfcBackend.Reader.Read(WriteTypeSample()), path, IfcSchema.Ifc4);

            var elements = ReadElements(path);
            Assert.Same(elements["Beam A"].Type, elements["Beam C"].Type);
            Assert.Equal("Pset_BeamCommon", Assert.Single(elements["Beam A"].Type.PropertySets).Name);
            Assert.NotNull(elements["Beam B"].TypeTransform);
            AssertPlacedType(elements["Beam B"]);
            Assert.Null(elements["Beam C"].TypeTransform);
        }

        [Fact]
        public void EditedType_IsWrittenOntoTheFileTypeForAllItsOccurrences()
        {
            var model = IfcBackend.Reader.Read(WriteTypeSample());
            var type = (ElementType)ReadElements(model)["Beam A"].Type.Copy();
            Assert.True(type.Edited);
            type.Name = "Heavy beam";
            type.SetType("JOIST");
            var set = new PropertySet("Pset_BeamCommon");
            set.Add("Reference", "B2");
            type.MergePropertySets(new[] { set });
            type.ReplaceGeometry(new[] { GeometryTests.Cube().Transform(p => p.Select(c => c * 2).ToArray()) });
            var path = TestFiles.NewPath();

            IfcBackend.Writer.Write(ModelEdits.Apply(model, new[] { type }, out var notFound), path);

            Assert.Empty(notFound);
            using (var ifc = MemoryModel.OpenRead(path))
            {
                Assert.Single(ifc.Instances.OfType<IfcBeamType>());
                Assert.Single(ifc.Instances.OfType<IfcRepresentationMap>());
                Assert.Single(ifc.Instances.OfType<IfcPropertySet>());
            }
            var elements = ReadElements(path);
            var read = elements["Beam A"].Type;
            Assert.Equal("Heavy beam", read.Name);
            Assert.Equal("JOIST", read.PredefinedType);
            Assert.Equal("B2", ((PropertySet)Assert.Single(read.PropertySets))["Reference"].Value);
            Assert.Equal(type.Geometry[0].Vertices.OrderBy(Key), read.Geometry[0].Vertices.OrderBy(Key), new PointComparer(1e-9));
            foreach (var name in new[] { "Beam A", "Beam B" }) AssertPlacedType(elements[name]);
            Assert.Equal(GeometryTests.Cube().Vertices.Select(p => new[] { p[0] + 8, p[1], p[2] }).OrderBy(Key), elements["Beam C"].Geometry[0].Vertices.OrderBy(Key), new PointComparer(1e-9));
        }

        [Fact]
        public void EditedTypeOfAnEditedElement_IsWrittenToo()
        {
            var model = IfcBackend.Reader.Read(WriteTypeSample());
            var beam = (Element)ReadElements(model)["Beam A"].Copy();
            var type = (ElementType)beam.Type.Copy();
            type.Name = "Renamed";
            beam.PlaceType(type, beam.TypeTransform);
            var path = TestFiles.NewPath();

            IfcBackend.Writer.Write(ModelEdits.Apply(model, new[] { beam }, out _), path);

            var elements = ReadElements(path);
            Assert.Equal("Renamed", elements["Beam B"].Type.Name);
            Assert.Same(elements["Beam A"].Type, elements["Beam B"].Type);
            AssertPlacedType(elements["Beam A"]);
        }

        [Fact]
        public void TypeOfReadElements_IsReassignedAndRemovedInPlace()
        {
            var model = IfcBackend.Reader.Read(WriteTypeSample());
            var read = ReadElements(model);
            var other = new ElementType("Light beam", "IfcBeamType");
            other.Geometry.Add(GeometryTests.Cube().Transform(p => new[] { p[0] * 0.5, p[1], p[2] }));
            var a = (Element)read["Beam A"].Copy();
            a.PlaceType(other, a.TypeTransform);
            var c = (Element)read["Beam C"].Copy();
            c.AssignType(null);
            var path = TestFiles.NewPath();

            IfcBackend.Writer.Write(ModelEdits.Apply(model, new[] { a, c }, out _), path);

            using (var ifc = MemoryModel.OpenRead(path))
            {
                Assert.Equal(2, ifc.Instances.OfType<IfcBeamType>().Count());
                Assert.Equal(new[] { 1, 1 }, ifc.Instances.OfType<IfcRelDefinesByType>().Select(r => r.RelatedObjects.Count).OrderBy(n => n));
            }
            var elements = ReadElements(path);
            Assert.Equal("Light beam", elements["Beam A"].Type.Name);
            Assert.Equal(new[] { 2.0, 0, 0 }, elements["Beam A"].TypeTransform.Origin, new PointComparer().Coordinate);
            AssertPlacedType(elements["Beam A"]);
            Assert.Equal("Steel beam", elements["Beam B"].Type.Name);
            Assert.Null(elements["Beam C"].Type);
        }

        private static Dictionary<string, Element> ReadElements(IfcHopperModel model) =>
            ((Building)model.Project.Sites[0].Facilities[0]).Storeys[0].Elements.ToDictionary(e => e.Name);

        /// <summary>
        /// A column type (unit cube, Pset with Material = Steel) placed by "Placed" (rotated 90° about Z at x = 3) and "Scaled" (mirrored in Y,
        /// scaled 1, 2, 3, at x = 6 with a placement of its own), and associated with "Own", which keeps its own cube at x = 9.
        /// </summary>
        private static IfcHopperModel TypedModel(out ElementType type)
        {
            type = new ElementType("Column type", "IfcColumnType");
            type.SetType("COLUMN");
            type.Geometry.Add(GeometryTests.Cube());
            var set = new PropertySet("Pset_Custom");
            set.Add("Material", "Steel");
            type.PropertySets.Add(set);

            var own = new Element("Own", "IfcColumn");
            own.Geometry.Add(GeometryTests.Cube(9));
            own.AssignType(type);
            return Model(TypedModelElement(type, "Placed"), TypedModelElement(type, "Scaled"), own);
        }

        private static Element TypedModelElement(ElementType type, string name)
        {
            var element = new Element(name, "IfcColumn");
            if (name == "Placed") element.PlaceType(type, new Affine(new[] { 3.0, 0, 0 }, new[] { 0, 1.0, 0 }, new[] { -1.0, 0, 0 }, new[] { 0, 0, 1.0 }));
            else
            {
                element.Placement = Placement.Translation(6, 1, 0);
                element.PlaceType(type, new Affine(new[] { 6.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0, -2.0, 0 }, new[] { 0, 0, 3.0 }));
            }
            return element;
        }

        private static IfcHopperModel Model(params Element[] elements)
        {
            var storey = new Storey();
            storey.Elements.AddRange(elements);
            var building = new Building();
            building.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project { Units = new Units { Length = Units.Find(UnitKind.Length, "mm") } };
            project.Sites.Add(site);
            return new IfcHopperModel(project);
        }

        /// <summary>The element geometry is the type geometry moved by the type transform.</summary>
        private static void AssertPlacedType(Element element)
        {
            var transform = element.TypeTransform;
            var expected = element.Type.Geometry.SelectMany(m => m.Vertices).Select(transform.Apply).OrderBy(Key);
            var actual = element.Geometry.SelectMany(m => m.Vertices).OrderBy(Key);
            Assert.Equal(expected, actual, new PointComparer(1e-6));
        }

        private static (long, long, long) Key(double[] p) => ((long)Math.Round(p[0] * 1e4), (long)Math.Round(p[1] * 1e4), (long)Math.Round(p[2] * 1e4));

        private sealed class PointComparer : IEqualityComparer<double[]>, IEqualityComparer<double>
        {
            private readonly double _tolerance;

            public PointComparer(double tolerance = 1e-9)
            {
                _tolerance = tolerance;
            }

            public IEqualityComparer<double> Coordinate => this;

            public bool Equals(double[] a, double[] b) => a.Length == b.Length && a.Zip(b, (x, y) => Math.Abs(x - y) <= _tolerance * (1 + Math.Abs(x))).All(c => c);
            public int GetHashCode(double[] p) => 0;
            public bool Equals(double a, double b) => Math.Abs(a - b) <= _tolerance;
            public int GetHashCode(double v) => 0;
        }

        private static IEnumerable<ModelObject> Descendants(ModelObject parent) => parent.Children.SelectMany(c => new[] { c }.Concat(Descendants(c)));

        private static Dictionary<string, Element> ReadElements(string path) =>
            ((Building)IfcBackend.Reader.Read(path).Project.Sites[0].Facilities[0]).Storeys[0].Elements.ToDictionary(e => e.Name);

        /// <summary>
        /// A millimetre file with a beam type (Pset_BeamCommon, one Body map holding the unit cube of Beam A) and its occurrences:
        /// Beam A at x = 2 m and Beam B at x = 5 m map it (B 500 mm along X at scale 2), Beam C keeps its own geometry; Plain has no type.
        /// </summary>
        private static string WriteTypeSample()
        {
            var storey = new Storey();
            foreach (var (name, x) in new[] { ("Beam A", 2.0), ("Beam B", 5.0), ("Beam C", 8.0), ("Plain", 11.0) })
            {
                var element = new Element(name, name == "Plain" ? "IfcWall" : "IfcBeam") { Placement = Placement.Translation(x, 0, 0) };
                element.Geometry.Add(GeometryTests.Cube(x));
                storey.Elements.Add(element);
            }
            var building = new Building();
            building.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project { Units = new Units { Length = Units.Find(UnitKind.Length, "mm") } };
            project.Sites.Add(site);
            var source = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(project), source);

            var path = TestFiles.NewPath();
            using (var ifc = MemoryModel.OpenRead(source))
            {
                using (var txn = ifc.BeginTransaction("types"))
                {
                    var byName = ifc.Instances.OfType<IfcElement>().ToDictionary(e => (string)e.Name);
                    IfcShapeRepresentation Body(IfcElement element) => element.Representation.Representations.OfType<IfcShapeRepresentation>().Single();
                    IfcCartesianPoint Point(double x) => ifc.Instances.New<IfcCartesianPoint>(p => p.Coordinates.AddRange(new IfcLengthMeasure[] { x, 0, 0 }));

                    var cube = Body(byName["Beam A"]);
                    var map = ifc.Instances.New<IfcRepresentationMap>(m =>
                    {
                        m.MappingOrigin = ifc.Instances.New<IfcAxis2Placement3D>(a => a.Location = Point(0));
                        m.MappedRepresentation = ifc.Instances.New<IfcShapeRepresentation>(r =>
                        {
                            r.ContextOfItems = cube.ContextOfItems;
                            r.RepresentationIdentifier = "Body";
                            r.RepresentationType = "Tessellation";
                            r.Items.AddRange(cube.Items);
                        });
                    });
                    var type = ifc.Instances.New<IfcBeamType>(t =>
                    {
                        t.GlobalId = GlobalIds.New();
                        t.Name = "Steel beam";
                        t.PredefinedType = IfcBeamTypeEnum.BEAM;
                        t.RepresentationMaps.Add(map);
                        t.HasPropertySets.Add(ifc.Instances.New<IfcPropertySet>(s =>
                        {
                            s.GlobalId = GlobalIds.New();
                            s.Name = "Pset_BeamCommon";
                            s.HasProperties.Add(ifc.Instances.New<IfcPropertySingleValue>(p =>
                            {
                                p.Name = "Reference";
                                p.NominalValue = new IfcIdentifier("B1");
                            }));
                        }));
                    });
                    ifc.Instances.New<IfcRelDefinesByType>(r =>
                    {
                        r.GlobalId = GlobalIds.New();
                        r.RelatingType = type;
                        r.RelatedObjects.AddRange(new[] { byName["Beam A"], byName["Beam B"], byName["Beam C"] });
                    });

                    foreach (var (name, offset, scale) in new[] { ("Beam A", 0.0, 1.0), ("Beam B", 500.0, 2.0) })
                    {
                        var body = Body(byName[name]);
                        body.Items.Clear();
                        body.RepresentationType = "MappedRepresentation";
                        body.Items.Add(ifc.Instances.New<IfcMappedItem>(i =>
                        {
                            i.MappingSource = map;
                            i.MappingTarget = ifc.Instances.New<IfcCartesianTransformationOperator3D>(o =>
                            {
                                o.LocalOrigin = Point(offset);
                                o.Scale = scale;
                            });
                        }));
                    }
                    txn.Commit();
                }
                using (var stream = File.Create(path)) ifc.SaveAsStep21(stream);
            }
            return path;
        }
    }
}
