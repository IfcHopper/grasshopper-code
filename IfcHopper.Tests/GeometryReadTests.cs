using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IfcHopper.Core.Geometry;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Common;
using Xbim.Ifc4x3.GeometricModelResource;
using Xbim.Ifc4x3.GeometryResource;
using Xbim.Ifc4x3.MeasureResource;
using Xbim.Ifc4x3.ProductExtension;
using Xbim.Ifc4x3.ProfileResource;
using Xbim.Ifc4x3.RepresentationResource;
using Xbim.IO.Memory;
using Xunit;
using Xunit.Abstractions;

namespace IfcHopper.Tests
{
    public class GeometryReadTests
    {
        private readonly ITestOutputHelper _output;

        public GeometryReadTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Triangulator_HandlesHolesAndConcavePolygons()
        {
            var square = new[] { P(0, 0), P(4, 0), P(4, 4), P(0, 4) };
            var hole = new[] { P(1, 1), P(1, 3), P(3, 3), P(3, 1) };
            var all = square.Concat(hole).ToList();
            var triangles = Triangulator.Triangulate(square, new[] { hole });
            Assert.Equal(8, triangles.Count);
            Assert.Equal(12, triangles.Sum(t => Area(all, t)), 9);
            Assert.All(triangles, t => Assert.True(Area(all, t) > 0));

            var l = new[] { P(0, 0), P(2, 0), P(2, 1), P(1, 1), P(1, 2), P(0, 2) };
            var lTriangles = Triangulator.Triangulate(l);
            Assert.Equal(4, lTriangles.Count);
            Assert.Equal(3, lTriangles.Sum(t => Area(l, t)), 9);
        }

        [Fact]
        public void Mesh_RoundTripsInWorldCoordinatesWithRotatedPlacement()
        {
            var cube = GeometryTests.Cube(2);
            var element = new Element("Box") { Placement = new Placement(new[] { 1.0, 2, 3 }, new[] { 0.0, 1, 0 }, new[] { 0.0, 0, 1 }) };
            element.Geometry.Add(cube);
            var read = ReadElement(GeometryTests.Write(element, new Units { Length = Units.Find(UnitKind.Length, "mm") }));

            var mesh = Assert.Single(read.Geometry);
            Assert.Empty(read.SkippedGeometry);
            Assert.True(mesh.IsClosed);
            Assert.Equal(cube.Faces, mesh.Faces);
            for (int i = 0; i < cube.Vertices.Count; i++)
                for (int k = 0; k < 3; k++) Assert.Equal(cube.Vertices[i][k], mesh.Vertices[i][k], 9);
        }

        [Fact]
        public void HollowRectangleExtrusion_IsFacetedClosedAndPlaced()
        {
            var path = WriteWithBody(ifc => new IfcRepresentationItem[]
            {
                Extruded(ifc, ifc.Instances.New<IfcRectangleHollowProfileDef>(p =>
                {
                    p.ProfileType = IfcProfileTypeEnum.AREA;
                    p.XDim = 2000;
                    p.YDim = 1000;
                    p.WallThickness = 100;
                }), 3000),
            });

            var mesh = Assert.Single(ReadElement(path).Geometry);
            Assert.True(mesh.IsClosed);
            Assert.Equal((2 * 1 - 1.8 * 0.8) * 3, mesh.SignedVolume(), 9);
            Assert.Equal(0, mesh.Vertices.Min(v => v[0]), 9);
            Assert.Equal(2, mesh.Vertices.Max(v => v[0]), 9);
            Assert.Equal(3, mesh.Vertices.Max(v => v[2]), 9);
        }

