using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4x3.Kernel;
using Xbim.Ifc4x3.MeasureResource;
using Xbim.Ifc4x3.ProductExtension;
using Xbim.Ifc4x3.PropertyResource;
using Xbim.Ifc4x3.SharedBldgElements;
using Xbim.IO.Memory;
using Xunit;
using Xunit.Abstractions;

namespace IfcHopper.Tests
{
    public class PropertySetTests
    {
        private readonly ITestOutputHelper _output;

        public PropertySetTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Templates_AreReadFromAnnexA()
        {
            var wall = PropertyTemplates.Find("Pset_WallCommon");
            Assert.NotNull(wall);
            Assert.False(wall.IsQuantitySet);
            Assert.Contains("IfcWall", wall.ApplicableClasses);
            Assert.Equal("IfcBoolean", wall.Find("IsExternal").ValueType);
            Assert.Equal(PropertyKind.Enumerated, wall.Find("Status").Kind);
            Assert.Contains("NEW", wall.Find("Status").EnumItems);

            var quantities = PropertyTemplates.Find("Qto_WallBaseQuantities");
            Assert.True(quantities.IsQuantitySet);
            Assert.Equal(QuantityKind.Length, quantities.Find("Length").QuantityKind);
            Assert.Equal(QuantityKind.Weight, quantities.Find("GrossWeight").QuantityKind);
            Assert.True(PropertyTemplates.Names.Count() > 700);
            Assert.Null(PropertyTemplates.Find("My_Set"));
        }

        [Fact]
        public void Add_ResolvesValueTypes()
        {
            var set = new PropertySet("Pset_WallCommon");
            Assert.Equal("IfcBoolean", set.Add("IsExternal", "true").ValueType);
            Assert.Equal(true, set["IsExternal"].Value);

            var status = set.Add("Status", "new");
            Assert.Equal(PropertyKind.Enumerated, status.Kind);
            Assert.Equal(new object[] { "NEW" }, (IEnumerable<object>)status.Value);

            Assert.Equal("IfcThermalTransmittanceMeasure", set.Add("ThermalTransmittance", 0.3).ValueType);
            Assert.Equal("IfcInteger", set.Add("Custom count", 3).ValueType);
            Assert.Equal(3L, set["Custom count"].Value);
            Assert.Equal("IfcReal", set.Add("Custom number", 2.5).ValueType);
            Assert.Equal("IfcLabel", set.Add("Custom text", "abc").ValueType);
            Assert.Equal("IfcPositiveLengthMeasure", set.Add("Custom length", "0.2", "ifcpositivelengthmeasure").ValueType);
            Assert.Equal(0.2, set["Custom length"].Value);

            Assert.Throws<ArgumentException>(() => set.Add("IsExternal", "maybe"));
            Assert.Throws<ArgumentException>(() => set.Add("X", 1, "IfcNotAType"));
            Assert.Equal(7, set.Properties.Count);

            var warnings = PropertyTemplates.Check(set).ToList();
            Assert.Contains(warnings, w => w.Contains("'Custom count' is not a property"));
            Assert.DoesNotContain(warnings, w => w.Contains("IsExternal"));
        }

        [Fact]
        public void QuantityKinds_ComeFromTheTemplateOrTheCaller()
        {
            var set = new QuantitySet("Qto_WallBaseQuantities");
            Assert.Equal(QuantityKind.Area, set.Add("NetSideArea", 12).Kind);
            Assert.Equal(QuantityKind.Count, set.Add("Custom", 2, QuantityKind.Count).Kind);
            Assert.Throws<ArgumentException>(() => set.Add("Unknown", 1));
        }

        [Fact]
        public void Merge_ReplacesByNameAndRemovesEmptySets()
        {
            var element = new Element();
            element.PropertySets.Add(new PropertySet("A"));
            ((PropertySet)element.PropertySets[0]).Add("X", 1);
            element.PropertySets.Add(new QuantitySet("Q"));
            ((QuantitySet)element.PropertySets[1]).Add("L", 1, QuantityKind.Length);

            var replacement = new PropertySet("A");
            replacement.Add("Y", 2);
            var added = new PropertySet("B");
            added.Add("Z", 3);
            element.MergePropertySets(new PropertySetDefinition[] { replacement, new QuantitySet("Q"), added });

            Assert.Equal(new[] { "A", "B" }, element.PropertySets.Select(s => s.Name));
            Assert.Same(replacement, element.PropertySets[0]);
        }

