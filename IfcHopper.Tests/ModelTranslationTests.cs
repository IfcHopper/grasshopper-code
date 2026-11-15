using System;
using System.IO;
using System.Linq;
using IfcHopper.Core.Geometry;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Ifc4x3.ProductExtension;
using Xbim.Ifc4x3.GeometryResource;
using Xbim.Ifc4x3.GeometricConstraintResource;
using Xbim.Ifc4x3.MeasureResource;
using Xbim.Ifc4x3.SharedBldgElements;
using Xbim.IO.Memory;
using Xunit;

namespace IfcHopper.Tests
{
    public class ModelTranslationTests
    {
        private static Element Wall(IfcHopperModel model) => ((Building)model.Project.Sites[0].Facilities[0]).Storeys[0].Elements[0];

        [Theory]
        [InlineData(IfcSchema.Ifc2x3, "m")]
        [InlineData(IfcSchema.Ifc4, "mm")]
        [InlineData(IfcSchema.Ifc4x3, "m")]
        public void SurveyModel_MovesOnceAndWritesInPlace(IfcSchema schema, string unit)
        {
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(SyntheticLargeCoordinates.Create(true, unit), path, schema);
            var before = File.ReadAllBytes(path);
            var source = IfcBackend.Reader.Read(path);
            var moved = ModelTranslation.Move(source, -500000, -5000000);
            var wall = Wall(moved);
            Assert.Equal(new[] { 0.0, 0, 0 }, wall.Placement.Origin);
            Assert.Equal(0, wall.Geometry[0].Vertices.Min(p => p[1]), 8);
            Assert.Equal(0.02, wall.Geometry[0].Vertices.Max(p => p[1]), 8);
            Assert.Equal(5000000, Wall(source).Placement.Origin[1]);
            Assert.Equal(5000000, Wall(source).Geometry[0].Vertices.Min(p => p[1]), 8);
            Assert.Equal(Wall(source).GlobalId, wall.GlobalId);
            Assert.Same(Wall(source).Source, wall.Source);
            Assert.Equal(source.Project.Sites[0].Placement.XAxis, moved.Project.Sites[0].Placement.XAxis);
            var output = TestFiles.NewPath();
            IfcBackend.Writer.Write(moved, output);
            Assert.Equal(before, File.ReadAllBytes(path));
            var read = IfcBackend.Reader.Read(output);
            Assert.Equal(new[] { 0.0, 0, 0 }, Wall(read).Placement.Origin);
            Assert.Equal(0.02, Wall(read).Geometry[0].Vertices.Max(p => p[1]), 8);
            Assert.All(Wall(read).Geometry[0].Vertices, p => Assert.All(p, value => Assert.InRange(Math.Abs(value), 0, 4)));
        }

        [Theory]
        [InlineData(IfcSchema.Ifc2x3, true)]
        [InlineData(IfcSchema.Ifc2x3, false)]
        [InlineData(IfcSchema.Ifc4, true)]
        [InlineData(IfcSchema.Ifc4, false)]
        [InlineData(IfcSchema.Ifc4x3, true)]
        [InlineData(IfcSchema.Ifc4x3, false)]
        public void Georeference_RotationAndScaleFollowChosenMode(IfcSchema schema, bool keep)
        {
            var source = SyntheticLargeCoordinates.Create(true);
            source.Project.Georeference = new Georeference("EPSG:32632")
            {
                Eastings = 100, Northings = 200, OrthogonalHeight = 50,
                XAxisAbscissa = 8, XAxisOrdinate = 6, Scale = 0.9996,
            };
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(source, path, schema);
            source = IfcBackend.Reader.Read(path);
            var oldPoint = Wall(source).Geometry[0].Vertices[0];
            var moved = ModelTranslation.Move(source, -500000, -5000000, keep);
            var newPoint = Wall(moved).Geometry[0].Vertices[0];
            var oldMap = Map(source.Project.Georeference, oldPoint);
            var newMap = Map(moved.Project.Georeference, newPoint);
            Assert.Equal(keep ? oldMap[0] : oldMap[0] + 0.9996 * (0.8 * -500000 - 0.6 * -5000000), newMap[0], 7);
            Assert.Equal(keep ? oldMap[1] : oldMap[1] + 0.9996 * (0.6 * -500000 + 0.8 * -5000000), newMap[1], 7);
            Assert.Equal(50, moved.Project.Georeference.OrthogonalHeight);
            Assert.Equal(100, source.Project.Georeference.Eastings);
            var output = TestFiles.NewPath();
            IfcBackend.Writer.Write(moved, output);
            var reread = IfcBackend.Reader.Read(output);
            Assert.Equal(newMap[0], Map(reread.Project.Georeference, Wall(reread).Geometry[0].Vertices[0])[0], 7);
            Assert.Equal(newMap[1], Map(reread.Project.Georeference, Wall(reread).Geometry[0].Vertices[0])[1], 7);
        }

