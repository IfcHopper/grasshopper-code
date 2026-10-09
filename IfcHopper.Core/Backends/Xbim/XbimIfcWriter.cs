using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using IfcHopper.Core.Geometry;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.MeasureResource;
using Xbim.IO.Memory;

namespace IfcHopper.Core.Backends.Xbim
{
    /// <summary>
    /// Writes an <see cref="IfcHopperModel"/> using xBIM: models read from a file are written from their source file (in its schema),
    /// other models are built as new IFC4X3_ADD2, IFC4 or IFC2X3 files.
    /// </summary>
    public class XbimIfcWriter : IIfcWriter
    {
        public const string OriginatingSystem = "IfcHopper";

        public IReadOnlyList<string> Write(IfcHopperModel model, string path, IfcSchema? schema = null)
        {
            if (model?.Project == null) throw new ArgumentException("Model has no project.", nameof(model));

            if (model.Project.Source is IPersistEntity source && source.Model is MemoryModel sourceModel)
            {
                var sourceSchema = SchemaOf(sourceModel);
                if (schema == null || schema == sourceSchema) return WriteInPlace(sourceModel, model, path);

                var warnings = new List<string>
                {
                    $"The model was read from an {sourceSchema.Name()} file and is rebuilt as a new {schema.Value.Name()} file: data IfcHopper does not model " +
                    "(e.g. alignments, other representations, unmapped objects and relationships) is not carried over.",
                };
                warnings.AddRange(WriteNew(model, path, schema.Value));
                return warnings;
            }
            return WriteNew(model, path, schema ?? IfcSchema.Ifc4x3);
        }

        internal static IfcSchema SchemaOf(IModel model) =>
            model.SchemaVersion == XbimSchemaVersion.Ifc2X3 ? IfcSchema.Ifc2x3 : model.SchemaVersion == XbimSchemaVersion.Ifc4 ? IfcSchema.Ifc4 : IfcSchema.Ifc4x3;

        /// <summary>
        /// Writes a model read from a file: a fresh copy of the source file is opened, the edits are applied to it
        /// (<see cref="XbimInPlaceEditor"/>) and it is saved, so data IfcHopper does not map is preserved.
        /// The cached source model is never modified.
        /// </summary>
        private static IReadOnlyList<string> WriteInPlace(MemoryModel source, IfcHopperModel model, string path)
        {
            var sourcePath = XbimDocumentCache.PathOf(source) ?? throw new InvalidOperationException("The source file of this model is no longer known; read it again.");
            using (var ifc = MemoryModel.OpenRead(sourcePath))
            {
                var editor = new XbimInPlaceEditor(ifc, model.Author, model.Organization);
                using (var txn = ifc.BeginTransaction("IfcHopper edit"))
                {
                    editor.Apply(model.Project, model.Types, model.Materials);
                    txn.Commit();
                }

                var fileName = ifc.Header.FileName;
                fileName.Name = Path.GetFileName(path);
                if (!string.IsNullOrWhiteSpace(model.Author)) fileName.AuthorName = new List<string> { model.Author };
                if (!string.IsNullOrWhiteSpace(model.Organization)) fileName.Organization = new List<string> { model.Organization };
                Save(ifc, path);
                return editor.Warnings;
            }
        }

        /// <summary>Saves through a temporary file so a failed write never leaves a truncated file, e.g. when overwriting the source.</summary>
        private static void Save(MemoryModel ifc, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp = path + ".tmp";
            using (var stream = File.Create(temp))
                ifc.SaveAsStep21(stream);
            File.Move(temp, path, true);
        }

        private static IReadOnlyList<string> WriteNew(IfcHopperModel model, string path, IfcSchema schema)
        {
            using (var ifc = new MemoryModel(Factory(schema)))
            {
                var builder = new Builder(ifc) { Author = model.Author, Organization = model.Organization };
                using (var txn = ifc.BeginTransaction("IfcHopper write"))
                {
                    builder.AddProject(model.Project);
                    txn.Commit();
                }

                ifc.Header.FileName.Name = Path.GetFileName(path);
                ifc.Header.FileName.OriginatingSystem = OriginatingSystem;
                if (!string.IsNullOrWhiteSpace(model.Author)) ifc.Header.FileName.AuthorName = new List<string> { model.Author };
                if (!string.IsNullOrWhiteSpace(model.Organization)) ifc.Header.FileName.Organization = new List<string> { model.Organization };
                Save(ifc, path);
                return builder.Warnings;
            }
        }

        private static IEntityFactory Factory(IfcSchema schema)
        {
            switch (schema)
            {
                case IfcSchema.Ifc2x3: return new global::Xbim.Ifc2x3.EntityFactoryIfc2x3();
                case IfcSchema.Ifc4: return new global::Xbim.Ifc4.EntityFactoryIfc4();
                default: return new global::Xbim.Ifc4x3.EntityFactoryIfc4x3Add2();
            }
        }

        /// <summary>
        /// Maps the IfcHopper model onto entities of the model's schema (IFC2X3, IFC4 or IFC4X3) inside an open transaction, through the
        /// xBIM IFC4 interfaces. Entities are created by name in the target schema; what the schema lacks is downgraded where IFC allows
        /// (e.g. IfcRoad as IfcBuilding, meshes as faceted Breps in IFC2X3) and reported in <see cref="Warnings"/>.
        /// Also used by <see cref="XbimInPlaceEditor"/> to add new objects to an existing file.
        /// </summary>
        internal sealed class Builder
        {
            private readonly IModel _ifc;
            private readonly IfcSchema _schema;

            /// <summary>Converts Core lengths (metres) to the file length unit.</summary>
            private double _fromMetres = 1.0;

            /// <summary>File units, for property and quantity values in areas and volumes.</summary>
            private Units _units = new Units();

            private readonly HashSet<string> _globalIds = new HashSet<string>();
            private readonly Dictionary<string, int> _warnings = new Dictionary<string, int>();
            private readonly Dictionary<string, Type> _valueTypes;

            /// <summary>Owning user of the IFC2X3 owner history (where it is mandatory).</summary>
            public string Author { get; set; }
            public string Organization { get; set; }

            /// <param name="fromMetres">Converts metres to the file length unit (set from the project units by <see cref="AddProject"/>).</param>
            /// <param name="existingGlobalIds">GlobalIds already in the file, so new objects cannot reuse them.</param>
            /// <param name="units">File units (set from the project units by <see cref="AddProject"/>).</param>
            public Builder(IModel ifc, double fromMetres = 1.0, IEnumerable<string> existingGlobalIds = null, Units units = null)
            {
                _ifc = ifc;
                _schema = SchemaOf(ifc);
                _fromMetres = fromMetres;
                _units = units ?? _units;
                _valueTypes = ValueTypes(ifc);
                if (existingGlobalIds != null) _globalIds.UnionWith(existingGlobalIds);
            }

            /// <summary>What was downgraded or not saved because the schema does not support it; repeated messages are counted.</summary>
            public IReadOnlyList<string> Warnings => _warnings.Select(w => w.Value > 1 ? $"{w.Key} (×{w.Value})" : w.Key).ToList();

            private void Warn(string message) => _warnings[message] = _warnings.TryGetValue(message, out var count) ? count + 1 : 1;

            private string SchemaName => _schema.Name();

            public void AddProject(Project project)
            {
                var units = project.Units ?? new Units();
                _fromMetres = 1.0 / units.Length.ToSi;
                _units = units;

                var ifcProject = New<IIfcProject>(p => SetRoot(p, project));
                ifcProject.UnitsInContext = NewUnits(units);
                var contexts = project.Contexts.Count > 0 ? project.Contexts : new List<RepresentationContext> { new RepresentationContext() };
                var ifcContexts = contexts.Select(AddContext).ToList();
                ifcProject.RepresentationContexts.AddRange(ifcContexts);
                if (project.Georeference != null)
                    AddGeoreference(project.Georeference, ifcProject, ifcContexts.First(c => Dimension(c) == 3), units);

                // The georeference is written above, also when it was read from IFC2X3 property sets.
                AddPropertySets(project, ifcProject, project.Georeference == null ? null : new[] { MapConversionSet, ProjectedCrsSet });
                Classify(ifcProject, project.Classifications);
                AddChildren(project, ifcProject, Frame.Root);
                FinishTypes();
                FinishMaterials();
                FinishClassifications();
                FinishOpenings();
            }

            internal IIfcGeometricRepresentationContext AddContext(RepresentationContext context)
            {
                var ifcContext = New<IIfcGeometricRepresentationContext>(c =>
                {
                    c.ContextType = context.ContextType;
                    c.CoordinateSpaceDimension = new global::Xbim.Ifc4.GeometryResource.IfcDimensionCount(context.Dimension);
                    c.Precision = context.Precision * _fromMetres;
                    c.WorldCoordinateSystem = context.Dimension == 2 ? (IIfcAxis2Placement)NewAxis2D() : NewAxis(Placement.World);
                    if (context.TrueNorth != null) c.TrueNorth = NewDirection(context.TrueNorth);
                });

                foreach (var sub in context.SubContexts)
                {
                    New<IIfcGeometricRepresentationSubContext>(s =>
                    {
                        s.ContextIdentifier = sub.Identifier;
                        s.ContextType = context.ContextType;
                        s.ParentContext = ifcContext;
                        s.TargetView = Enum.Parse<IfcGeometricProjectionEnum>(sub.TargetView);
                        if (sub.UserDefinedTargetView != null) s.UserDefinedTargetView = sub.UserDefinedTargetView;
                        if (sub.TargetScale.HasValue) s.TargetScale = new IfcPositiveRatioMeasure(sub.TargetScale.Value);
                    });
                }
                return ifcContext;
            }

            private static int Dimension(IIfcGeometricRepresentationContext context) => Convert.ToInt32(context.CoordinateSpaceDimension.Value);

            private IIfcAxis2Placement2D NewAxis2D() => New<IIfcAxis2Placement2D>(a => a.Location = NewPoint(new[] { 0.0, 0.0 }));

