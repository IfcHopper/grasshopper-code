using System;
using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.Geometry;
using IfcHopper.Core.Model;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.MeasureResource;
using Xbim.IO.Memory;
using Builder = IfcHopper.Core.Backends.Xbim.XbimIfcWriter.Builder;
using Frame = IfcHopper.Core.Backends.Xbim.XbimIfcWriter.Builder.Frame;

namespace IfcHopper.Core.Backends.Xbim
{
    /// <summary>
    /// Writes edits of a model read from a file onto a fresh copy of that file, so everything IfcHopper does not map is kept.
    /// Objects are matched by GlobalId and compared with their state in the source file (the baseline): only differences are written.
    /// Child lists that were never loaded are unchanged. A loaded list replaces the file's children of the same kinds:
    /// missing ones are deleted with their subtree, new ones are created.
    /// </summary>
    internal class XbimInPlaceEditor
    {
        private const double LengthTolerance = 1e-6;
        private const double DirectionTolerance = 1e-9;

        private readonly MemoryModel _ifc;
        private readonly double _toMetres;
        private readonly Dictionary<string, IIfcRoot> _byGlobalId;
        private readonly Dictionary<IModel, XbimModelLoader> _loaders = new Dictionary<IModel, XbimModelLoader>();
        private Builder _builder;

        /// <summary>File units, for property and quantity values in areas and volumes.</summary>
        private readonly Units _units;
        private readonly string _author, _organization;

        public XbimInPlaceEditor(MemoryModel ifc, string author = null, string organization = null)
        {
            _ifc = ifc;
            _toMetres = ifc.ModelFactors.LengthToMetresConversionFactor;
            _byGlobalId = ifc.Instances.OfType<IIfcRoot>().GroupBy(r => (string)r.GlobalId).ToDictionary(g => g.Key, g => g.First());
            var project = ifc.Instances.FirstOrDefault<IIfcProject>();
            _units = project == null ? null : Loader(project).ReadUnits(project);
            _author = author;
            _organization = organization;
        }

        /// <summary>Creates new entities in the schema of the file, downgrading what it does not support (see <see cref="Warnings"/>).</summary>
        private Builder Builder => _builder ?? (_builder = new Builder(_ifc, 1.0 / _toMetres, _byGlobalId.Keys, _units)
        {
            Author = _author,
            Organization = _organization,
            Existing = id => _byGlobalId.TryGetValue(id, out var entity) ? entity : null,
            ExistingSource = InFile,
        });

        /// <summary>The cached model the edited objects were read from; its entities have the labels of this copy.</summary>
        private IModel _sourceModel;

        /// <summary>This copy's entity for an entity of the source model (e.g. a material, which has no GlobalId); null for other sources.</summary>
        private IPersistEntity InFile(object source) =>
            source is IPersistEntity entity && entity.Model == _sourceModel ? _ifc.Instances[entity.EntityLabel] : null;

        private readonly List<string> _warnings = new List<string>();

        /// <summary>What was downgraded or not saved because the schema of the file does not support it, and edits that were not applied.</summary>
        public IReadOnlyList<string> Warnings => _warnings.Concat(_builder?.Warnings ?? new List<string>()).ToList();

        /// <param name="types">Edited types of the model (Apply Edits); edited types of loaded elements are applied as well.</param>
        /// <param name="materials">Edited materials of the model (Apply Edits); edited materials of loaded elements and of edited types are applied as well.</param>
        public void Apply(Project project, IEnumerable<ElementType> types = null, IEnumerable<MaterialDefinition> materials = null)
        {
            var entity = Find(project) as IIfcProject ?? throw new InvalidOperationException("The project was not found in its source file.");
            _sourceModel = (project.Source as IPersistEntity)?.Model;
            var baseline = Loader(project.Source as IIfcProject ?? entity).LoadProject(project.Source as IIfcProject ?? entity);

            ApplyRoot(project, entity, baseline);
            if (project.PropertySetsLoaded) ApplyPropertySets(project, entity, baseline);
            if (project.ClassificationsLoaded) ApplyClassifications(project, entity, baseline);
            if (project.Units != null) CheckUnits(project.Units, baseline.Units);
            if (project.ContextsLoaded) ApplyContexts(project, entity, baseline.Contexts);
            if (!SameGeoreference(project.Georeference, baseline.Georeference)) ReplaceGeoreference(project, entity);

            // Types first, so new and changed instances map the edited representation maps.
            var elements = LoadedDescendants(project).OfType<Element>().ToList();
            var editedTypes = (types ?? Enumerable.Empty<ElementType>()).Concat(elements.Select(e => e.Type).Where(t => t != null && t.Edited)).ToList();
            ApplyTypes(editedTypes);
            var usedMaterials = elements.Where(e => e.MaterialLoaded).Select(e => e.OwnMaterial).Concat(editedTypes.Where(t => t.MaterialLoaded).Select(t => t.Material));
            ApplyMaterialEdits((materials ?? Enumerable.Empty<MaterialDefinition>()).Concat(usedMaterials).Where(m => m != null && m.Edited));
            ApplyChildren(project, entity, new Scope(null, Placement.World, Placement.World, null));
            _builder?.FinishTypes();
            _builder?.FinishMaterials();
            _builder?.FinishClassifications();
            _builder?.FinishOpenings();
        }

        /// <summary>
        /// Matches the object's own references with the file's by value: references no longer wanted leave their relationship (deleted when
        /// empty), new ones are associated. Classifications and references stay in the file, like a library. References from the type are ignored.
        /// </summary>
        private void ApplyClassifications(ModelObject obj, IIfcObjectDefinition entity, ModelObject baseline)
        {
            var current = baseline.Classifications.Where(c => !c.FromType).ToList();
            var added = new List<ClassificationReference>();
            foreach (var reference in obj.Classifications.Where(c => !c.FromType))
            {
                var index = current.FindIndex(c => c.SameAs(reference));
                if (index >= 0) current.RemoveAt(index);
                else added.Add(reference);
            }
            foreach (var old in current)
            {
                // Baselines of objects without a source are read from this copy.
                var select = old.Source is IPersistEntity own && own.Model == _ifc ? own : InFile(old.Source);
                foreach (var rel in entity.HasAssociations.OfType<IIfcRelAssociatesClassification>().Where(r => r.RelatingClassification == select).ToList())
                {
                    rel.RelatedObjects.Remove(entity);
                    if (!rel.RelatedObjects.Any()) Delete(rel);
                }
            }
            if (added.Count > 0) Builder.Classify(entity, added);
        }

