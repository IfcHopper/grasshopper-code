using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xunit;

namespace IfcHopper.Tests
{
    public class GlobalIdTests
    {
        [Fact]
        public void FromGuid_MatchesXbimEncoding()
        {
            for (int i = 0; i < 100; i++)
            {
                var guid = Guid.NewGuid();
                Assert.Equal(Xbim.Ifc4.UtilityResource.IfcGloballyUniqueId.ConvertToBase64(guid), GlobalIds.FromGuid(guid));
            }
        }

        [Fact]
        public void FromSeed_IsStableAndValid()
        {
            var id = GlobalIds.FromSeed("component/0");
            Assert.True(GlobalIds.IsValid(id));
            Assert.Equal(id, GlobalIds.FromSeed("component/0"));
            Assert.NotEqual(id, GlobalIds.FromSeed("component/1"));
        }

        [Fact]
        public void TryParse_AcceptsIfcIdsAndGuids()
        {
            var ifcId = GlobalIds.New();
            Assert.True(GlobalIds.TryParse(ifcId, out var parsed));
            Assert.Equal(ifcId, parsed);

            var guid = Guid.NewGuid();
            Assert.True(GlobalIds.TryParse(guid.ToString(), out parsed));
            Assert.Equal(GlobalIds.FromGuid(guid), parsed);

            Assert.False(GlobalIds.TryParse("not an id", out _));
            Assert.False(GlobalIds.IsValid("4" + new string('0', 21)));
        }

        [Fact]
        public void Write_KeepsGivenIdsAndDerivesStableRelationshipIds()
        {
            Project BuildProject()
            {
                var site = new Site { GlobalId = GlobalIds.FromSeed("site") };
                var project = new Project { GlobalId = GlobalIds.FromSeed("project") };
                project.Sites.Add(site);
                return project;
            }

            var first = TestFiles.NewPath();
            var second = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(BuildProject()), first);
            IfcBackend.Writer.Write(new IfcHopperModel(BuildProject()), second);

            Assert.Equal(GlobalIds.FromSeed("site"), IfcBackend.Reader.Read(first).Project.Sites[0].GlobalId);
            Assert.Equal(RootIds(first), RootIds(second));
        }

        [Fact]
        public void Write_RejectsDuplicateIds()
        {
            var storey = new Storey { GlobalId = GlobalIds.FromSeed("storey") };
            var a = new Building("A");
            var b = new Building("B");
            a.Storeys.Add(storey);
            b.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.AddRange(new[] { a, b });
            var project = new Project();
            project.Sites.Add(site);

            var error = Assert.Throws<ArgumentException>(() => IfcBackend.Writer.Write(new IfcHopperModel(project), TestFiles.NewPath()));
            Assert.Contains("more than once", error.Message);
        }

        [Fact]
        public void Write_RejectsInvalidIds()
        {
            var project = new Project { GlobalId = "bad" };
            Assert.Throws<ArgumentException>(() => IfcBackend.Writer.Write(new IfcHopperModel(project), TestFiles.NewPath()));
        }

        /// <summary>The first attribute (GlobalId) of every rooted entity, in file order.</summary>
        private static string[] RootIds(string path) =>
            File.ReadLines(path).Select(l => Regex.Match(l, @"^#\d+=IFC\w+\('([^']{22})'")).Where(m => m.Success).Select(m => m.Groups[1].Value).ToArray();
    }
}
