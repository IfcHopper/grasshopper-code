using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.IO.Memory;
using Xunit;

namespace IfcHopper.Tests
{
    public class PublicFixtureTests
    {
        [Theory]
        [InlineData("wall-extruded-solid.ifc", "IfcWall")]
        [InlineData("slab-openings.ifc", "IfcSlab")]
        [InlineData("column-straight-rectangle-tessellation.ifc", "IfcColumn")]
        public void PublicModel_ReadsElementsAndGeometry(string file, string ifcClass)
        {
            var model = IfcBackend.Reader.Read(TestFiles.PublicFixture(file));
            var elements = Descendants(model.Project).OfType<Element>().Where(e => e.IfcClass == ifcClass).ToList();
            Assert.NotEmpty(elements);
            Assert.All(elements, e => Assert.NotEmpty(e.Geometry));
            Assert.All(elements, e => Assert.NotNull(e.Placement));
        }

        [Theory]
        [InlineData("wall-extruded-solid.ifc")]
        [InlineData("mapped-shape-with-transformation.ifc")]
        [InlineData("linear-placement-of-signal.ifc")]
        public void PublicModel_UnchangedWritePreservesEntitiesAndIdentity(string file)
        {
            var source = TestFiles.PublicFixture(file);
            var model = IfcBackend.Reader.Read(source);
            var ids = Descendants(model.Project).Select(o => o.GlobalId).OrderBy(id => id).ToArray();
            var output = TestFiles.NewPath();
            Assert.Empty(IfcBackend.Writer.Write(model, output));
            // STEP writers normalize comments and line wrapping; compare entities, not text lines.
            using (var original = MemoryModel.OpenRead(source))
            using (var written = MemoryModel.OpenRead(output))
                Assert.Equal(original.Instances.Select(e => (e.EntityLabel, e.GetType().Name)).OrderBy(e => e.EntityLabel),
                    written.Instances.Select(e => (e.EntityLabel, e.GetType().Name)).OrderBy(e => e.EntityLabel));
            Assert.Equal(ids, Descendants(IfcBackend.Reader.Read(output).Project).Select(o => o.GlobalId).OrderBy(id => id));
        }

        [Fact]
        public void PublicSlab_ReadsOpenings()
        {
            var model = IfcBackend.Reader.Read(TestFiles.PublicFixture("slab-openings.ifc"));
            var slabs = Descendants(model.Project).OfType<Element>().Where(e => e.IfcClass == "IfcSlab");
            Assert.Contains(slabs, e => e.Openings.Count > 0);
        }

        private static IEnumerable<ModelObject> Descendants(ModelObject parent) =>
            parent.Children.SelectMany(child => new[] { child }.Concat(Descendants(child)));
    }
}
