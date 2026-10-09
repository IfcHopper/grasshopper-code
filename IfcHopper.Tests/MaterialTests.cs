using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Common;
using Xbim.Ifc4x3.Kernel;
using Xbim.Ifc4x3.MaterialResource;
using Xbim.Ifc4x3.MeasureResource;
using Xbim.Ifc4x3.PresentationAppearanceResource;
using Xbim.Ifc4x3.ProductExtension;
using Xbim.Ifc4x3.RepresentationResource;
using Xbim.Ifc4x3.SharedBldgElements;
using Xbim.IO.Memory;
using Xunit;
using Xunit.Abstractions;

namespace IfcHopper.Tests
{
    public class MaterialTests
    {
        private readonly ITestOutputHelper _output;

        public MaterialTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void MaterialKinds_AreReadInMetresWithColours()
        {
            var elements = ReadElements(WriteMaterialSample());

            var wall = Assert.IsType<MaterialLayerSet>(elements["Wall"].Material);
            Assert.False(elements["Wall"].MaterialFromType);
            Assert.Equal("Cavity wall", wall.Name);
            Assert.Equal(new[] { "Brick", "Insulation", "Brick" }, wall.Layers.Select(l => l.Material.Name));
            Assert.Equal(new[] { 0.1, 0.08, 0.1 }, wall.Layers.Select(l => l.Thickness));
            Assert.Equal(0.28, wall.TotalThickness, 9);
            Assert.Same(wall.Layers[0].Material, wall.Layers[2].Material);
            Assert.Same(wall, elements["Wall 2"].Material);

            var column = Assert.IsType<MaterialConstituentSet>(elements["Column"].Material);
            Assert.Equal(new[] { "Core", "Cover" }, column.Constituents.Select(c => c.Name));
            Assert.Equal(new double?[] { 0.9, null }, column.Constituents.Select(c => c.Fraction));
            Assert.Equal("Concrete", column.Constituents[0].Material.Name);
            Assert.Equal("Structural", column.Constituents[0].Material.Category);

            var slab = Assert.IsType<Material>(elements["Slab"].Material);
            Assert.Equal("Concrete", slab.Name);
            Assert.Same(slab, column.Constituents[0].Material);
            Assert.Null(elements["Plain"].Material);
        }

        [Fact]
        public void MaterialOfType_IsUsedWhenTheElementHasNone()
        {
            var beam = ReadElements(WriteMaterialSample())["Beam"];
            Assert.True(beam.MaterialFromType);
            Assert.Equal("Steel", Assert.IsType<Material>(beam.Material).Name);
        }

        [Fact]
        public void DisplayColour_PrefersTheGeometryStyleOverTheMaterial()
        {
            var elements = ReadElements(WriteMaterialSample());

            // Wall: first layer material colour; Slab: its own red style over the blue material.
            Assert.Equal(new Colour(0.6, 0.2, 0.1), elements["Wall"].DisplayColour(Assert.Single(elements["Wall"].Geometry)));
            Assert.Equal(new Colour(1, 0, 0), elements["Slab"].DisplayColour(Assert.Single(elements["Slab"].Geometry)));
            Assert.Equal(new Colour(0, 0, 1), elements["Slab"].Material.DisplayColour);
            Assert.Null(elements["Plain"].DisplayColour(Assert.Single(elements["Plain"].Geometry)));
        }

        [SampleModelFact("IFC Schependomlaan.ifc")]
        public void SchependomlaanSample_MaterialsAreRead()
        {
            var project = IfcBackend.Reader.Read(TestFiles.Sample("IFC Schependomlaan.ifc")).Project;
            var elements = Descendants(project).OfType<Element>().ToList();
            foreach (var group in elements.GroupBy(e => e.Material?.IfcClass ?? "none")) _output.WriteLine($"{group.Key}: {group.Count()}");
            var meshes = elements.SelectMany(e => e.Geometry.Select(m => (Element: e, Mesh: m))).ToList();
            _output.WriteLine($"{meshes.Count} meshes, {meshes.Count(m => m.Mesh.Colour == null)} without own style, " +
                $"{meshes.Count(m => m.Element.DisplayColour(m.Mesh) == null)} without display colour");

            Assert.Contains(elements, e => e.Material is MaterialLayerSet set && set.TotalThickness > 0);
            Assert.All(meshes.Where(m => m.Element.Material?.DisplayColour != null), m => Assert.NotNull(m.Element.DisplayColour(m.Mesh)));
            Assert.True(meshes.Count(m => m.Element.DisplayColour(m.Mesh) == null) < meshes.Count(m => m.Mesh.Colour == null) / 10);
        }

