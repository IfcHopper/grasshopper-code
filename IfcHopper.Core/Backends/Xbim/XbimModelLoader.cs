using System;
using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Common;
using Xbim.Ifc.Extensions;
using Xbim.Ifc4.Interfaces;

namespace IfcHopper.Core.Backends.Xbim
{
    /// <summary>
    /// Maps xBIM entities to the IfcHopper model. Each object is converted on its own and registers loaders
    /// for its children, so the spatial tree is only converted as far as it is accessed.
    /// Uses the IFC4 interfaces, implemented by IFC2X3, IFC4 and IFC4X3 entities alike.
    /// </summary>
    internal class XbimModelLoader
    {
        private readonly double _toMetres;
        private readonly XbimAlignments _alignments = new XbimAlignments();
        private readonly XbimMaterials _materials;
        private readonly XbimPropertySets _propertySets;
        private readonly XbimClassifications _classifications = new XbimClassifications();
        private readonly Dictionary<IIfcTypeProduct, ElementType> _types = new Dictionary<IIfcTypeProduct, ElementType>();

        public XbimModelLoader(IModel model)
        {
            _toMetres = model.ModelFactors.LengthToMetresConversionFactor;
            var project = model.Instances.FirstOrDefault<IIfcProject>();
            _propertySets = new XbimPropertySets(_toMetres, project == null ? null : ReadUnits(project));
            _materials = new XbimMaterials(_toMetres, _propertySets);
        }

        public Project LoadProject(IIfcProject entity)
        {
            var project = Init(new Project(), entity);
            project.Units = ReadUnits(entity);
            project.Georeference = ReadGeoreference(entity);
            LoadChildren(project, entity);
            project.LoadContexts(() => entity.RepresentationContexts.OfType<IIfcGeometricRepresentationContext>()
                .Where(c => !(c is IIfcGeometricRepresentationSubContext)).Select(LoadContext));
            return project;
        }

        /// <summary>
        /// Reads the project units. Units IfcHopper has no definition for keep the SI default;
        /// lengths are still converted with the file's real factor.
        /// </summary>
        internal Units ReadUnits(IIfcProject entity)
        {
            var units = new Units();
            units.Length = Units.FromFactor(UnitKind.Length, _toMetres) ?? units.Length;
            var named = entity.UnitsInContext?.Units.OfType<IIfcNamedUnit>().ToList() ?? new List<IIfcNamedUnit>();
            units.Area = FindUnit(named, IfcUnitEnum.AREAUNIT, UnitKind.Area, 2) ?? units.Area;
            units.Volume = FindUnit(named, IfcUnitEnum.VOLUMEUNIT, UnitKind.Volume, 3) ?? units.Volume;
            units.Angle = FindUnit(named, IfcUnitEnum.PLANEANGLEUNIT, UnitKind.Angle, 1) ?? units.Angle;
            return units;
        }

        private static UnitDefinition FindUnit(IEnumerable<IIfcNamedUnit> units, IfcUnitEnum type, UnitKind kind, int exponent)
        {
            var unit = units.FirstOrDefault(u => u.UnitType == type);
            var toSi = unit == null ? null : ToSi(unit, exponent);
            return toSi.HasValue ? Units.FromFactor(kind, toSi.Value) : null;
        }

        /// <summary>Factor from a named unit to its SI base unit; prefixes are raised to the unit's exponent (mm² = 1e-6 m²).</summary>
        private static double? ToSi(IIfcNamedUnit unit, int exponent)
        {
            switch (unit)
            {
                case IIfcSIUnit si:
                    return si.Prefix.HasValue ? Math.Pow(PrefixFactor(si.Prefix.Value), exponent) : 1.0;
                case IIfcConversionBasedUnit converted:
                    var factor = Convert.ToDouble(converted.ConversionFactor.ValueComponent.Value);
                    var baseFactor = converted.ConversionFactor.UnitComponent is IIfcNamedUnit baseUnit ? ToSi(baseUnit, exponent) : 1.0;
                    return baseFactor.HasValue ? factor * baseFactor.Value : (double?)null;
                default:
                    return null;
            }
        }

