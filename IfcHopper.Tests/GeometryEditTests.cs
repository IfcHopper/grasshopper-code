using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Ifc4x3.GeometricModelResource;
using Xbim.Ifc4x3.GeometryResource;
using Xbim.Ifc4x3.ProductExtension;
using Xbim.Ifc4x3.ProfileResource;
using Xbim.Ifc4x3.RepresentationResource;
using Xbim.IO.Memory;
using Xunit;

namespace IfcHopper.Tests
{
    public class GeometryEditTests
    {
        /// <summary>A file with one element at x = 1 m whose Body is a 1 m cube extrusion, read back.</summary>
        private static (IfcHopperModel Model, Element Element) ReadExtrusion()
        {
            var path = GeometryReadTests.WriteWithBody(ifc => new IfcRepresentationItem[]
            {
                GeometryReadTests.Extruded(ifc, ifc.Instances.New<IfcRectangleProfileDef>(p =>
                {
                    p.ProfileType = IfcProfileTypeEnum.AREA;
                    p.XDim = 1000;
                    p.YDim = 1000;
                }), 1000),
            });
            var model = IfcBackend.Reader.Read(path);
            return (model, ((Building)model.Project.Sites[0].Facilities[0]).Storeys[0].Elements[0]);
        }

        private static string WriteBack(IfcHopperModel model)
        {
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(model, path);
            return path;
        }

        [Fact]
        public void LoadedUnchangedGeometry_KeepsTheOriginalItems()
        {
            var (model, element) = ReadExtrusion();
            Assert.Single(element.Geometry);

            using (var ifc = MemoryModel.OpenRead(WriteBack(model)))
            {
                Assert.Single(ifc.Instances.OfType<IfcExtrudedAreaSolid>());
                Assert.Empty(ifc.Instances.OfType<IfcPolygonalFaceSet>());
            }
        }

        [Fact]
        public void GeometryMovedWithItsPlacement_KeepsTheOriginalItems()
        {
            var (model, element) = ReadExtrusion();
            Assert.Single(element.Geometry);
            var target = Placement.Translation(5, 0, 0);
            element.MoveGeometry(element.Placement, target);
            element.Placement = target;
            Assert.Equal(4.5, element.Geometry[0].Vertices.Min(v => v[0]), 9);

            var path = WriteBack(model);
            using (var ifc = MemoryModel.OpenRead(path))
                Assert.Single(ifc.Instances.OfType<IfcExtrudedAreaSolid>());
            Assert.Equal(4.5, GeometryReadTests.ReadElement(path).Geometry[0].Vertices.Min(v => v[0]), 9);
        }

        [Fact]
        public void ReplacedGeometry_ReplacesTheBody()
        {
            var (model, element) = ReadExtrusion();
            element.ReplaceGeometry(new[] { GeometryTests.Cube(3) });
            Assert.Empty(element.SkippedGeometry);

            var path = WriteBack(model);
            using (var ifc = MemoryModel.OpenRead(path))
            {
                Assert.Empty(ifc.Instances.OfType<IfcExtrudedAreaSolid>());
                Assert.Single(ifc.Instances.OfType<IfcPolygonalFaceSet>());
                Assert.Single(ifc.Instances.OfType<IfcShapeRepresentation>());
            }
            var mesh = Assert.Single(GeometryReadTests.ReadElement(path).Geometry);
            Assert.Equal(3, mesh.Vertices.Min(v => v[0]), 9);
            Assert.Equal(1, mesh.SignedVolume(), 9);
        }

        [Fact]
        public void RemovedGeometry_RemovesTheRepresentation()
        {
            var (model, element) = ReadExtrusion();
            element.ReplaceGeometry(new MeshGeometry[0]);

            using (var ifc = MemoryModel.OpenRead(WriteBack(model)))
            {
                Assert.Null(Assert.Single(ifc.Instances.OfType<IfcElement>()).Representation);
                Assert.Empty(ifc.Instances.OfType<IfcShapeRepresentation>());
            }
        }

        [SampleModelFact]
        public void BridgeSample_PreviewedButUnchangedGeometryIsWrittenUnchanged()
        {
            var model = IfcBackend.Reader.Read(TestFiles.BridgeSample);
            foreach (var part in model.Project.Sites.SelectMany(s => s.Facilities).SelectMany(f => f.Parts))
                foreach (var element in part.Elements) _ = element.Geometry;

            int Count<T>(string file)
            {
                using (var ifc = MemoryModel.OpenRead(file)) return ifc.Instances.OfType<T>().Count();
            }
            var path = WriteBack(model);
            Assert.Equal(Count<IfcShapeRepresentation>(TestFiles.BridgeSample), Count<IfcShapeRepresentation>(path));
            Assert.Equal(Count<IfcPolygonalFaceSet>(TestFiles.BridgeSample), Count<IfcPolygonalFaceSet>(path));
            Assert.Equal(Count<IfcMappedItem>(TestFiles.BridgeSample), Count<IfcMappedItem>(path));
        }

        [Fact]
        public void CopiedElement_KeepsLazyGeometryAndMovesIt()
        {
            var (_, element) = ReadExtrusion();
            var copy = (Element)element.Copy();
            copy.MoveGeometry(Placement.Translation(1, 0, 0), Placement.Translation(1, 0, 2));

            Assert.Equal(2, copy.Geometry[0].Vertices.Min(v => v[2]), 9);
            Assert.Equal(0, element.Geometry[0].Vertices.Min(v => v[2]), 9);
        }
    }
}