        [Theory]
        [InlineData(IfcSchema.Ifc4x3)]
        [InlineData(IfcSchema.Ifc4)]
        [InlineData(IfcSchema.Ifc2x3)]
        public void WrittenMaterials_AreReadBackAndShared(IfcSchema schema)
        {
            var brick = new Material("Brick") { Colour = new Colour(0.6, 0.2, 0.1) };
            var walls = new MaterialLayerSet("Cavity wall");
            walls.Layers.Add(new MaterialLayer { Material = brick, Thickness = 0.1, Name = "Outer" });
            walls.Layers.Add(new MaterialLayer { Material = new Material("Insulation"), Thickness = 0.08 });
            walls.Layers.Add(new MaterialLayer { Material = brick, Thickness = 0.1 });
            var floor = new MaterialLayerSet("Floor");
            floor.Layers.Add(new MaterialLayer { Material = new Material("Screed"), Thickness = 0.05 });
            var column = new MaterialConstituentSet("Column");
            column.Constituents.Add(new MaterialConstituent { Name = "Core", Material = new Material("Steel"), Fraction = 0.9 });
            var steel = new Material("Steel");
            var beamType = new ElementType("Beam type", "IfcBeamType");
            beamType.SetMaterial(steel);

            Element Make(string name, string ifcClass, MaterialDefinition material)
            {
                var element = new Element(name, ifcClass);
                element.Geometry.Add(GeometryTests.Cube());
                element.SetMaterial(material);
                return element;
            }
            var beam = Make("Beam", "IfcBeam", null);
            beam.AssignType(beamType);
            var storey = new Storey();
            storey.Elements.AddRange(new[]
            {
                Make("Wall", "IfcWall", walls), Make("Wall 2", "IfcWall", walls), Make("Floor", "IfcSlab", floor), Make("Column", "IfcColumn", column),
                Make("Slab", "IfcSlab", new Material("Concrete") { Category = "Structural", Colour = new Colour(0, 0, 1) }),
                Make("Slab 2", "IfcSlab", new Material("Concrete") { Category = "Structural", Colour = new Colour(0, 0, 1) }),
                beam, Make("Plain", "IfcWall", null),
            });
            var building = new Building();
            building.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project();
            project.Sites.Add(site);
            var path = TestFiles.NewPath();

            var warnings = IfcBackend.Writer.Write(new IfcHopperModel(project), path, schema);

            foreach (var warning in warnings) _output.WriteLine(warning);
            using (var ifc = MemoryModel.OpenRead(path))
            {
                // Brick, Insulation, Screed, Steel (constituent and type: same values), Concrete (both slabs).
                Assert.Equal(5, ifc.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcMaterial>().Count());
                Assert.Equal(2, ifc.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcMaterialLayerSet>().Count());
                var usages = ifc.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcMaterialLayerSetUsage>().ToDictionary(u => (string)u.ForLayerSet.LayerSetName);
                Assert.Equal(Xbim.Ifc4.Interfaces.IfcLayerSetDirectionEnum.AXIS2, usages["Cavity wall"].LayerSetDirection);
                Assert.Equal(Xbim.Ifc4.Interfaces.IfcLayerSetDirectionEnum.AXIS3, usages["Floor"].LayerSetDirection);
            }

            var elements = ReadElements(path);
            var wall = Assert.IsType<MaterialLayerSet>(elements["Wall"].Material);
            Assert.Same(wall, elements["Wall 2"].Material);
            Assert.Equal(new[] { "Brick", "Insulation", "Brick" }, wall.Layers.Select(l => l.Material.Name));
            Assert.Equal(new[] { 0.1, 0.08, 0.1 }, wall.Layers.Select(l => l.Thickness).Select(t => Math.Round(t, 9)));
            Assert.Same(wall.Layers[0].Material, wall.Layers[2].Material);
            Assert.Equal(new Colour(0.6, 0.2, 0.1), wall.Layers[0].Material.Colour);
            Assert.Same(elements["Slab"].Material, elements["Slab 2"].Material);
            Assert.Equal(new Colour(0, 0, 1), elements["Slab"].Material.DisplayColour);
            Assert.True(elements["Beam"].MaterialFromType);
            Assert.Equal("Steel", elements["Beam"].Material.Name);
            Assert.Null(elements["Plain"].Material);
            var constituents = Assert.IsType<MaterialConstituentSet>(elements["Column"].Material);
            Assert.Equal("Steel", Assert.Single(constituents.Constituents).Material.Name);
            if (schema == IfcSchema.Ifc2x3) return;

            Assert.Equal("Outer", wall.Layers[0].Name);
            Assert.Equal("Structural", ((Material)elements["Slab"].Material).Category);
            Assert.Equal(0.9, constituents.Constituents[0].Fraction);
            Assert.Equal("Core", constituents.Constituents[0].Name);
        }

