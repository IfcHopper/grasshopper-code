using System;
using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.Geometry;
using IfcHopper.Core.IO;

namespace IfcHopper.Core.Model
{
    public class Element : TypedModelObject
    {
        public const string DefaultIfcClass = "IfcBuildingElementProxy";
        public const string DefaultName = "Hopper Object";

        /// <summary>IFC entity name, e.g. "IfcWall". Must be an instantiable IfcElement subclass.</summary>
        public string IfcClass { get; set; }

        /// <summary>Predefined types of <see cref="IfcClass"/>, from the active backend's schema.</summary>
        public override IReadOnlyList<string> PredefinedTypes => IfcBackend.Schema.GetPredefinedTypes(IfcClass);

        /// <summary>Identifier of the element, e.g. a mark or the id of the authoring tool (IfcElement.Tag); null when not set.</summary>
        public string Tag { get; set; }

        private Lazy<Dictionary<string, object>> _attributes = new Lazy<Dictionary<string, object>>(() => new Dictionary<string, object>());

        /// <summary>
        /// Attributes of <see cref="IfcClass"/> beyond those modelled here, e.g. OverallHeight of IfcDoor (see ISchemaInfo.GetAttributes):
        /// text, double (lengths in metres, areas in m², volumes in m³), long, bool, or the name of an enumeration value.
        /// </summary>
        public Dictionary<string, object> Attributes => _attributes.Value;
        internal bool AttributesLoaded => _attributes.IsValueCreated;

        /// <summary>Reads <see cref="Attributes"/> on first access (models read from a file).</summary>
        internal void LoadAttributes(Func<Dictionary<string, object>> loader) => _attributes = new Lazy<Dictionary<string, object>>(loader);

        /// <summary>Elements this one is made of (IfcRelAggregates), e.g. the parts of an IfcElementAssembly.</summary>
        public ChildView<Element> Parts => new ChildView<Element>(this);

        private LazyList<Opening> _openings = new LazyList<Opening>();

        /// <summary>Openings and recesses voiding the element (IfcRelVoidsElement); the element's geometry is stored without them.</summary>
        public List<Opening> Openings => _openings.Value;
        internal bool OpeningsLoaded => _openings.IsLoaded;

        /// <summary>Fills <see cref="Openings"/> on first access (models read from a file).</summary>
        internal void LoadOpenings(Func<IEnumerable<Opening>> loader) => _openings.SetLoader(loader);

        private LazyList<MeshGeometry> _geometry = new LazyList<MeshGeometry>();
        /// <summary>Body geometry in world coordinates (metres). Written relative to the element placement.</summary>
        public List<MeshGeometry> Geometry => _geometry.Value;
        /// <summary>True when the geometry was accessed or edited, so it must be compared with the file on write back.</summary>
        internal bool GeometryLoaded => _geometry.IsLoaded || _geometryEdited;
        private bool _geometryEdited;

        private List<string> _skippedGeometry = new List<string>();
        /// <summary>IFC classes of representation items that could not be faceted when <see cref="Geometry"/> was read from a file.</summary>
        public IReadOnlyList<string> SkippedGeometry
        {
            get
            {
                _ = Geometry;
                return _skippedGeometry;
            }
        }

        private Lazy<MaterialDefinition> _ownMaterial = new Lazy<MaterialDefinition>(() => null);

        /// <summary>The element's own material (IfcRelAssociatesMaterial); null when it has none.</summary>
        public MaterialDefinition OwnMaterial => _ownMaterial.Value;

        /// <summary>Material of the element, or of its type when the element has none of its own; null when there is none.</summary>
        public MaterialDefinition Material => OwnMaterial ?? Type?.Material;

        /// <summary>True when <see cref="Material"/> is inherited from the element's type.</summary>
        public bool MaterialFromType => OwnMaterial == null && Material != null;

        /// <summary>Reads <see cref="OwnMaterial"/> on first access (models read from a file).</summary>
        internal void LoadMaterial(Func<MaterialDefinition> loader) => _ownMaterial = new Lazy<MaterialDefinition>(loader);

        /// <summary>True when the own material was accessed or set, so it must be compared with the file on write back.</summary>
        internal bool MaterialLoaded => _ownMaterial.IsValueCreated || _materialSet;
        private bool _materialSet;

        /// <summary>
        /// Gives the element its own material (IfcRelAssociatesMaterial); null removes it, so the element shows its type's material.
        /// Layer sets are written with a layer set usage (defaults: along the thickness of slabs, plates, coverings and roofs, across that
        /// of other elements).
        /// </summary>
        public void SetMaterial(MaterialDefinition material)
        {
            _ownMaterial = new Lazy<MaterialDefinition>(() => material);
            _materialSet = true;
        }

        private Lazy<ElementType> _type = new Lazy<ElementType>(() => null);

        /// <summary>Type of the element (IfcRelDefinesByType); null when it has none.</summary>
        public ElementType Type => _type.Value;

        /// <summary>Reads <see cref="Type"/> on first access (models read from a file).</summary>
        internal void LoadType(Func<ElementType> loader) => _type = new Lazy<ElementType>(loader);

        private Lazy<Affine> _typeTransform = new Lazy<Affine>(() => null);