            /// <summary>
            /// Writes <paramref name="child"/> with its subtree; throws when <paramref name="parent"/> does not accept it.
            /// The caller relates it to the parent (<see cref="AddChildren"/>).
            /// </summary>
            internal IIfcObjectDefinition AddChild(ModelObject parent, ModelObject child, Frame frame)
            {
                var error = SpatialRules.Check(parent, child);
                if (error != null) throw new ArgumentException(error);

                IIfcProduct entity;
                Frame childFrame;
                switch (child)
                {
                    case Site site:
                        entity = NewSpatial<IIfcSite>("IfcSite", site, frame, site.Placement ?? frame.World);
                        childFrame = new Frame(entity.ObjectPlacement, site.Placement ?? frame.World);
                        break;
                    case Facility facility:
                        {
                            var world = facility.Placement ?? frame.World;
                            entity = NewFacility(facility, frame, world);
                            childFrame = facility.Type == FacilityType.Building
                                ? new Frame(entity.ObjectPlacement, world, world)
                                : new Frame(entity.ObjectPlacement, world, frame.ElevationBase);
                            break;
                        }
                    case Storey storey:
                        {
                            var world = (frame.ElevationBase ?? frame.World).Compose(Placement.Translation(0, 0, storey.Elevation));
                            var ifcStorey = NewSpatial<IIfcBuildingStorey>("IfcBuildingStorey", storey, frame, world);
                            ifcStorey.Elevation = storey.Elevation * _fromMetres;
                            entity = ifcStorey;
                            childFrame = new Frame(ifcStorey.ObjectPlacement, world, frame.ElevationBase);
                            break;
                        }
                    case FacilityPart part:
                        entity = AddPart(part, frame);
                        childFrame = new Frame(entity.ObjectPlacement, part.Placement ?? frame.World, frame.ElevationBase);
                        break;
                    case Space space:
                        {
                            var world = space.Placement ?? frame.World;
                            var ifcSpace = NewSpatial<IIfcSpace>("IfcSpace", space, frame, world);
                            // IFC2X3 spaces have a mandatory InteriorOrExteriorSpace and no PredefinedType.
                            SetEnum(ifcSpace, "InteriorOrExteriorSpace", "NOTDEFINED");
                            SetPredefinedType(ifcSpace, space, "IfcSpace");
                            entity = ifcSpace;
                            childFrame = new Frame(ifcSpace.ObjectPlacement, world, frame.ElevationBase);
                            break;
                        }
                    case Element element:
                        entity = AddElement(element, frame);
                        childFrame = new Frame(entity.ObjectPlacement, element.Placement ?? frame.World, frame.ElevationBase);
                        AddOpenings(element, (IIfcElement)entity, childFrame);
                        break;
                    default:
                        throw new ArgumentException($"Unsupported object '{child.Name}' ({child.GetType().Name}).");
                }
                AddPropertySets(child, entity);
                Classify(entity, child.Classifications);
                AddChildren(child, entity, childFrame);
                return entity;
            }

            /// <summary>Writes the children of <paramref name="parent"/>: aggregated, except elements in spatial objects, which are contained.</summary>
            internal void AddChildren(ModelObject parent, IIfcObjectDefinition entity, Frame frame)
            {
                var created = parent.Children.Select(c => AddChild(parent, c, frame)).ToList();
                if (entity is IIfcSpatialElement spatial)
                {
                    Aggregate(entity, created.Where(c => !(c is IIfcElement)));
                    var elements = created.OfType<IIfcElement>().ToList();
                    if (elements.Count > 0) Contain(spatial, elements);
                }
                else Aggregate(entity, created);
            }

            /// <summary>The facility class in IFC4X3; an IfcBuilding with the class in ObjectType in schemas without facilities.</summary>
            private IIfcSpatialStructureElement NewFacility(Facility facility, Frame parent, Placement world)
            {
                if (facility.Type == FacilityType.Building || _schema == IfcSchema.Ifc4x3)
                {
                    var entity = NewSpatial<IIfcSpatialStructureElement>(facility.IfcClass, facility, parent, world);
                    if (facility.HasPredefinedType) SetPredefinedType(entity, facility, facility.IfcClass);
                    return entity;
                }
                Warn($"{facility.IfcClass} is written as IfcBuilding with the class in ObjectType ({SchemaName} has no {facility.IfcClass}).");
                var building = NewSpatial<IIfcBuilding>("IfcBuilding", facility, parent, world);
                building.ObjectType = Downgraded(facility.IfcClass, facility);
                return building;
            }

            /// <summary>The facility part class in IFC4X3; an IfcBuildingStorey with the class in ObjectType in schemas without facility parts.</summary>
            private IIfcSpatialStructureElement AddPart(FacilityPart part, Frame parent)
            {
                var world = part.Placement ?? parent.World;
                if (_schema == IfcSchema.Ifc4x3)
                {
                    var entity = NewSpatial<IIfcSpatialStructureElement>(part.IfcClass, part, parent, world);
                    SetPredefinedType(entity, part, part.IfcClass);
                    SetEnum(entity, "UsageType", part.Usage.ToString());
                    return entity;
                }
                Warn($"{part.IfcClass} is written as IfcBuildingStorey with the class in ObjectType; its usage is not saved ({SchemaName} has no facility parts).");
                var storey = NewSpatial<IIfcBuildingStorey>("IfcBuildingStorey", part, parent, world);
                storey.ObjectType = Downgraded(part.IfcClass, part);
                return storey;
            }

            /// <summary>Object type of a downgraded object: its IFC class, with its type after a dot.</summary>
            private static string Downgraded(string ifcClass, TypedModelObject source)
            {
                var type = source.HasPredefinedType ? (source.PredefinedType == TypedModelObject.UserDefined ? source.ObjectType : source.PredefinedType) : null;
                return type == null ? ifcClass : $"{ifcClass}.{type}";
            }

            /// <summary>
            /// Sets PredefinedType and ObjectType. A predefined type the entity does not have in this schema is written as USERDEFINED
            /// (where the entity has a PredefinedType) with the value as ObjectType.
            /// </summary>
            private void SetPredefinedType(IIfcObject entity, TypedModelObject source, string ifcClass)
            {
                if (source.PredefinedType != null && source.PredefinedType != TypedModelObject.UserDefined && !SetEnum(entity, "PredefinedType", source.PredefinedType))
                {
                    Warn($"{ifcClass} predefined type {source.PredefinedType} is written as object type ({SchemaName} has no such value).");
                    SetEnum(entity, "PredefinedType", TypedModelObject.UserDefined);
                    entity.ObjectType = source.PredefinedType;
                    return;
                }
                if (source.PredefinedType == TypedModelObject.UserDefined) SetEnum(entity, "PredefinedType", TypedModelObject.UserDefined);
                if (source.ObjectType != null) entity.ObjectType = source.ObjectType;
            }

            /// <summary>
            /// Sets an enum attribute of the entity's own class by value name (ignoring case). Returns false when the class has no such
            /// attribute or value in this schema; xBIM's interfaces would drop or misplace the value silently.
            /// </summary>
            internal static bool SetEnum(object entity, string property, string value)
            {
                var info = entity.GetType().GetProperty(property);
                if (info == null || !info.CanWrite) return false;
                var enumType = Nullable.GetUnderlyingType(info.PropertyType) ?? info.PropertyType;
                if (!enumType.IsEnum) return false;
                if (value == null)
                {
                    info.SetValue(entity, null);
                    return true;
                }
                var name = Enum.GetNames(enumType).FirstOrDefault(n => n.Equals(value, StringComparison.OrdinalIgnoreCase));
                if (name == null) return false;
                info.SetValue(entity, Enum.Parse(enumType, name));
                return true;
            }

            /// <summary>
            /// The element with its placement and body, without its parts. An instance of its type maps the type's representation maps;
            /// other elements get their own body. The type relationship is written by <see cref="FinishTypes"/>.
            /// </summary>
            private IIfcElement AddElement(Element element, Frame frame)
            {
                var world = element.Placement ?? frame.World;
                var ifcElement = NewElement(element);
                ifcElement.ObjectPlacement = NewPlacement(frame, world);
                SetAttributes(ifcElement, element.Tag, element.Attributes);
                RelateToType(ifcElement, element.Type);
                AssociateMaterial(ifcElement, element.OwnMaterial);

                var body = NewInstanceBody(element, world) ?? (element.Geometry.Count > 0 ? NewBodyRepresentation(element.Geometry, world) : null);
                if (body != null) ifcElement.Representation = NewShape(new[] { body });
                return ifcElement;
            }

            private readonly Dictionary<string, IIfcElement> _elementEntities = new Dictionary<string, IIfcElement>();
            private readonly List<(IIfcElement Opening, Opening Source, Element Fill)> _fills = new List<(IIfcElement, Opening, Element)>();

            /// <summary>
            /// Writes the openings of an element placed relative to it, with their property sets and classifications, each voiding the element
            /// (IfcRelVoidsElement). Their fills are related when the elements are finished (<see cref="FinishOpenings"/>).
            /// </summary>
            internal void AddOpenings(Element host, IIfcElement entity, Frame frame)
            {
                if (!string.IsNullOrWhiteSpace(host.GlobalId)) _elementEntities[host.GlobalId] = entity;
                foreach (var opening in host.Openings) AddOpening(host, entity, opening, frame);
            }

            /// <summary>Writes one opening of <paramref name="host"/> (see <see cref="AddOpenings"/>); <paramref name="frame"/> is the host's.</summary>
            internal IIfcElement AddOpening(Element host, IIfcElement entity, Opening opening, Frame frame)
            {
                var ifcOpening = AddElement(opening, frame);
                AddPropertySets(opening, ifcOpening);
                Classify(ifcOpening, opening.Classifications);
                if (!(ifcOpening is IIfcFeatureElementSubtraction subtraction))
                {
                    Warn($"Opening '{opening.Name}' is written as an element that does not void '{host.Name}' ({SchemaName} has no {opening.IfcClass}).");
                    return ifcOpening;
                }
                New<IIfcRelVoidsElement>(r =>
                {
                    SetRelation(r, ifcOpening, "voids");
                    r.RelatingBuildingElement = entity;
                    r.RelatedOpeningElement = subtraction;
                });
                foreach (var fill in opening.Fills) Fill(ifcOpening, opening, fill);
                return ifcOpening;
            }

