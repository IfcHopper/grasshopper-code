using System.IO;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Ifc4.Interfaces;
using Xbim.IO.Memory;
using Xunit;

namespace IfcHopper.Tests
{
    public class ClassificationTests
    {
        private static ClassificationReference Uniclass(string code, string name = null) =>
            new ClassificationReference("Uniclass 2015", code, name, "v1.30", "https://uniclass.thenbs.com/taxon/" + code?.ToLowerInvariant());

        [Fact]
        public void Merge_ReplacesBySystemAndRemovesWithoutCode()
        {
            var element = new Element();
            element.Classifications.Add(Uniclass("Pr_20_93_52"));
            element.Classifications.Add(new ClassificationReference("OmniClass", "23-13 35 11"));
            element.Classifications.Add(Uniclass("Ss_20_10"));

            var replacement = Uniclass("Pr_20_93_53");
            element.MergeClassifications(new[] { replacement, new ClassificationReference("omniclass", null), new ClassificationReference("NL-SfB", "28.1") });

            Assert.Equal(new[] { "Pr_20_93_53", "28.1" }, element.Classifications.Select(c => c.Code));
            Assert.Same(replacement, element.Classifications[0]);
            Assert.Null(new ClassificationReference(" ", " ").System);
        }

        [Theory]
        [InlineData(IfcSchema.Ifc4x3)]
        [InlineData(IfcSchema.Ifc4)]
        [InlineData(IfcSchema.Ifc2x3)]
        public void References_AreWrittenOnceAndReadWithTypeReferences(IfcSchema schema)
        {
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(SampleModel(), path, schema);

            using (var ifc = MemoryModel.OpenRead(path))
            {
                Assert.Equal(2, ifc.Instances.OfType<IIfcClassification>().Count());
                Assert.Equal(4, ifc.Instances.OfType<IIfcClassificationReference>().Count());
                var shared = ifc.Instances.OfType<IIfcRelAssociatesClassification>()
                    .Single(r => (string)((IIfcClassificationReference)r.RelatingClassification).Identification == "Pr_20_93_52");
                Assert.Equal(2, shared.RelatedObjects.Count());
            }

            var model = IfcBackend.Reader.Read(path);
            Assert.Equal("Ss_25", Assert.Single(model.Project.Classifications).Code);
            var elements = Storey(model).Elements.ToDictionary(e => e.Name);

            var wall = elements["Wall"];
            var uniclass = wall.Classifications[0];
            Assert.Equal(("Uniclass 2015", "v1.30", "Pr_20_93_52", "Concrete walls"), (uniclass.System, uniclass.Edition, uniclass.Code, uniclass.Name));
            Assert.Equal("https://uniclass.thenbs.com/taxon/pr_20_93_52", uniclass.Location);
            Assert.Same(uniclass, elements["Wall 2"].Classifications.Single(c => !c.FromType));

            // The type's Uniclass reference is overridden by the wall's own; its OmniClass reference is inherited.
            Assert.Equal(new[] { ("Uniclass 2015", false), ("OmniClass", true) }, wall.Classifications.Select(c => (c.System, c.FromType)));
            var type = wall.Type;
            Assert.Equal(new[] { "Pr_20_93", "23-13 35 11" }, type.Classifications.Select(c => c.Code));
            Assert.All(type.Classifications, c => Assert.False(c.FromType));
            Assert.Null(elements["Wall 2"].Type);
        }