        [Fact]
        public void PartsOpeningsFillsAndTypeInstances_MoveWithoutChangingUpstream()
        {
            var model = IfcHopperModel.CreateDefault();
            var storey = ((Building)model.Project.Sites[0].Facilities[0]).Storeys[0];
            var type = new ElementType("Wall type", "IfcWallType");
            type.Geometry.Add(GeometryTests.Cube());
            var wall = new Element("Wall", "IfcWall") { Placement = Placement.Translation(10, 20, 3) };
            wall.PlaceType(type, Placement.Translation(10, 20, 3).ToAffine());
            var part = new Element { Placement = Placement.Translation(11, 22, 4) };
            part.Geometry.Add(GeometryTests.Cube(11));
            wall.Parts.Add(part);
            var door = new Element("Door", "IfcDoor") { GlobalId = GlobalIds.New(), Placement = Placement.Translation(12, 23, 3) };
            var opening = new Opening { Placement = Placement.Translation(12, 23, 3) };
            opening.Geometry.Add(GeometryTests.Cube(12));
            opening.Fills.Add(door);
            wall.Openings.Add(opening);
            storey.Elements.AddRange(new[] { wall, door });
            var moved = ModelTranslation.Move(model, -10, -20, false);
            var result = Wall(moved);
            Assert.Equal(new[] { 0.0, 0, 3 }, result.Placement.Origin);
            Assert.Equal(new[] { 1.0, 2, 4 }, result.Parts[0].Placement.Origin);
            Assert.Equal(new[] { 2.0, 3, 3 }, result.Openings[0].Placement.Origin);
            Assert.Same(result.Openings[0].Fills[0], ((Building)moved.Project.Sites[0].Facilities[0]).Storeys[0].Elements[1]);
            Assert.Equal(new[] { 2.0, 3, 3 }, result.Openings[0].Fills[0].Placement.Origin);
            Assert.Equal(new[] { 0.0, 0, 3 }, result.TypeTransform.Origin);
            Assert.Same(type, result.Type);
            Assert.Equal(0, type.Geometry[0].Vertices.Min(p => p[0]));
            Assert.Equal(10, wall.Geometry[0].Vertices.Min(p => p[0]));
            Assert.Equal(0, result.Geometry[0].Vertices.Min(p => p[0]));
            Assert.Equal(2, result.Openings[0].Geometry[0].Vertices.Min(p => p[0]));
            Assert.Equal(12, opening.Geometry[0].Vertices.Min(p => p[0]));

            var sourcePath = TestFiles.NewPath();
            IfcBackend.Writer.Write(model, sourcePath);
            var imported = IfcBackend.Reader.Read(sourcePath);
            var importedMove = ModelTranslation.Move(imported, -10, -20, false);
            var output = TestFiles.NewPath();
            IfcBackend.Writer.Write(importedMove, output);
            var written = Wall(IfcBackend.Reader.Read(output));
            Assert.Equal(new[] { 0.0, 0, 3 }, written.Placement.Origin);
            Assert.Equal(new[] { 2.0, 3, 3 }, written.Openings[0].Placement.Origin);
            Assert.Equal(new[] { 2.0, 3, 3 }, written.Openings[0].Fills[0].Placement.Origin);
            Assert.NotNull(written.TypeTransform);
            Assert.Equal(new[] { 0.0, 0, 3 }, written.TypeTransform.Origin);
            Assert.Equal(0, written.Geometry[0].Vertices.Min(p => p[0]), 8);
        }