        private static double PrefixFactor(IfcSIPrefix prefix)
        {
            switch (prefix)
            {
                case IfcSIPrefix.EXA: return 1e18;
                case IfcSIPrefix.PETA: return 1e15;
                case IfcSIPrefix.TERA: return 1e12;
                case IfcSIPrefix.GIGA: return 1e9;
                case IfcSIPrefix.MEGA: return 1e6;
                case IfcSIPrefix.KILO: return 1e3;
                case IfcSIPrefix.HECTO: return 1e2;
                case IfcSIPrefix.DECA: return 1e1;
                case IfcSIPrefix.DECI: return 1e-1;
                case IfcSIPrefix.CENTI: return 1e-2;
                case IfcSIPrefix.MILLI: return 1e-3;
                case IfcSIPrefix.MICRO: return 1e-6;
                case IfcSIPrefix.NANO: return 1e-9;
                case IfcSIPrefix.PICO: return 1e-12;
                case IfcSIPrefix.FEMTO: return 1e-15;
                case IfcSIPrefix.ATTO: return 1e-18;
                default: return 1.0;
            }
        }

        internal RepresentationContext LoadContext(IIfcGeometricRepresentationContext entity)
        {
            var context = new RepresentationContext(entity.ContextType, Convert.ToInt32(entity.CoordinateSpaceDimension.Value))
            {
                Precision = entity.Precision.HasValue ? Convert.ToDouble(entity.Precision.Value.Value) * _toMetres : RepresentationContext.DefaultPrecision,
                TrueNorth = entity.TrueNorth?.DirectionRatios.Take(2).Select(r => Convert.ToDouble(r.Value)).ToArray(),
            };
            foreach (var sub in entity.HasSubContexts)
            {
                var target = sub.TargetView.ToString();
                var subContext = new RepresentationSubContext(sub.ContextIdentifier,
                    target == RepresentationSubContext.UserDefined ? (string)sub.UserDefinedTargetView ?? target : target);
                if (sub.TargetScale.HasValue) subContext.TargetScale = Convert.ToDouble(sub.TargetScale.Value.Value);
                context.SubContexts.Add(subContext);
            }
            return context;
        }

        /// <summary>The model object of a spatial entity or element, or null for entities IfcHopper does not map.</summary>
        internal ModelObject LoadObject(IIfcObjectDefinition entity)
        {
            switch (entity)
            {
                case IIfcSite site: return LoadSite(site);
                case IIfcBuildingStorey storey: return LoadStorey(storey);
                case IIfcSpace space: return LoadSpace(space);
                case IIfcElement element: return LoadElement(element);
                case IIfcSpatialStructureElement spatial: return (ModelObject)LoadFacility(spatial) ?? LoadPart(spatial);
                default: return null;
            }
        }

        private Site LoadSite(IIfcSite entity) => LoadChildren(Init(new Site(), entity), entity);

        private Facility LoadFacility(IIfcSpatialStructureElement entity)
        {
            var type = GetFacilityType(entity.ExpressType.Name);
            if (type == null) return null;

            var facility = Init(type == FacilityType.Building ? new Building() : new Facility(type.Value), entity);
            if (facility.HasPredefinedType) ReadPredefinedType(facility, entity);
            return LoadChildren(facility, entity);
        }

        private Storey LoadStorey(IIfcBuildingStorey entity)
        {
            var storey = Init(new Storey(), entity);
            storey.Elevation = entity.Elevation.HasValue ? Convert.ToDouble(entity.Elevation.Value.Value) * _toMetres : 0.0;
            return LoadChildren(storey, entity);
        }

        private FacilityPart LoadPart(IIfcSpatialStructureElement entity)
        {
            var kind = GetPartKind(entity.ExpressType.Name);
            if (kind == null) return null;

            var part = Init(new FacilityPart(kind.Value), entity);
            ReadPredefinedType(part, entity);
            var usage = entity.GetType().GetProperty("UsageType")?.GetValue(entity)?.ToString();
            part.Usage = Enum.TryParse(usage, true, out FacilityUsage parsed) ? parsed : FacilityUsage.NotDefined;
            return LoadChildren(part, entity);
        }