        /// <summary>
        /// Writes edited materials read from this file onto the file's materials (the first edit of each): names, descriptions and, for single
        /// materials, category and colour. Every element using them changes.
        /// </summary>
        private void ApplyMaterialEdits(IEnumerable<MaterialDefinition> materials)
        {
            var ifc2x3 = _ifc.SchemaVersion == XbimSchemaVersion.Ifc2X3;
            foreach (var definition in materials.GroupBy(m => m.Source).Select(g => g.First()))
            {
                switch (definition, InFile(definition.Source))
                {
                    case (Material material, IIfcMaterial entity):
                        if (material.Name != entity.Name) entity.Name = material.Name ?? "";
                        if (!ifc2x3 && material.Description != entity.Description) entity.Description = material.Description == null ? (IfcText?)null : new IfcText(material.Description);
                        if (!ifc2x3 && material.Category != entity.Category) entity.Category = material.Category == null ? (IfcLabel?)null : new IfcLabel(material.Category);
                        if (Equals(material.Colour, XbimMaterials.ColourOf(entity))) break;
                        foreach (var representation in entity.HasRepresentation.ToList())
                        {
                            foreach (var styled in representation.Representations.ToList()) DeleteRepresentation(styled);
                            Delete(representation);
                        }
                        if (material.Colour != null) Builder.AddMaterialColour(entity, material.Colour);
                        break;
                    case (MaterialLayerSet set, IIfcMaterialLayerSet entity):
                        if (set.Name != entity.LayerSetName) entity.LayerSetName = set.Name == null ? (IfcLabel?)null : new IfcLabel(set.Name);
                        if (!ifc2x3 && set.Description != entity.Description) entity.Description = set.Description == null ? (IfcText?)null : new IfcText(set.Description);
                        break;
                    case (MaterialConstituentSet set, IIfcMaterialConstituentSet entity):
                        if (set.Name != entity.Name) entity.Name = set.Name == null ? (IfcLabel?)null : new IfcLabel(set.Name);
                        if (set.Description != entity.Description) entity.Description = set.Description == null ? (IfcText?)null : new IfcText(set.Description);
                        break;
                    case (MaterialProfileSet set, IIfcMaterialProfileSet entity):
                        if (set.Name != entity.Name) entity.Name = set.Name == null ? (IfcLabel?)null : new IfcLabel(set.Name);
                        if (set.Description != entity.Description) entity.Description = set.Description == null ? (IfcText?)null : new IfcText(set.Description);
                        break;
                    case (_, null):
                        _warnings.Add($"Material '{definition.Name}' was not found in the file and is not edited.");
                        break;
                }
                if (InFile(definition.Source) is IIfcMaterialDefinition target && definition.Source is IIfcMaterialDefinition source &&
                    !definition.SamePropertySets(Loader(source).LoadMaterialProperties(source).ToList()))
                    ReplaceMaterialProperties(target, definition.PropertySets);
            }
        }

        /// <summary>
        /// Associates an object or type with its new own material (the file's material it was read from, or a new one), removing its old
        /// association; relationships and layer set usages left unused are deleted.
        /// </summary>
        private void ApplyMaterial(MaterialDefinition material, IIfcObjectDefinition entity, MaterialDefinition baseline)
        {
            if (material == null ? baseline == null : baseline != null && InFile(material.Source) is IPersistEntity wanted && wanted == InFile(baseline.Source)) return;

            foreach (var rel in entity.HasAssociations.OfType<IIfcRelAssociatesMaterial>().ToList())
            {
                rel.RelatedObjects.Remove(entity);
                if (!rel.RelatedObjects.Any()) DeleteMaterialRelation(rel);
            }
            Builder.AssociateMaterial(entity, material);
        }

        /// <summary>
        /// Writes a changed tag and the attributes that differ from the file's (<paramref name="attributes"/> null when they were never loaded);
        /// attributes left out are cleared.
        /// </summary>
        private void ApplyAttributes(IPersistEntity entity, string tag, string baselineTag, IReadOnlyDictionary<string, object> attributes,
            IReadOnlyDictionary<string, object> baseline)
        {
            if (tag != baselineTag)
            {
                if (entity is IIfcElement element) element.Tag = tag == null ? (IfcIdentifier?)null : new IfcIdentifier(tag);
                else if (entity is IIfcTypeProduct type) type.Tag = tag == null ? (IfcLabel?)null : new IfcLabel(tag);
            }
            if (attributes == null) return;
            foreach (var name in attributes.Keys.Union(baseline.Keys).ToList())
            {
                attributes.TryGetValue(name, out var value);
                baseline.TryGetValue(name, out var old);
                if (!PropertySetDefinition.SameValue(value, old)) Builder.SetAttribute(entity, name, value);
            }
        }

        /// <summary>Deletes a material relationship, and its usage when nothing else uses it; materials and sets stay in the file, like types.</summary>
        private void DeleteMaterialRelation(IIfcRelAssociatesMaterial rel)
        {
            var select = rel.RelatingMaterial;
            Delete(rel);
            if ((select is IIfcMaterialLayerSetUsage || select is IIfcMaterialProfileSetUsage) && !_ifc.Instances.OfType<IIfcRelAssociatesMaterial>().Any(r => r.RelatingMaterial == select))
                Delete((IPersistEntity)select);
        }