        [Fact]
        public void EditedMaterial_IsWrittenOntoTheFileMaterialForEveryElement()
        {
            var model = IfcBackend.Reader.Read(WriteMaterialSample());
            var brick = (Material)((MaterialLayerSet)ReadElements(model)["Wall"].Material).Layers[0].Material.Copy();
            brick.Name = "Clinker";
            brick.Colour = new Colour(0.3, 0.1, 0.1);
            var path = TestFiles.NewPath();

            IfcBackend.Writer.Write(ModelEdits.ApplyMaterials(model, new[] { brick }, out var notFound), path);

            Assert.Empty(notFound);
            var elements = ReadElements(path);
            foreach (var name in new[] { "Wall", "Wall 2" })
            {
                var layers = ((MaterialLayerSet)elements[name].Material).Layers;
                Assert.Equal(new[] { "Clinker", "Insulation", "Clinker" }, layers.Select(l => l.Material.Name));
                Assert.Equal(new Colour(0.3, 0.1, 0.1), layers[0].Material.Colour);
            }
            using (var ifc = MemoryModel.OpenRead(path))
                Assert.DoesNotContain(ifc.Instances.OfType<IfcMaterial>(), m => m.Name == "Brick");
        }

        [Fact]
        public void MaterialOfReadElements_IsReassignedRemovedAndEditedInPlace()
        {
            var model = IfcBackend.Reader.Read(WriteMaterialSample());
            var read = ReadElements(model);
            var plain = (Element)read["Plain"].Copy();
            plain.SetMaterial(read["Slab"].Material);
            var column = (Element)read["Column"].Copy();
            column.SetMaterial(null);
            var beam = (Element)read["Beam"].Copy();
            beam.SetMaterial(new Material("Aluminium"));
            var slab = (Element)read["Slab"].Copy();
            var precast = (Material)slab.Material.Copy();
            precast.Category = "Precast";
            slab.SetMaterial(precast);
            var path = TestFiles.NewPath();

            IfcBackend.Writer.Write(ModelEdits.Apply(model, new[] { plain, column, beam, slab }, out _), path);

            using (var ifc = MemoryModel.OpenRead(path))
            {
                Assert.Single(ifc.Instances.OfType<IfcMaterial>(), m => m.Name == "Concrete");
                Assert.DoesNotContain(ifc.Instances.OfType<IfcRelAssociatesMaterial>(), r => r.RelatingMaterial is IfcMaterialConstituentSet);
            }
            var elements = ReadElements(path);
            Assert.Same(elements["Slab"].Material, elements["Plain"].Material);
            Assert.Equal("Precast", ((Material)elements["Plain"].Material).Category);
            Assert.Null(elements["Column"].Material);
            Assert.False(elements["Beam"].MaterialFromType);
            Assert.Equal("Aluminium", elements["Beam"].Material.Name);
            Assert.Equal("Cavity wall", elements["Wall"].Material.Name);
        }