        private Space LoadSpace(IIfcSpace entity)
        {
            var space = Init(new Space(), entity);
            ReadPredefinedType(space, entity);
            return LoadChildren(space, entity);
        }

        private Element LoadElement(IIfcElement entity)
        {
            var element = Init(entity is IIfcFeatureElementSubtraction ? new Opening(entity.Name, entity.ExpressType.Name) : new Element(entity.Name, entity.ExpressType.Name), entity);
            if (element.HasPredefinedType) ReadPredefinedType(element, entity);
            element.LoadOpenings(() => entity.HasOpenings.Select(r => r.RelatedOpeningElement).Where(o => o != null).Select(o => (Opening)LoadElement(o)));
            if (element is Opening opening && entity is IIfcOpeningElement ifcOpening)
                opening.LoadFills(() => ifcOpening.HasFillings.Select(r => r.RelatedBuildingElement).Where(e => e != null).Select(LoadElement));
            element.LoadGeometry(skipped => new XbimGeometryReader(_toMetres, _alignments, skipped).Read(entity));
            element.LoadMaterial(() => _materials.Associated(entity));
            element.Tag = entity.Tag;
            element.LoadAttributes(() => ReadAttributes(entity));
            var typeEntity = entity.IsTypedBy.Select(r => r.RelatingType).OfType<IIfcTypeProduct>().FirstOrDefault();
            if (typeEntity != null)
            {
                element.LoadType(() => LoadType(typeEntity));
                element.LoadTypeTransform(() => new XbimGeometryReader(_toMetres, _alignments, new List<string>()).InstanceTransform(entity, typeEntity));
            }
            return LoadChildren(element, entity);
        }

        /// <summary>
        /// Set attributes of the entity's class beyond those IfcHopper models (by the IFC4X3 class of the same name), in metres, m² and m³;
        /// enumeration values by name.
        /// </summary>
        private Dictionary<string, object> ReadAttributes(IPersistEntity entity)
        {
            var attributes = new Dictionary<string, object>();
            foreach (var info in IfcBackend.Schema.GetAttributes(entity.ExpressType.Name))
            {
                var value = entity.GetType().GetProperty(info.Name)?.GetValue(entity);
                if (value is Enum) attributes[info.Name] = value.ToString();
                else if (value is IExpressValueType simple && _propertySets.Value(simple).Value is object converted) attributes[info.Name] = converted;
            }
            return attributes;
        }

        /// <summary>Property sets of a material or material set (IfcMaterialProperties).</summary>
        internal IEnumerable<PropertySet> LoadMaterialProperties(IIfcMaterialDefinition material) => _propertySets.ReadMaterial(material);

        /// <summary>The type of an element, converted once so that its occurrences share it.</summary>
        internal ElementType LoadType(IIfcTypeProduct entity)
        {
            if (_types.TryGetValue(entity, out var converted)) return converted;

            var type = Init(new ElementType(entity.Name, entity.ExpressType.Name), entity);
            if (type.HasPredefinedType) ReadPredefinedType(type, entity, (entity as IIfcElementType)?.ElementType);
            type.LoadGeometry(skipped => new XbimGeometryReader(_toMetres, _alignments, skipped).Read(entity));
            type.LoadMaterial(() => _materials.Associated(entity));
            type.Tag = entity.Tag;
            type.LoadAttributes(() => ReadAttributes(entity));
            return _types[entity] = type;
        }

        private T LoadChildren<T>(T target, IIfcObjectDefinition entity) where T : ModelObject
        {
            target.LoadChildren(() => MappedChildren(entity).Select(LoadObject));
            return target;
        }