        [Fact]
        public void HalfSpaceClipping_CutsAndCapsWithHoles()
        {
            var path = WriteWithBody(ifc =>
            {
                var profile = ifc.Instances.New<IfcRectangleHollowProfileDef>(p =>
                {
                    p.ProfileType = IfcProfileTypeEnum.AREA;
                    p.XDim = 2000;
                    p.YDim = 1000;
                    p.WallThickness = 100;
                });
                IfcHalfSpaceSolid HalfSpace(double z, bool agreement) => ifc.Instances.New<IfcHalfSpaceSolid>(h =>
                {
                    h.BaseSurface = ifc.Instances.New<IfcPlane>(p => p.Position = ifc.Instances.New<IfcAxis2Placement3D>(a =>
                    {
                        a.Location = Point(ifc, 0, 0, z);
                        a.Axis = Direction(ifc, 0, 0, 1);
                        a.RefDirection = Direction(ifc, 1, 0, 0);
                    }));
                    h.AgreementFlag = agreement;
                });
                var top = ifc.Instances.New<IfcBooleanClippingResult>(c =>
                {
                    c.Operator = IfcBooleanOperator.DIFFERENCE;
                    c.FirstOperand = Extruded(ifc, profile, 3000);
                    c.SecondOperand = HalfSpace(2000, true);
                });
                return new IfcRepresentationItem[]
                {
                    ifc.Instances.New<IfcBooleanClippingResult>(c =>
                    {
                        c.Operator = IfcBooleanOperator.DIFFERENCE;
                        c.FirstOperand = top;
                        c.SecondOperand = HalfSpace(1000, false);
                    }),
                };
            });

            var element = ReadElement(path);
            Assert.Empty(element.SkippedGeometry);
            var mesh = Assert.Single(element.Geometry);
            Assert.True(mesh.IsClosed);
            Assert.Equal((2 * 1 - 1.8 * 0.8) * 1, mesh.SignedVolume(), 9);
            Assert.Equal(1, mesh.Vertices.Min(v => v[2]), 9);
            Assert.Equal(2, mesh.Vertices.Max(v => v[2]), 9);
        }

        [Fact]
        public void TrimmedCircleInProfile_IsFacetedAsArc()
        {
            var path = WriteWithBody(ifc =>
            {
                IfcCompositeCurveSegment Segment(IfcCurve parent) => ifc.Instances.New<IfcCompositeCurveSegment>(s =>
                {
                    s.Transition = IfcTransitionCode.CONTINUOUS;
                    s.SameSense = true;
                    s.ParentCurve = parent;
                });
                IfcPolyline Polyline(params IfcCartesianPoint[] points) => ifc.Instances.New<IfcPolyline>(p => p.Points.AddRange(points));
                IfcCartesianPoint P2(double x, double y) => ifc.Instances.New<IfcCartesianPoint>(p => p.Coordinates.AddRange(new IfcLengthMeasure[] { x, y }));

                // Right half of a 1 m circle closing a 2 x 2 m square, from (2000, 0) counter-clockwise to (2000, 2000).
                var arc = ifc.Instances.New<IfcTrimmedCurve>(t =>
                {
                    t.BasisCurve = ifc.Instances.New<IfcCircle>(c =>
                    {
                        c.Radius = 1000;
                        c.Position = ifc.Instances.New<IfcAxis2Placement2D>(a => a.Location = P2(2000, 1000));
                    });
                    t.Trim1.Add(P2(2000, 0));
                    t.Trim2.Add(P2(2000, 2000));
                    t.SenseAgreement = true;
                    t.MasterRepresentation = IfcTrimmingPreference.CARTESIAN;
                });
                var outline = ifc.Instances.New<IfcCompositeCurve>(c =>
                {
                    c.SelfIntersect = false;
                    c.Segments.Add(Segment(Polyline(P2(0, 0), P2(2000, 0))));
                    c.Segments.Add(Segment(arc));
                    c.Segments.Add(Segment(Polyline(P2(2000, 2000), P2(0, 2000), P2(0, 0))));
                });
                var profile = ifc.Instances.New<IfcArbitraryClosedProfileDef>(p =>
                {
                    p.ProfileType = IfcProfileTypeEnum.AREA;
                    p.OuterCurve = outline;
                });
                return new IfcRepresentationItem[] { Extruded(ifc, profile, 1000) };
            });

            var element = ReadElement(path);
            Assert.Empty(element.SkippedGeometry);
            var mesh = Assert.Single(element.Geometry);
            Assert.InRange(mesh.SignedVolume(), 4 + Math.PI / 2 - 0.03, 4 + Math.PI / 2);
            Assert.Equal(1 + 3, mesh.Vertices.Max(v => v[0]), 9);
        }

