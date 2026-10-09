using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.Model;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace IfcHopper.Core.Backends.Xbim
{
    /// <summary>
    /// Reads the material of elements and types (IfcRelAssociatesMaterial); elements fall back to the material of their type in the model.
    /// Each IFC material or set is converted once, so elements sharing it in the file share it in the model.
    /// </summary>
    internal class XbimMaterials
    {
        private readonly double _toMetres;
        private readonly XbimPropertySets _propertySets;
        private readonly Dictionary<IPersistEntity, MaterialDefinition> _converted = new Dictionary<IPersistEntity, MaterialDefinition>();

        public XbimMaterials(double toMetres, XbimPropertySets propertySets)
        {
            _toMetres = toMetres;
            _propertySets = propertySets;
        }

        /// <summary>The material associated with an object or type itself, or null.</summary>
        public MaterialDefinition Associated(IIfcObjectDefinition entity) =>
            entity?.HasAssociations.OfType<IIfcRelAssociatesMaterial>().Select(r => Definition(r.RelatingMaterial)).FirstOrDefault(m => m != null);

        /// <summary>
        /// The definition of a material select; usages give their set (the usage itself stays in the file) and
        /// a single layer, constituent or profile gives a set of one.
        /// </summary>
        private MaterialDefinition Definition(IIfcMaterialSelect select)
        {
            switch (select)
            {
                case IIfcMaterialLayerSetUsage usage: return Definition(usage.ForLayerSet);
                case IIfcMaterialProfileSetUsage usage: return Definition(usage.ForProfileSet);
                case IIfcMaterial material: return Material(material);
            }
            if (!(select is IPersistEntity entity)) return null;
            if (_converted.TryGetValue(entity, out var converted)) return converted;

            MaterialDefinition definition;
            switch (select)
            {
                case IIfcMaterialLayerSet set:
                    var layerSet = new MaterialLayerSet(set.LayerSetName) { Description = set.Description };
                    layerSet.Layers.AddRange(set.MaterialLayers.Select(Layer));
                    definition = layerSet;
                    break;
                case IIfcMaterialLayer layer:
                    var oneLayer = new MaterialLayerSet();
                    oneLayer.Layers.Add(Layer(layer));
                    definition = oneLayer;
                    break;
                case IIfcMaterialConstituentSet set:
                    var constituentSet = new MaterialConstituentSet(set.Name) { Description = set.Description };
                    constituentSet.Constituents.AddRange(set.MaterialConstituents.Select(Constituent));
                    definition = constituentSet;
                    break;
                case IIfcMaterialConstituent constituent:
                    var oneConstituent = new MaterialConstituentSet();
                    oneConstituent.Constituents.Add(Constituent(constituent));
                    definition = oneConstituent;
                    break;
                case IIfcMaterialList list:
                    var listSet = new MaterialConstituentSet();
                    listSet.Constituents.AddRange(list.Materials.Select(m => new MaterialConstituent { Material = Material(m) }));
                    definition = listSet;
                    break;
                case IIfcMaterialProfileSet set:
                    var profileSet = new MaterialProfileSet(set.Name) { Description = set.Description };
                    profileSet.Profiles.AddRange(set.MaterialProfiles.Select(Profile));
                    definition = profileSet;
                    break;
                case IIfcMaterialProfile profile:
                    var oneProfile = new MaterialProfileSet();
                    oneProfile.Profiles.Add(Profile(profile));
                    definition = oneProfile;
                    break;
                default:
                    return null;
            }
            definition.Source = entity;
            if (entity is IIfcMaterialDefinition withProperties) definition.PropertySets.AddRange(_propertySets.ReadMaterial(withProperties));
            return _converted[entity] = definition;
        }

        private MaterialLayer Layer(IIfcMaterialLayer layer) => new MaterialLayer
        {
            Material = Material(layer.Material),
            Thickness = XbimGeometryReader.Double(layer.LayerThickness) * _toMetres,
            Name = layer.Name,
            Category = layer.Category,
        };

        private MaterialConstituent Constituent(IIfcMaterialConstituent constituent) => new MaterialConstituent
        {
            Material = Material(constituent.Material),
            Name = constituent.Name,
            Category = constituent.Category,
            Fraction = constituent.Fraction.HasValue ? XbimGeometryReader.Double(constituent.Fraction.Value) : (double?)null,
        };

        private MaterialProfile Profile(IIfcMaterialProfile profile) => new MaterialProfile
        {
            Material = Material(profile.Material),
            Name = profile.Name,
            Category = profile.Category,
        };

        /// <summary>The material with the colour of its style (IfcMaterialDefinitionRepresentation); null for a missing material.</summary>
        private Material Material(IIfcMaterial entity)
        {
            if (entity == null) return null;
            if (_converted.TryGetValue(entity, out var converted)) return (Material)converted;

            var material = new Material(entity.Name)
            {
                Description = entity.Description,
                Category = entity.Category,
                Colour = ColourOf(entity),
                Source = entity,
            };
            material.PropertySets.AddRange(_propertySets.ReadMaterial(entity));
            _converted[entity] = material;
            return material;
        }

        /// <summary>Surface colour of a material's style (IfcMaterialDefinitionRepresentation), or null.</summary>
        internal static Colour ColourOf(IIfcMaterial entity) =>
            XbimGeometryReader.SurfaceColour(entity.HasRepresentation.SelectMany(r => r.Representations).OfType<IIfcStyledRepresentation>()
                .SelectMany(r => r.Items).OfType<IIfcStyledItem>());
    }
}