            /// <summary>Relates an opening to an element filling it when the openings are finished (<see cref="FinishOpenings"/>).</summary>
            internal void Fill(IIfcElement opening, Opening source, Element fill)
            {
                if (opening is IIfcOpeningElement) _fills.Add((opening, source, fill));
                else Warn($"Fills of '{source.Name}' are not saved: only IfcOpeningElement can be filled.");
            }

            /// <summary>
            /// Relates the written openings to the elements filling them (IfcRelFillsElement), found by GlobalId among the written elements or,
            /// in a file being edited, the file's elements. A fill that is not part of the model is left out with a warning.
            /// </summary>
            internal void FinishOpenings()
            {
                foreach (var (opening, source, fill) in _fills)
                {
                    var filling = fill.GlobalId == null ? null
                        : _elementEntities.TryGetValue(fill.GlobalId, out var written) ? written : Existing?.Invoke(fill.GlobalId) as IIfcElement;
                    if (filling == null)
                    {
                        Warn($"'{fill.Name}' filling opening '{source.Name}' is not saved as its fill: it is not part of the model (add it to a storey or space too).");
                        continue;
                    }
                    New<IIfcRelFillsElement>(r =>
                    {
                        SetRelation(r, opening, "fills/" + filling.GlobalId);
                        r.RelatingOpeningElement = (IIfcOpeningElement)opening;
                        r.RelatedBuildingElement = filling;
                    });
                }
                _fills.Clear();
            }

            /// <summary>Sets the tag and the class-specific attributes of an element or type.</summary>
            internal void SetAttributes(IPersistEntity entity, string tag, IReadOnlyDictionary<string, object> attributes)
            {
                if (tag != null)
                {
                    if (entity is IIfcElement element) element.Tag = tag;
                    else if (entity is IIfcTypeProduct type) type.Tag = tag;
                }
                foreach (var attribute in attributes) SetAttribute(entity, attribute.Key, attribute.Value);
            }

            /// <summary>
            /// Sets an attribute of the entity's own class by name (null clears an optional one), converting lengths, areas and volumes to the
            /// file units. Attributes or enumeration values the class lacks in this schema are not saved, with a warning.
            /// </summary>
            internal void SetAttribute(IPersistEntity entity, string name, object value)
            {
                var className = entity.ExpressType.Name;
                var info = entity.GetType().GetProperty(name);
                if (info == null || !info.CanWrite)
                {
                    Warn($"{className}.{name} is not saved ({SchemaName} has no such attribute).");
                    return;
                }
                var type = Nullable.GetUnderlyingType(info.PropertyType) ?? info.PropertyType;
                if (value == null)
                {
                    if (info.PropertyType.IsValueType && Nullable.GetUnderlyingType(info.PropertyType) == null) Warn($"{className}.{name} is mandatory and cannot be removed.");
                    else info.SetValue(entity, null);
                }
                else if (type.IsEnum)
                {
                    if (!SetEnum(entity, name, value as string)) Warn($"{className}.{name} value {value} is not saved ({SchemaName} has no such value).");
                }
                else if (typeof(IExpressValueType).IsAssignableFrom(type)) info.SetValue(entity, NewValue(type.Name, value, true));
                // Some attributes are plain values, e.g. IfcDoorStyle.ParameterTakesPrecedence in IFC2X3.
                else info.SetValue(entity, Convert.ChangeType(value, type, CultureInfo.InvariantCulture));
            }

            /// <summary>Relates an element to the entity of its type when the types are finished (<see cref="FinishTypes"/>); nothing for null.</summary>
            internal void RelateToType(IIfcObject entity, ElementType type)
            {
                if (type != null) _typeOccurrences[TypeEntity(type)].Add(entity);
            }

            /// <summary>
            /// Body of an instance of its type: mapped items of the type's Body maps relative to the element's world placement. Null when the
            /// element is no instance, its type has no geometry, or the transform shears (with a warning).
            /// </summary>
            internal IIfcShapeRepresentation NewInstanceBody(Element element, Placement world)
            {
                if (element.Type == null || element.TypeTransform == null) return null;
                var maps = XbimGeometryReader.BodyMaps(TypeEntity(element.Type)).ToList();
                if (maps.Count == 0) return null;
                var target = NewOperator(world, element.TypeTransform);
                if (target != null) return NewMappedRepresentation(maps, target);
                Warn("Elements placing their type with a shear are written with their own geometry (IFC mapped items cannot shear).");
                return null;
            }

            /// <summary>Finds an entity of the file being edited by GlobalId; null when writing a new file.</summary>
            internal Func<string, IIfcRoot> Existing { get; set; }

            private readonly Dictionary<object, IIfcTypeProduct> _typeEntities = new Dictionary<object, IIfcTypeProduct>();
            private readonly Dictionary<IIfcTypeProduct, List<IIfcObject>> _typeOccurrences = new Dictionary<IIfcTypeProduct, List<IIfcObject>>();

            /// <summary>
            /// The entity of a type: written once per GlobalId (or per object without one); in a file being edited, the type of the file with
            /// that GlobalId is used (its edits are written by the editor).
            /// </summary>
            internal IIfcTypeProduct TypeEntity(ElementType type)
            {
                var key = string.IsNullOrWhiteSpace(type.GlobalId) ? (object)type : type.GlobalId;
                if (_typeEntities.TryGetValue(key, out var entity)) return entity;

                entity = string.IsNullOrWhiteSpace(type.GlobalId) ? null : Existing?.Invoke(type.GlobalId) as IIfcTypeProduct;
                entity = entity ?? NewType(type);
                _typeOccurrences[entity] = new List<IIfcObject>();
                return _typeEntities[key] = entity;
            }

            /// <summary>
            /// The type class in this schema with its own property sets and a Body representation map of its geometry. Door and window types
            /// are written as styles in IFC2X3; other classes the schema lacks as IfcBuildingElementProxyType with the class in ElementType.
            /// </summary>
            private IIfcTypeProduct NewType(ElementType type)
            {
                var requested = string.IsNullOrWhiteSpace(type.IfcClass) ? "IfcBuildingElementProxyType" : type.IfcClass.Trim();
                var className = IfcBackend.Schema.FindTypeClass(requested) ?? throw new ArgumentException($"'{requested}' is not an IFC4X3 element type class.");

                IIfcTypeProduct entity;
                var style = className.Replace("Type", "Style");
                if (ExpressType(className) is Type typeClass && typeof(IIfcTypeProduct).IsAssignableFrom(typeClass))
                {
                    entity = (IIfcTypeProduct)_ifc.Instances.New(typeClass);
                    SetRoot(entity, type);
                    SetTypePredefinedType(entity, type, className);
                }
                else if ((className == "IfcDoorType" || className == "IfcWindowType") && ExpressType(style) != null)
                {
                    Warn($"{className} is written as {style} ({SchemaName} has no {className}).");
                    entity = New<IIfcTypeProduct>(style, s => SetRoot(s, type));
                    if (!SetEnum(entity, "OperationType", type.PredefinedType ?? "NOTDEFINED")) SetEnum(entity, "OperationType", "NOTDEFINED");
                    SetEnum(entity, "ConstructionType", "NOTDEFINED");
                    entity.GetType().GetProperty("ParameterTakesPrecedence")?.SetValue(entity, false);
                    entity.GetType().GetProperty("Sizeable")?.SetValue(entity, false);
                }
                else
                {
                    Warn($"{className} is written as IfcBuildingElementProxyType with the class in ElementType ({SchemaName} has no {className}).");
                    var proxy = New<IIfcElementType>("IfcBuildingElementProxyType", p => SetRoot(p, type));
                    SetEnum(proxy, "PredefinedType", TypedModelObject.UserDefined);
                    proxy.ElementType = Downgraded(className, type);
                    entity = proxy;
                }

                foreach (var set in type.PropertySets.Where(s => !s.FromType))
                    entity.HasPropertySets.Add(NewPropertySet(set, entity, NewItems(set)));
                if (type.Geometry.Count > 0) entity.RepresentationMaps.Add(NewRepresentationMap(type.Geometry));
                AssociateMaterial(entity, type.Material);
                Classify(entity, type.Classifications);
                SetAttributes(entity, type.Tag, type.Attributes);
                return entity;
            }

            /// <summary>Body representation map of type geometry, at the type's origin.</summary>
            internal IIfcRepresentationMap NewRepresentationMap(IEnumerable<MeshGeometry> meshes) =>
                New<IIfcRepresentationMap>(m =>
                {
                    m.MappingOrigin = NewAxis(Placement.World);
                    m.MappedRepresentation = NewBodyRepresentation(meshes, Placement.World);
                });

            /// <summary>
            /// Sets PredefinedType (mandatory on most type classes, so NOTDEFINED when not set) and ElementType, which holds the user defined
            /// type. A predefined type the class does not have in this schema is written as USERDEFINED with the value as ElementType.
            /// </summary>
            private void SetTypePredefinedType(IIfcTypeProduct entity, ElementType source, string className)
            {
                var userDefined = source.ObjectType;
                if (source.PredefinedType != null && source.PredefinedType != TypedModelObject.UserDefined && !SetEnum(entity, "PredefinedType", source.PredefinedType))
                {
                    Warn($"{className} predefined type {source.PredefinedType} is written as element type ({SchemaName} has no such value).");
                    SetEnum(entity, "PredefinedType", TypedModelObject.UserDefined);
                    userDefined = source.PredefinedType;
                }
                else if (source.PredefinedType == null) SetEnum(entity, "PredefinedType", "NOTDEFINED");
                else if (source.PredefinedType == TypedModelObject.UserDefined) SetEnum(entity, "PredefinedType", TypedModelObject.UserDefined);
                if (userDefined != null && entity is IIfcElementType elementType) elementType.ElementType = userDefined;
            }