        [Fact]
        public void PropertySets_AreWrittenAndReadInFileUnits()
        {
            var path = WriteSample();

            using (var ifc = MemoryModel.OpenRead(path))
            {
                var wall = ifc.Instances.OfType<IfcWall>().Single();
                var sets = wall.IsDefinedBy.Select(r => r.RelatingPropertyDefinition).OfType<IfcPropertySetDefinition>().ToDictionary(s => (string)s.Name);
                var custom = (IfcPropertySet)sets["My_Set"];
                var width = (IfcPropertySingleValue)custom.HasProperties.Single(p => p.Name == "Width");
                Assert.IsType<IfcPositiveLengthMeasure>(width.NominalValue);
                Assert.Equal(200.0, (double)((IfcPositiveLengthMeasure)width.NominalValue).Value, 9);
                var length = ((IfcElementQuantity)sets["Qto_WallBaseQuantities"]).Quantities.OfType<IIfcQuantityLength>().Single();
                Assert.Equal(5000.0, (double)length.LengthValue.Value, 9);
            }

            var model = IfcBackend.Reader.Read(path);
            Assert.Equal(new[] { "Project info" }, model.Project.PropertySets.Select(s => s.Name));
            var wallRead = Storey(model).Elements.Single();
            Assert.Equal(new[] { "Pset_WallCommon", "My_Set", "Qto_WallBaseQuantities" }, wallRead.PropertySets.Select(s => s.Name));

            var common = (PropertySet)wallRead.PropertySets[0];
            Assert.Equal(true, common["IsExternal"].Value);
            Assert.Equal(new object[] { "NEW" }, (IEnumerable<object>)common["Status"].Value);
            Assert.Equal(PropertyKind.Enumerated, common["Status"].Kind);

            var mySet = (PropertySet)wallRead.PropertySets[1];
            Assert.Equal(0.2, (double)mySet["Width"].Value, 9);
            Assert.Equal(3L, mySet["Count"].Value);
            Assert.Equal("abc", mySet["Text"].Value);
            Assert.Equal("Custom values", mySet.Description);

            var quantities = (QuantitySet)wallRead.PropertySets[2];
            Assert.Equal(5.0, quantities["Length"].Value, 9);
            Assert.Equal(12.0, quantities["NetSideArea"].Value, 9);
            Assert.Equal(QuantityKind.Area, quantities["NetSideArea"].Kind);
            Assert.Equal("BaseQuantities", quantities.MethodOfMeasurement);
            Assert.All(wallRead.PropertySets, s => Assert.False(s.FromType));
        }

        [Fact]
        public void SetsOfTheType_AreInheritedUnlessOverridden()
        {
            var path = WithType(WriteSample());
            var wall = Storey(IfcBackend.Reader.Read(path)).Elements.Single();

            var names = wall.PropertySets.Select(s => (s.Name, s.FromType)).ToList();
            Assert.Contains(("Pset_WallCommon", false), names);
            Assert.Contains(("Type only", true), names);
            Assert.Single(names, n => n.Name == "Pset_WallCommon");
            Assert.Equal("Type value", ((PropertySet)wall.PropertySets.Single(s => s.Name == "Type only"))["Note"].Value);
        }

        [Fact]
        public void InPlace_EditAddAndDeleteSets()
        {
            var source = WriteSample();
            var model = IfcBackend.Reader.Read(source);
            var wall = Storey(model).Elements.Single();

            var common = new PropertySet("Pset_WallCommon");
            common.Add("IsExternal", false);
            common.Add("Status", "NEW");
            var added = new PropertySet("Added");
            added.Add("A", "b");
            wall.MergePropertySets(new PropertySetDefinition[] { common, new PropertySet("My_Set"), added });

            var path = TestFiles.NewPath("edited.ifc");
            IfcBackend.Writer.Write(model, path);

            var read = Storey(IfcBackend.Reader.Read(path)).Elements.Single();
            Assert.Equal(new[] { "Pset_WallCommon", "Qto_WallBaseQuantities", "Added" }, read.PropertySets.Select(s => s.Name));
            Assert.Equal(false, ((PropertySet)read.PropertySets[0])["IsExternal"].Value);
            Assert.Equal(5.0, ((QuantitySet)read.PropertySets[1])["Length"].Value, 9);

            using (var ifc = MemoryModel.OpenRead(path))
            {
                // The edited set keeps its entity; the deleted one leaves no orphans.
                var originalId = (string)((IIfcRoot)Storey(IfcBackend.Reader.Read(source)).Elements.Single().PropertySets[0].Source).GlobalId;
                Assert.Contains(ifc.Instances.OfType<IfcPropertySet>(), s => s.GlobalId == originalId);
                Assert.DoesNotContain(ifc.Instances.OfType<IfcPropertySet>(), s => s.Name == "My_Set");
                Assert.DoesNotContain(ifc.Instances.OfType<IfcPropertySingleValue>(), p => p.Name == "Text");
                Assert.All(ifc.Instances.OfType<IfcRelDefinesByProperties>(), r => Assert.NotEmpty(r.RelatedObjects));
            }
        }

