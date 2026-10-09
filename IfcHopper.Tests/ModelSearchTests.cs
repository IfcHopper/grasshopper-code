using System;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xunit;

namespace IfcHopper.Tests
{
    public class ModelSearchTests
    {
        [Fact]
        public void Classes_MatchSubclassesUnlessExact()
        {
            var project = SampleProject();
            Assert.Equal(new[] { "Wall", "Slab", "Door" }, Names(project, q => q.Classes.Add("ifcbuiltelement")));
            Assert.Empty(Names(project, q => { q.Classes.Add("IfcBuiltElement"); q.ExactClass = true; }));
            Assert.Equal(new[] { "Wall", "Door" }, Names(project, q => { q.Classes.Add("IfcWall"); q.Classes.Add("IfcDoor"); }));
            Assert.Equal(new[] { "Ground floor" }, Names(project, q => q.Classes.Add("IfcSpatialElement")).Where(n => n == "Ground floor"));

            var opening = ModelSearch.Find(project, Query(q => q.Classes.Add("IfcOpeningElement"))).Single();
            Assert.Equal(("Hole", "Wall"), (opening.Object.Name, opening.Parent.Name));
            Assert.Equal("Project / Site / House / Ground floor / Wall", opening.Path);
            Assert.Throws<ArgumentException>(() => Names(project, q => q.Classes.Add("IfcNotAClass")));
        }

        [Fact]
        public void NamesAndGlobalIds_Match()
        {
            var project = SampleProject();
            Assert.Equal(new[] { "Wall" }, Names(project, q => q.Name = "*WALL"));
            Assert.Equal(new[] { "Slab" }, Names(project, q => { q.Name = "s*"; q.Classes.Add("IfcElement"); }));
            Assert.Equal(new[] { "Door" }, Names(project, q => q.Name = "?oor"));

            Assert.Equal(new[] { "Door" }, Names(project, q => q.GlobalIds.Add(DoorGuid.ToString())));
            Assert.Equal(new[] { "Door" }, Names(project, q => q.GlobalIds.Add(GlobalIds.FromGuid(DoorGuid))));
            Assert.Throws<ArgumentException>(() => Names(project, q => q.GlobalIds.Add("not an id")));
        }

        [Fact]
        public void PropertyFilters_CompareValuesOfObjectsAndTheirTypes()
        {
            var project = SampleProject();
            Assert.Equal(new[] { "Wall", "Slab" }, Names(project, q => q.Properties.Add(PropertyFilter.Parse("pset_wallcommon.isexternal = TRUE"))));
            Assert.Equal(new[] { "Slab" }, Names(project, q => q.Properties.Add(PropertyFilter.Parse("Pset_WallCommon.FireRating"))));
            Assert.Equal(new[] { "Wall" }, Names(project, q => q.Properties.Add(PropertyFilter.Parse("Qto_WallBaseQuantities.Length > 5"))));
            Assert.Empty(Names(project, q => q.Properties.Add(PropertyFilter.Parse("Qto_WallBaseQuantities.Length < 5"))));
            Assert.Equal(new[] { "Door" }, Names(project, q => q.Properties.Add(PropertyFilter.Parse("Pset_DoorCommon.Reference != D02"))));

            // Numbers in other units are converted, e.g. millimetres.
            Assert.Equal(new[] { "Wall" }, Names(project, q =>
            {
                q.Properties.Add(PropertyFilter.Parse("Qto_WallBaseQuantities.Length >= 6000"));
                q.NumberToSi = (value, dimension) => value * Math.Pow(0.001, dimension);
            }));
            Assert.Throws<ArgumentException>(() => PropertyFilter.Parse("IsExternal = true"));
            Assert.Equal(("Pset_A", "My prop", "<=", "3"), Fields(PropertyFilter.Parse(" Pset_A.My prop <= 3 ")));
        }