            /// <summary>Body representation mapping each of <paramref name="maps"/> with <paramref name="target"/>.</summary>
            private IIfcShapeRepresentation NewMappedRepresentation(IEnumerable<IIfcRepresentationMap> maps, IIfcCartesianTransformationOperator3D target) =>
                New<IIfcShapeRepresentation>(r =>
                {
                    r.ContextOfItems = BodyContext();
                    r.RepresentationIdentifier = BodyIdentifier;
                    r.RepresentationType = "MappedRepresentation";
                    r.Items.AddRange(maps.Select(map => New<IIfcMappedItem>(i =>
                    {
                        i.MappingSource = map;
                        i.MappingTarget = target;
                    })));
                });

            /// <summary>
            /// The mapping target of a type instance: <paramref name="transform"/> (type coordinates to world, metres) relative to the element's
            /// world placement, with a uniform or non-uniform scale. Null when the transform shears, which an operator cannot hold.
            /// </summary>
            private IIfcCartesianTransformationOperator3D NewOperator(Placement world, Affine transform)
            {
                double[] Local(double[] v) => world.PointToLocal(world.Origin.Zip(v, (o, c) => o + c).ToArray());
                var axes = new[] { Local(transform.X), Local(transform.Y), Local(transform.Z) };
                var scales = axes.Select(a => Math.Sqrt(a.Sum(c => c * c))).ToArray();
                if (scales.Any(s => s < 1e-12)) return null;
                var units = axes.Select((a, i) => a.Select(c => c / scales[i]).ToArray()).ToArray();
                double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
                if (Math.Abs(Dot(units[0], units[1])) > 1e-6 || Math.Abs(Dot(units[0], units[2])) > 1e-6 || Math.Abs(Dot(units[1], units[2])) > 1e-6) return null;

                var nonUniform = Math.Abs(scales[1] - scales[0]) > 1e-9 * scales[0] || Math.Abs(scales[2] - scales[0]) > 1e-9 * scales[0];
                var target = nonUniform ? New<IIfcCartesianTransformationOperator3DnonUniform>() : New<IIfcCartesianTransformationOperator3D>();
                target.LocalOrigin = NewPoint(world.PointToLocal(transform.Origin).Select(v => v * _fromMetres));
                var identity = new[] { new[] { 1.0, 0, 0 }, new[] { 0, 1.0, 0 }, new[] { 0, 0, 1.0 } };
                if (units.Where((u, i) => u.Zip(identity[i], (a, b) => Math.Abs(a - b)).Max() > 1e-12).Any())
                {
                    target.Axis1 = NewDirection(units[0]);
                    target.Axis2 = NewDirection(units[1]);
                    target.Axis3 = NewDirection(units[2]);
                }
                if (Math.Abs(scales[0] - 1) > 1e-12) target.Scale = scales[0];
                if (target is IIfcCartesianTransformationOperator3DnonUniform scaled)
                {
                    scaled.Scale2 = scales[1];
                    scaled.Scale3 = scales[2];
                }
                return target;
            }

            /// <summary>Finds the entity of the file being edited that a definition was read from (its source); null when writing a new file.</summary>
            internal Func<object, IPersistEntity> ExistingSource { get; set; }

            private readonly Dictionary<MaterialDefinition, IIfcMaterialSelect> _materialEntities = new Dictionary<MaterialDefinition, IIfcMaterialSelect>();
            private readonly Dictionary<IIfcMaterialSelect, List<IIfcDefinitionSelect>> _materialOccurrences = new Dictionary<IIfcMaterialSelect, List<IIfcDefinitionSelect>>();
            private readonly Dictionary<(IIfcMaterialLayerSet, IfcLayerSetDirectionEnum), IIfcMaterialLayerSetUsage> _layerSetUsages =
                new Dictionary<(IIfcMaterialLayerSet, IfcLayerSetDirectionEnum), IIfcMaterialLayerSetUsage>();

            /// <summary>
            /// Associates an object or type with a material when the materials are finished (<see cref="FinishMaterials"/>); nothing for null.
            /// Elements take a layer set through a layer set usage with default values.
            /// </summary>
            internal void AssociateMaterial(IIfcDefinitionSelect entity, MaterialDefinition material)
            {
                var select = material == null ? null : MaterialEntity(material);
                if (select == null) return;
                if (select is IIfcMaterialLayerSet set && entity is IIfcElement element) select = LayerSetUsage(set, element);
                if (!_materialOccurrences.TryGetValue(select, out var related)) _materialOccurrences[select] = related = new List<IIfcDefinitionSelect>();
                related.Add(entity);
            }

            /// <summary>Usage of a layer set with the layers along the thickness of slabs, plates, coverings and roofs, across that of other elements.</summary>
            private IIfcMaterialLayerSetUsage LayerSetUsage(IIfcMaterialLayerSet set, IIfcElement element)
            {
                var direction = new[] { "IfcSlab", "IfcPlate", "IfcCovering", "IfcRoof" }.Any(c => element.ExpressType.Name.StartsWith(c))
                    ? IfcLayerSetDirectionEnum.AXIS3 : IfcLayerSetDirectionEnum.AXIS2;
                if (_layerSetUsages.TryGetValue((set, direction), out var usage)) return usage;
                return _layerSetUsages[(set, direction)] = New<IIfcMaterialLayerSetUsage>(u =>
                {
                    u.ForLayerSet = set;
                    u.LayerSetDirection = direction;
                    u.DirectionSense = IfcDirectionSenseEnum.POSITIVE;
                    u.OffsetFromReferenceLine = new IfcLengthMeasure(0);
                });
            }

            /// <summary>
            /// The entity of a material definition, written once per definition: in a file being edited the entity it was read from; for a
            /// single material an existing one with the same name and values; else a new one. Null for definitions that cannot be written.
            /// </summary>
            internal IIfcMaterialSelect MaterialEntity(MaterialDefinition definition)
            {
                if (_materialEntities.TryGetValue(definition, out var entity)) return entity;
                entity = ExistingSource?.Invoke(definition.Source) as IIfcMaterialSelect
                    ?? (definition is Material material ? SameMaterial(material) : null)
                    ?? NewMaterial(definition);
                return _materialEntities[definition] = entity;
            }

            /// <summary>Materials written by this builder, to compare their property sets when reusing them.</summary>
            private readonly Dictionary<IIfcMaterial, Material> _newMaterials = new Dictionary<IIfcMaterial, Material>();

            /// <summary>
            /// An existing material with the same name, description, category, colour and property sets; materials already in the file are
            /// reused only when neither has property sets.
            /// </summary>
            private IIfcMaterial SameMaterial(Material material) =>
                _ifc.Instances.OfType<IIfcMaterial>().FirstOrDefault(e => (string)e.Name == material.Name &&
                    (_schema == IfcSchema.Ifc2x3 || (string)e.Description == material.Description && (string)e.Category == material.Category) &&
                    Equals(XbimMaterials.ColourOf(e), material.Colour) &&
                    (_newMaterials.TryGetValue(e, out var written) ? material.SamePropertySets(written.PropertySets) : material.PropertySets.Count == 0 && !e.HasProperties.Any()));

            private IIfcMaterialSelect NewMaterial(MaterialDefinition definition)
            {
                var entity = NewMaterialEntity(definition);
                if (definition.PropertySets.Count > 0)
                {
                    if (entity is IIfcMaterialDefinition withProperties) AddMaterialProperties(withProperties, definition.PropertySets);
                    else if (entity != null) Warn("Property sets of material lists are not saved.");
                }
                if (entity is IIfcMaterial material && definition is Material written) _newMaterials[material] = written;
                return entity;
            }

            /// <summary>Property sets of a material or set (IfcMaterialProperties); IFC2X3 only has typed material properties.</summary>
            internal void AddMaterialProperties(IIfcMaterialDefinition material, IEnumerable<PropertySet> sets)
            {
                if (_schema == IfcSchema.Ifc2x3)
                {
                    Warn("Material property sets are not saved (IFC2X3 has only typed material properties).");
                    return;
                }
                foreach (var set in sets)
                    New<IIfcMaterialProperties>(p =>
                    {
                        p.Name = set.Name;
                        if (set.Description != null) p.Description = set.Description;
                        p.Material = material;
                        // The IFC4 interface exposes the properties read-only; the schema class has the list.
                        foreach (var property in set.Properties) AddValue(p, "Properties", NewProperty(property));
                    });
            }

            private IIfcMaterialSelect NewMaterialEntity(MaterialDefinition definition)
            {
                var ifc2x3 = _schema == IfcSchema.Ifc2x3;
                switch (definition)
                {
                    case Material material:
                        var entity = New<IIfcMaterial>(m =>
                        {
                            m.Name = material.Name ?? "";
                            if (ifc2x3) return;
                            if (material.Description != null) m.Description = material.Description;
                            if (material.Category != null) m.Category = material.Category;
                        });
                        if (ifc2x3 && (material.Description != null || material.Category != null))
                            Warn("Material descriptions and categories are not saved (IFC2X3 has none).");
                        if (material.Colour != null) AddMaterialColour(entity, material.Colour);
                        return entity;
                    case MaterialLayerSet set:
                        return New<IIfcMaterialLayerSet>(s =>
                        {
                            if (set.Name != null) s.LayerSetName = set.Name;
                            if (set.Description != null && !ifc2x3) s.Description = set.Description;
                            s.MaterialLayers.AddRange(set.Layers.Select(l => New<IIfcMaterialLayer>(layer =>
                            {
                                if (l.Material != null) layer.Material = (IIfcMaterial)MaterialEntity(l.Material);
                                layer.LayerThickness = new IfcNonNegativeLengthMeasure(l.Thickness * _fromMetres);
                                if (ifc2x3) return;
                                if (l.Name != null) layer.Name = l.Name;
                                if (l.Category != null) layer.Category = l.Category;
                            })));
                        });
                    case MaterialConstituentSet set when ifc2x3:
                        Warn("Material constituent sets are written as material lists without names, categories and fractions (IFC2X3 has no constituent sets).");
                        return New<IIfcMaterialList>(l => l.Materials.AddRange(set.Materials.Where(m => m != null).Select(m => (IIfcMaterial)MaterialEntity(m))));
                    case MaterialConstituentSet set:
                        if (set.Constituents.Any(c => c.Material == null)) Warn("Material constituents without a material are not saved.");
                        return New<IIfcMaterialConstituentSet>(s =>
                        {
                            if (set.Name != null) s.Name = set.Name;
                            if (set.Description != null) s.Description = set.Description;
                            s.MaterialConstituents.AddRange(set.Constituents.Where(c => c.Material != null).Select(c => New<IIfcMaterialConstituent>(constituent =>
                            {
                                constituent.Material = (IIfcMaterial)MaterialEntity(c.Material);
                                if (c.Name != null) constituent.Name = c.Name;
                                if (c.Category != null) constituent.Category = c.Category;
                                if (c.Fraction.HasValue) constituent.Fraction = new IfcNormalisedRatioMeasure(c.Fraction.Value);
                            })));
                        });
                    default:
                        Warn($"{definition.IfcClass} materials are not saved (their profiles are not modelled).");
                        return null;
                }
            }