        [Fact]
        public void InPlace_LoadedButUnchangedSetsAreKept()
        {
            var source = WriteSample();
            var model = IfcBackend.Reader.Read(source);
            _ = model.Project.PropertySets;
            _ = Storey(model).Elements.Single().PropertySets;

            var path = TestFiles.NewPath("unchanged.ifc");
            IfcBackend.Writer.Write(model, path);

            long Count(string file)
            {
                using (var ifc = MemoryModel.OpenRead(file)) return ifc.Instances.Count;
            }
            Assert.Equal(Count(source), Count(path));
        }

        [Fact]
        public void InPlace_ProjectSetsAndNewElementsAreWritten()
        {
            var model = IfcBackend.Reader.Read(WriteSample());
            var info = new PropertySet("Project info");
            info.Add("Phase", "Construction");
            model.Project.MergePropertySets(new[] { info });

            var column = new Element("Column", "IfcColumn");
            var quantities = new QuantitySet("Qto_ColumnBaseQuantities");
            quantities.Add("Length", 3);
            column.PropertySets.Add(quantities);
            Storey(model).Elements.Add(column);

            var path = TestFiles.NewPath("added.ifc");
            IfcBackend.Writer.Write(model, path);

            var read = IfcBackend.Reader.Read(path);
            Assert.Equal("Construction", ((PropertySet)read.Project.PropertySets.Single())["Phase"].Value);
            var readColumn = Storey(read).Elements.Single(e => e.Name == "Column");
            Assert.Equal(3.0, ((QuantitySet)readColumn.PropertySets.Single())["Length"].Value, 9);
        }

        [Fact]
        public void InPlace_SharedSetIsCopiedForTheEditedObject()
        {
            var source = WriteSample(shareWithSlab: true);
            var model = IfcBackend.Reader.Read(source);
            var storey = Storey(model);
            var wall = storey.Elements.Single(e => e.Name == "Wall");
            var shared = (PropertySet)wall.PropertySets.Single(s => s.Name == "Shared");
            Assert.Same(shared, storey.Elements.Single(e => e.Name == "Slab").PropertySets.Single(s => s.Name == "Shared"));

            var edited = new PropertySet("Shared");
            edited.Add("Code", "W-2");
            wall.MergePropertySets(new[] { edited });
            var path = TestFiles.NewPath("shared.ifc");
            IfcBackend.Writer.Write(model, path);

            var read = Storey(IfcBackend.Reader.Read(path)).Elements.ToDictionary(e => e.Name);
            Assert.Equal("W-2", ((PropertySet)read["Wall"].PropertySets.Single(s => s.Name == "Shared"))["Code"].Value);
            Assert.Equal("S-1", ((PropertySet)read["Slab"].PropertySets.Single(s => s.Name == "Shared"))["Code"].Value);
        }

        [SampleModelFact("IFC Schependomlaan.ifc")]
        public void SchependomlaanSample_PropertySetsAreRead()
        {
            var project = IfcBackend.Reader.Read(TestFiles.Sample("IFC Schependomlaan.ifc")).Project;
            var elements = Descendants(project).OfType<Element>().ToList();
            var sets = elements.SelectMany(e => e.PropertySets).ToList();
            foreach (var group in sets.GroupBy(s => (s.Name, s.FromType)).OrderByDescending(g => g.Count()).Take(10))
                _output.WriteLine($"{group.Key.Name}{(group.Key.FromType ? " (type)" : "")}: {group.Count()}");

            Assert.NotEmpty(sets);
            Assert.Contains(sets.OfType<PropertySet>().SelectMany(s => s.Properties), p => p.Value != null);
        }

