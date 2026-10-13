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

        private static IEnumerable<Element> AllElements(FacilityPart part) => part.Elements.Concat(part.Parts.SelectMany(AllElements));
    }
}