        [SampleModelFact("IFC Schependomlaan.ifc")]
        public void SchependomlaanSample_ClippingsAndTrimmedProfilesAreRead()
        {
            var project = IfcBackend.Reader.Read(TestFiles.Sample("IFC Schependomlaan.ifc")).Project;
            var elements = Descendants(project).OfType<Element>().ToList();
            var meshes = elements.SelectMany(e => e.Geometry).ToList();
            var skipped = elements.SelectMany(e => e.SkippedGeometry).ToList();

            _output.WriteLine($"{elements.Count} elements, {elements.Count(e => e.Geometry.Count > 0)} with geometry, {meshes.Count} meshes");
            foreach (var group in skipped.GroupBy(s => s)) _output.WriteLine($"skipped {group.Key}: {group.Count()}");

            Assert.DoesNotContain(skipped, s => s.Contains("IfcTrimmedCurve") || s.Contains("IfcBooleanClippingResult"));
            Assert.All(meshes.Where(m => m.IsClosed), m => Assert.True(m.SignedVolume() > 0));
        }

        private static IEnumerable<ModelObject> Descendants(ModelObject parent) => parent.Children.SelectMany(c => new[] { c }.Concat(Descendants(c)));

        [Fact]
        public void MirroredMappedItem_KeepsOutwardFaces()
        {
            var path = WriteWithBody(ifc =>
            {
                var profile = ifc.Instances.New<IfcIShapeProfileDef>(p =>
                {
                    p.ProfileType = IfcProfileTypeEnum.AREA;
                    p.OverallWidth = 200;
                    p.OverallDepth = 400;
                    p.WebThickness = 10;
                    p.FlangeThickness = 20;
                });
                var map = ifc.Instances.New<IfcRepresentationMap>(m =>
                {
                    m.MappingOrigin = ifc.Instances.New<IfcAxis2Placement3D>(a => a.Location = Point(ifc, 0, 0, 0));
                    m.MappedRepresentation = ifc.Instances.New<IfcShapeRepresentation>(r =>
                    {
                        r.ContextOfItems = ifc.Instances.OfType<IfcGeometricRepresentationContext>().First();
                        r.RepresentationType = "SweptSolid";
                        r.Items.Add(Extruded(ifc, profile, 1000));
                    });
                });
                return new IfcRepresentationItem[]
                {
                    ifc.Instances.New<IfcMappedItem>(i =>
                    {
                        i.MappingSource = map;
                        i.MappingTarget = ifc.Instances.New<IfcCartesianTransformationOperator3D>(o =>
                        {
                            o.Axis1 = Direction(ifc, -1, 0, 0);
                            o.LocalOrigin = Point(ifc, 5000, 0, 0);
                        });
                    }),
                };
            });

            var mesh = Assert.Single(ReadElement(path).Geometry);
            Assert.Equal(2 * 0.2 * 0.02 + 0.36 * 0.01, mesh.SignedVolume(), 9);
            Assert.Equal(6, (mesh.Vertices.Min(v => v[0]) + mesh.Vertices.Max(v => v[0])) / 2, 9);
        }

        [Fact]
        public void UnsupportedItems_AreListedAsSkipped()
        {
            var path = WriteWithBody(ifc => new IfcRepresentationItem[]
            {
                ifc.Instances.New<IfcSphere>(s =>
                {
                    s.Radius = 100;
                    s.Position = ifc.Instances.New<IfcAxis2Placement3D>(a => a.Location = Point(ifc, 0, 0, 0));
                }),
            });

            var element = ReadElement(path);
            Assert.Empty(element.Geometry);
            Assert.Equal(new[] { "IfcSphere" }, element.SkippedGeometry);
        }

