using System.IO;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xunit;
using Xbim.IO.Memory;

namespace IfcHopper.Tests
{
    public class ReadWarningTests
    {
        [Theory]
        [InlineData(IfcSchema.Ifc2x3)]
        [InlineData(IfcSchema.Ifc4)]
        [InlineData(IfcSchema.Ifc4x3)]
        public void UnsupportedProducts_AreCountedByClass(IfcSchema schema)
        {
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(IfcHopperModel.CreateDefault(), path, schema);
            Insert(path,
                "#900001=IFCSTRUCTURALCURVEMEMBER('0000000000000000000001',$,'First',$,$,$,$,.RIGID_JOINED_MEMBER.,$);\n" +
                "#900002=IFCSTRUCTURALCURVEMEMBER('0000000000000000000002',$,'Second',$,$,$,$,.RIGID_JOINED_MEMBER.,$);");
            var read = IfcBackend.Reader.Read(path);
            var warning = Assert.Single(read.ReadWarnings);
            Assert.Contains("IfcStructuralCurveMember: 2", warning);
            Assert.Contains("re-export", warning);
        }

        [Theory]
        [InlineData(IfcSchema.Ifc2x3)]
        [InlineData(IfcSchema.Ifc4)]
        [InlineData(IfcSchema.Ifc4x3)]
        public void InvalidAbstractProduct_IsReported(IfcSchema schema)
        {
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(IfcHopperModel.CreateDefault(), path, schema);
            Insert(path, "#900001=IFCSTRUCTURALMEMBER('0000000000000000000001',$,'Invalid',$,$,$,$);");
            Assert.Contains(IfcBackend.Reader.Read(path).ReadWarnings, w => w.Contains("IfcStructuralMember"));
        }

        [Fact]
        public void SourceScreening_IgnoresStringsAndCommentsAndAcceptsMultilineDeclarations()
        {
            var path = TestFiles.NewPath();
            var project = new Project("Quoted '#900009=IFCSTRUCTURALMEMBER(' text");
            IfcBackend.Writer.Write(new IfcHopperModel(project), path);
            Insert(path, "/* #900010=IFCSTRUCTURALMEMBER(); */\n" +
                "#900001\n= /* between tokens */\nIFCSTRUCTURALMEMBER('0000000000000000000001',$,'Invalid',$,$,$,$);");
            var warning = Assert.Single(IfcBackend.Reader.Read(path).ReadWarnings);
            Assert.Contains("IfcStructuralMember: 1 invalid abstract", warning);
        }

        [Theory]
        [InlineData(IfcSchema.Ifc2x3)]
        [InlineData(IfcSchema.Ifc4)]
        [InlineData(IfcSchema.Ifc4x3)]
        public void SupportedProductsAndOpenings_DoNotWarnOrForceLazyLoading(IfcSchema schema)
        {
            var model = IfcHopperModel.CreateDefault();
            var wall = new Element("Wall", "IfcWall");
            wall.Geometry.Add(GeometryTests.Cube());
            wall.Openings.Add(new Opening("Opening"));
            ((Building)model.Project.Sites[0].Facilities[0]).Storeys[0].Elements.Add(wall);
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(model, path, schema);
            var read = IfcBackend.Reader.Read(path);
            Assert.Empty(read.ReadWarnings);
            Assert.False(read.Project.ChildrenLoaded);
            var element = ((Building)read.Project.Sites[0].Facilities[0]).Storeys[0].Elements[0];
            Assert.False(element.GeometryLoaded);
            Assert.False(element.OpeningsLoaded);
        }

        [Fact]
        public void SupportedProductOutsideProjectTree_IsReported()
        {
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(IfcHopperModel.CreateDefault(), path);
            using (var source = MemoryModel.OpenRead(path))
            {
                using (var transaction = source.BeginTransaction("Add orphan"))
                {
                    var wall = source.Instances.New<Xbim.Ifc4x3.SharedBldgElements.IfcWall>();
                    wall.GlobalId = "0000000000000000000001";
                    transaction.Commit();
                }
                using (var stream = File.Create(path)) source.SaveAsStep21(stream);
            }
            Assert.Contains("IfcWall: 1", Assert.Single(IfcBackend.Reader.Read(path).ReadWarnings));
        }

        private static void Insert(string path, string entities)
        {
            var text = File.ReadAllText(path);
            var end = text.LastIndexOf("ENDSEC;", System.StringComparison.Ordinal);
            File.WriteAllText(path, text.Insert(end, entities + "\n"));
        }
    }
}
