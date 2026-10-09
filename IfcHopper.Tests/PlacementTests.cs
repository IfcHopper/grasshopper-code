using System;
using System.IO;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xunit;

namespace IfcHopper.Tests
{
    public class PlacementTests
    {
        private static readonly Placement Rotated = new Placement(new[] { 10.0, 20.0, 1.0 }, new[] { 0.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 });

        private static void AssertClose(double[] expected, double[] actual, int digits = 9)
        {
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], actual[i], digits);
        }

        private static void AssertSame(Placement expected, Placement actual, int digits = 9)
        {
            Assert.NotNull(actual);
            AssertClose(expected.Origin, actual.Origin, digits);
            AssertClose(expected.XAxis, actual.XAxis, digits);
            AssertClose(expected.ZAxis, actual.ZAxis, digits);
        }

        [Fact]
        public void RelativeToAndCompose_AreInverse()
        {
            var child = new Placement(new[] { 3.0, -2.0, 5.0 }, new[] { 1.0, 1.0, 0.0 }, new[] { 0.0, 0.0, 1.0 });
            var relative = child.RelativeTo(Rotated);
            AssertSame(child, Rotated.Compose(relative));
        }

        [Fact]
        public void Constructor_OrthogonalisesXAndRejectsParallelAxes()
        {
            var placement = new Placement(new double[3], new[] { 1.0, 0.0, 1.0 }, new[] { 0.0, 0.0, 2.0 });
            AssertClose(new[] { 1.0, 0.0, 0.0 }, placement.XAxis);
            AssertClose(new[] { 0.0, 0.0, 1.0 }, placement.ZAxis);
            AssertClose(new[] { 0.0, 1.0, 0.0 }, placement.YAxis);
            Assert.Throws<ArgumentException>(() => new Placement(new double[3], new[] { 0.0, 0.0, 1.0 }, new[] { 0.0, 0.0, 1.0 }));
        }

        [Fact]
        public void Placements_RoundTripInWorldCoordinates()
        {
            var partPlacement = new Placement(new[] { 15.0, 25.0, 3.0 }, new[] { 1.0, 0.0, 0.0 }, new[] { 0.0, 0.0, 1.0 });
            var part = new FacilityPart(FacilityType.Bridge) { Placement = partPlacement };
            var bridge = new Facility(FacilityType.Bridge);
            bridge.Parts.Add(part);
            var building = new Building { Placement = Placement.Translation(100, 0, 0) };
            building.Storeys.Add(new Storey("L1", 3.0));
            var site = new Site { Placement = Rotated };
            site.Facilities.AddRange(new Facility[] { bridge, building });
            var project = new Project { Units = new Units { Length = Units.Find(UnitKind.Length, "mm") } };
            project.Sites.Add(site);

            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(project), path);
            var read = IfcBackend.Reader.Read(path).Project.Sites[0];

            AssertSame(Rotated, read.Placement);
            AssertSame(Rotated, read.Facilities[0].Placement);
            AssertSame(partPlacement, read.Facilities[0].Parts[0].Placement);
            AssertSame(Placement.Translation(100, 0, 0), read.Facilities[1].Placement);
            AssertSame(Placement.Translation(100, 0, 3), ((Building)read.Facilities[1]).Storeys[0].Placement);
        }

        [Fact]
        public void Georeference_RoundTripsWithUnitScale()
        {
            var georeference = new Georeference("EPSG:5110")
            {
                GeodeticDatum = "EUREF89",
                VerticalDatum = "NN2000",
                Eastings = 145700,
                Northings = 6566100,
                OrthogonalHeight = 12.5,
                XAxisAbscissa = 0.8,
                XAxisOrdinate = 0.6,
                Scale = 0.9996,
            };
            var project = new Project { Georeference = georeference, Units = new Units { Length = Units.Find(UnitKind.Length, "mm") } };

            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(project), path);
            var read = IfcBackend.Reader.Read(path).Project.Georeference;

            Assert.Contains(File.ReadLines(path), l => l.Contains("IFCMAPCONVERSION") && l.Contains(",0.0009996)"));
            Assert.Equal(("EPSG:5110", "EUREF89", "NN2000"), (read.CrsName, read.GeodeticDatum, read.VerticalDatum));
            Assert.Equal(145700, read.Eastings, 6);
            Assert.Equal(6566100, read.Northings, 6);
            Assert.Equal(12.5, read.OrthogonalHeight, 9);
            Assert.Equal((0.8, 0.6), (read.XAxisAbscissa, read.XAxisOrdinate));
            Assert.Equal(0.9996, read.Scale, 12);
        }

        [Fact]
        public void Georeference_IsOptional()
        {
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(new Project()), path);
            Assert.Null(IfcBackend.Reader.Read(path).Project.Georeference);
            Assert.DoesNotContain(File.ReadLines(path), l => l.Contains("IFCMAPCONVERSION"));
        }

        [SampleModelFact]
        public void BridgeSample_ReadsGeoreferenceAndPlacements()
        {
            var project = IfcBackend.Reader.Read(TestFiles.BridgeSample).Project;

            var georeference = project.Georeference;
            Assert.Equal("EPSG:5110", georeference.CrsName);
            Assert.Equal(("EUREF89", "NN2000", "NTM 10"), (georeference.GeodeticDatum, georeference.VerticalDatum, georeference.MapZone));
            Assert.Equal(145700, georeference.Eastings, 6);
            Assert.Equal(6566100, georeference.Northings, 6);
            Assert.Equal(1.0, georeference.Scale, 12);

            var piers = project.Sites[0].Facilities[0].Parts[0].Parts;
            Assert.All(piers, p => Assert.NotNull(p.Placement));
            // Bridge parts sit at their elevations; their elements are placed along the alignment.
            Assert.Equal(55.75, piers[0].Placement.Origin[2], 6);
            Assert.True(piers.Select(p => p.Placement.Origin[2]).Distinct().Count() > 1, "Piers should be at different heights.");
        }
    }
}