        private static IEnumerable<ModelObject> LoadedDescendants(ModelObject parent) =>
            parent.ChildrenLoaded ? parent.Children.SelectMany(c => new[] { c }.Concat(LoadedDescendants(c))) : Enumerable.Empty<ModelObject>();

        /// <summary>
        /// Writes edited types read from this file onto the file's types (the first edit of each GlobalId): name, description, predefined
        /// type, own property sets and, when changed, the Body representation map, which changes every instance of the type.
        /// </summary>
        private void ApplyTypes(IEnumerable<ElementType> types)
        {
            foreach (var type in types.Where(t => t.Source is IIfcTypeProduct && t.GlobalId != null).GroupBy(t => t.GlobalId).Select(g => g.First()))
            {
                if (!(Find(type) is IIfcTypeProduct entity))
                {
                    _warnings.Add($"Type '{type.Name}' ({type.GlobalId}) was not found in the file and is not edited.");
                    continue;
                }
                if (!string.Equals(type.IfcClass, entity.ExpressType.Name, StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException($"Type '{type.Name}' changes class ({entity.ExpressType.Name} to {type.IfcClass}); this is not supported in place.");

                var source = (IIfcTypeProduct)type.Source;
                var baseline = Loader(source).LoadType(source);
                ApplyRoot(type, entity, baseline);
                if (type.PropertySetsLoaded) ApplyPropertySets(type, entity, baseline);
                if (type.ClassificationsLoaded) ApplyClassifications(type, entity, baseline);
                if (type.MaterialLoaded) ApplyMaterial(type.Material, entity, baseline.Material);
                ApplyAttributes(entity, type.Tag, baseline.Tag, type.AttributesLoaded ? type.Attributes : null, baseline.Attributes);
                if (type.GeometryLoaded && !SameGeometry(type.Geometry, Placement.World, baseline.Geometry, Placement.World)) ReplaceTypeGeometry(type, entity);
            }
        }

        /// <summary>
        /// Puts the type's geometry in its first Body map (keeping the map, which its instances refer to); further Body maps are deleted
        /// with the mapped items using them. A type without geometry loses its Body maps; a type without maps gets one.
        /// </summary>
        private void ReplaceTypeGeometry(ElementType type, IIfcTypeProduct entity)
        {
            var maps = XbimGeometryReader.BodyMaps(entity).ToList();
            if (maps.Count == 0)
            {
                if (type.Geometry.Count > 0) entity.RepresentationMaps.Add(Builder.NewRepresentationMap(type.Geometry));
                return;
            }

            foreach (var map in maps.Skip(type.Geometry.Count > 0 ? 1 : 0)) DeleteMap(map);
            if (type.Geometry.Count == 0) return;

            var first = maps[0];
            var axis = XbimGeometryReader.Axis(first.MappingOrigin);
            var origin = new Placement(axis.Origin.Select(v => v * _toMetres).ToArray(), axis.X, axis.Z);
            var old = first.MappedRepresentation;
            first.MappedRepresentation = Builder.NewBodyRepresentation(type.Geometry, origin);
            if (!_ifc.Instances.OfType<IIfcRepresentationMap>().Any(m => m.MappedRepresentation == old)) DeleteRepresentation(old);
        }

        /// <summary>Deletes a representation map, its representation and the mapped items using it.</summary>
        private void DeleteMap(IIfcRepresentationMap map)
        {
            foreach (var item in _ifc.Instances.OfType<IIfcMappedItem>().Where(i => i.MappingSource == map).ToList())
            {
                foreach (var style in item.StyledByItem.ToList()) Delete(style);
                Delete(item);
            }
            var representation = map.MappedRepresentation;
            Delete(map);
            DeleteRepresentation(representation);
        }

        /// <summary>Applies an object onto its entity in the file, then its children.</summary>
        private void ApplyObject(ModelObject obj, IIfcObjectDefinition entity, Scope parent)
        {
            var ifcClass = SpatialRules.IfcClassOf(obj);
            if (!string.Equals(ifcClass, entity.ExpressType.Name, StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"'{obj.Name}' changes class ({entity.ExpressType.Name} to {ifcClass}); this is not supported in place.");
            var source = obj.Source as IIfcObjectDefinition ?? entity;
            var baseline = Loader(source).LoadObject(source);
            ApplyRoot(obj, entity, baseline);
            if (obj.PropertySetsLoaded) ApplyPropertySets(obj, entity, baseline);
            if (obj.ClassificationsLoaded) ApplyClassifications(obj, entity, baseline);

            var product = (IIfcProduct)entity;
            Scope scope;
            switch (obj)
            {
                case Storey storey:
                    scope = ApplyStorey(storey, (IIfcBuildingStorey)entity, (Storey)baseline, parent);
                    break;
                case FacilityPart part:
                    if (part.Usage != ((FacilityPart)baseline).Usage) SetEnum(entity, "UsageType", part.Usage.ToString().ToUpperInvariant());
                    scope = ApplyPlacement(obj, product, baseline, parent, null);
                    break;
                default:
                    scope = ApplyPlacement(obj, product, baseline, parent, null);
                    break;
            }
            if (obj is Element element)
            {
                ApplyElementType(element, (IIfcElement)entity, (Element)baseline);
                if (element.MaterialLoaded) ApplyMaterial(element.OwnMaterial, entity, ((Element)baseline).OwnMaterial);
                ApplyAttributes(entity, element.Tag, ((Element)baseline).Tag, element.AttributesLoaded ? element.Attributes : null, ((Element)baseline).Attributes);
                if (element.GeometryLoaded) ApplyGeometry(element, (IIfcElement)entity, (Element)baseline);
                if (element.OpeningsLoaded) ApplyOpenings(element, (IIfcElement)entity, scope);
                if (element is Opening opening && opening.FillsLoaded && entity is IIfcOpeningElement ifcOpening) ApplyFills(opening, ifcOpening);
            }
            ApplyChildren(obj, entity, scope);
        }

        /// <summary>
        /// Matches the element's openings with those voiding it in the file by GlobalId: matches are applied like objects (placed relative to the
        /// element), missing ones are deleted and new ones written.
        /// </summary>
        private void ApplyOpenings(Element element, IIfcElement entity, Scope scope) =>
            Diff(entity.HasOpenings.Select(r => (IIfcObjectDefinition)r.RelatedOpeningElement).Where(o => o != null), element.Openings,
                (opening, e) =>
                {
                    PlaceOnHost(opening, (IIfcProduct)e, entity, scope);
                    ApplyObject(opening, e, scope);
                },
                opening => Builder.AddOpening(element, entity, opening, scope.Frame),
                created => { });

        /// <summary>
        /// When the host moves, an opening the file places relative to something else (e.g. the storey) is placed relative to the host at its
        /// old world position, so it moves with the host like openings placed relative to it.
        /// </summary>
        private void PlaceOnHost(Opening opening, IIfcProduct entity, IIfcProduct host, Scope scope)
        {
            if (!(entity.ObjectPlacement is IIfcLocalPlacement local) || host.ObjectPlacement == null || local.PlacementRelTo == host.ObjectPlacement ||
                SamePlacement(scope.OldWorld, scope.NewWorld))
                return;
            var source = opening.Source as IIfcObjectDefinition ?? entity;
            var oldWorld = Loader(source).LoadObject(source).Placement;
            if (oldWorld == null) return;
            local.PlacementRelTo = host.ObjectPlacement;
            local.RelativePlacement = Builder.NewAxis(oldWorld.RelativeTo(scope.OldWorld));
        }

        /// <summary>Matches the opening's fills with the file's by GlobalId: relationships to elements no longer filling it are deleted, new fills related.</summary>
        private void ApplyFills(Opening opening, IIfcOpeningElement entity)
        {
            var added = opening.Fills.ToList();
            foreach (var rel in entity.HasFillings.ToList())
            {
                var kept = added.FindIndex(f => f.GlobalId != null && f.GlobalId == rel.RelatedBuildingElement?.GlobalId);
                if (kept >= 0) added.RemoveAt(kept);
                else Delete(rel);
            }
            foreach (var fill in added) Builder.Fill(entity, opening, fill);
        }

        /// <summary>Relates the element to its new type (the file's type with its GlobalId, or a new one), removing it from the old one.</summary>
        private void ApplyElementType(Element element, IIfcElement entity, Element baseline)
        {
            var type = element.Type;
            if (type?.GlobalId != null && type.GlobalId == baseline.Type?.GlobalId || type == null && baseline.Type == null) return;

            foreach (var rel in entity.IsTypedBy.ToList())
            {
                rel.RelatedObjects.Remove(entity);
                if (!rel.RelatedObjects.Any()) Delete(rel);
            }
            Builder.RelateToType(entity, type);
        }

        /// <summary>
        /// Matches loaded children with the entity's mapped children by GlobalId: matches are applied, missing ones deleted and new ones
        /// created (spatial objects and element parts aggregated, elements in spatial objects contained). Unmapped children are kept.
        /// </summary>
        private void ApplyChildren(ModelObject obj, IIfcObjectDefinition entity, Scope scope)
        {
            if (!obj.ChildrenLoaded) return;
            Diff(XbimModelLoader.MappedChildren(entity), obj.Children,
                (child, e) => ApplyObject(child, e, scope),
                child => Builder.AddChild(obj, child, scope.Frame),
                created =>
                {
                    if (entity is IIfcSpatialElement spatial)
                    {
                        AttachAggregated(entity, created.Where(c => !(c is IIfcElement)).ToList());
                        AttachContained(spatial, created.OfType<IIfcElement>().ToList());
                    }
                    else AttachAggregated(entity, created);
                });
        }

        /// <summary>A new elevation moves the storey by the difference, unless it is also given an explicit placement.</summary>
        private Scope ApplyStorey(Storey storey, IIfcBuildingStorey entity, Storey baseline, Scope parent)
        {
            Placement target = null;
            var delta = storey.Elevation - baseline.Elevation;
            if (Math.Abs(delta) > LengthTolerance)
            {
                entity.Elevation = storey.Elevation / _toMetres;
                var current = Moved(storey.Placement, baseline.Placement) ? storey.Placement : Unmoved(baseline, parent);
                target = new Placement(new[] { current.Origin[0], current.Origin[1], current.Origin[2] + delta }, current.XAxis, current.ZAxis);
            }
            return ApplyPlacement(storey, entity, baseline, parent, target);
        }

        /// <summary>
        /// Replaces the Body representation when the geometry differs from the file's, compared relative to the element placement
        /// (geometry moved with its element is unchanged). Unchanged geometry keeps the original items, including skipped ones; so does an
        /// instance still placing the same type in the same way, also when the type's geometry is edited. Instances of a type get mapped items.
        /// </summary>
        private void ApplyGeometry(Element element, IIfcElement entity, Element baseline)
        {
            var placement = element.Placement ?? baseline.Placement ?? Placement.World;
            var baselinePlacement = baseline.Placement ?? Placement.World;
            if (element.TypeTransform != null && baseline.TypeTransform != null && element.Type?.GlobalId != null && element.Type.GlobalId == baseline.Type?.GlobalId &&
                SameTransform(element.TypeTransform, placement, baseline.TypeTransform, baselinePlacement))
                return;
            if (SameGeometry(element.Geometry, placement, baseline.Geometry, baselinePlacement)) return;

            var product = (IIfcProduct)entity;
            var oldShape = product.Representation;
            var bodies = XbimGeometryReader.BodyRepresentations(entity).Cast<IIfcRepresentation>().ToList();
            var kept = new List<IIfcRepresentation>();

            // A shape shared with other products is left to them and this element gets its own Body only,
            // since a representation belongs to one product shape.
            if (oldShape != null && !(oldShape is IIfcProductDefinitionShape shared && shared.ShapeOfProduct.Count() > 1))
            {
                kept.AddRange(oldShape.Representations.Where(r => !bodies.Contains(r)));
                foreach (var body in bodies) DeleteRepresentation(body);
                Delete(oldShape);
            }

            var newBody = Builder.NewInstanceBody(element, placement) ?? (element.Geometry.Count > 0 ? Builder.NewBodyRepresentation(element.Geometry, placement) : null);
            if (newBody != null) kept.Add(newBody);
            product.Representation = kept.Count == 0 ? null : Builder.NewShape(kept);
        }

        /// <summary>True when two type transforms are the same relative to their element placements.</summary>
        private static bool SameTransform(Affine a, Placement placement, Affine b, Placement baselinePlacement)
        {
            double[] Local(Affine t, Placement p, double[] axis) => p.PointToLocal(t.Origin.Zip(axis, (o, v) => o + v).ToArray());
            return Close(placement.PointToLocal(a.Origin), baselinePlacement.PointToLocal(b.Origin), LengthTolerance) &&
                new[] { (a.X, b.X), (a.Y, b.Y), (a.Z, b.Z) }.All(axes => Close(Local(a, placement, axes.Item1), Local(b, baselinePlacement, axes.Item2), LengthTolerance));
        }

        /// <summary>Deletes a representation with its items, their styles and the point lists and faces of face sets (deeper data is left).</summary>
        private void DeleteRepresentation(IIfcRepresentation representation)
        {
            foreach (var item in representation.Items.ToList())
            {
                foreach (var style in item.StyledByItem.ToList()) Delete(style);
                if (item is IIfcTessellatedFaceSet faceSet) Delete(faceSet.Coordinates);
                if (item is IIfcPolygonalFaceSet polygonal) foreach (var face in polygonal.Faces.ToList()) Delete(face);
                Delete(item);
            }
            Delete(representation);
        }

        private static bool SameGeometry(List<MeshGeometry> meshes, Placement placement, List<MeshGeometry> baseline, Placement baselinePlacement)
        {
            if (meshes.Count != baseline.Count) return false;
            for (int i = 0; i < meshes.Count; i++)
            {
                MeshGeometry a = meshes[i], b = baseline[i];
                if (a.IsClosed != b.IsClosed || !Equals(a.Colour, b.Colour) || a.Vertices.Count != b.Vertices.Count || a.Faces.Count != b.Faces.Count) return false;
                if (!a.Faces.Zip(b.Faces, (f, g) => f.SequenceEqual(g)).All(same => same)) return false;
                for (int v = 0; v < a.Vertices.Count; v++)
                    if (!Close(placement.PointToLocal(a.Vertices[v]), baselinePlacement.PointToLocal(b.Vertices[v]), LengthTolerance)) return false;
            }
            return true;
        }

        /// <summary>
        /// Matches the wanted objects with the file's children by GlobalId: matches are edited, missing children are deleted
        /// with their subtree and new objects are created and attached.
        /// </summary>
        private void Diff<T, TCreated>(IEnumerable<IIfcObjectDefinition> current, List<T> wanted,
            Action<T, IIfcObjectDefinition> applyExisting, Func<T, TCreated> create, Action<List<TCreated>> attach) where T : ModelObject
        {
            var existing = current.ToDictionary(e => (string)e.GlobalId);
            var created = new List<TCreated>();
            foreach (var item in wanted)
            {
                if (item.GlobalId != null && existing.TryGetValue(item.GlobalId, out var entity))
                {
                    existing.Remove(item.GlobalId);
                    applyExisting(item, entity);
                }
                else if (item.GlobalId != null && _byGlobalId.ContainsKey(item.GlobalId))
                    throw new NotSupportedException($"'{item.Name}' ({item.GlobalId}) is used twice or moved to another parent; moving objects in place is not supported yet.");
                else
                    created.Add(create(item));
            }
            foreach (var entity in existing.Values) DeleteSubtree(entity);
            if (created.Count > 0) attach(created);
        }

        private void AttachAggregated(IIfcObjectDefinition parent, List<IIfcObjectDefinition> children)
        {
            if (children.Count == 0) return;
            var rel = parent.IsDecomposedBy.FirstOrDefault();
            if (rel != null) foreach (var child in children) rel.RelatedObjects.Add(child);
            else Builder.Aggregate(parent, children);
        }

        private void AttachContained(IIfcSpatialElement structure, List<IIfcElement> elements)
        {
            if (elements.Count == 0) return;
            var rel = structure.ContainsElements.FirstOrDefault();
            if (rel != null) foreach (var element in elements) rel.RelatedElements.Add(element);
            else Builder.Contain(structure, elements);
        }

        /// <summary>Deletes an object with everything aggregated or contained in it, their placements and shapes, and emptied relationships.</summary>
        private void DeleteSubtree(IIfcObjectDefinition root)
        {
            var doomed = new List<IPersistEntity>();
            Collect(root, doomed);
            var doomedSet = new HashSet<IPersistEntity>(doomed);

            foreach (var entity in doomed)
            {
                if (entity is IIfcObjectPlacement placement &&
                    _ifc.Instances.OfType<IIfcLocalPlacement>().Any(l => l.PlacementRelTo == placement && !doomedSet.Contains(l)))
                    continue;
                if (entity is IIfcRoot rootEntity) _byGlobalId.Remove(rootEntity.GlobalId);
                Delete(entity);
            }

            foreach (var rel in _ifc.Instances.OfType<IIfcRelAggregates>().Where(r => r.RelatingObject == null || !r.RelatedObjects.Any()).ToList())
                Delete(rel);
            foreach (var rel in _ifc.Instances.OfType<IIfcRelContainedInSpatialStructure>().Where(r => r.RelatingStructure == null || !r.RelatedElements.Any()).ToList())
                Delete(rel);
            foreach (var rel in _ifc.Instances.OfType<IIfcRelDefinesByProperties>().Where(r => !r.RelatedObjects.Any()).ToList())
                Delete(rel);
            // Types stay in the file without occurrences, like a type library.
            foreach (var rel in _ifc.Instances.OfType<IIfcRelDefinesByType>().Where(r => !r.RelatedObjects.Any()).ToList())
                Delete(rel);
            foreach (var rel in _ifc.Instances.OfType<IIfcRelAssociatesMaterial>().Where(r => !r.RelatedObjects.Any()).ToList())
                DeleteMaterialRelation(rel);
            foreach (var rel in _ifc.Instances.OfType<IIfcRelAssociatesClassification>().Where(r => !r.RelatedObjects.Any()).ToList())
                Delete(rel);
            foreach (var rel in _ifc.Instances.OfType<IIfcRelVoidsElement>().Where(r => r.RelatingBuildingElement == null || r.RelatedOpeningElement == null).ToList())
                Delete(rel);
            foreach (var rel in _ifc.Instances.OfType<IIfcRelFillsElement>().Where(r => r.RelatingOpeningElement == null || r.RelatedBuildingElement == null).ToList())
                Delete(rel);
        }

        /// <summary>xBIM's delete uses a static cache of referencing types that is not thread-safe, so deletes are serialised.</summary>
        private static readonly object DeleteLock = new object();

        private void Delete(IPersistEntity entity)
        {
            lock (DeleteLock) _ifc.Delete(entity);
        }

        private static void Collect(IIfcObjectDefinition entity, List<IPersistEntity> doomed)
        {
            if (doomed.Contains(entity)) return;
            doomed.Add(entity);
            if (entity is IIfcProduct product)
            {
                if (product.ObjectPlacement != null) doomed.Add(product.ObjectPlacement);
                if (product.Representation != null) doomed.Add(product.Representation);
            }
            foreach (var child in XbimModelLoader.Children(entity)) Collect(child, doomed);
            if (entity is IIfcElement host)
                foreach (var opening in host.HasOpenings.Select(r => r.RelatedOpeningElement).Where(o => o != null).ToList()) Collect(opening, doomed);
            if (entity is IIfcSpatialElement spatial)
                foreach (var element in spatial.ContainsElements.SelectMany(r => r.RelatedElements)) Collect(element, doomed);
        }

        private static void ApplyRoot(ModelObject obj, IIfcRoot entity, ModelObject baseline)
        {
            if (obj.Name != baseline.Name) entity.Name = obj.Name == null ? (IfcLabel?)null : new IfcLabel(obj.Name);
            if (obj.Description != baseline.Description) entity.Description = obj.Description == null ? (IfcText?)null : new IfcText(obj.Description);

            if (obj is TypedModelObject typed && baseline is TypedModelObject typedBaseline &&
                (typed.PredefinedType != typedBaseline.PredefinedType || typed.ObjectType != typedBaseline.ObjectType))
            {
                var userDefined = typed.ObjectType == null ? (IfcLabel?)null : new IfcLabel(typed.ObjectType);
                switch (entity)
                {
                    case IIfcObject ifcObject:
                        SetEnum(entity, "PredefinedType", typed.PredefinedType);
                        ifcObject.ObjectType = userDefined;
                        break;
                    case IIfcTypeProduct _:
                        // Mandatory on most type classes.
                        SetEnum(entity, "PredefinedType", typed.PredefinedType ?? "NOTDEFINED");
                        if (entity is IIfcElementType elementType) elementType.ElementType = userDefined;
                        break;
                }
            }
        }

        /// <summary>Sets an enum attribute that only exists on some (schema-specific) entity classes; null clears it.</summary>
        private static void SetEnum(object entity, string property, string value)
        {
            var info = entity.GetType().GetProperty(property);
            if (info == null) return;
            var enumType = Nullable.GetUnderlyingType(info.PropertyType) ?? info.PropertyType;
            info.SetValue(entity, value == null ? null : Enum.Parse(enumType, value));
        }

        /// <summary>
        /// Writes an explicitly changed placement (or <paramref name="target"/>) relative to the parent, and returns the scope for children.
        /// Unchanged placements keep their position relative to the parent, so they move with it.
        /// </summary>
        private Scope ApplyPlacement(ModelObject obj, IIfcProduct entity, ModelObject baseline, Scope parent, Placement target)
        {
            target = target ?? (Moved(obj.Placement, baseline.Placement) ? obj.Placement : null);
            if (target == null) return ChildScope(obj, entity, baseline.Placement ?? parent.OldWorld, Unmoved(baseline, parent), parent);

            if (!(entity.ObjectPlacement is IIfcLocalPlacement local))
                throw new NotSupportedException($"'{obj.Name}' has no local placement and cannot be moved in place.");
            // Keep the placement entity: children placements refer to it.
            local.RelativePlacement = Builder.NewAxis(target.RelativeTo(parent.NewWorld));
            return ChildScope(obj, entity, baseline.Placement ?? parent.OldWorld, target, parent);
        }

        /// <summary>Scope of an object's children; buildings start a new base for storey elevations.</summary>
        private static Scope ChildScope(ModelObject obj, IIfcProduct entity, Placement oldWorld, Placement newWorld, Scope parent) =>
            new Scope(entity, oldWorld, newWorld, obj is Facility facility && facility.Type == FacilityType.Building ? newWorld : parent.ElevationBase);

        private static Placement Unmoved(ModelObject baseline, Scope parent) =>
            baseline.Placement == null ? parent.NewWorld : parent.NewWorld.Compose(baseline.Placement.RelativeTo(parent.OldWorld));

        private static bool Moved(Placement wanted, Placement baseline) =>
            wanted != null && (baseline == null || !SamePlacement(wanted, baseline));

        private static bool SamePlacement(Placement a, Placement b) =>
            Close(a.Origin, b.Origin, LengthTolerance) && Close(a.XAxis, b.XAxis, DirectionTolerance) && Close(a.ZAxis, b.ZAxis, DirectionTolerance);

        private static bool Close(double[] a, double[] b, double tolerance) =>
            a.Length == b.Length && a.Zip(b, (x, y) => Math.Abs(x - y) <= tolerance).All(c => c);

        private static void CheckUnits(Units wanted, Units baseline)
        {
            if (wanted.Length != baseline.Length || wanted.Area != baseline.Area || wanted.Volume != baseline.Volume || wanted.Angle != baseline.Angle)
                throw new NotSupportedException("Changing the units of a file in place is not supported; build a new project to write different units.");
        }

        /// <summary>New contexts are added; changing or removing existing contexts is not supported in place.</summary>
        private void ApplyContexts(Project project, IIfcProject entity, List<RepresentationContext> baseline)
        {
            var unmatched = baseline.ToList();
            foreach (var context in project.Contexts)
            {
                var match = unmatched.FirstOrDefault(c => c.ContextType == context.ContextType && c.Dimension == context.Dimension);
                if (match == null)
                {
                    entity.RepresentationContexts.Add(Builder.AddContext(context));
                    continue;
                }
                unmatched.Remove(match);
                if (!SameContext(context, match))
                    throw new NotSupportedException($"Changing the '{context.ContextType}' context in place is not supported yet.");
            }
            if (unmatched.Count > 0)
                throw new NotSupportedException($"Removing the '{unmatched[0].ContextType}' context in place is not supported.");
        }

        private static bool SameContext(RepresentationContext a, RepresentationContext b) =>
            Math.Abs(a.Precision - b.Precision) <= 1e-12 &&
            (a.TrueNorth == null ? b.TrueNorth == null : b.TrueNorth != null && Close(a.TrueNorth, b.TrueNorth, DirectionTolerance)) &&
            a.SubContexts.Count == b.SubContexts.Count &&
            a.SubContexts.Zip(b.SubContexts, (x, y) => x.Identifier == y.Identifier && x.TargetView == y.TargetView &&
                x.UserDefinedTargetView == y.UserDefinedTargetView && x.TargetScale == y.TargetScale).All(s => s);

        private static bool SameGeoreference(Georeference a, Georeference b)
        {
            if (a == null || b == null) return a == b;
            return a.CrsName == b.CrsName && a.CrsDescription == b.CrsDescription && a.GeodeticDatum == b.GeodeticDatum &&
                   a.VerticalDatum == b.VerticalDatum && a.MapProjection == b.MapProjection && a.MapZone == b.MapZone &&
                   Math.Abs(a.Eastings - b.Eastings) <= LengthTolerance && Math.Abs(a.Northings - b.Northings) <= LengthTolerance &&
                   Math.Abs(a.OrthogonalHeight - b.OrthogonalHeight) <= LengthTolerance &&
                   Math.Abs(a.XAxisAbscissa - b.XAxisAbscissa) <= DirectionTolerance && Math.Abs(a.XAxisOrdinate - b.XAxisOrdinate) <= DirectionTolerance &&
                   Math.Abs(a.Scale - b.Scale) <= 1e-12;
        }

        private void ReplaceGeoreference(Project project, IIfcProject entity)
        {
            foreach (var set in XbimPropertySets.Definitions(entity).Where(s => s.Name == Builder.MapConversionSet || s.Name == Builder.ProjectedCrsSet).ToList())
                Detach(entity, set);
            var contexts = entity.RepresentationContexts.OfType<IIfcGeometricRepresentationContext>().Where(c => !(c is IIfcGeometricRepresentationSubContext)).ToList();
            foreach (var conversion in contexts.SelectMany(c => c.HasCoordinateOperation).OfType<IIfcMapConversion>().ToList())
            {
                var crs = conversion.TargetCRS;
                Delete(conversion);
                if (crs != null && !crs.HasCoordinateOperation.Any()) Delete(crs);
            }
            if (project.Georeference == null) return;

            var context = contexts.FirstOrDefault(c => c.CoordinateSpaceDimension == 3)
                ?? throw new InvalidOperationException("The file has no 3D context to georeference.");
            Builder.AddGeoreference(project.Georeference, entity, context, project.Units ?? new Units());
        }

        /// <summary>
        /// Matches the object's own sets with the file's by name: unchanged sets are kept, changed ones are edited (or copied for this
        /// object when other objects or types share them), missing ones are detached and new ones created. Sets from the type are ignored.
        /// </summary>
        private void ApplyPropertySets(ModelObject obj, IIfcObjectDefinition entity, ModelObject baseline)
        {
            var current = baseline.PropertySets.Where(s => !s.FromType).GroupBy(s => s.Name).ToDictionary(g => g.Key, g => g.First());
            foreach (var set in obj.PropertySets.Where(s => !s.FromType))
            {
                if (set.Name != null && current.TryGetValue(set.Name, out var old))
                {
                    current.Remove(set.Name);
                    if (!set.SameAs(old)) ReplacePropertySet(entity, set, old);
                }
                else Attach(entity, Builder.NewPropertySet(set, entity, Builder.NewItems(set)));
            }
            foreach (var old in current.Values) Detach(entity, FindSet(old));
        }

        /// <summary>Gives the object a set: in a type's HasPropertySets, else by a new IfcRelDefinesByProperties.</summary>
        private void Attach(IIfcObjectDefinition entity, IIfcPropertySetDefinition set)
        {
            if (entity is IIfcTypeObject type) type.HasPropertySets.Add(set);
            else Builder.DefineBy(entity, set);
        }

        private void ReplacePropertySet(IIfcObjectDefinition entity, PropertySetDefinition set, PropertySetDefinition old)
        {
            var ifcSet = FindSet(old);
            var existing = Items(ifcSet).ToList();
            var items = Items(set).Select(item =>
            {
                var oldItem = Items(old).FirstOrDefault(o => ItemName(o) == ItemName(item));
                var reused = oldItem != null && SameItem(item, oldItem) ? existing.FirstOrDefault(e => EntityName(e) == ItemName(item)) : null;
                return reused ?? (IPersistEntity)(item is Property property ? Builder.NewProperty(property) : (IPersistEntity)Builder.NewQuantity((Quantity)item));
            }).ToList();

            var users = ifcSet.DefinesOccurrence.SelectMany(r => r.RelatedObjects).Cast<IPersistEntity>().Concat(ifcSet.DefinesType);
            if (users.Any(u => u != entity))
            {
                Detach(entity, ifcSet);
                Attach(entity, Builder.NewPropertySet(set, entity, items));
                return;
            }

            if (set.Description != old.Description) ifcSet.Description = set.Description == null ? (IfcText?)null : new IfcText(set.Description);
            switch (ifcSet)
            {
                case IIfcPropertySet properties:
                    properties.HasProperties.Clear();
                    properties.HasProperties.AddRange(items.Cast<IIfcProperty>());
                    break;
                case IIfcElementQuantity quantities:
                    var method = ((QuantitySet)set).MethodOfMeasurement;
                    if (method != ((QuantitySet)old).MethodOfMeasurement) quantities.MethodOfMeasurement = method == null ? (IfcLabel?)null : new IfcLabel(method);
                    quantities.Quantities.Clear();
                    quantities.Quantities.AddRange(items.Cast<IIfcPhysicalQuantity>());
                    break;
            }
            foreach (var item in existing.Where(e => !items.Contains(e))) DeleteUnused(item);
        }

        private static IEnumerable<object> Items(PropertySetDefinition set) =>
            set is PropertySet properties ? properties.Properties.Cast<object>() : ((QuantitySet)set).Quantities;

        private static IEnumerable<IPersistEntity> Items(IIfcPropertySetDefinition set)
        {
            switch (set)
            {
                case IIfcPropertySet properties: return properties.HasProperties.Cast<IPersistEntity>();
                case IIfcElementQuantity quantities: return quantities.Quantities.Cast<IPersistEntity>();
                default: return Enumerable.Empty<IPersistEntity>();
            }
        }

        private static string ItemName(object item) => item is Property property ? property.Name : ((Quantity)item).Name;

        private static string EntityName(IPersistEntity entity) => entity is IIfcProperty property ? (string)property.Name : ((IIfcPhysicalQuantity)entity).Name;

        private static bool SameItem(object a, object b) =>
            a is Property property ? b is Property other && property.SameAs(other) : b is Quantity quantity && ((Quantity)a).SameAs(quantity);

        /// <summary>Removes the object from the relationships defining it by the set; the set is deleted when nothing uses it any more.</summary>
        private void Detach(IIfcObjectDefinition entity, IIfcPropertySetDefinition set)
        {
            if (entity is IIfcTypeObject type) type.HasPropertySets.Remove(set);
            foreach (var rel in XbimPropertySets.Relations(entity).Where(r => XbimPropertySets.Definitions(r.RelatingPropertyDefinition).Contains(set)).ToList())
            {
                if (!(rel.RelatingPropertyDefinition is IIfcPropertySetDefinition))
                    throw new NotSupportedException($"'{set.Name}' of '{entity.Name}' is defined together with other sets (IfcPropertySetDefinitionSet); editing it in place is not supported.");
                rel.RelatedObjects.Remove(entity);
                if (!rel.RelatedObjects.Any()) Delete(rel);
            }
            if (set.DefinesOccurrence.Any() || set.DefinesType.Any()) return;
            var items = Items(set).ToList();
            Delete(set);
            foreach (var item in items) DeleteUnused(item);
        }

        /// <summary>Deletes a property or quantity that no set uses any more.</summary>
        private void DeleteUnused(IPersistEntity item)
        {
            var used = item is IIfcProperty property
                ? property.PartOfPset.Any() || property.PartOfComplex.Any() || _ifc.Instances.OfType<IIfcExtendedProperties>().Any(e => e.Properties.Contains(property))
                : _ifc.Instances.OfType<IIfcElementQuantity>().Any(q => q.Quantities.Contains(item));
            if (!used) Delete(item);
        }

        /// <summary>Puts new material property sets (IfcMaterialProperties) in place of a material's, deleting the old ones and their unused properties.</summary>
        private void ReplaceMaterialProperties(IIfcMaterialDefinition material, IEnumerable<PropertySet> sets)
        {
            foreach (var old in material.HasProperties.ToList())
            {
                var properties = old.Properties.Cast<IPersistEntity>().ToList();
                Delete(old);
                foreach (var property in properties) DeleteUnused(property);
            }
            Builder.AddMaterialProperties(material, sets);
        }

        /// <summary>The copy of a set read from the source file.</summary>
        private IIfcPropertySetDefinition FindSet(PropertySetDefinition set) =>
            set.Source is IIfcRoot source && _byGlobalId.TryGetValue(source.GlobalId, out var entity) && entity is IIfcPropertySetDefinition definition
                ? definition
                : throw new InvalidOperationException($"Property set '{set.Name}' was not found in the source file.");

        private IIfcRoot Find(ModelObject obj) =>
            obj.GlobalId != null && _byGlobalId.TryGetValue(obj.GlobalId, out var entity) ? entity : null;

        /// <summary>Loader of the model a baseline entity belongs to (the cached source, or this copy for objects without a source).</summary>
        private XbimModelLoader Loader(IPersistEntity entity)
        {
            if (!_loaders.TryGetValue(entity.Model, out var loader))
                _loaders[entity.Model] = loader = new XbimModelLoader(entity.Model);
            return loader;
        }

        /// <summary>Parent of objects being applied: its entity and its world placement before and after the edits.</summary>
        private sealed class Scope
        {
            private readonly IIfcProduct _entity;

            public Placement OldWorld { get; }
            public Placement NewWorld { get; }

            /// <summary>World placement of the enclosing building after the edits; null outside buildings.</summary>
            public Placement ElevationBase { get; }

            public Scope(IIfcProduct entity, Placement oldWorld, Placement newWorld, Placement elevationBase)
            {
                _entity = entity;
                OldWorld = oldWorld;
                NewWorld = newWorld;
                ElevationBase = elevationBase;
            }

            /// <summary>Frame for new children (IFC4X3 files only).</summary>
            public Frame Frame => _entity == null ? Frame.Root : new Frame(_entity.ObjectPlacement, NewWorld, ElevationBase);
        }
    }
}