        [Theory]
        [InlineData(IfcSchema.Ifc4x3)]
        [InlineData(IfcSchema.Ifc2x3)]
        public void MaterialPropertySets_AreWrittenAndDecideReuse(IfcSchema schema)
        {
            Material Concrete(double density)
            {
                var material = new Material("Concrete");
                var set = new PropertySet("Pset_MaterialCommon");
                set.Add("MassDensity", density);
                material.PropertySets.Add(set);
                return material;
            }
            var storey = new Storey();
            foreach (var (name, density) in new[] { ("A", 2400.0), ("B", 2400.0), ("C", 2500.0) })
            {
                var element = new Element(name, "IfcSlab");
                element.SetMaterial(Concrete(density));
                storey.Elements.Add(element);
            }
            var building = new Building();
            building.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project();
            project.Sites.Add(site);
            var path = TestFiles.NewPath();

            var warnings = IfcBackend.Writer.Write(new IfcHopperModel(project), path, schema);

            var elements = ReadElements(path);
            if (schema == IfcSchema.Ifc2x3)
            {
                Assert.Contains(warnings, w => w.Contains("Material property sets"));
                Assert.Empty(elements["A"].Material.PropertySets);
                return;
            }
            Assert.Same(elements["A"].Material, elements["B"].Material);
            Assert.NotSame(elements["A"].Material, elements["C"].Material);
            var set = Assert.Single(elements["C"].Material.PropertySets);
            Assert.Equal("Pset_MaterialCommon", set.Name);
            Assert.Equal("IfcMassDensityMeasure", set["MassDensity"].ValueType);
            Assert.Equal(2500.0, set["MassDensity"].Value);
        }

        [Fact]
        public void MaterialPropertySets_AreAddedAndRemovedInPlace()
        {
            var model = IfcBackend.Reader.Read(WriteMaterialSample());
            var concrete = ReadElements(model)["Slab"].Material.Copy();
            var set = new PropertySet("Pset_MaterialCommon");
            set.Add("Porosity", 0.1);
            concrete.MergePropertySets(new[] { set });
            var added = TestFiles.NewPath();
            IfcBackend.Writer.Write(ModelEdits.ApplyMaterials(model, new[] { concrete }, out _), added);

            var read = ReadElements(added);
            Assert.Equal(0.1, Assert.Single(read["Column"].Material.Materials.First().PropertySets)["Porosity"].Value);

            var model2 = IfcBackend.Reader.Read(added);
            var cleared = ReadElements(model2)["Slab"].Material.Copy();
            cleared.MergePropertySets(new[] { new PropertySet("Pset_MaterialCommon") });
            var removed = TestFiles.NewPath();
            IfcBackend.Writer.Write(ModelEdits.ApplyMaterials(model2, new[] { cleared }, out _), removed);

            Assert.Empty(ReadElements(removed)["Slab"].Material.PropertySets);
            using (var ifc = MemoryModel.OpenRead(removed))
            {
                Assert.Empty(ifc.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcMaterialProperties>());
                Assert.Empty(ifc.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcPropertySingleValue>().Where(p => p.Name == "Porosity"));
            }
        }

        private static Dictionary<string, Element> ReadElements(IfcHopperModel model) =>
            ((Building)model.Project.Sites[0].Facilities[0]).Storeys[0].Elements.ToDictionary(e => e.Name);

        private static IEnumerable<ModelObject> Descendants(ModelObject parent) => parent.Children.SelectMany(c => new[] { c }.Concat(Descendants(c)));

        private static Dictionary<string, Element> ReadElements(string path) =>
            ((Building)IfcBackend.Reader.Read(path).Project.Sites[0].Facilities[0]).Storeys[0].Elements.ToDictionary(e => e.Name);