            /// <summary>Surface style of a material (IfcMaterialDefinitionRepresentation in the 3D model context).</summary>
            internal void AddMaterialColour(IIfcMaterial material, Colour colour) =>
                New<IIfcMaterialDefinitionRepresentation>(d =>
                {
                    d.RepresentedMaterial = material;
                    d.Representations.Add(New<IIfcStyledRepresentation>(r =>
                    {
                        r.ContextOfItems = BodyContext().ParentContext;
                        r.RepresentationIdentifier = "Style";
                        r.RepresentationType = "Material";
                        r.Items.Add(New<IIfcStyledItem>(s => s.Styles.Add(SurfaceStyle(colour))));
                    }));
                });

            /// <summary>Relates the written objects and types to their materials: added to the material's relationship in a file being edited, else a new one.</summary>
            internal void FinishMaterials()
            {
                foreach (var pair in _materialOccurrences.Where(p => p.Value.Count > 0))
                {
                    var rel = _ifc.Instances.OfType<IIfcRelAssociatesMaterial>().FirstOrDefault(r => r.RelatingMaterial == pair.Key);
                    if (rel != null) rel.RelatedObjects.AddRange(pair.Value);
                    else
                        New<IIfcRelAssociatesMaterial>(r =>
                        {
                            SetRelation(r, (IIfcRoot)pair.Value[0], "material");
                            r.RelatingMaterial = pair.Key;
                            r.RelatedObjects.AddRange(pair.Value);
                        });
                    pair.Value.Clear();
                }
            }

            /// <summary>Relates the written elements to their types: added to the type's relationship in a file being edited, else a new one.</summary>
            internal void FinishTypes()
            {
                foreach (var pair in _typeOccurrences.Where(p => p.Value.Count > 0))
                {
                    var rel = _ifc.Instances.OfType<IIfcRelDefinesByType>().FirstOrDefault(r => r.RelatingType == pair.Key);
                    if (rel != null) rel.RelatedObjects.AddRange(pair.Value);
                    else
                        New<IIfcRelDefinesByType>(r =>
                        {
                            SetRelation(r, pair.Key, "types");
                            r.RelatingType = pair.Key;
                            r.RelatedObjects.AddRange(pair.Value);
                        });
                    pair.Value.Clear();
                }
            }

            private readonly Dictionary<(string, string, string, string, string), IIfcClassificationSelect> _classificationEntities =
                new Dictionary<(string, string, string, string, string), IIfcClassificationSelect>();
            private readonly Dictionary<IIfcClassificationSelect, List<IIfcDefinitionSelect>> _classified = new Dictionary<IIfcClassificationSelect, List<IIfcDefinitionSelect>>();
            private readonly Dictionary<IIfcClassificationSelect, string> _classificationSeeds = new Dictionary<IIfcClassificationSelect, string>();

            /// <summary>Associates an object or type with its own classification references when they are finished (<see cref="FinishClassifications"/>).</summary>
            internal void Classify(IIfcDefinitionSelect entity, IEnumerable<ClassificationReference> references)
            {
                foreach (var reference in references.Where(r => !r.FromType))
                {
                    var select = ClassificationEntity(reference);
                    if (select == null) continue;
                    if (!_classified.TryGetValue(select, out var related)) _classified[select] = related = new List<IIfcDefinitionSelect>();
                    if (!related.Contains(entity)) related.Add(entity);
                }
            }

            /// <summary>
            /// The entity of a reference, written once per value: in a file being edited the entity it was read from; else an existing one with
            /// the same values; else a new reference in the classification of its system. A reference without code and name is its whole system.
            /// </summary>
            internal IIfcClassificationSelect ClassificationEntity(ClassificationReference reference)
            {
                var key = (reference.System, reference.Edition, reference.Code, reference.Name, reference.Location);
                if (_classificationEntities.TryGetValue(key, out var entity)) return entity;
                var whole = reference.Code == null && reference.Name == null;
                entity = ExistingSource?.Invoke(reference.Source) as IIfcClassificationSelect
                    ?? (whole ? (IIfcClassificationSelect)_ifc.Instances.OfType<IIfcClassification>().FirstOrDefault(c => reference.SameAs(XbimClassifications.Values(c)))
                        : _ifc.Instances.OfType<IIfcClassificationReference>().FirstOrDefault(r => reference.SameAs(XbimClassifications.Values(r))))
                    ?? NewClassification(reference, whole);
                if (entity != null) _classificationSeeds[entity] = string.Join("/", key.System, key.Edition, key.Code, key.Name, key.Location);
                return _classificationEntities[key] = entity;
            }

            private IIfcClassificationSelect NewClassification(ClassificationReference reference, bool whole)
            {
                if (whole)
                {
                    if (_schema != IfcSchema.Ifc2x3) return Classification(reference.System, reference.Edition, reference.Location);
                    Warn("Associations with a whole classification system are not saved (IFC2X3 only associates classification references).");
                    return null;
                }
                return New<IIfcClassificationReference>(r =>
                {
                    if (reference.Code != null) r.Identification = reference.Code;
                    if (reference.Name != null) r.Name = reference.Name;
                    if (reference.Location != null) r.Location = reference.Location;
                    if (reference.System != null) r.ReferencedSource = Classification(reference.System, reference.Edition, null);
                });
            }

            /// <summary>The classification (system) with the name and edition: an existing one, else a new one (IFC2X3 needs a source and an edition).</summary>
            private IIfcClassification Classification(string system, string edition, string location) =>
                _ifc.Instances.OfType<IIfcClassification>().FirstOrDefault(c => XbimClassifications.Values(c) is ClassificationReference v &&
                        v.System == system && v.Edition == edition && (location == null || v.Location == location))
                    ?? New<IIfcClassification>(c =>
                    {
                        c.Name = system ?? "";
                        if (edition != null) c.Edition = edition;
                        if (location != null) c.Location = location;
                        if (_schema != IfcSchema.Ifc2x3) return;
                        c.Source = "";
                        if (edition == null) c.Edition = "";
                    });

            /// <summary>Relates the written objects and types to their classification references: added to the reference's relationship in a file being edited, else a new one.</summary>
            internal void FinishClassifications()
            {
                foreach (var pair in _classified.Where(p => p.Value.Count > 0))
                {
                    var rel = _ifc.Instances.OfType<IIfcRelAssociatesClassification>().FirstOrDefault(r => r.RelatingClassification == pair.Key);
                    if (rel != null) rel.RelatedObjects.AddRange(pair.Value.Where(o => !rel.RelatedObjects.Contains(o)).ToList());
                    else
                        New<IIfcRelAssociatesClassification>(r =>
                        {
                            SetRelation(r, (IIfcRoot)pair.Value[0], "classification/" + _classificationSeeds[pair.Key]);
                            r.RelatingClassification = pair.Key;
                            r.RelatedObjects.AddRange(pair.Value);
                        });
                    pair.Value.Clear();
                }
            }

            /// <summary>The element class in this schema, or an IfcBuildingElementProxy with the class in ObjectType when the schema has no such class.</summary>
            private IIfcElement NewElement(Element element)
            {
                var requested = string.IsNullOrWhiteSpace(element.IfcClass) ? Element.DefaultIfcClass : element.IfcClass.Trim();
                var className = IfcBackend.Schema.FindElementClass(requested)
                    ?? throw new ArgumentException($"'{requested}' is not an instantiable IFC4X3 element class.");

                var type = ExpressType(className);
                if (type != null && typeof(IIfcElement).IsAssignableFrom(type))
                {
                    var entity = (IIfcElement)_ifc.Instances.New(type);
                    SetRoot(entity, element);
                    if (element.HasPredefinedType) SetPredefinedType(entity, element, className);
                    return entity;
                }

                Warn($"{className} is written as IfcBuildingElementProxy with the class in ObjectType ({SchemaName} has no {className}).");
                var proxy = New<IIfcBuildingElementProxy>(p => SetRoot(p, element));
                SetEnum(proxy, "PredefinedType", TypedModelObject.UserDefined);
                proxy.ObjectType = Downgraded(className, element);
                return proxy;
            }

            internal IIfcProductDefinitionShape NewShape(IEnumerable<IIfcRepresentation> representations) =>
                New<IIfcProductDefinitionShape>(s => s.Representations.AddRange(representations));

