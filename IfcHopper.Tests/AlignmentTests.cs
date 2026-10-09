using System;
using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.Backends.Xbim;
using IfcHopper.Core.Geometry;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Ifc4x3.GeometryResource;
using Xbim.IO.Memory;
using Xunit;
using Xunit.Abstractions;

namespace IfcHopper.Tests
{
    public class AlignmentTests
    {
        private readonly ITestOutputHelper _output;

        public AlignmentTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Clothoid_MatchesSeriesExpansion()
        {
            double a = 100, l = 50;
            var segment = new CurveSegment2D(new ClothoidCurve(new[] { 0.0, 0 }, 0, a), 0, l, new[] { 0.0, 0 }, 0);
            var (point, angle) = segment.At(l);
            Assert.Equal(l - Math.Pow(l, 5) / (40 * Math.Pow(a, 4)) + Math.Pow(l, 9) / (3456 * Math.Pow(a, 8)), point[0], 1e-6);
            Assert.Equal(Math.Pow(l, 3) / (6 * a * a) - Math.Pow(l, 7) / (336 * Math.Pow(a, 6)) + Math.Pow(l, 11) / (42240 * Math.Pow(a, 10)), point[1], 1e-6);
            Assert.Equal(l * l / (2 * a * a), angle, 12);

            var right = new CurveSegment2D(new ClothoidCurve(new[] { 0.0, 0 }, 0, -a), 0, l, new[] { 0.0, 0 }, 0);
            Assert.Equal(-point[1], right.At(l).Point[1], 9);
        }

        [Fact]
        public void Segment_IsPlacedByItsStartPointAndTangent()
        {
            // Circle of radius 10 starting at its point (10, 0), heading +Y; the segment starts at (5, 5) heading +X.
            var arc = new CurveSegment2D(new CircleCurve(new[] { 0.0, 0 }, 0, 10), 0, Math.PI * 5, new[] { 5.0, 5 }, 0);
            var (point, angle) = arc.At(Math.PI * 5);
            Assert.Equal(15, point[0], 9);
            Assert.Equal(15, point[1], 9);
            Assert.Equal(Math.PI / 2, angle, 9);

            // Negative length runs the parent backwards; the travel direction still follows the placement.
            var back = new CurveSegment2D(new LineCurve(new[] { 0.0, 0 }, 0), 0, -4, new[] { 1.0, 1 }, Math.PI / 2);
            Assert.Equal(new[] { 1.0, 5.0 }, back.At(4).Point.Select(v => Math.Round(v, 9)));
        }

        [Fact]
        public void Vertical_HeightsFollowTheGradientCurve()
        {
            var slope = Math.Atan(0.02);
            var vertical = new Polycurve2D(new[]
            {
                new CurveSegment2D(new LineCurve(new[] { 0.0, 0 }, 0), 0, 100 / Math.Cos(slope), new[] { 0.0, 10 }, slope),
                new CurveSegment2D(new LineCurve(new[] { 0.0, 0 }, 0), 0, 50, new[] { 100.0, 12 }, 0),
            });
            var curve = new AlignmentCurve(new Polycurve2D(new[] { new CurveSegment2D(new LineCurve(new[] { 0.0, 0 }, 0), 0, 150, new[] { 0.0, 0 }, 0) }), vertical);
            Assert.Equal(11, curve.At(50).Point[2], 9);
            Assert.Equal(12, curve.At(130).Point[2], 9);
            Assert.Equal(new[] { 0.0, 100, 150 }, curve.Stations(0, 150, 0.005));
        }