        [Theory]
        [InlineData(IfcSchema.Ifc4x3)]
        [InlineData(IfcSchema.Ifc2x3)]
        public void ListBoundedTableAndComplexProperties_AreWrittenAndRead(IfcSchema schema)
        {
            var set = new PropertySet("My_Kinds");
            Assert.Equal(PropertyKind.List, set.Add("Widths", "0.1; 0.2", "IfcPositiveLengthMeasure", PropertyKind.List).Kind);
            set.Add("Range", new BoundedValue(0.5, 1.5, 1.0), "IfcLengthMeasure");

            set.Add("Loads", "1: 10; 2: 20", "IfcReal", PropertyKind.Table);
            var inner = new PropertySet("Dimensions");
            inner.Add("Depth", 0.3, "IfcLengthMeasure");
            inner.Add("Note", "deep");
            Assert.Equal("Dimensions", set.Add("Nested", inner).UsageName);
            var wall = new Element("Wall", "IfcWall");
            wall.PropertySets.Add(set);
            var path = TestFiles.NewPath();

            var warnings = IfcBackend.Writer.Write(Model(wall), path, schema);

            var read = (PropertySet)Assert.Single(ReadElement(path).PropertySets);
            Assert.Equal(PropertyKind.List, read["Widths"].Kind);
            Assert.Equal(new object[] { 0.1, 0.2 }, ((IEnumerable<object>)read["Widths"].Value).Select(v => Math.Round((double)v, 9)).Cast<object>());
            var range = Assert.IsType<BoundedValue>(read["Range"].Value);
            Assert.Equal(0.5, (double)range.Lower, 9);
            Assert.Equal(1.5, (double)range.Upper, 9);
            var table = Assert.IsType<TableValue>(read["Loads"].Value);
            Assert.Equal(new object[] { 10.0, 20.0 }, table.DefinedValues);
            Assert.Equal("IfcReal", table.DefiningValueType);
            var nested = Assert.IsAssignableFrom<IEnumerable<Property>>(read["Nested"].Value).ToList();
            Assert.Equal("Dimensions", read["Nested"].UsageName);
            Assert.Equal(0.3, (double)nested.Single(p => p.Name == "Depth").Value, 9);
            Assert.Equal("deep", nested.Single(p => p.Name == "Note").Value);
            if (schema == IfcSchema.Ifc2x3)
            {
                Assert.Contains(warnings, w => w.Contains("Set points"));
                Assert.Null(range.SetPoint);
            }
            else Assert.Equal(1.0, (double)range.SetPoint, 9);
        }

        [Fact]
        public void TextGivesBoundedAndTableValues_AndTemplatesKeepPlainValuesSingle()
        {
            var set = new PropertySet("My_Kinds");
            var bounded = Assert.IsType<BoundedValue>(set.Add("Range", "1 .. 2", "IfcReal", PropertyKind.Bounded).Value);
            Assert.Equal(1.0, bounded.Lower);
            Assert.Equal(PropertyKind.Single, set.Add("Plain", "1 .. 2").Kind);
            Assert.Throws<ArgumentException>(() => set.Add("Ref", "x", null, PropertyKind.Reference));
            var common = new PropertySet("Pset_WallCommon");
            Assert.Equal(PropertyKind.Single, common.Add("Status", "NEW", "IfcLabel").Kind);
            Assert.Equal(new object[] { "NEW", "EXISTING" }, (IEnumerable<object>)common.Add("Status", "new; existing").Value);
        }

        private static IfcHopperModel Model(Element element)
        {
            var storey = new Storey();
            storey.Elements.Add(element);
            var building = new Building();
            building.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project { Units = new Units { Length = Units.Find(UnitKind.Length, "mm") } };
            project.Sites.Add(site);
            return new IfcHopperModel(project);
        }

        private static Element ReadElement(string path) => Storey(IfcBackend.Reader.Read(path)).Elements.Single();

        private static IEnumerable<ModelObject> Descendants(ModelObject parent) => parent.Children.SelectMany(c => new[] { c }.Concat(Descendants(c)));

        private static Storey Storey(IfcHopperModel model) => ((Building)model.Project.Sites[0].Facilities[0]).Storeys[0];