            /// <summary>
            /// Body representation relative to the element placement: one IfcPolygonalFaceSet per mesh, or in IFC2X3 one IfcFacetedBrep per
            /// mesh (IfcFaceBasedSurfaceModels when a mesh is open).
            /// </summary>
            internal IIfcShapeRepresentation NewBodyRepresentation(IEnumerable<MeshGeometry> meshes, Placement placement)
            {
                var local = meshes.Select(m => m.Transform(placement.PointToLocal)).ToList();
                string type;
                List<IIfcRepresentationItem> items;
                if (_schema != IfcSchema.Ifc2x3)
                {
                    type = "Tessellation";
                    items = local.Select(m => Styled(NewFaceSet(m), m.Colour)).ToList();
                }
                else if (local.All(m => m.IsClosed))
                {
                    type = "Brep";
                    items = local.Select(m => Styled(New<IIfcFacetedBrep>(b => b.Outer = New<IIfcClosedShell>(s => s.CfsFaces.AddRange(NewFaces(m)))), m.Colour)).ToList();
                }
                else
                {
                    type = "SurfaceModel";
                    items = local.Select(m => Styled(New<IIfcFaceBasedSurfaceModel>(s =>
                        s.FbsmFaces.Add(New<IIfcConnectedFaceSet>("IfcConnectedFaceSet", f => f.CfsFaces.AddRange(NewFaces(m))))), m.Colour)).ToList();
                }
                return New<IIfcShapeRepresentation>(r =>
                {
                    r.ContextOfItems = BodyContext();
                    r.RepresentationIdentifier = BodyIdentifier;
                    r.RepresentationType = type;
                    r.Items.AddRange(items);
                });
            }

            private IIfcRepresentationItem NewFaceSet(MeshGeometry mesh)
            {
                var points = New<IIfcCartesianPointList3D>();
                for (int i = 0; i < mesh.Vertices.Count; i++)
                    points.CoordList.GetAt(i).AddRange(mesh.Vertices[i].Select(v => new IfcLengthMeasure(v * _fromMetres)));

                return New<IIfcPolygonalFaceSet>(f =>
                {
                    f.Coordinates = points;
                    f.Closed = mesh.IsClosed;
                    f.Faces.AddRange(mesh.Faces.Select(face => New<IIfcIndexedPolygonalFace>(p => p.CoordIndex.AddRange(face.Select(i => new IfcPositiveInteger(i + 1))))));
                });
            }

            /// <summary>Faces bounded by poly loops over shared points, for IFC2X3 Breps and surface models.</summary>
            private List<IIfcFace> NewFaces(MeshGeometry mesh)
            {
                var points = mesh.Vertices.Select(v => NewPoint(v.Select(c => c * _fromMetres))).ToList();
                return mesh.Faces.Select(face => New<IIfcFace>(f => f.Bounds.Add(New<IIfcFaceOuterBound>(b =>
                {
                    b.Bound = New<IIfcPolyLoop>(l => l.Polygon.AddRange(face.Select(i => points[i])));
                    b.Orientation = true;
                })))).ToList();
            }

            private IIfcRepresentationItem Styled(IIfcRepresentationItem item, Colour colour)
            {
                if (colour != null)
                    New<IIfcStyledItem>(s =>
                    {
                        s.Item = item;
                        s.Styles.Add(SurfaceStyle(colour));
                    });
                return item;
            }

            private readonly Dictionary<Colour, IIfcSurfaceStyle> _surfaceStyles = new Dictionary<Colour, IIfcSurfaceStyle>();

            /// <summary>
            /// Surface style of a colour, shared by all items of that colour written by this builder. IFC2X3 shading has no transparency,
            /// so transparent colours are written as rendering styles there.
            /// </summary>
            private IIfcSurfaceStyle SurfaceStyle(Colour colour)
            {
                if (_surfaceStyles.TryGetValue(colour, out var style)) return style;
                var shading = colour.Transparency > 0 && _schema == IfcSchema.Ifc2x3
                    ? New<IIfcSurfaceStyleRendering>(r => r.ReflectanceMethod = IfcReflectanceMethodEnum.NOTDEFINED)
                    : New<IIfcSurfaceStyleShading>("IfcSurfaceStyleShading");
                shading.SurfaceColour = New<IIfcColourRgb>(c =>
                {
                    c.Red = colour.Red;
                    c.Green = colour.Green;
                    c.Blue = colour.Blue;
                });
                if (colour.Transparency > 0) shading.Transparency = colour.Transparency;
                return _surfaceStyles[colour] = New<IIfcSurfaceStyle>(s =>
                {
                    s.Name = colour.ToString();
                    s.Side = IfcSurfaceSide.BOTH;
                    s.Styles.Add(shading);
                });
            }

            private const string BodyIdentifier = "Body";
            private IIfcGeometricRepresentationSubContext _bodyContext;

            /// <summary>The Body subcontext of the 3D model context; created when the file has none.</summary>
            private IIfcGeometricRepresentationSubContext BodyContext()
            {
                if (_bodyContext != null) return _bodyContext;
                _bodyContext = _ifc.Instances.OfType<IIfcGeometricRepresentationSubContext>()
                    .FirstOrDefault(s => string.Equals(s.ContextIdentifier, BodyIdentifier, StringComparison.OrdinalIgnoreCase) && IsModelContext(s.ParentContext));
                if (_bodyContext != null) return _bodyContext;

                var model = _ifc.Instances.OfType<IIfcGeometricRepresentationContext>().FirstOrDefault(c => !(c is IIfcGeometricRepresentationSubContext) && IsModelContext(c))
                    ?? throw new InvalidOperationException("The file has no 3D model representation context for element geometry.");
                return _bodyContext = New<IIfcGeometricRepresentationSubContext>(s =>
                {
                    s.ContextIdentifier = BodyIdentifier;
                    s.ContextType = model.ContextType;
                    s.ParentContext = model;
                    s.TargetView = IfcGeometricProjectionEnum.MODEL_VIEW;
                });
            }

            private static bool IsModelContext(IIfcGeometricRepresentationContext context) =>
                context != null && Dimension(context) == 3 && string.Equals(context.ContextType, RepresentationContext.ModelType, StringComparison.OrdinalIgnoreCase);

            /// <summary>New containment relationship; its id is derived from the structure.</summary>
            internal void Contain(IIfcSpatialElement structure, IEnumerable<IIfcProduct> elements) =>
                New<IIfcRelContainedInSpatialStructure>(r =>
                {
                    SetRelation(r, structure, "contains");
                    r.RelatingStructure = structure;
                    r.RelatedElements.AddRange(elements);
                });

            internal void Aggregate(IIfcObjectDefinition parent, IEnumerable<IIfcObjectDefinition> children)
            {
                var related = children.ToList();
                if (related.Count == 0) return;

                New<IIfcRelAggregates>(r =>
                {
                    SetRelation(r, parent, "aggregates");
                    r.RelatingObject = parent;
                    r.RelatedObjects.AddRange(related);
                });
            }

            /// <summary>Writes the object's own property and quantity sets; sets from its type are not written.</summary>
            internal void AddPropertySets(ModelObject source, IIfcObjectDefinition entity, ICollection<string> skipped = null)
            {
                foreach (var set in source.PropertySets.Where(s => !s.FromType && (skipped == null || !skipped.Contains(s.Name))))
                    DefineBy(entity, NewPropertySet(set, entity, NewItems(set)));
            }

            /// <summary>New properties or quantities of a set.</summary>
            internal IEnumerable<IPersistEntity> NewItems(PropertySetDefinition set) =>
                set is QuantitySet quantities ? quantities.Quantities.Select(NewQuantity) : ((PropertySet)set).Properties.Select(NewProperty).Cast<IPersistEntity>();

            /// <summary>New set holding <paramref name="items"/> (properties or quantities); its id is derived from the object it is written for.</summary>
            internal IIfcPropertySetDefinition NewPropertySet(PropertySetDefinition set, IIfcRoot owner, IEnumerable<IPersistEntity> items)
            {
                IIfcPropertySetDefinition entity;
                if (set is QuantitySet quantities)
                {
                    var quantitySet = New<IIfcElementQuantity>(q => q.Quantities.AddRange(items.Cast<IIfcPhysicalQuantity>()));
                    if (quantities.MethodOfMeasurement != null) quantitySet.MethodOfMeasurement = quantities.MethodOfMeasurement;
                    entity = quantitySet;
                }
                else entity = New<IIfcPropertySet>(p => p.HasProperties.AddRange(items.Cast<IIfcProperty>()));

                entity.GlobalId = Register(GlobalIds.FromSeed(owner.GlobalId + "/pset/" + set.Name), set.Name);
                entity.OwnerHistory = OwnerHistory();
                entity.Name = set.Name;
                if (set.Description != null) entity.Description = set.Description;
                return entity;
            }

            internal void DefineBy(IIfcObjectDefinition obj, IIfcPropertySetDefinition set) =>
                New<IIfcRelDefinesByProperties>(r =>
                {
                    SetRelation(r, set, "defines");
                    r.RelatingPropertyDefinition = set;
                    r.RelatedObjects.Add(obj);
                });