        /// <summary>
        /// Transform from the coordinates of <see cref="Type"/> to world (metres) when the geometry is the type's geometry placed once
        /// (IfcMappedItem of its representation maps), like a block instance; null otherwise.
        /// </summary>
        public Affine TypeTransform => _typeTransform.Value;

        /// <summary>Reads <see cref="TypeTransform"/> on first access (models read from a file).</summary>
        internal void LoadTypeTransform(Func<Affine> loader) => _typeTransform = new Lazy<Affine>(loader);

        /// <summary>Gives the element a type (null removes it); the element keeps its own geometry, so it is no instance of the type.</summary>
        public void AssignType(ElementType type)
        {
            _type = new Lazy<ElementType>(() => type);
            _typeTransform = new Lazy<Affine>(() => null);
        }

        /// <summary>
        /// Makes the element an instance of <paramref name="type"/>, like a block instance: its geometry becomes the type's geometry placed by
        /// <paramref name="transform"/> (type coordinates to world, metres), written as mapped items of the type's representation maps.
        /// </summary>
        public void PlaceType(ElementType type, Affine transform)
        {
            _type = new Lazy<ElementType>(() => type);
            _typeTransform = new Lazy<Affine>(() => transform);
            _geometry = new LazyList<MeshGeometry>();
            _geometry.SetLoader(() => type.Geometry.Select(m => Placed(m, transform)));
            _skippedGeometry = new List<string>();
            _geometryEdited = true;
        }

        /// <summary>A mesh moved by <paramref name="transform"/>, its faces reversed when the transform mirrors.</summary>
        private static MeshGeometry Placed(MeshGeometry mesh, Affine transform)
        {
            var placed = mesh.Transform(transform.Apply);
            return transform.IsMirrored ? placed.Flip() : placed;
        }

        /// <summary>Colour to show a mesh of this element in: its own style, else the colour of the element's material.</summary>
        public Colour DisplayColour(MeshGeometry mesh) => mesh.Colour ?? Material?.DisplayColour;

        /// <summary>Fills <see cref="Geometry"/> on first access (models read from a file); the loader reports skipped items.</summary>
        internal void LoadGeometry(Func<ICollection<string>, IEnumerable<MeshGeometry>> loader)
        {
            var skipped = _skippedGeometry;
            _geometry.SetLoader(() => loader(skipped));
        }

        /// <summary>Replaces the geometry; on write back the Body representation of the source is replaced (unless it is unchanged).</summary>
        public void ReplaceGeometry(IEnumerable<MeshGeometry> meshes)
        {
            _geometry = new LazyList<MeshGeometry>();
            _geometry.Value.AddRange(meshes);
            _skippedGeometry = new List<string>();
            _geometryEdited = true;
            _typeTransform = new Lazy<Affine>(() => null);
        }

        /// <summary>
        /// Moves the geometry and the openings rigidly from <paramref name="from"/> to <paramref name="to"/>, so they keep their position relative
        /// to the placement. Geometry and openings not yet read from the file stay unread until accessed.
        /// </summary>
        public void MoveGeometry(Placement from, Placement to)
        {
            Func<double[], double[]> move = p => to.PointToWorld(from.PointToLocal(p));
            MapGeometry(m => m.Transform(move));
            var transform = _typeTransform;
            _typeTransform = new Lazy<Affine>(() => transform.Value == null ? null : Moved(transform.Value, move));

            var openings = _openings;
            _openings = new LazyList<Opening>();
            _openings.SetLoader(() => openings.Value.Select(o =>
            {
                var moved = (Opening)o.Copy();
                if (moved.Placement != null) moved.Placement = to.Compose(moved.Placement.RelativeTo(from));
                moved.MoveGeometry(from, to);
                return moved;
            }));
        }

        /// <summary>The transform followed by the rigid <paramref name="move"/>.</summary>
        private static Affine Moved(Affine transform, Func<double[], double[]> move)
        {
            var origin = move(transform.Origin);
            double[] Axis(double[] axis) => move(transform.Origin.Zip(axis, (o, a) => o + a).ToArray()).Zip(origin, (p, o) => p - o).ToArray();
            return new Affine(origin, Axis(transform.X), Axis(transform.Y), Axis(transform.Z));
        }

        /// <summary>
        /// Gives every mesh <paramref name="colour"/> (null removes colours). Geometry not yet read stays unread until accessed.
        /// The geometry is then no longer the type's geometry as it is.
        /// </summary>
        public void SetColour(Colour colour)
        {
            MapGeometry(m => m.WithColour(colour));
            _typeTransform = new Lazy<Affine>(() => null);
        }

        private void MapGeometry(Func<MeshGeometry, MeshGeometry> map)
        {
            var source = _geometry;
            _geometry = new LazyList<MeshGeometry>();
            _geometry.SetLoader(() => source.Value.Select(map));
            _geometryEdited = true;
        }

        public Element(string name = DefaultName, string ifcClass = DefaultIfcClass) : base(name)
        {
            IfcClass = ifcClass;
        }

        protected override void CopyChildren()
        {
            _geometry = LazyList<MeshGeometry>.CopyOf(_geometry);
            _openings = LazyList<Opening>.CopyOf(_openings);
            var attributes = _attributes;
            _attributes = new Lazy<Dictionary<string, object>>(() => new Dictionary<string, object>(attributes.Value));
        }
    }
}