        /// <summary>
        /// Children IfcHopper maps: aggregated spatial entities and elements, then (for spatial entities) contained elements.
        /// Elements that are parts of another element are left to that element, openings to the element they void.
        /// </summary>
        internal static IEnumerable<IIfcObjectDefinition> MappedChildren(IIfcObjectDefinition entity)
        {
            var aggregated = Children(entity).Where(c => IsMapped(c) && !IsVoiding(c));
            if (!(entity is IIfcSpatialElement spatial)) return aggregated;
            var contained = spatial.ContainsElements.SelectMany(r => r.RelatedElements).OfType<IIfcElement>()
                .Where(e => !e.Decomposes.Any(r => r.RelatingObject is IIfcElement) && !IsVoiding(e));
            return aggregated.Concat(contained).Distinct();
        }

        /// <summary>True for an opening that voids an element (listed in that element's openings).</summary>
        private static bool IsVoiding(IIfcObjectDefinition entity) => entity is IIfcFeatureElementSubtraction opening && opening.VoidsElements != null;

        /// <summary>True for entities with a model object: sites, facilities, storeys, facility parts, spaces and elements.</summary>
        internal static bool IsMapped(IIfcObjectDefinition entity) =>
            entity is IIfcSite || entity is IIfcBuildingStorey || entity is IIfcSpace || entity is IIfcElement ||
            GetFacilityType(entity.ExpressType.Name) != null || GetPartKind(entity.ExpressType.Name) != null;

        internal static IEnumerable<IIfcObjectDefinition> Children(IIfcObjectDefinition entity) =>
            entity.IsDecomposedBy.SelectMany(r => r.RelatedObjects);

        private T Init<T>(T target, IIfcRoot entity) where T : ModelObject
        {
            target.GlobalId = entity.GlobalId;
            target.Name = entity.Name;
            target.Description = entity.Description;
            target.Source = entity;
            if (entity is IIfcProduct product) target.Placement = ReadPlacement(product.ObjectPlacement);
            if (entity is IIfcObjectDefinition definition)
            {
                target.LoadPropertySets(() => _propertySets.Read(definition));
                target.LoadClassifications(() => _classifications.Read(definition));
            }
            return target;
        }

