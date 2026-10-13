using System;
using System.IO;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xunit;

namespace IfcHopper.Tests
{
    /// <summary>Reader behaviour: file cache, in-place writing and source identity.</summary>
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

    }
}