        [Fact]
        public void Summary_CountsClassesAndDrawsTheTree()
        {
            var project = SampleProject();
            var counts = ModelSearch.CountByClass(project).ToDictionary(c => c.IfcClass, c => c.Count);
            Assert.Equal(1, counts["IfcWall"]);
            Assert.Equal(1, counts["IfcOpeningElement"]);
            Assert.Equal(1, counts["IfcBuildingStorey"]);

            Assert.Equal(string.Join(Environment.NewLine,
                "Project [IfcProject]",
                "  Site [IfcSite]",
                "    House [IfcBuilding]",
                "      Ground floor [IfcBuildingStorey]: 1 IfcWall, 1 IfcSlab, 1 IfcDoor"), ModelSearch.Tree(project));
            Assert.Contains("        Hole [IfcOpeningElement]", ModelSearch.Tree(project, true));
        }

        [Fact]
        public void ReadModels_AreSearchedAsTheyLoad()
        {
            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(SampleProject()), path);
            var project = IfcBackend.Reader.Read(path).Project;

            var wall = ModelSearch.Find(project, Query(q => q.Properties.Add(PropertyFilter.Parse("Qto_WallBaseQuantities.Length = 6")))).Single();
            Assert.Equal("Wall", wall.Object.Name);
            Assert.NotNull(wall.Object.Source);
            Assert.Equal("Ground floor", wall.Parent.Name);
        }

        private static readonly Guid DoorGuid = new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff");

        private static (string, string, string, string) Fields(PropertyFilter f) => (f.SetName, f.PropertyName, f.Operator, f.Value);

        private static ObjectQuery Query(Action<ObjectQuery> setup)
        {
            var query = new ObjectQuery();
            setup(query);
            return query;
        }

        private static string[] Names(ModelObject root, Action<ObjectQuery> setup) =>
            ModelSearch.Find(root, Query(setup)).Select(r => r.Object.Name).ToArray();

        private static System.Collections.Generic.IEnumerable<Element> Elements(Project project) =>
            ModelSearch.Find(project, new ObjectQuery()).Select(r => r.Object).OfType<Element>();

        /// <summary>
        /// Project > Site > House > Ground floor with Wall (external, 6 m long, voided by Hole), Slab (external through its type, which also
        /// has a fire rating) and Door (reference D01).
        /// </summary>
        private static Project SampleProject()
        {
            var wall = new Element("Wall", "IfcWall");
            wall.Geometry.Add(GeometryTests.Cube());
            var common = new PropertySet("Pset_WallCommon");
            common.Add("IsExternal", true);
            var quantities = new QuantitySet("Qto_WallBaseQuantities");
            quantities.Add("Length", 6.0);
            wall.PropertySets.Add(common);
            wall.PropertySets.Add(quantities);
            var hole = new Opening("Hole");
            hole.Geometry.Add(GeometryTests.Cube());
            wall.Openings.Add(hole);

            var slabType = new ElementType("Slab type", "IfcSlabType");
            var typeSet = new PropertySet("Pset_WallCommon");
            typeSet.Add("IsExternal", true);
            typeSet.Add("FireRating", "REI 60");
            slabType.PropertySets.Add(typeSet);
            var slab = new Element("Slab", "IfcSlab");
            slab.AssignType(slabType);
            // Created objects do not inherit type sets on their own; read ones do. Mirror the read state.
            slab.PropertySets.Add(new PropertySet("Pset_WallCommon"));
            ((PropertySet)slab.PropertySets[0]).Add("IsExternal", true);
            ((PropertySet)slab.PropertySets[0]).Add("FireRating", "REI 60");

            var door = new Element("Door", "IfcDoor") { GlobalId = GlobalIds.FromGuid(DoorGuid) };
            var doorSet = new PropertySet("Pset_DoorCommon");
            doorSet.Add("Reference", "D01");
            door.PropertySets.Add(doorSet);

            var storey = new Storey { Name = "Ground floor" };
            storey.Elements.Add(wall);
            storey.Elements.Add(slab);
            storey.Elements.Add(door);
            var building = new Building { Name = "House" };
            building.Storeys.Add(storey);
            var site = new Site { Name = "Site" };
            site.Facilities.Add(building);
            var project = new Project { Name = "Project" };
            project.Sites.Add(site);
            return project;
        }
    }
}