        /// <summary>
        /// A millimetre file: a project with "Project info" and a wall with Pset_WallCommon, My_Set (200 mm width, count, text) and
        /// Qto_WallBaseQuantities (5 m, 12 m²). With <paramref name="shareWithSlab"/> a slab is added and a "Shared" set relates to both.
        /// </summary>
        private static string WriteSample(bool shareWithSlab = false)
        {
            var wall = new Element("Wall", "IfcWall");
            var common = new PropertySet("Pset_WallCommon");
            common.Add("IsExternal", true);
            common.Add("Status", "new");
            var custom = new PropertySet("My_Set") { Description = "Custom values" };
            custom.Add("Width", 0.2, "IfcPositiveLengthMeasure");
            custom.Add("Count", 3);
            custom.Add("Text", "abc");
            var quantities = new QuantitySet("Qto_WallBaseQuantities") { MethodOfMeasurement = "BaseQuantities" };
            quantities.Add("Length", 5);
            quantities.Add("NetSideArea", 12);
            wall.PropertySets.AddRange(new PropertySetDefinition[] { common, custom, quantities });

            var storey = new Storey();
            storey.Elements.Add(wall);
            if (shareWithSlab) storey.Elements.Add(new Element("Slab", "IfcSlab"));
            var building = new Building();
            building.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project { Units = new Units { Length = Units.Find(UnitKind.Length, "mm") } };
            var info = new PropertySet("Project info");
            info.Add("Phase", "Design");
            project.PropertySets.Add(info);
            project.Sites.Add(site);

            var path = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(project), path);
            if (!shareWithSlab) return path;

            var sharedPath = TestFiles.NewPath();
            using (var ifc = MemoryModel.OpenRead(path))
            {
                using (var txn = ifc.BeginTransaction("shared"))
                {
                    var set = ifc.Instances.New<IfcPropertySet>(s =>
                    {
                        s.GlobalId = GlobalIds.New();
                        s.Name = "Shared";
                        s.HasProperties.Add(ifc.Instances.New<IfcPropertySingleValue>(p =>
                        {
                            p.Name = "Code";
                            p.NominalValue = new IfcLabel("S-1");
                        }));
                    });
                    ifc.Instances.New<IfcRelDefinesByProperties>(r =>
                    {
                        r.GlobalId = GlobalIds.New();
                        r.RelatingPropertyDefinition = set;
                        r.RelatedObjects.AddRange(ifc.Instances.OfType<IfcElement>());
                    });
                    txn.Commit();
                }
                using (var stream = File.Create(sharedPath)) ifc.SaveAsStep21(stream);
            }
            return sharedPath;
        }

        /// <summary>Adds a wall type with its own Pset_WallCommon and a "Type only" set.</summary>
        private static string WithType(string path)
        {
            var typed = TestFiles.NewPath();
            using (var ifc = MemoryModel.OpenRead(path))
            {
                using (var txn = ifc.BeginTransaction("type"))
                {
                    IfcPropertySet Set(string name, string property, string value) => ifc.Instances.New<IfcPropertySet>(s =>
                    {
                        s.GlobalId = GlobalIds.New();
                        s.Name = name;
                        s.HasProperties.Add(ifc.Instances.New<IfcPropertySingleValue>(p =>
                        {
                            p.Name = property;
                            p.NominalValue = new IfcLabel(value);
                        }));
                    });
                    var type = ifc.Instances.New<IfcWallType>(t =>
                    {
                        t.GlobalId = GlobalIds.New();
                        t.Name = "Wall type";
                        t.PredefinedType = Xbim.Ifc4x3.SharedBldgElements.IfcWallTypeEnum.SOLIDWALL;
                        t.HasPropertySets.Add(Set("Pset_WallCommon", "Reference", "T1"));
                        t.HasPropertySets.Add(Set("Type only", "Note", "Type value"));
                    });
                    ifc.Instances.New<IfcRelDefinesByType>(r =>
                    {
                        r.GlobalId = GlobalIds.New();
                        r.RelatingType = type;
                        r.RelatedObjects.Add(ifc.Instances.OfType<IfcWall>().Single());
                    });
                    txn.Commit();
                }
                using (var stream = File.Create(typed)) ifc.SaveAsStep21(stream);
            }
            return typed;
        }
    }
}