        /// <summary>World placement in metres, or null when the placement is missing or cannot be evaluated (e.g. unsupported alignment curves).</summary>
        private Placement ReadPlacement(IIfcObjectPlacement placement)
        {
            if (placement == null) return null;
            try
            {
                var world = _alignments.Resolve(placement);
                return new Placement(world.Origin.Select(v => v * _toMetres).ToArray(), world.X, world.Z);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Map conversion of the first 3D context, converted to metres; in files without one (IFC2X3) the ePSet_MapConversion and
        /// ePSet_ProjectedCRS property sets of the project.
        /// </summary>
        internal Georeference ReadGeoreference(IIfcProject project)
        {
            var conversion = project.RepresentationContexts.OfType<IIfcGeometricRepresentationContext>()
                .Where(c => !(c is IIfcGeometricRepresentationSubContext))
                .SelectMany(c => c.HasCoordinateOperation).OfType<IIfcMapConversion>().FirstOrDefault();
            if (conversion == null) return ReadGeoreferenceSets(project);

            var crs = conversion.TargetCRS;
            var projected = crs as IIfcProjectedCRS;
            var mapToMetres = projected?.MapUnit != null ? ToSi(projected.MapUnit, 1) ?? _toMetres : _toMetres;
            var fileScale = conversion.Scale.HasValue ? Convert.ToDouble(conversion.Scale.Value.Value) : 1.0;
            return new Georeference(crs?.Name)
            {
                CrsDescription = crs?.Description,
                GeodeticDatum = crs?.GeodeticDatum,
                VerticalDatum = crs?.VerticalDatum,
                MapProjection = projected?.MapProjection,
                MapZone = projected?.MapZone,
                Eastings = Convert.ToDouble(conversion.Eastings.Value) * mapToMetres,
                Northings = Convert.ToDouble(conversion.Northings.Value) * mapToMetres,
                OrthogonalHeight = Convert.ToDouble(conversion.OrthogonalHeight.Value) * mapToMetres,
                XAxisAbscissa = conversion.XAxisAbscissa.HasValue ? Convert.ToDouble(conversion.XAxisAbscissa.Value.Value) : 1.0,
                XAxisOrdinate = conversion.XAxisOrdinate.HasValue ? Convert.ToDouble(conversion.XAxisOrdinate.Value.Value) : 0.0,
                // IFC scale converts model units to map units; the Core scale is metres to metres.
                Scale = fileScale * mapToMetres / _toMetres,
            };
        }

        private Georeference ReadGeoreferenceSets(IIfcProject project)
        {
            var sets = XbimPropertySets.Definitions(project).OfType<IIfcPropertySet>().ToList();
            var conversion = sets.FirstOrDefault(s => s.Name == XbimIfcWriter.Builder.MapConversionSet);
            if (conversion == null) return null;
            var crs = sets.FirstOrDefault(s => s.Name == XbimIfcWriter.Builder.ProjectedCrsSet);

            object Value(IIfcPropertySet set, string name) =>
                (set?.HasProperties.OfType<IIfcPropertySingleValue>().FirstOrDefault(p => p.Name == name)?.NominalValue as IExpressValueType)?.Value;
            double Number(IIfcPropertySet set, string name, double fallback) => Value(set, name) is object v ? Convert.ToDouble(v) : fallback;
            string Text(string name) => Value(crs, name)?.ToString();

            var mapToMetres = Units.Find(UnitKind.Length, Text("MapUnit"))?.ToSi ?? 1.0;
            return new Georeference(Text("Name"))
            {
                CrsDescription = Text("Description"),
                GeodeticDatum = Text("GeodeticDatum"),
                VerticalDatum = Text("VerticalDatum"),
                MapProjection = Text("MapProjection"),
                MapZone = Text("MapZone"),
                Eastings = Number(conversion, "Eastings", 0) * mapToMetres,
                Northings = Number(conversion, "Northings", 0) * mapToMetres,
                OrthogonalHeight = Number(conversion, "OrthogonalHeight", 0) * mapToMetres,
                XAxisAbscissa = Number(conversion, "XAxisAbscissa", 1),
                XAxisOrdinate = Number(conversion, "XAxisOrdinate", 0),
                Scale = Number(conversion, "Scale", 1) * mapToMetres / _toMetres,
            };
        }

        /// <summary>
        /// Reads PredefinedType and ObjectType; PredefinedType exists on different entities per schema, so it is read by name.
        /// Entities without one in their schema (e.g. IFC2X3 walls) keep their type in ObjectType, which is read as the type.
        /// </summary>
        private static void ReadPredefinedType(TypedModelObject target, IIfcObject entity) => ReadPredefinedType(target, entity, entity.ObjectType);

        /// <param name="objectType">The user defined type: ObjectType of objects, ElementType of types.</param>
        private static void ReadPredefinedType(TypedModelObject target, IPersistEntity entity, string objectType)
        {
            var property = entity.GetType().GetProperty("PredefinedType");
            if (property == null)
            {
                if (!string.IsNullOrWhiteSpace(objectType)) target.SetType(objectType);
                return;
            }
            var value = property.GetValue(entity)?.ToString();
            if (value == TypedModelObject.UserDefined) target.SetType(objectType ?? value);
            else if (value != null && value != "NOTDEFINED") target.SetType(value);
        }

        internal static FacilityType? GetFacilityType(string entityName)
        {
            switch (entityName)
            {
                case "IfcBuilding": return FacilityType.Building;
                case "IfcBridge": return FacilityType.Bridge;
                case "IfcRoad": return FacilityType.Road;
                case "IfcRailway": return FacilityType.Railway;
                case "IfcMarineFacility": return FacilityType.MarineFacility;
                case "IfcFacility": return FacilityType.Facility;
                default: return null;
            }
        }

        internal static FacilityType? GetPartKind(string entityName)
        {
            switch (entityName)
            {
                case "IfcRoadPart": return FacilityType.Road;
                case "IfcBridgePart": return FacilityType.Bridge;
                case "IfcRailwayPart": return FacilityType.Railway;
                case "IfcMarinePart": return FacilityType.MarineFacility;
                case "IfcFacilityPartCommon": return FacilityType.Facility;
                default: return null;
            }
        }
    }
}