        /// <summary>
        /// A millimetre file with elements Wall and Wall 2 (one layer set through two usages), Column (constituent set),
        /// Slab (blue material, red body style), Beam (material on its type only) and Plain (no material).
        /// </summary>
        private static string WriteMaterialSample()
        {
            var storey = new Storey();
            foreach (var (name, ifcClass) in new[] { ("Wall", "IfcWall"), ("Wall 2", "IfcWall"), ("Column", "IfcColumn"), ("Slab", "IfcSlab"), ("Beam", "IfcBeam"), ("Plain", "IfcWall") })
            {
                var element = new Element(name, ifcClass);
                element.Geometry.Add(name == "Slab" ? GeometryTests.Cube().WithColour(new Colour(1, 0, 0)) : GeometryTests.Cube());
                storey.Elements.Add(element);
            }
            var building = new Building();
            building.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project { Units = new Units { Length = Units.Find(UnitKind.Length, "mm") } };
            project.Sites.Add(site);
            var source = TestFiles.NewPath();
            IfcBackend.Writer.Write(new IfcHopperModel(project), source);

            var path = TestFiles.NewPath();
            using (var ifc = MemoryModel.OpenRead(source))
            {
                using (var txn = ifc.BeginTransaction("materials"))
                {
                    var byName = ifc.Instances.OfType<IfcElement>().ToDictionary(e => (string)e.Name);
                    var context = ifc.Instances.OfType<IfcGeometricRepresentationContext>().First();
                    var brick = Material(ifc, context, "Brick", null, 0.6, 0.2, 0.1);
                    var concrete = Material(ifc, context, "Concrete", "Structural", 0, 0, 1);

                    var layers = ifc.Instances.New<IfcMaterialLayerSet>(s =>
                    {
                        s.LayerSetName = "Cavity wall";
                        s.MaterialLayers.Add(Layer(ifc, brick, 100));
                        s.MaterialLayers.Add(Layer(ifc, Material(ifc, context, "Insulation", null), 80));
                        s.MaterialLayers.Add(Layer(ifc, brick, 100));
                    });
                    foreach (var wall in new[] { "Wall", "Wall 2" })
                        Associate(ifc, byName[wall], ifc.Instances.New<IfcMaterialLayerSetUsage>(u =>
                        {
                            u.ForLayerSet = layers;
                            u.LayerSetDirection = IfcLayerSetDirectionEnum.AXIS2;
                            u.DirectionSense = IfcDirectionSenseEnum.POSITIVE;
                            u.OffsetFromReferenceLine = 0;
                        }));

                    Associate(ifc, byName["Column"], ifc.Instances.New<IfcMaterialConstituentSet>(s =>
                    {
                        s.MaterialConstituents.Add(ifc.Instances.New<IfcMaterialConstituent>(c =>
                        {
                            c.Name = "Core";
                            c.Material = concrete;
                            c.Fraction = 0.9;
                        }));
                        s.MaterialConstituents.Add(ifc.Instances.New<IfcMaterialConstituent>(c =>
                        {
                            c.Name = "Cover";
                            c.Material = Material(ifc, context, "Render", null);
                        }));
                    }));
                    Associate(ifc, byName["Slab"], concrete);

                    var beamType = ifc.Instances.New<IfcBeamType>(t =>
                    {
                        t.GlobalId = GlobalIds.New();
                        t.Name = "Steel beam";
                        t.PredefinedType = IfcBeamTypeEnum.BEAM;
                    });
                    ifc.Instances.New<IfcRelDefinesByType>(r =>
                    {
                        r.GlobalId = GlobalIds.New();
                        r.RelatingType = beamType;
                        r.RelatedObjects.Add(byName["Beam"]);
                    });
                    Associate(ifc, beamType, Material(ifc, context, "Steel", null));
                    txn.Commit();
                }
                using (var stream = File.Create(path)) ifc.SaveAsStep21(stream);
            }
            return path;
        }

        /// <summary>A material, styled with a surface colour when <paramref name="rgb"/> is given.</summary>
        private static IfcMaterial Material(IModel ifc, IfcGeometricRepresentationContext context, string name, string category, params double[] rgb)
        {
            var material = ifc.Instances.New<IfcMaterial>(m =>
            {
                m.Name = name;
                if (category != null) m.Category = category;
            });
            if (rgb.Length == 0) return material;

            var style = ifc.Instances.New<IfcSurfaceStyle>(s =>
            {
                s.Side = IfcSurfaceSide.BOTH;
                s.Styles.Add(ifc.Instances.New<IfcSurfaceStyleShading>(sh => sh.SurfaceColour = ifc.Instances.New<IfcColourRgb>(c =>
                {
                    c.Red = rgb[0];
                    c.Green = rgb[1];
                    c.Blue = rgb[2];
                })));
            });
            ifc.Instances.New<IfcMaterialDefinitionRepresentation>(d =>
            {
                d.RepresentedMaterial = material;
                d.Representations.Add(ifc.Instances.New<IfcStyledRepresentation>(r =>
                {
                    r.ContextOfItems = context;
                    r.Items.Add(ifc.Instances.New<IfcStyledItem>(i => i.Styles.Add(style)));
                }));
            });
            return material;
        }

        private static IfcMaterialLayer Layer(IModel ifc, IfcMaterial material, double thickness) =>
            ifc.Instances.New<IfcMaterialLayer>(l =>
            {
                l.Material = material;
                l.LayerThickness = thickness;
            });

        private static void Associate(IModel ifc, IfcDefinitionSelect target, IfcMaterialSelect material) =>
            ifc.Instances.New<IfcRelAssociatesMaterial>(r =>
            {
                r.GlobalId = GlobalIds.New();
                r.RelatingMaterial = material;
                r.RelatedObjects.Add(target);
            });
    }
}