            internal IIfcProperty NewProperty(Property property)
            {
                switch (property.Kind)
                {
                    case PropertyKind.Single:
                        var single = New<IIfcPropertySingleValue>(p => SetProperty(p, property.Name, property.Description));
                        SetValue(single, "NominalValue", NewValue(property.ValueType, property.Value, true));
                        return single;
                    case PropertyKind.Enumerated:
                        var enumerated = New<IIfcPropertyEnumeratedValue>(p => SetProperty(p, property.Name, property.Description));
                        var values = (property.Value as IEnumerable)?.Cast<object>() ?? Enumerable.Empty<object>();
                        foreach (var value in values.Select(v => NewValue(property.ValueType, v, true)).Where(v => v != null))
                            AddValue(enumerated, "EnumerationValues", value);
                        return enumerated;
                    case PropertyKind.List:
                        var list = New<IIfcPropertyListValue>(p => SetProperty(p, property.Name, property.Description));
                        foreach (var value in ((property.Value as IEnumerable)?.Cast<object>() ?? Enumerable.Empty<object>()).Select(v => NewValue(property.ValueType, v, true)).Where(v => v != null))
                            AddValue(list, "ListValues", value);
                        return list;
                    case PropertyKind.Bounded:
                        var bounds = property.Value as BoundedValue ?? new BoundedValue(null, null);
                        var bounded = New<IIfcPropertyBoundedValue>(p => SetProperty(p, property.Name, property.Description));
                        SetValue(bounded, "LowerBoundValue", NewValue(property.ValueType, bounds.Lower, true));
                        SetValue(bounded, "UpperBoundValue", NewValue(property.ValueType, bounds.Upper, true));
                        if (bounds.SetPoint != null)
                        {
                            if (bounded.GetType().GetProperty("SetPointValue") != null) SetValue(bounded, "SetPointValue", NewValue(property.ValueType, bounds.SetPoint, true));
                            else Warn($"Set points of bounded properties are not saved ({SchemaName} has none).");
                        }
                        return bounded;
                    case PropertyKind.Table:
                        var rows = property.Value as TableValue ?? new TableValue(Enumerable.Empty<object>(), Enumerable.Empty<object>(), null);
                        var table = New<IIfcPropertyTableValue>(p => SetProperty(p, property.Name, property.Description));
                        foreach (var value in rows.DefiningValues.Select(v => NewValue(rows.DefiningValueType, v, true))) AddValue(table, "DefiningValues", value);
                        foreach (var value in rows.DefinedValues.Select(v => NewValue(property.ValueType, v, true))) AddValue(table, "DefinedValues", value);
                        return table;
                    case PropertyKind.Complex:
                        var nested = ((property.Value as IEnumerable<Property>) ?? Enumerable.Empty<Property>()).Select(NewProperty).ToList();
                        return New<IIfcComplexProperty>(p =>
                        {
                            SetProperty(p, property.Name, property.Description);
                            p.UsageName = property.UsageName ?? property.Name;
                            p.HasProperties.AddRange(nested);
                        });
                    default:
                        throw new NotSupportedException($"'{property.Name}' is a {property.Kind.ToString().ToLowerInvariant()} property; these can be read but not written.");
                }
            }

            /// <summary>A single value property written as given, without unit conversion.</summary>
            private IIfcProperty NewRawProperty(string name, string valueType, object value)
            {
                var property = New<IIfcPropertySingleValue>(p => SetProperty(p, name, null));
                SetValue(property, "NominalValue", NewValue(valueType, value, false));
                return property;
            }

            private static void SetProperty(IIfcProperty entity, string name, string description)
            {
                entity.Name = name;
                // Written to Specification in IFC4X3, which renamed IfcProperty.Description.
                if (description != null) entity.Description = description;
            }

            /// <summary>Simple value types of this schema by name, e.g. "IfcLabel".</summary>
            private static Dictionary<string, Type> ValueTypes(IModel ifc) =>
                ifc.Metadata.Types().First().Type.Assembly.GetTypes()
                    .Where(t => t.IsValueType && typeof(IExpressValueType).IsAssignableFrom(t))
                    .GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.First());

            /// <summary>
            /// A value of this schema's struct for <paramref name="valueType"/>; types the schema lacks are written as their base type
            /// (IfcLabel, IfcReal, IfcInteger, IfcBoolean or IfcLogical). Lengths, areas and volumes are converted from metres, m² and m³
            /// to the file units when <paramref name="convertUnits"/>.
            /// </summary>
            private object NewValue(string valueType, object value, bool convertUnits)
            {
                if (value == null) return null;
                if (valueType == null || !_valueTypes.TryGetValue(valueType, out var type))
                {
                    var fallback = BaseType(IfcBackend.Schema.GetValueKind(valueType) ?? KindOf(value));
                    if (valueType != null) Warn($"{valueType} values are written as {fallback} ({SchemaName} has no {valueType}).");
                    type = _valueTypes[fallback];
                }

                var underlying = ((IExpressValueType)Activator.CreateInstance(type)).UnderlyingSystemType;
                var target = Nullable.GetUnderlyingType(underlying) ?? underlying;
                object converted;
                if (target == typeof(string)) converted = Property.ValueText(value);
                else if (target == typeof(bool)) converted = value is bool b ? b : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                else if (target == typeof(double))
                {
                    var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    var dimension = convertUnits ? Units.MeasureDimension(valueType) : 0;
                    converted = dimension == 0 ? number : number * FromSi(dimension);
                }
                else converted = Convert.ChangeType(value, target, CultureInfo.InvariantCulture);

                var constructor = type.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 1 && c.GetParameters()[0].ParameterType == underlying)
                    ?? throw new NotSupportedException($"{type.Name} values cannot be written.");
                return constructor.Invoke(new[] { converted });
            }

            private static ValueKind KindOf(object value) =>
                value is bool ? ValueKind.Boolean : value is long || value is int ? ValueKind.Integer : value is double ? ValueKind.Real : ValueKind.Text;

            private static string BaseType(ValueKind kind)
            {
                switch (kind)
                {
                    case ValueKind.Boolean: return "IfcBoolean";
                    case ValueKind.Logical: return "IfcLogical";
                    case ValueKind.Integer: return "IfcInteger";
                    case ValueKind.Real: return "IfcReal";
                    default: return "IfcLabel";
                }
            }

            /// <summary>Sets a value attribute of the entity's own class, which takes this schema's value structs.</summary>
            private static void SetValue(object entity, string property, object value) => entity.GetType().GetProperty(property).SetValue(entity, value);

            private static void AddValue(object entity, string property, object value)
            {
                var list = entity.GetType().GetProperty(property).GetValue(entity);
                list.GetType().GetMethods().First(m => m.Name == "Add" && m.GetParameters().Length == 1).Invoke(list, new[] { value });
            }

            internal IIfcPhysicalQuantity NewQuantity(Quantity quantity)
            {
                var value = quantity.Value * FromSi(Quantity.Dimension(quantity.Kind));
                IIfcPhysicalSimpleQuantity entity;
                switch (quantity.Kind)
                {
                    case QuantityKind.Length: entity = New<IIfcQuantityLength>(q => q.LengthValue = new IfcLengthMeasure(value)); break;
                    case QuantityKind.Area: entity = New<IIfcQuantityArea>(q => q.AreaValue = new IfcAreaMeasure(value)); break;
                    case QuantityKind.Volume: entity = New<IIfcQuantityVolume>(q => q.VolumeValue = new IfcVolumeMeasure(value)); break;
                    case QuantityKind.Count: entity = New<IIfcQuantityCount>(q => q.CountValue = new IfcCountMeasure(value)); break;
                    case QuantityKind.Weight: entity = New<IIfcQuantityWeight>(q => q.WeightValue = new IfcMassMeasure(value)); break;
                    case QuantityKind.Time: entity = New<IIfcQuantityTime>(q => q.TimeValue = new IfcTimeMeasure(value)); break;
                    default:
                        if (ExpressType("IfcQuantityNumber") != null)
                        {
                            entity = New<IIfcPhysicalSimpleQuantity>("IfcQuantityNumber");
                            SetValue(entity, "NumberValue", NewValue("IfcNumericMeasure", value, false));
                        }
                        else
                        {
                            Warn($"Number quantities are written as count quantities ({SchemaName} has no IfcQuantityNumber).");
                            entity = New<IIfcQuantityCount>(q => q.CountValue = new IfcCountMeasure(value));
                        }
                        break;
                }
                entity.Name = quantity.Name;
                if (quantity.Description != null) entity.Description = quantity.Description;
                if (quantity.Formula != null)
                {
                    var formula = entity.GetType().GetProperty("Formula");
                    if (formula != null) formula.SetValue(entity, NewValue("IfcLabel", quantity.Formula, false));
                    else Warn($"Quantity formulas are not saved ({SchemaName} has none).");
                }
                return entity;
            }

            /// <summary>Factor from metres, m² or m³ (dimension 1 to 3) to the file units; 1 for other dimensions.</summary>
            private double FromSi(int dimension) => dimension == 1 ? _fromMetres : 1.0 / _units.ToSi(dimension);

            /// <summary>Local placement of a world placement, relative to the parent frame.</summary>
            internal IIfcLocalPlacement NewPlacement(Frame parent, Placement world) =>
                New<IIfcLocalPlacement>(p =>
                {
                    p.PlacementRelTo = parent.Ifc;
                    p.RelativePlacement = NewAxis(world.RelativeTo(parent.World));
                });

            internal IIfcAxis2Placement3D NewAxis(Placement placement) =>
                New<IIfcAxis2Placement3D>(a =>
                {
                    a.Location = NewPoint(placement.Origin.Select(v => v * _fromMetres));
                    if (placement.HasDefaultAxes) return;
                    a.Axis = NewDirection(placement.ZAxis);
                    a.RefDirection = NewDirection(placement.XAxis);
                });

            private IIfcCartesianPoint NewPoint(IEnumerable<double> coordinates) =>
                New<IIfcCartesianPoint>(p => p.Coordinates.AddRange(coordinates.Select(v => new IfcLengthMeasure(v))));

            private IIfcDirection NewDirection(IEnumerable<double> ratios) =>
                New<IIfcDirection>(d => d.DirectionRatios.AddRange(ratios.Select(v => new IfcReal(v))));

