using System;
using System.IO;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xunit;

namespace IfcHopper.Tests
{
    /// <summary>Reader behaviour: file cache, in-place writing and the bridge sample model.</summary>
    public class ReadTests
    {
        [Fact]
        public void Read_MissingFileThrows()
        {
            Assert.Throws<FileNotFoundException>(() => IfcBackend.Reader.Read(TestFiles.NewPath("missing.ifc")));
        }

        [Fact]
        public void Read_ReloadsWhenFileChanges()
        {
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(new Project("First")), path);
            Assert.Equal("First", IfcBackend.Reader.Read(path).Project.Name);

            IfcBackend.Writer.Write(new IfcHopperModel(new Project("Second")), path);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(1));
            Assert.Equal("Second", IfcBackend.Reader.Read(path).Project.Name);
        }

        [Fact]
        public void Read_KeepsSourceAndGlobalIds()
        {
            var path = TestFiles.NewPath();
            var site = new Site();
            var project = new Project();
            project.Sites.Add(site);
            IfcBackend.Writer.Write(new IfcHopperModel(project), path);

            var read = IfcBackend.Reader.Read(path);
            Assert.Equal(Path.GetFullPath(path), read.SourcePath);
            Assert.NotNull(read.Project.Source);
            Assert.Equal(22, read.Project.GlobalId.Length);
            Assert.Equal(22, read.Project.Sites[0].GlobalId.Length);
        }

        [Fact]
        public void WriteInPlace_PreservesSourceAndRestoresCachedHeader()
        {
            var source = TestFiles.NewPath("source.ifc");
            var model = new IfcHopperModel(new Project("P")) { Author = "Original" };
            IfcBackend.Writer.Write(model, source);

            var read = IfcBackend.Reader.Read(source);
            read.Author = "Editor";
            var copy = TestFiles.NewPath("copy.ifc");
            IfcBackend.Writer.Write(read, copy);

            Assert.Equal(File.ReadLines(source).Count(), File.ReadLines(copy).Count());
            Assert.Equal(new[] { "Editor" }, IfcBackend.Reader.ReadHeader(copy).Author);
            Assert.Equal(new[] { "Original" }, IfcBackend.Reader.ReadHeader(source).Author);
        }

        [Fact]
        public void Rebuild_FromReadObjectsKeepsGlobalIds()
        {
            var path = TestFiles.NewPath();
            var project = new Project();
            project.Sites.Add(new Site());
            IfcBackend.Writer.Write(new IfcHopperModel(project), path);
            var readSite = IfcBackend.Reader.Read(path).Project.Sites[0];

            var rebuilt = new Project("Rebuilt");
            rebuilt.Sites.Add(readSite);
            var rebuiltPath = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(rebuilt), rebuiltPath);

            Assert.Equal(readSite.GlobalId, IfcBackend.Reader.Read(rebuiltPath).Project.Sites[0].GlobalId);
        }

        [SampleModelFact]
        public void BridgeSample_ReadsNestedParts()
        {
            var bridge = IfcBackend.Reader.Read(TestFiles.BridgeSample).Project.Sites.Single().Facilities.Single();

            Assert.Equal("IfcBridge", bridge.IfcClass);
            Assert.Equal("SLAB_BRIDGE", bridge.ObjectType);
            Assert.Equal(new[] { "Substructure", "Superstructure" }, bridge.Parts.Select(p => p.Name));
            Assert.Equal(new[] { "A1", "P1", "P2", "P3", "P4", "A2" }, bridge.Parts[0].Parts.Select(p => p.Name));

            var pier = bridge.Parts[0].Parts[1];
            Assert.Equal(("PIER", FacilityUsage.Vertical), (pier.PredefinedType, pier.Usage));
            Assert.Contains(pier.Elements, e => e.IfcClass == "IfcColumn");
        }

        [SampleModelFact]
        public void BridgeSample_ReadsUnitsAndContexts()
        {
            var project = IfcBackend.Reader.Read(TestFiles.BridgeSample).Project;

            Assert.Equal("Metre", project.Units.Length.Name);
            var model = project.Contexts.Single(c => c.Dimension == 3);
            Assert.Equal(new[] { "Body", "Axis", "Box", "Reference" }, model.SubContexts.Select(s => s.Identifier));
            Assert.NotNull(model.TrueNorth);
        }

        [SampleModelFact]
        public void BridgeSample_WriteInPlaceKeepsAllData()
        {
            var copy = TestFiles.NewPath("bridge.ifc");
            IfcBackend.Writer.Write(IfcBackend.Reader.Read(TestFiles.BridgeSample), copy);

            Assert.Equal(File.ReadLines(TestFiles.BridgeSample).Count(), File.ReadLines(copy).Count());
        }
    }
}