        [Fact]
        public void InheritedPlacementsAndStoreyElevations_ArePreserved()
        {
            var source = IfcHopperModel.CreateDefault();
            var storey = ((Building)source.Project.Sites[0].Facilities[0]).Storeys[0];
            storey.Elevation = 3;
            storey.Elements.Add(new Element());
            var moved = ModelTranslation.Move(source, 10, -20);
            Assert.Null(source.Project.Sites[0].Placement);
            Assert.Equal(new[] { 10.0, -20, 0 }, moved.Project.Sites[0].Placement.Origin);
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(moved, path);
            var read = IfcBackend.Reader.Read(path);
            Assert.Equal(new[] { 10.0, -20, 3 }, Wall(read).Placement.Origin);
            Assert.Equal(3, ((Building)read.Project.Sites[0].Facilities[0]).Storeys[0].Elevation);
        }

        [Fact]
        public void UnmappedSourceProduct_IsRejectedInsteadOfPartiallyMovingFile()
        {
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(SyntheticLargeCoordinates.Create(true), path);
            using (var ifc = MemoryModel.OpenRead(path))
            {
                using (var tx = ifc.BeginTransaction("Add unsupported product"))
                {
                    ifc.Instances.New<IfcAnnotation>(a => { a.GlobalId = GlobalIds.New(); a.Name = "Survey note"; });
                    tx.Commit();
                }
                using (var stream = File.Create(path)) ifc.SaveAsStep21(stream);
            }
            var ex = Assert.Throws<NotSupportedException>(() => ModelTranslation.Move(IfcBackend.Reader.Read(path), 1, 2));
            Assert.Contains("IfcAnnotation", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(double.NaN, 0)]
        [InlineData(0, double.PositiveInfinity)]
        public void NonFiniteOffsets_AreRejected(double x, double y) =>
            Assert.Throws<ArgumentException>(() => ModelTranslation.Move(IfcHopperModel.CreateDefault(), x, y));

        [Fact]
        public void SourcePlacementIndependentOfContainment_WritesCorrectWorldPosition()
        {
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(SyntheticLargeCoordinates.Create(true), path);
            using (var ifc = MemoryModel.OpenRead(path))
            {
                using (var tx = ifc.BeginTransaction("Place wall independently of its storey"))
                {
                    var wall = ifc.Instances.OfType<IfcWall>().Single();
                    var local = (IfcLocalPlacement)wall.ObjectPlacement;
                    local.PlacementRelTo = null;
                    ((IfcAxis2Placement3D)local.RelativePlacement).Location = ifc.Instances.New<IfcCartesianPoint>(p =>
                        p.Coordinates.AddRange(new IfcLengthMeasure[] { 500000, 5000000, 0 }));
                    tx.Commit();
                }
                using (var stream = File.Create(path)) ifc.SaveAsStep21(stream);
            }
            var moved = ModelTranslation.Move(IfcBackend.Reader.Read(path), -500000, -5000000);
            var output = TestFiles.NewPath();
            IfcBackend.Writer.Write(moved, output);
            var written = Wall(IfcBackend.Reader.Read(output));
            Assert.Equal(new[] { 0.0, 0, 0 }, written.Placement.Origin);
            Assert.Equal(0, written.Geometry[0].Vertices.Min(p => p[1]), 8);
        }

        private static double[] Map(Georeference geo, double[] point)
        {
            var norm = Math.Sqrt(geo.XAxisAbscissa * geo.XAxisAbscissa + geo.XAxisOrdinate * geo.XAxisOrdinate);
            return new[]
            {
                geo.Eastings + geo.Scale * (geo.XAxisAbscissa * point[0] - geo.XAxisOrdinate * point[1]) / norm,
                geo.Northings + geo.Scale * (geo.XAxisOrdinate * point[0] + geo.XAxisAbscissa * point[1]) / norm,
            };
        }
    }
}