            /// <summary>
            /// Georeference of the model context: an IfcMapConversion to an IfcProjectedCRS in metres, or in IFC2X3 (which has neither)
            /// the ePSet_MapConversion and ePSet_ProjectedCRS property sets on the project.
            /// IFC scale converts model length units to map units, so the file length unit is folded into it.
            /// </summary>
            internal void AddGeoreference(Georeference georeference, IIfcProject project, IIfcGeometricRepresentationContext context, Units units)
            {
                var scale = georeference.Scale * units.Length.ToSi;
                if (_schema == IfcSchema.Ifc2x3)
                {
                    Warn("The georeference is written as the ePSet_MapConversion and ePSet_ProjectedCRS property sets of the project (IFC2X3 has no IfcMapConversion).");
                    var conversion = new List<IPersistEntity>
                    {
                        NewRawProperty("Eastings", "IfcLengthMeasure", georeference.Eastings),
                        NewRawProperty("Northings", "IfcLengthMeasure", georeference.Northings),
                        NewRawProperty("OrthogonalHeight", "IfcLengthMeasure", georeference.OrthogonalHeight),
                        NewRawProperty("XAxisAbscissa", "IfcReal", georeference.XAxisAbscissa),
                        NewRawProperty("XAxisOrdinate", "IfcReal", georeference.XAxisOrdinate),
                        NewRawProperty("Scale", "IfcReal", scale),
                    };
                    DefineBy(project, NewPropertySet(new PropertySet(MapConversionSet), project, conversion));
                    var crs = new List<IPersistEntity> { NewRawProperty("Name", "IfcLabel", georeference.CrsName), NewRawProperty("MapUnit", "IfcLabel", "METRE") };
                    foreach (var (name, value) in new[] { ("Description", georeference.CrsDescription), ("GeodeticDatum", georeference.GeodeticDatum),
                        ("VerticalDatum", georeference.VerticalDatum), ("MapProjection", georeference.MapProjection), ("MapZone", georeference.MapZone) })
                        if (value != null) crs.Add(NewRawProperty(name, "IfcLabel", value));
                    DefineBy(project, NewPropertySet(new PropertySet(ProjectedCrsSet), project, crs));
                    return;
                }

                var ifcCrs = New<IIfcProjectedCRS>(c =>
                {
                    c.Name = georeference.CrsName;
                    if (georeference.CrsDescription != null) c.Description = georeference.CrsDescription;
                    if (georeference.GeodeticDatum != null) c.GeodeticDatum = georeference.GeodeticDatum;
                    if (georeference.VerticalDatum != null) c.VerticalDatum = georeference.VerticalDatum;
                    if (georeference.MapProjection != null) c.MapProjection = georeference.MapProjection;
                    if (georeference.MapZone != null) c.MapZone = georeference.MapZone;
                    c.MapUnit = NewSIUnit(IfcUnitEnum.LENGTHUNIT, IfcSIUnitName.METRE, null);
                });
                New<IIfcMapConversion>(m =>
                {
                    m.SourceCRS = context;
                    m.TargetCRS = ifcCrs;
                    m.Eastings = new IfcLengthMeasure(georeference.Eastings);
                    m.Northings = new IfcLengthMeasure(georeference.Northings);
                    m.OrthogonalHeight = new IfcLengthMeasure(georeference.OrthogonalHeight);
                    m.XAxisAbscissa = new IfcReal(georeference.XAxisAbscissa);
                    m.XAxisOrdinate = new IfcReal(georeference.XAxisOrdinate);
                    if (Math.Abs(scale - 1.0) > 1e-12) m.Scale = new IfcReal(scale);
                });
            }

            /// <summary>Names of the IFC2X3 georeferencing property sets (buildingSMART Australasia convention).</summary>
            internal const string MapConversionSet = "ePSet_MapConversion";
            internal const string ProjectedCrsSet = "ePSet_ProjectedCRS";

            /// <summary>An IFC object placement with its world placement, used to place children relative to it.</summary>
            internal sealed class Frame
            {
                public static readonly Frame Root = new Frame(null, Placement.World);

                public IIfcObjectPlacement Ifc { get; }
                public Placement World { get; }

                /// <summary>World placement of the enclosing building, where storey elevations are measured; null outside buildings.</summary>
                public Placement ElevationBase { get; }

                public Frame(IIfcObjectPlacement ifc, Placement world, Placement elevationBase = null)
                {
                    Ifc = ifc;
                    World = world;
                    ElevationBase = elevationBase;
                }
            }

            private IIfcUnitAssignment NewUnits(Units units) =>
                New<IIfcUnitAssignment>(u =>
                {
                    u.Units.Add(NewUnit(units.Length, IfcUnitEnum.LENGTHUNIT, IfcSIUnitName.METRE, 1));
                    u.Units.Add(NewUnit(units.Area, IfcUnitEnum.AREAUNIT, IfcSIUnitName.SQUARE_METRE, 2));
                    u.Units.Add(NewUnit(units.Volume, IfcUnitEnum.VOLUMEUNIT, IfcSIUnitName.CUBIC_METRE, 3));
                    u.Units.Add(NewUnit(units.Angle, IfcUnitEnum.PLANEANGLEUNIT, IfcSIUnitName.RADIAN, 0));
                });

            /// <summary>SI units are written with their prefix; other units as IfcConversionBasedUnit with a factor to the SI unit.</summary>
            private IIfcNamedUnit NewUnit(UnitDefinition unit, IfcUnitEnum type, IfcSIUnitName siName, int lengthExponent)
            {
                if (unit.IsSi) return NewSIUnit(type, siName, unit.SiPrefix);

                return New<IIfcConversionBasedUnit>(c =>
                {
                    c.UnitType = type;
                    c.Name = unit.ConversionName;
                    c.Dimensions = New<IIfcDimensionalExponents>(d => d.LengthExponent = lengthExponent);
                    c.ConversionFactor = New<IIfcMeasureWithUnit>(m =>
                    {
                        m.ValueComponent = NewMeasure(unit.Kind, unit.ToSi);
                        m.UnitComponent = NewSIUnit(type, siName, null);
                    });
                });
            }

            private static IIfcValue NewMeasure(UnitKind kind, double value)
            {
                switch (kind)
                {
                    case UnitKind.Length: return new IfcLengthMeasure(value);
                    case UnitKind.Area: return new IfcAreaMeasure(value);
                    case UnitKind.Volume: return new IfcVolumeMeasure(value);
                    default: return new IfcPlaneAngleMeasure(value);
                }
            }

            private IIfcSIUnit NewSIUnit(IfcUnitEnum type, IfcSIUnitName name, string prefix) =>
                New<IIfcSIUnit>(u =>
                {
                    u.UnitType = type;
                    u.Name = name;
                    if (prefix != null) u.Prefix = Enum.Parse<IfcSIPrefix>(prefix);
                });

            private IIfcOwnerHistory _ownerHistory;

            /// <summary>Owner history of new rooted entities in IFC2X3, where it is mandatory: the file's first one, else a new one. Null in later schemas.</summary>
            private IIfcOwnerHistory OwnerHistory()
            {
                if (_schema != IfcSchema.Ifc2x3) return null;
                if (_ownerHistory != null) return _ownerHistory;
                _ownerHistory = _ifc.Instances.OfType<IIfcOwnerHistory>().FirstOrDefault();
                if (_ownerHistory != null) return _ownerHistory;

                var person = New<IIfcPerson>(p => p.FamilyName = string.IsNullOrWhiteSpace(Author) ? "Unknown" : Author);
                var organization = New<IIfcOrganization>(o => o.Name = string.IsNullOrWhiteSpace(Organization) ? "Unknown" : Organization);
                var developer = New<IIfcOrganization>(o => o.Name = OriginatingSystem);
                return _ownerHistory = New<IIfcOwnerHistory>(h =>
                {
                    h.OwningUser = New<IIfcPersonAndOrganization>(po =>
                    {
                        po.ThePerson = person;
                        po.TheOrganization = organization;
                    });
                    h.OwningApplication = New<IIfcApplication>(a =>
                    {
                        a.ApplicationDeveloper = developer;
                        a.Version = typeof(Builder).Assembly.GetName().Version?.ToString(2) ?? "1.0";
                        a.ApplicationFullName = OriginatingSystem;
                        a.ApplicationIdentifier = OriginatingSystem;
                    });
                    h.ChangeAction = IfcChangeActionEnum.ADDED;
                    h.CreationDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                });
            }

            private T NewSpatial<T>(string ifcClass, ModelObject source, Frame parent, Placement world) where T : class, IIfcSpatialStructureElement =>
                New<T>(ifcClass, s =>
                {
                    SetRoot(s, source);
                    s.CompositionType = IfcElementCompositionEnum.ELEMENT;
                    s.ObjectPlacement = NewPlacement(parent, world);
                });

            /// <summary>New entity of the class named like the interface (IIfcWall: IfcWall) in this schema.</summary>
            private T New<T>(Action<T> init = null) where T : class, IPersistEntity => New(typeof(T).Name.Substring(1), init);

            /// <summary>New entity of <paramref name="ifcClass"/> in this schema, seen through the IFC4 interface <typeparamref name="T"/>.</summary>
            private T New<T>(string ifcClass, Action<T> init = null) where T : class, IPersistEntity
            {
                var type = ExpressType(ifcClass) ?? throw new NotSupportedException($"{ifcClass} cannot be written to an {SchemaName} file.");
                var entity = (T)_ifc.Instances.New(type);
                init?.Invoke(entity);
                return entity;
            }

            /// <summary>The instantiable class of this schema with the given IFC name, or null.</summary>
            private Type ExpressType(string ifcClass)
            {
                var type = _ifc.Metadata.ExpressType(ifcClass.ToUpperInvariant());
                return type == null || type.Type.IsAbstract ? null : type.Type;
            }

            private void SetRoot(IIfcRoot entity, ModelObject source)
            {
                entity.GlobalId = Register(string.IsNullOrWhiteSpace(source.GlobalId) ? GlobalIds.New() : source.GlobalId, source.Name);
                entity.OwnerHistory = OwnerHistory();
                entity.Name = source.Name;
                if (source.Description != null) entity.Description = source.Description;
            }

            /// <summary>Relationship ids are derived from their parent, so they stay stable when the parent id is stable.</summary>
            private void SetRelation(IIfcRoot relation, IIfcRoot parent, string kind)
            {
                // A file edited in place may still hold the id, e.g. from a relationship that was emptied and deleted.
                var globalId = GlobalIds.FromSeed(parent.GlobalId + "/" + kind);
                relation.GlobalId = Register(_globalIds.Contains(globalId) ? GlobalIds.New() : globalId, kind);
                relation.OwnerHistory = OwnerHistory();
            }

            private string Register(string globalId, string owner)
            {
                if (!GlobalIds.IsValid(globalId))
                    throw new ArgumentException($"'{globalId}' of '{owner}' is not a valid IFC GlobalId.");
                if (!_globalIds.Add(globalId))
                    throw new ArgumentException($"GlobalId '{globalId}' of '{owner}' is used more than once; is the same object connected twice?");
                return globalId;
            }
        }
    }
}