        [Fact]
        public void References_AreEditedInPlace()
        {
            var source = TestFiles.NewPath();
            IfcBackend.Writer.Write(SampleModel(), source);
            var model = IfcBackend.Reader.Read(source);
            var wall = (Element)Storey(model).Elements.Single(e => e.Name == "Wall").Copy();
            wall.MergeClassifications(new[] { Uniclass("Pr_20_93_53", "Precast walls"), new ClassificationReference("NL-SfB", "21.1") });
            var type = (ElementType)wall.Type.Copy();
            type.MergeClassifications(new[] { new ClassificationReference("OmniClass", null) });
            var path = TestFiles.NewPath("edited.ifc");

            IfcBackend.Writer.Write(ModelEdits.Apply(model, new ModelObject[] { wall, type }, out var notFound), path);

            Assert.Empty(notFound);
            var elements = Storey(IfcBackend.Reader.Read(path)).Elements.ToDictionary(e => e.Name);
            Assert.Equal(new[] { "Pr_20_93_53", "21.1" }, elements["Wall"].Classifications.Select(c => c.Code));
            Assert.Equal("Pr_20_93_52", elements["Wall 2"].Classifications.Single(c => !c.FromType).Code);
            Assert.Equal(new[] { "Pr_20_93" }, elements["Wall"].Type.Classifications.Select(c => c.Code));

            using (var ifc = MemoryModel.OpenRead(path))
            {
                // References no longer used stay in the file like a library; their emptied relationships are deleted.
                Assert.Equal(6, ifc.Instances.OfType<IIfcClassificationReference>().Count());
                Assert.Equal(3, ifc.Instances.OfType<IIfcClassification>().Count());
                Assert.All(ifc.Instances.OfType<IIfcRelAssociatesClassification>(), r => Assert.NotEmpty(r.RelatedObjects));
                Assert.Equal(1, ifc.Instances.OfType<IIfcClassificationReference>().Count(r => (string)r.Identification == "Pr_20_93_53"));
            }
        }

        [Fact]
        public void UnchangedReferences_KeepTheFile()
        {
            var source = TestFiles.NewPath();
            IfcBackend.Writer.Write(SampleModel(), source);
            var model = IfcBackend.Reader.Read(source);
            var wall = (Element)Storey(model).Elements.Single(e => e.Name == "Wall").Copy();
            _ = wall.Classifications;
            var path = TestFiles.NewPath("unchanged.ifc");

            IfcBackend.Writer.Write(ModelEdits.Apply(model, new[] { wall }, out _), path);

            long Count(string file)
            {
                using (var ifc = MemoryModel.OpenRead(file)) return ifc.Instances.Count;
            }
            Assert.Equal(Count(source), Count(path));
        }

        [Fact]
        public void NestedReferences_TakeTheSystemOfTheTop()
        {
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(SampleModel(), path);
            var nested = TestFiles.NewPath("nested.ifc");
            using (var ifc = MemoryModel.OpenRead(path))
            {
                using (var txn = ifc.BeginTransaction("nest"))
                {
                    var reference = ifc.Instances.OfType<IIfcClassificationReference>().Single(r => (string)r.Identification == "Pr_20_93_52");
                    IIfcClassificationReference parent = ifc.Instances.New<Xbim.Ifc4x3.ExternalReferenceResource.IfcClassificationReference>();
                    parent.Identification = "Pr_20";
                    parent.ReferencedSource = reference.ReferencedSource;
                    reference.ReferencedSource = parent;
                    txn.Commit();
                }
                using (var stream = File.Create(nested)) ifc.SaveAsStep21(stream);
            }

            var wall = Storey(IfcBackend.Reader.Read(nested)).Elements.Single(e => e.Name == "Wall");
            Assert.Equal(("Uniclass 2015", "v1.30", "Pr_20_93_52"), (wall.Classifications[0].System, wall.Classifications[0].Edition, wall.Classifications[0].Code));
        }

        private static Storey Storey(IfcHopperModel model) => ((Building)model.Project.Sites[0].Facilities[0]).Storeys[0];

        /// <summary>
        /// A project (Uniclass Ss_25) with Wall (Uniclass Pr_20_93_52) of a wall type (Uniclass Pr_20_93, OmniClass 23-13 35 11) and Wall 2
        /// sharing the wall's reference made by another call.
        /// </summary>
        private static IfcHopperModel SampleModel()
        {
            var type = new ElementType("Wall type", "IfcWallType");
            type.MergeClassifications(new[] { Uniclass("Pr_20_93"), new ClassificationReference("OmniClass", "23-13 35 11") });
            var wall = new Element("Wall", "IfcWall");
            wall.AssignType(type);
            wall.MergeClassifications(new[] { Uniclass("Pr_20_93_52", "Concrete walls") });
            var wall2 = new Element("Wall 2", "IfcWall");
            wall2.MergeClassifications(new[] { Uniclass("Pr_20_93_52", "Concrete walls") });
            var storey = new Storey();
            storey.Elements.Add(wall);
            storey.Elements.Add(wall2);
            var building = new Building();
            building.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project();
            project.Sites.Add(site);
            project.MergeClassifications(new[] { Uniclass("Ss_25") });
            return new IfcHopperModel(project);
        }
    }
}
