using System;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xunit;

namespace IfcHopper.Tests
{
    public class LargeCoordinateTests
    {
        [Theory]
        [InlineData(IfcSchema.Ifc2x3, "m", true)]
        [InlineData(IfcSchema.Ifc4, "mm", true)]
        [InlineData(IfcSchema.Ifc4x3, "m", true)]
        [InlineData(IfcSchema.Ifc2x3, "mm", false)]
        [InlineData(IfcSchema.Ifc4, "m", false)]
        [InlineData(IfcSchema.Ifc4x3, "mm", false)]
        public void SurveyWall_WarnsAndKeepsCoordinatesThroughInPlaceWrite(IfcSchema schema, string unit, bool placed)
        {
            var path = TestFiles.NewPath();
            var original = SyntheticLargeCoordinates.Create(placed, unit);
            if (schema != IfcSchema.Ifc2x3)
                original.Project.Georeference = new Georeference("EPSG:32632")
                {
                    Eastings = 100, Northings = 200, OrthogonalHeight = 50,
                    XAxisAbscissa = 0.8, XAxisOrdinate = 0.6, Scale = 0.9996,
                };
            IfcBackend.Writer.Write(original, path, schema);
            var read = IfcBackend.Reader.Read(path);
            Assert.Single(read.ReadWarnings);
            var wall = Wall(read);
            AssertThickness(wall);
            Assert.Equal(500000, wall.Geometry[0].Vertices.Min(p => p[0]), 8);
            Assert.Equal(5000000, wall.Geometry[0].Vertices.Min(p => p[1]), 8);
            Assert.Equal(placed ? 5000000 : 0, wall.Placement.Origin[1], 8);
            wall.Name = "Edited without relocating";
            var output = TestFiles.NewPath();
            IfcBackend.Writer.Write(read, output);
            var rereadModel = IfcBackend.Reader.Read(output);
            if (original.Project.Georeference != null)
            {
                var geo = rereadModel.Project.Georeference;
                Assert.Equal("EPSG:32632", geo.CrsName);
                Assert.Equal(100, geo.Eastings);
                Assert.Equal(200, geo.Northings);
                Assert.Equal(50, geo.OrthogonalHeight);
                Assert.Equal(0.8, geo.XAxisAbscissa);
                Assert.Equal(0.6, geo.XAxisOrdinate);
                Assert.Equal(0.9996, geo.Scale, 10);
            }
            var reread = Wall(rereadModel);
            Assert.Equal(wall.Name, reread.Name);
            AssertThickness(reread);
            Assert.Equal(wall.Placement.Origin, reread.Placement.Origin);
        }

        [Fact]
        public void LocalGeometryWithLargeMapConversion_DoesNotWarn()
        {
            var model = IfcHopperModel.CreateDefault();
            model.Project.Georeference = new Georeference("EPSG:32632") { Eastings = 500000, Northings = 5000000 };
            var element = new Element();
            element.Geometry.Add(GeometryTests.Cube());
            ((Building)model.Project.Sites[0].Facilities[0]).Storeys[0].Elements.Add(element);
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(model, path);
            var read = IfcBackend.Reader.Read(path);
            Assert.Empty(read.ReadWarnings);
            Assert.Equal(5000000, read.Project.Georeference.Northings);
            Assert.Equal(0, Wall(read).Geometry[0].Vertices.Min(p => p[1]));
        }

        [Fact]
        public void SmallRelativePlacementsWithLargeWorldSum_Warn()
        {
            var model = IfcHopperModel.CreateDefault();
            var site = model.Project.Sites[0];
            var building = (Building)site.Facilities[0];
            site.Placement = Placement.Translation(-60000, 0, 0);
            building.Placement = Placement.Translation(-120000, 0, 0);
            building.Storeys[0].Placement = Placement.Translation(-180000, 0, 0);
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(model, path);
            Assert.Single(IfcBackend.Reader.Read(path).ReadWarnings);
        }

        private static Element Wall(IfcHopperModel model) => ((Building)model.Project.Sites[0].Facilities[0]).Storeys[0].Elements[0];

        private static void AssertThickness(Element wall)
        {
            var mesh = Assert.Single(wall.Geometry);
            Assert.True(mesh.IsClosed);
            Assert.InRange(Math.Abs(mesh.Vertices.Max(p => p[1]) - mesh.Vertices.Min(p => p[1]) - 0.02), 0, 1e-8);
        }
    }
}
