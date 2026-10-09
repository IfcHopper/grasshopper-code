using System;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xunit;

namespace IfcHopper.Tests
{
    public class AttributeTests
    {
        [Fact]
        public void Schema_ListsClassSpecificSimpleAttributes()
        {
            var door = IfcBackend.Schema.GetAttributes("ifcdoor").ToDictionary(a => a.Name);
            Assert.Equal(new[] { "OverallHeight", "OverallWidth", "OperationType", "UserDefinedOperationType" }, door.Keys);
            Assert.Equal(ValueKind.Real, door["OverallHeight"].Kind);
            Assert.Equal(1, door["OverallHeight"].Dimension);
            Assert.Contains("DOUBLE_DOOR_SINGLE_SWING", door["OperationType"].EnumValues);
            Assert.Equal("DOUBLE_DOOR_SINGLE_SWING", door["OperationType"].Convert("double_door_single_swing"));
            Assert.Throws<ArgumentException>(() => door["OperationType"].Convert("revolving sideways"));
            Assert.Equal(2.1, door["OverallHeight"].Convert("2.1"));

            Assert.Contains(IfcBackend.Schema.GetAttributes("IfcDoorType"), a => a.Name == "ParameterTakesPrecedence" && a.Kind == ValueKind.Boolean);
            Assert.Empty(IfcBackend.Schema.GetAttributes("IfcWall"));
            Assert.Empty(IfcBackend.Schema.GetAttributes("NotAClass"));
        }

        [Theory]
        [InlineData(IfcSchema.Ifc4x3)]
        [InlineData(IfcSchema.Ifc2x3)]
        public void TagAndAttributes_AreWrittenInFileUnitsAndRead(IfcSchema schema)
        {
            var path = TestFiles.NewPath();
            var warnings = IfcBackend.Writer.Write(DoorModel(), path, schema);

            var door = ReadDoor(IfcBackend.Reader.Read(path));
            Assert.Equal("D01", door.Tag);
            Assert.Equal(2.1, (double)door.Attributes["OverallHeight"], 9);
            Assert.Equal(0.9, (double)door.Attributes["OverallWidth"], 9);
            Assert.Equal("T1", door.Type.Tag);
            if (schema == IfcSchema.Ifc2x3)
            {
                Assert.Contains(warnings, w => w.Contains("IfcDoor.OperationType"));
                return;
            }
            Assert.Equal("DOUBLE_DOOR_SINGLE_SWING", door.Attributes["OperationType"]);
            Assert.Equal(true, door.Type.Attributes["ParameterTakesPrecedence"]);
        }

        [Fact]
        public void TagAndAttributes_AreEditedInPlace()
        {
            var source = TestFiles.NewPath();
            IfcBackend.Writer.Write(DoorModel(), source);
            var model = IfcBackend.Reader.Read(source);
            var door = (Element)ReadDoor(model).Copy();
            door.Tag = "D02";
            door.Attributes["OverallHeight"] = 2.4;
            door.Attributes.Remove("OverallWidth");
            var path = TestFiles.NewPath();

            IfcBackend.Writer.Write(ModelEdits.Apply(model, new[] { door }, out _), path);

            var read = ReadDoor(IfcBackend.Reader.Read(path));
            Assert.Equal("D02", read.Tag);
            Assert.Equal(2.4, (double)read.Attributes["OverallHeight"], 9);
            Assert.False(read.Attributes.ContainsKey("OverallWidth"));
            Assert.Equal("DOUBLE_DOOR_SINGLE_SWING", read.Attributes["OperationType"]);
        }

        private static Element ReadDoor(IfcHopperModel model) => ((Building)model.Project.Sites[0].Facilities[0]).Storeys[0].Elements.Single();

        /// <summary>A millimetre model with door D01 (2.1 × 0.9 m, double door) of type T1 (parameters take precedence).</summary>
        private static IfcHopperModel DoorModel()
        {
            var type = new ElementType("Door type", "IfcDoorType") { Tag = "T1" };
            type.Attributes["ParameterTakesPrecedence"] = true;
            var door = new Element("Door", "IfcDoor") { Tag = "D01" };
            door.Attributes["OverallHeight"] = 2.1;
            door.Attributes["OverallWidth"] = 0.9;
            door.Attributes["OperationType"] = "DOUBLE_DOOR_SINGLE_SWING";
            door.AssignType(type);
            var storey = new Storey();
            storey.Elements.Add(door);
            var building = new Building();
            building.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project { Units = new Units { Length = Units.Find(UnitKind.Length, "mm") } };
            project.Sites.Add(site);
            return new IfcHopperModel(project);
        }
    }
}