        [SampleModelFact]
        public void BridgeSample_GeometryIsFaceted()
        {
            var project = IfcBackend.Reader.Read(TestFiles.BridgeSample).Project;
            var elements = project.Sites.SelectMany(s => s.Facilities).SelectMany(f => f.Parts).SelectMany(AllElements).ToList();
            var meshes = elements.SelectMany(e => e.Geometry).ToList();
            var skipped = elements.SelectMany(e => e.SkippedGeometry).ToList();

            _output.WriteLine($"{elements.Count} elements, {elements.Count(e => e.Geometry.Count > 0)} with geometry, {meshes.Count} meshes");
            foreach (var group in skipped.GroupBy(s => s)) _output.WriteLine($"skipped {group.Key}: {group.Count()}");

            Assert.NotEmpty(meshes);
            Assert.All(meshes.Where(m => m.IsClosed), m => Assert.True(m.SignedVolume() > 0));
        }

        private static IEnumerable<Element> AllElements(FacilityPart part) => part.Elements.Concat(part.Parts.SelectMany(AllElements));

        internal static Element ReadElement(string path) =>
            Assert.Single(((Building)IfcBackend.Reader.Read(path).Project.Sites[0].Facilities[0]).Storeys[0].Elements);

        /// <summary>A file with one element at x = 1 m (millimetre units) whose Body holds the given items.</summary>
        internal static string WriteWithBody(Func<IModel, IEnumerable<IfcRepresentationItem>> items)
        {
            var source = GeometryTests.Write(new Element("E") { Placement = Placement.Translation(1, 0, 0) }, new Units { Length = Units.Find(UnitKind.Length, "mm") });
            var path = TestFiles.NewPath();
            using (var ifc = MemoryModel.OpenRead(source))
            {
                using (var txn = ifc.BeginTransaction("geometry"))
                {
                    var shape = ifc.Instances.New<IfcShapeRepresentation>(r =>
                    {
                        r.ContextOfItems = ifc.Instances.OfType<IfcGeometricRepresentationContext>().First();
                        r.RepresentationIdentifier = "Body";
                        r.RepresentationType = "SweptSolid";
                        r.Items.AddRange(items(ifc));
                    });
                    ifc.Instances.OfType<IfcElement>().Single().Representation = ifc.Instances.New<IfcProductDefinitionShape>(s => s.Representations.Add(shape));
                    txn.Commit();
                }
                using (var stream = File.Create(path)) ifc.SaveAsStep21(stream);
            }
            return path;
        }

        internal static IfcExtrudedAreaSolid Extruded(IModel ifc, IfcProfileDef profile, double depth) =>
            ifc.Instances.New<IfcExtrudedAreaSolid>(e =>
            {
                e.SweptArea = profile;
                e.ExtrudedDirection = Direction(ifc, 0, 0, 1);
                e.Depth = depth;
                e.Position = ifc.Instances.New<IfcAxis2Placement3D>(a => a.Location = Point(ifc, 0, 0, 0));
            });

        private static IfcCartesianPoint Point(IModel ifc, double x, double y, double z) =>
            ifc.Instances.New<IfcCartesianPoint>(p => p.Coordinates.AddRange(new IfcLengthMeasure[] { x, y, z }));

        private static IfcDirection Direction(IModel ifc, double x, double y, double z) =>
            ifc.Instances.New<IfcDirection>(d => d.DirectionRatios.AddRange(new IfcReal[] { x, y, z }));

        private static double[] P(double x, double y) => new[] { x, y, 0.0 };

        private static double Area(IReadOnlyList<double[]> points, int[] t)
        {
            double[] a = points[t[0]], b = points[t[1]], c = points[t[2]];
            return ((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])) / 2;
        }
    }
}
