using System;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Ifc4x3.GeometricModelResource;
using Xbim.Ifc4x3.ProductExtension;
using Xbim.Ifc4x3.RepresentationResource;
using Xbim.IO.Memory;
using Xunit;

namespace IfcHopper.Tests
{
    public class GeometryTests
    {
        /// <summary>Unit cube (metres) at <paramref name="x"/>, faces outward.</summary>
        internal static MeshGeometry Cube(double x = 0)
        {
            var vertices = new[]
            {
                new[] { x, 0.0, 0.0 }, new[] { x + 1, 0.0, 0.0 }, new[] { x + 1, 1.0, 0.0 }, new[] { x, 1.0, 0.0 },
                new[] { x, 0.0, 1.0 }, new[] { x + 1, 0.0, 1.0 }, new[] { x + 1, 1.0, 1.0 }, new[] { x, 1.0, 1.0 },
            };
            var faces = new[]
            {
                new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 },
                new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 },
            };
            return new MeshGeometry(vertices, faces, true);
        }

        internal static string Write(Element element, Units units = null)
        {
            var storey = new Storey();
            storey.Elements.Add(element);
            var building = new Building();
            building.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project { Units = units };
            project.Sites.Add(site);

            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(project), path);
            return path;
        }

        [Fact]
        public void Mesh_RejectsInvalidFaces()
        {
            var vertices = new[] { new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 } };
            Assert.Throws<ArgumentException>(() => new MeshGeometry(vertices, new[] { new[] { 0, 1 } }));
            Assert.Throws<ArgumentException>(() => new MeshGeometry(vertices, new[] { new[] { 0, 1, 3 } }));
            Assert.Throws<ArgumentException>(() => new MeshGeometry(vertices, new int[0][]));
        }

        [Fact]
        public void Geometry_IsWrittenAsBodyFaceSetRelativeToPlacementInFileUnits()
        {
            var element = new Element("Box", "IfcWall") { Placement = Placement.Translation(10, 0, 0) };
            element.Geometry.Add(Cube(10));
            var path = Write(element, new Units { Length = Units.Find(UnitKind.Length, "mm") });

            using (var ifc = MemoryModel.OpenRead(path))
            {
                var wall = Assert.Single(ifc.Instances.OfType<IfcElement>());
                var body = Assert.Single(wall.Representation.Representations.OfType<IfcShapeRepresentation>());
                Assert.Equal("Body", (string)body.RepresentationIdentifier);
                Assert.Equal("Tessellation", (string)body.RepresentationType);
                Assert.Equal("Model", (string)((IfcGeometricRepresentationSubContext)body.ContextOfItems).ParentContext.ContextType);

                var faceSet = Assert.IsType<IfcPolygonalFaceSet>(Assert.Single(body.Items));
                Assert.True(faceSet.Closed.Value);
                Assert.Equal(6, faceSet.Faces.Count);
                Assert.Equal(new long[] { 1, 4, 3, 2 }, faceSet.Faces[0].CoordIndex.Select(i => (long)i.Value));
                var coordinates = faceSet.Coordinates.CoordList.Select(p => p.Select(v => (double)v.Value).ToArray()).ToList();
                Assert.Equal(8, coordinates.Count);
                Assert.Equal(new[] { 1000.0, 1000.0, 1000.0 }, coordinates[6]);
            }
        }

        [Fact]
        public void Geometry_WithoutPlacementIsRelativeToParentAndSharesOneBodyContext()
        {
            var a = new Element("A");
            a.Geometry.Add(Cube(2));
            var path = Write(a);

            using (var ifc = MemoryModel.OpenRead(path))
            {
                var faceSet = Assert.Single(ifc.Instances.OfType<IfcPolygonalFaceSet>());
                Assert.Equal(new[] { 2.0, 0.0, 0.0 }, faceSet.Coordinates.CoordList[0].Select(v => (double)v.Value));
                Assert.Single(ifc.Instances.OfType<IfcGeometricRepresentationSubContext>());
            }
        }

        [Fact]
        public void Geometry_OfElementAddedInPlaceUsesTheExistingBodyContext()
        {
            var first = new Element("A");
            first.Geometry.Add(Cube());
            var path = Write(first);

            var model = IfcBackend.Reader.Read(path);
            var added = new Element("B");
            added.Geometry.Add(Cube(5));
            ((Building)model.Project.Sites[0].Facilities[0]).Storeys[0].Elements.Add(added);
            var edited = TestFiles.NewPath();
            IfcBackend.Writer.Write(model, edited);

            using (var ifc = MemoryModel.OpenRead(edited))
            {
                Assert.Equal(2, ifc.Instances.OfType<IfcPolygonalFaceSet>().Count());
                Assert.Single(ifc.Instances.OfType<IfcGeometricRepresentationSubContext>());
            }
        }

        [Fact]
        public void Element_WithoutGeometryHasNoRepresentation()
        {
            var path = Write(new Element());
            using (var ifc = MemoryModel.OpenRead(path))
                Assert.Null(Assert.Single(ifc.Instances.OfType<IfcElement>()).Representation);
        }
    }
}
