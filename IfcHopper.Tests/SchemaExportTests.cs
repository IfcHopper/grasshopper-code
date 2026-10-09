using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Common.Step21;
using Xbim.Ifc4.Interfaces;
using Xbim.IO.Memory;
using Xunit;
using Xunit.Abstractions;

namespace IfcHopper.Tests
{
    /// <summary>Writing IFC4X3, IFC4 and IFC2X3 files, with downgrades and warnings for what a schema does not support.</summary>
    public class SchemaExportTests
    {
        private readonly ITestOutputHelper _output;

        public SchemaExportTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData(IfcSchema.Ifc4x3, XbimSchemaVersion.Ifc4x3)]
        [InlineData(IfcSchema.Ifc4, XbimSchemaVersion.Ifc4)]
        [InlineData(IfcSchema.Ifc2x3, XbimSchemaVersion.Ifc2X3)]
        public void NewModel_IsWrittenInTheChosenSchema(IfcSchema schema, XbimSchemaVersion version)
        {
            var path = TestFiles.NewPath();
            var warnings = IfcBackend.Writer.Write(new IfcHopperModel(SampleProject()), path, schema);
            foreach (var warning in warnings) _output.WriteLine(warning);
            using (var ifc = MemoryModel.OpenRead(path)) Assert.Equal(version, ifc.SchemaVersion);

            var project = IfcBackend.Reader.Read(path).Project;
            Assert.Equal(1000.0, project.Georeference.Eastings, 6);
            Assert.Equal("EPSG:32632", project.Georeference.CrsName);

            var site = project.Sites.Single();
            var building = (Building)site.Facilities.Single(f => f.Name == "Building");
            var wall = building.Storeys.Single().Elements.Single();
            Assert.Equal("IfcWall", wall.IfcClass);
            var mesh = Assert.Single(wall.Geometry);
            // IFC2X3 Breps are read with one vertex per face corner, so distinct positions are compared.
            Assert.Equal(8, mesh.Vertices.Select(v => string.Join(",", v.Select(c => System.Math.Round(c, 6)))).Distinct().Count());
            Assert.Equal(new Colour(1, 0, 0), mesh.Colour);
            var common = (PropertySet)wall.PropertySets.Single(s => s.Name == "Pset_WallCommon");
            Assert.Equal(true, common["IsExternal"].Value);
            Assert.Equal(0.2, (double)((PropertySet)wall.PropertySets.Single(s => s.Name == "My_Set"))["Width"].Value, 9);
            Assert.Equal(5.0, ((QuantitySet)wall.PropertySets.Single(s => s.Name == "Qto_WallBaseQuantities"))["Length"].Value, 9);

            var bridge = site.Facilities.Single(f => f.Name == "Bridge");
            var course = Descendants(bridge).OfType<Element>().Single();
            if (schema == IfcSchema.Ifc4x3)
            {
                Assert.Empty(warnings);
                Assert.Equal("SOLIDWALL", wall.PredefinedType);
                Assert.Equal(FacilityType.Bridge, bridge.Type);
                Assert.Equal("IfcCourse", course.IfcClass);
                return;
            }

            // Downgrades are named in the warnings and kept in ObjectType.
            Assert.Contains(warnings, w => w.StartsWith("IfcBridge is written as IfcBuilding"));
            Assert.Contains(warnings, w => w.StartsWith("IfcBridgePart is written as IfcBuildingStorey"));
            Assert.Contains(warnings, w => w.StartsWith("IfcCourse is written as IfcBuildingElementProxy"));
            Assert.Contains(warnings, w => w.StartsWith("Number quantities are written as count quantities"));
            Assert.Equal(FacilityType.Building, bridge.Type);
            Assert.Equal("IfcBuildingElementProxy", course.IfcClass);
            Assert.Equal("IfcCourse.PAVEMENT", course.ObjectType);
            if (schema == IfcSchema.Ifc4)
            {
                Assert.Equal("SOLIDWALL", wall.PredefinedType);
                return;
            }
            Assert.Contains(warnings, w => w.StartsWith("The georeference is written as the ePSet_MapConversion"));
            Assert.Contains(warnings, w => w.StartsWith("IfcWall predefined type SOLIDWALL is written as object type"));
            Assert.Contains(warnings, w => w.StartsWith("IfcNonNegativeLengthMeasure values are written as IfcReal"));
            Assert.Equal("SOLIDWALL", wall.PredefinedType);
        }

        [Fact]
        public void Ifc2x3File_IsEditedInPlaceInItsSchema()
        {
            var source = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(SampleProject()), source, IfcSchema.Ifc2x3);
            var model = IfcBackend.Reader.Read(source);
            var storey = ((Building)model.Project.Sites[0].Facilities.Single(f => f.Name == "Building")).Storeys[0];

            var added = new PropertySet("Added");
            added.Add("Note", "edited");
            storey.Elements[0].MergePropertySets(new[] { added });
            var column = new Element("Column", "IfcColumn");
            column.Geometry.Add(GeometryTests.Cube());
            storey.Elements.Add(column);