        /// <summary>
        /// Each segment must end where the next one is placed. The bridge alignment (#159751, its gradient curve #159884) closes;
        /// the two other alignments of the file have up to 0.31 m gaps in their own data (placement directions off by ~0.2°).
        /// </summary>
        [SampleModelFact("Viadotto Acerno.ifc")]
        public void AcernoSample_BridgeAlignmentSegmentsAreContinuous()
        {
            using (var ifc = MemoryModel.OpenRead(TestFiles.AcernoSample))
            {
                foreach (var composite in ifc.Instances.OfType<IfcCompositeCurve>())
                {
                    var segments = XbimAlignments.Polycurve(composite.Segments).Segments;
                    var worst = 0.0;
                    for (int i = 0; i + 1 < segments.Count; i++)
                    {
                        var end = segments[i].At(segments[i].Length).Point;
                        var next = segments[i + 1].At(0).Point;
                        worst = Math.Max(worst, Math.Sqrt(Math.Pow(end[0] - next[0], 2) + Math.Pow(end[1] - next[1], 2)));
                    }
                    _output.WriteLine($"#{composite.EntityLabel} {composite.GetType().Name}: largest gap {worst:0.######} m");
                    if (composite.EntityLabel == 159751 || composite.EntityLabel == 159884) Assert.True(worst < 0.001);
                }
            }
        }

        [SampleModelFact("Viadotto Acerno.ifc")]
        public void AcernoSample_AlignmentGeometryIsRead()
        {
            var project = IfcBackend.Reader.Read(TestFiles.AcernoSample).Project;
            var elements = project.Sites.SelectMany(s => s.Facilities).SelectMany(f => f.Parts).SelectMany(AllElements).ToList();
            var meshes = elements.SelectMany(e => e.Geometry).ToList();
            var skipped = elements.SelectMany(e => e.SkippedGeometry).ToList();

            _output.WriteLine($"{elements.Count} elements, {elements.Count(e => e.Geometry.Count > 0)} with geometry, {meshes.Count} meshes, {elements.Count(e => e.Placement == null)} without placement");
            foreach (var group in skipped.GroupBy(s => s)) _output.WriteLine($"skipped {group.Key}: {group.Count()}");
            foreach (var group in elements.Where(e => e.Geometry.Count == 0).GroupBy(e => e.IfcClass)) _output.WriteLine($"no geometry: {group.Key} {group.Count()}");
            foreach (var group in elements.Where(e => e.Geometry.Count > 0).GroupBy(e => e.IfcClass))
            {
                var z = group.SelectMany(e => e.Geometry).SelectMany(m => m.Vertices).Select(v => v[2]).ToList();
                _output.WriteLine($"{group.Key}: {group.Count()} elements, z {z.Min():0.00} to {z.Max():0.00}");
            }

            Assert.DoesNotContain(skipped, s => s.Contains("IfcSectionedSolidHorizontal") || s.Contains("IfcLinearPlacement"));
            Assert.All(elements, e => Assert.NotNull(e.Placement));
            Assert.All(meshes.Where(m => m.IsClosed), m => Assert.True(m.SignedVolume() > 0));
        }

        /// <summary>The terrain (#159309) is an IfcGeographicElement contained directly in the site, with a mapped polygonal face set.</summary>
        [SampleModelFact("Viadotto Acerno.ifc")]
        public void AcernoSample_TerrainIsReadOnSite()
        {
            var site = Assert.Single(IfcBackend.Reader.Read(TestFiles.AcernoSample).Project.Sites);
            var terrain = Assert.Single(site.Elements, e => e.IfcClass == "IfcGeographicElement");
            var z = terrain.Geometry.SelectMany(m => m.Vertices).Select(v => v[2]).ToList();
            _output.WriteLine($"{site.Elements.Count} site elements; terrain: {terrain.Geometry.Count} meshes, {z.Count} vertices, z {z.DefaultIfEmpty().Min():0.00} to {z.DefaultIfEmpty().Max():0.00}, skipped {string.Join(", ", terrain.SkippedGeometry)}");

            Assert.Empty(terrain.SkippedGeometry);
            Assert.NotEmpty(terrain.Geometry);
        }

        private static IEnumerable<Element> AllElements(FacilityPart part) => part.Elements.Concat(part.Parts.SelectMany(AllElements));
    }
}
