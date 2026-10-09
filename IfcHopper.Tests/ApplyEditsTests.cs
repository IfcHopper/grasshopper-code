using System.IO;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xunit;

namespace IfcHopper.Tests
{
    public class ApplyEditsTests
    {
        /// <summary>Project > Site > Bridge > Substructure > (Pier with a column, Abutment).</summary>
        private static IfcHopperModel ReadSource()
        {
            var pier = new FacilityPart(FacilityType.Bridge, "Pier");
            pier.Elements.Add(new Element("Column", "IfcColumn"));
            var substructure = new FacilityPart(FacilityType.Bridge, "Substructure");
            substructure.Parts.AddRange(new[] { pier, new FacilityPart(FacilityType.Bridge, "Abutment") });
            var bridge = new Facility(FacilityType.Bridge, "Bridge");
            bridge.Parts.Add(substructure);
            var site = new Site("Site");
            site.Facilities.Add(bridge);
            var project = new Project("Project");
            project.Sites.Add(site);

            var path = TestFiles.NewPath("source.ifc");
            IfcBackend.Writer.Write(new IfcHopperModel(project), path);
            return IfcBackend.Reader.Read(path);
        }

        private static FacilityPart Substructure(IfcHopperModel model) => model.Project.Sites[0].Facilities[0].Parts[0];

        [Fact]
        public void DeepEdits_AreWrittenWithoutRebuildingParents()
        {
            var model = ReadSource();
            var pier = (FacilityPart)Substructure(model).Parts[0].Copy();
            pier.Name = "Pier P1";
            var column = (Element)pier.Elements[0].Copy();
            column.SetType("PIERSTEM");

            var edited = ModelEdits.Apply(model, new ModelObject[] { pier, column }, out var notFound);
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(edited, path);
            var read = Substructure(IfcBackend.Reader.Read(path));

            Assert.Empty(notFound);
            Assert.Equal(new[] { "Pier P1", "Abutment" }, read.Parts.Select(p => p.Name));
            Assert.Equal("PIERSTEM", read.Parts[0].Elements[0].PredefinedType);
            Assert.Equal("Pier", Substructure(model).Parts[0].Name);
        }

        [Fact]
        public void EditedListsAreKept()
        {
            var model = ReadSource();
            var substructure = (FacilityPart)Substructure(model).Copy();
            substructure.Parts.RemoveAt(1);

            var edited = ModelEdits.Apply(model, new ModelObject[] { substructure }, out _);
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(edited, path);

            Assert.Equal("Pier", Assert.Single(Substructure(IfcBackend.Reader.Read(path)).Parts).Name);
        }

        [Fact]
        public void UnknownObjects_AreReported()
        {
            var model = ReadSource();
            var created = new Site("New") { GlobalId = GlobalIds.New() };
            var fromOtherFile = ReadSource().Project.Sites[0];

            ModelEdits.Apply(model, new ModelObject[] { created, fromOtherFile }, out var notFound);

            Assert.Equal(2, notFound.Count);
        }

        [Fact]
        public void ProjectEdit_ReplacesTheProject()
        {
            var model = ReadSource();
            var project = (Project)model.Project.Copy();
            project.Name = "Renamed";

            var edited = ModelEdits.Apply(model, new ModelObject[] { project }, out var notFound);

            Assert.Empty(notFound);
            Assert.Equal("Renamed", edited.Project.Name);
            Assert.Equal(model.SourcePath, edited.SourcePath);
        }
    }
}
