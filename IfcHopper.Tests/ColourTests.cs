using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Common;
using Xbim.Ifc4x3.GeometricModelResource;
using Xbim.Ifc4x3.GeometryResource;
using Xbim.Ifc4x3.MeasureResource;
using Xbim.Ifc4x3.PresentationAppearanceResource;
using Xbim.Ifc4x3.ProductExtension;
using Xbim.Ifc4x3.ProfileResource;
using Xbim.Ifc4x3.RepresentationResource;
using Xbim.IO.Memory;
using Xunit;

namespace IfcHopper.Tests
{
    public class ColourTests
    {
        private static readonly Colour Red = new Colour(1, 0, 0, 0.5);
        private static readonly Colour Blue = new Colour(0, 0, 1);

        [Fact]
        public void Colours_RoundTripAndShareStyles()
        {
            var element = new Element("Boxes");
            element.Geometry.AddRange(new[] { GeometryTests.Cube(0).WithColour(Red), GeometryTests.Cube(2).WithColour(Blue), GeometryTests.Cube(4).WithColour(Red), GeometryTests.Cube(6) });
            var path = GeometryTests.Write(element, new Units { Length = Units.Find(UnitKind.Length, "mm") });

            using (var ifc = MemoryModel.OpenRead(path))
            {
                Assert.Equal(3, ifc.Instances.OfType<IfcStyledItem>().Count());
                Assert.Equal(2, ifc.Instances.OfType<IfcSurfaceStyle>().Count());
            }
            Assert.Equal(new[] { Red, Blue, Red, null }, GeometryReadTests.ReadElement(path).Geometry.Select(m => m.Colour));
        }

        [Fact]
        public void MappedItemStyle_IsInheritedUnlessTheItemHasItsOwn()
        {
            var path = GeometryReadTests.WriteWithBody(ifc =>
            {
                IfcExtrudedAreaSolid Box() => GeometryReadTests.Extruded(ifc, ifc.Instances.New<IfcRectangleProfileDef>(p =>
                {
                    p.ProfileType = IfcProfileTypeEnum.AREA;
                    p.XDim = 1000;
                    p.YDim = 1000;
                }), 1000);
                var own = Box();
                Style(ifc, own, 0, 0, 1);
                var map = ifc.Instances.New<IfcRepresentationMap>(m =>
                {
                    m.MappingOrigin = ifc.Instances.New<IfcAxis2Placement3D>(a => a.Location = ifc.Instances.New<IfcCartesianPoint>(p => p.Coordinates.AddRange(new IfcLengthMeasure[] { 0, 0, 0 })));
                    m.MappedRepresentation = ifc.Instances.New<IfcShapeRepresentation>(r =>
                    {
                        r.ContextOfItems = ifc.Instances.OfType<IfcGeometricRepresentationContext>().First();
                        r.Items.AddRange(new IfcRepresentationItem[] { Box(), own });
                    });
                });
                var mapped = ifc.Instances.New<IfcMappedItem>(i =>
                {
                    i.MappingSource = map;
                    i.MappingTarget = ifc.Instances.New<IfcCartesianTransformationOperator3D>(o =>
                        o.LocalOrigin = ifc.Instances.New<IfcCartesianPoint>(p => p.Coordinates.AddRange(new IfcLengthMeasure[] { 0, 0, 0 })));
                });
                Style(ifc, mapped, 1, 0, 0);
                return new IfcRepresentationItem[] { mapped };
            });

            Assert.Equal(new[] { new Colour(1, 0, 0), new Colour(0, 0, 1) }, GeometryReadTests.ReadElement(path).Geometry.Select(m => m.Colour));
        }

        [Fact]
        public void ChangedColour_ReplacesTheBodyAndUnchangedColourKeepsIt()
        {
            var element = new Element("Box") { Placement = Placement.Translation(1, 0, 0) };
            element.Geometry.Add(GeometryTests.Cube(1).WithColour(Blue));
            var source = GeometryTests.Write(element);

            var model = IfcBackend.Reader.Read(source);
            var read = ((Building)model.Project.Sites[0].Facilities[0]).Storeys[0].Elements[0];
            Assert.Equal(Blue, read.Geometry[0].Colour);
            var unchanged = TestFiles.NewPath();
            IfcBackend.Writer.Write(model, unchanged);
            using (var ifc = MemoryModel.OpenRead(unchanged))
                Assert.Equal(FaceSetLabel(source), (int)Assert.Single(ifc.Instances.OfType<IfcPolygonalFaceSet>()).EntityLabel);

            read.SetColour(Red);
            var recoloured = TestFiles.NewPath();
            IfcBackend.Writer.Write(model, recoloured);
            using (var ifc = MemoryModel.OpenRead(recoloured))
                Assert.Single(ifc.Instances.OfType<IfcStyledItem>());
            Assert.Equal(Red, Assert.Single(GeometryReadTests.ReadElement(recoloured).Geometry).Colour);
        }

        [SampleModelFact]
        public void BridgeSample_ColoursAreRead()
        {
            var project = IfcBackend.Reader.Read(TestFiles.BridgeSample).Project;
            var meshes = project.Sites.SelectMany(s => s.Facilities).SelectMany(f => f.Parts).SelectMany(AllElements).SelectMany(e => e.Geometry).ToList();
            Assert.Contains(meshes, m => m.Colour != null);
        }

        private static IEnumerable<Element> AllElements(FacilityPart part) => part.Elements.Concat(part.Parts.SelectMany(AllElements));

        private static int FaceSetLabel(string path)
        {
            using (var ifc = MemoryModel.OpenRead(path)) return ifc.Instances.OfType<IfcPolygonalFaceSet>().Single().EntityLabel;
        }

        private static void Style(IModel ifc, IfcRepresentationItem item, double r, double g, double b) =>
            ifc.Instances.New<IfcStyledItem>(s =>
            {
                s.Item = item;
                s.Styles.Add(ifc.Instances.New<IfcSurfaceStyle>(style =>
                {
                    style.Side = IfcSurfaceSide.BOTH;
                    style.Styles.Add(ifc.Instances.New<IfcSurfaceStyleShading>(shading =>
                        shading.SurfaceColour = ifc.Instances.New<IfcColourRgb>(c =>
                        {
                            c.Red = r;
                            c.Green = g;
                            c.Blue = b;
                        })));
                }));
            });
    }
}
