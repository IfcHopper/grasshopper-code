using System;
using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.IO;

namespace IfcHopper.Core.Model
{
    /// <summary>
    /// Type of elements (IfcElementType subtype, or an IFC2X3 door or window style), shared by its occurrences through IfcRelDefinesByType.
    /// Its geometry (IfcRepresentationMap) is in the type's own coordinates, like a Rhino block definition; occurrences place it with
    /// <see cref="Element.TypeTransform"/>. Types read from a file are shared by every element that uses them.
    /// </summary>
    public class ElementType : TypedModelObject
    {
        /// <summary>IFC entity name, e.g. "IfcWallType".</summary>
        public string IfcClass { get; set; }

        /// <summary>Predefined types of <see cref="IfcClass"/>, from the active backend's schema.</summary>
        public override IReadOnlyList<string> PredefinedTypes => IfcBackend.Schema.GetPredefinedTypes(IfcClass);

        private LazyList<MeshGeometry> _geometry = new LazyList<MeshGeometry>();
        /// <summary>
        /// Body geometry of the representation maps in type coordinates (metres). Written back, a changed geometry changes every
        /// instance of the type.
        /// </summary>
        public List<MeshGeometry> Geometry => _geometry.Value;
        /// <summary>True when the geometry was accessed or edited, so it must be compared with the file on write back.</summary>
        internal bool GeometryLoaded => _geometry.IsLoaded || _geometryEdited;
        private bool _geometryEdited;

        /// <summary>
        /// True for an edited copy of a type (Modify Element Type). Edited types read from a file are written back onto the file's type,
        /// which changes it for all its occurrences.
        /// </summary>
        public bool Edited { get; private set; }

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

        private Lazy<MaterialDefinition> _material = new Lazy<MaterialDefinition>(() => null);

        /// <summary>Material of the type (IfcRelAssociatesMaterial); null when there is none.</summary>
        public MaterialDefinition Material => _material.Value;

        /// <summary>Reads <see cref="Material"/> on first access (models read from a file).</summary>
        internal void LoadMaterial(Func<MaterialDefinition> loader) => _material = new Lazy<MaterialDefinition>(loader);

        /// <summary>True when the material was accessed or set, so it must be compared with the file on write back.</summary>
        internal bool MaterialLoaded => _material.IsValueCreated || _materialSet;
        private bool _materialSet;

        /// <summary>Gives the type a material (IfcRelAssociatesMaterial), shown by its occurrences without one of their own; null removes it.</summary>
        public void SetMaterial(MaterialDefinition material)
        {
            _material = new Lazy<MaterialDefinition>(() => material);
            _materialSet = true;
        }

        /// <summary>Colour to show a mesh of this type in: its own style, else the colour of the type's material.</summary>
        public Colour DisplayColour(MeshGeometry mesh) => mesh.Colour ?? Material?.DisplayColour;

        /// <summary>Fills <see cref="Geometry"/> on first access (models read from a file); the loader reports skipped items.</summary>
        internal void LoadGeometry(Func<ICollection<string>, IEnumerable<MeshGeometry>> loader) => _geometry.SetLoader(() => loader(_skippedGeometry));

        /// <summary>Replaces the geometry; on write back the Body representation maps of the source are replaced.</summary>
        public void ReplaceGeometry(IEnumerable<MeshGeometry> meshes)
        {
            _geometry = new LazyList<MeshGeometry>();
            _geometry.Value.AddRange(meshes);
            _skippedGeometry = new List<string>();
            _geometryEdited = true;
        }

        /// <summary>Gives every mesh <paramref name="colour"/> (null removes colours).</summary>
        public void SetColour(Colour colour)
        {
            var source = _geometry;
            _geometry = new LazyList<MeshGeometry>();
            _geometry.SetLoader(() => source.Value.Select(m => m.WithColour(colour)));
            _geometryEdited = true;
        }

        protected override void CopyChildren()
        {
            _geometry = LazyList<MeshGeometry>.CopyOf(_geometry);
            var attributes = _attributes;
            _attributes = new Lazy<Dictionary<string, object>>(() => new Dictionary<string, object>(attributes.Value));
            Edited = true;
        }

        /// <summary>Identifier of the type (IfcTypeProduct.Tag); null when not set.</summary>
        public string Tag { get; set; }

        private Lazy<Dictionary<string, object>> _attributes = new Lazy<Dictionary<string, object>>(() => new Dictionary<string, object>());

        /// <summary>
        /// Attributes of <see cref="IfcClass"/> beyond those modelled here, e.g. OperationType of IfcDoorType (see ISchemaInfo.GetAttributes):
        /// text, double (lengths in metres, areas in m², volumes in m³), long, bool, or the name of an enumeration value.
        /// </summary>
        public Dictionary<string, object> Attributes => _attributes.Value;
        internal bool AttributesLoaded => _attributes.IsValueCreated;

        /// <summary>Reads <see cref="Attributes"/> on first access (models read from a file).</summary>
        internal void LoadAttributes(Func<Dictionary<string, object>> loader) => _attributes = new Lazy<Dictionary<string, object>>(loader);

        public const string DefaultName = "Hopper Type";

        public ElementType(string name, string ifcClass) : base(name)
        {
            IfcClass = ifcClass;
        }
    }
}