            var path = TestFiles.NewPath("edited.ifc");
            var warnings = IfcBackend.Writer.Write(model, path);
            Assert.Empty(warnings);
            using (var ifc = MemoryModel.OpenRead(path))
            {
                Assert.Equal(XbimSchemaVersion.Ifc2X3, ifc.SchemaVersion);
                Assert.All(ifc.Instances.OfType<IIfcRoot>(), r => Assert.NotNull(r.OwnerHistory));
            }

            var read = ((Building)IfcBackend.Reader.Read(path).Project.Sites[0].Facilities.Single(f => f.Name == "Building")).Storeys[0];
            Assert.Equal("edited", ((PropertySet)read.Elements[0].PropertySets.Single(s => s.Name == "Added"))["Note"].Value);
            Assert.Single(read.Elements.Single(e => e.Name == "Column").Geometry);
        }

        [Fact]
        public void ReadFile_WrittenInAnotherSchema_IsRebuilt()
        {
            var source = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(SampleProject()), source);
            var model = IfcBackend.Reader.Read(source);

            var path = TestFiles.NewPath("ifc4.ifc");
            var warnings = IfcBackend.Writer.Write(model, path, IfcSchema.Ifc4);
            Assert.StartsWith("The model was read from an IFC4X3_ADD2 file and is rebuilt as a new IFC4 file", warnings[0]);

            var project = IfcBackend.Reader.Read(path).Project;
            Assert.Equal(model.Project.GlobalId, project.GlobalId);
            var wall = ((Building)project.Sites[0].Facilities.Single(f => f.Name == "Building")).Storeys[0].Elements[0];
            Assert.Single(wall.Geometry);
            Assert.Contains(wall.PropertySets, s => s.Name == "Pset_WallCommon");
        }

        [Fact]
        public void SchemaNames_AreParsed()
        {
            Assert.Equal(IfcSchema.Ifc4x3, IfcSchemas.Parse("ifc4x3_add2"));
            Assert.Equal(IfcSchema.Ifc4, IfcSchemas.Parse(" IFC4 "));
            Assert.Equal(IfcSchema.Ifc2x3, IfcSchemas.Parse("IFC2x3"));
            Assert.Null(IfcSchemas.Parse("IFC5"));
        }

        [SampleModelFact("IFC Schependomlaan.ifc")]
        public void SchependomlaanSample_PropertySetIsAddedInPlace()
        {
            var model = IfcBackend.Reader.Read(TestFiles.Sample("IFC Schependomlaan.ifc"));
            var wall = Descendants(model.Project).OfType<Element>().First(e => e.IfcClass == "IfcWallStandardCase" || e.IfcClass == "IfcWall");
            var set = new PropertySet("IfcHopper_Check");
            set.Add("Checked", true);
            wall.MergePropertySets(new[] { set });

            var path = TestFiles.NewPath("schependomlaan.ifc");
            Assert.Empty(IfcBackend.Writer.Write(model, path));
            var read = Descendants(IfcBackend.Reader.Read(path).Project).OfType<Element>().Single(e => e.GlobalId == wall.GlobalId);
            Assert.Equal(true, ((PropertySet)read.PropertySets.Single(s => s.Name == "IfcHopper_Check"))["Checked"].Value);
        }

        private static IEnumerable<ModelObject> Descendants(ModelObject parent) => parent.Children.SelectMany(c => new[] { c }.Concat(Descendants(c)));

        /// <summary>
        /// A millimetre project with a georeference, a building with a red typed wall (property and quantity sets, one value type
        /// IFC2X3 lacks) and a bridge with a part holding an IfcCourse.
        /// </summary>
        private static Project SampleProject()
        {
            var wall = new Element("Wall", "IfcWall");
            wall.SetType("SOLIDWALL");
            wall.Geometry.Add(GeometryTests.Cube().WithColour(new Colour(1, 0, 0)));
            var common = new PropertySet("Pset_WallCommon");
            common.Add("IsExternal", true);
            var custom = new PropertySet("My_Set");
            custom.Add("Width", 0.2, "IfcPositiveLengthMeasure");
            custom.Add("Clearance", 0.05, "IfcNonNegativeLengthMeasure");
            var quantities = new QuantitySet("Qto_WallBaseQuantities");
            quantities.Add("Length", 5);
            quantities.Add("Layers", 3, QuantityKind.Number);
            wall.PropertySets.AddRange(new PropertySetDefinition[] { common, custom, quantities });

            var storey = new Storey();
            storey.Elements.Add(wall);
            var building = new Building("Building");
            building.Storeys.Add(storey);

            var course = new Element("Course", "IfcCourse");
            course.SetType("PAVEMENT");
            var part = new FacilityPart(FacilityType.Bridge, "Deck");
            part.Elements.Add(course);
            var bridge = new Facility(FacilityType.Bridge, "Bridge");
            bridge.Parts.Add(part);

            var site = new Site();
            site.Facilities.AddRange(new Facility[] { building, bridge });
            var project = new Project
            {
                Units = new Units { Length = Units.Find(UnitKind.Length, "mm") },
                Georeference = new Georeference("EPSG:32632") { Eastings = 1000, Northings = 2000, OrthogonalHeight = 10 },
            };
            project.Sites.Add(site);
            return project;
        }
    }
}
