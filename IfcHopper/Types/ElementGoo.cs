using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using IfcHopper.Components;
using IfcHopper.Core.Model;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace IfcHopper.Types
{
    /// <summary>Element wrapper that previews and bakes its body geometry (loaded from the file on first draw).</summary>
    public class ElementGoo : IfcHopperGoo<Element>, IGH_PreviewData, IGH_BakeAwareData
    {
        private Mesh[] _meshes;

        public ElementGoo() { }

        public ElementGoo(Element value) : base(value) { }

        public override string TypeName => "IFC Element";

        public override string TypeDescription => "IfcHopper element (IfcElement subtype).";

        public override IGH_Goo Duplicate() => new ElementGoo(Value);

        public override string ToString() => Value == null ? "Null Element" : $"{Value.IfcClass}: {Value.Name}";

        /// <summary>Display colour of each mesh: its own style, else the material colour of its element; null when neither is styled.</summary>
        private Colour[] _colours;

        /// <summary>
        /// Body meshes of the element and of its parts (recursively) with their openings subtracted, in document units; empty when there is
        /// none or it cannot be read.
        /// </summary>
        private Mesh[] Meshes
        {
            get
            {
                if (_meshes != null) return _meshes;
                try
                {
                    var body = Value == null ? new List<(Mesh Mesh, Colour Colour)>() : WithParts(Value).SelectMany(e => DocumentOpenings.CutBody(e, out _)).ToList();
                    _colours = body.Select(b => b.Colour).ToArray();
                    _meshes = body.Select(b => b.Mesh).ToArray();
                }
                catch (Exception)
                {
                    _colours = Array.Empty<Colour>();
                    _meshes = Array.Empty<Mesh>();
                }
                return _meshes;
            }
        }

        private static IEnumerable<Element> WithParts(Element element) => new[] { element }.Concat(element.Parts.SelectMany(WithParts));

        /// <summary>All body meshes joined, or null without geometry.</summary>
        private Mesh JoinedMesh()
        {
            if (Meshes.Length == 0) return null;
            var joined = new Mesh();
            joined.Append(Meshes);
            return joined;
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(GH_Mesh)))
            {
                var mesh = JoinedMesh();
                if (mesh == null) return false;
                target = (Q)(object)new GH_Mesh(mesh);
                return true;
            }
            return base.CastTo(ref target);
        }

        public BoundingBox ClippingBox
        {
            get
            {
                var box = BoundingBox.Empty;
                foreach (var mesh in Meshes) box.Union(mesh.GetBoundingBox(false));
                return box;
            }
        }

        public void DrawViewportMeshes(GH_PreviewMeshArgs args)
        {
            foreach (var mesh in Meshes) args.Pipeline.DrawMeshShaded(mesh, args.Material);
        }

        private DisplayMaterial[] _materials;

        /// <summary>Draws each mesh in its IFC colour when <paramref name="useColours"/>, otherwise (and for uncoloured meshes) with <paramref name="material"/>.</summary>
        internal void DrawShaded(DisplayPipeline pipeline, DisplayMaterial material, bool useColours)
        {
            var meshes = Meshes;
            if (meshes.Length == 0) return;
            _materials = _materials ?? _colours.Select(c => c == null ? null
                : new DisplayMaterial(DocumentGeometry.ToColor(c), c.Transparency)).ToArray();
            for (int i = 0; i < meshes.Length; i++)
                pipeline.DrawMeshShaded(meshes[i], useColours && i < _materials.Length ? _materials[i] ?? material : material);
        }

        public void DrawViewportWires(GH_PreviewWireArgs args)
        {
            if (!Grasshopper.CentralSettings.PreviewMeshEdges) return;
            foreach (var mesh in Meshes) args.Pipeline.DrawMeshWires(mesh, args.Color, args.Thickness);
        }

        public bool BakeGeometry(RhinoDoc doc, ObjectAttributes att, out Guid obj_guid) => BakeGeometry(doc, att, true, out obj_guid);

        /// <summary>
        /// Bakes the element named after it, with its IFC class and GlobalId as user text. When <paramref name="asBlock"/> and its geometry is
        /// its type's geometry placed once (and it has no parts or openings), it is baked as an instance of the type's block definition, else as
        /// one mesh object with its parts and its openings subtracted.
        /// </summary>
        internal bool BakeGeometry(RhinoDoc doc, ObjectAttributes att, bool asBlock, out Guid obj_guid)
        {
            obj_guid = Guid.Empty;
            var attributes = att?.Duplicate() ?? doc.CreateDefaultAttributes();
            attributes.Name = Value.Name;
            attributes.SetUserString(DocumentBlocks.IfcClassKey, Value.IfcClass ?? "");
            if (!string.IsNullOrEmpty(Value.GlobalId)) attributes.SetUserString(DocumentBlocks.GlobalIdKey, Value.GlobalId);

            if (asBlock && Value.Type != null && Value.TypeTransform != null && Value.Parts.Count == 0 && Value.Openings.Count == 0)
            {
                var definition = DocumentBlocks.Definition(doc, Value.Type);
                if (definition >= 0)
                {
                    SetColour(attributes, Value.Material?.DisplayColour);
                    obj_guid = doc.Objects.AddInstanceObject(definition, DocumentBlocks.ToTransform(Value.TypeTransform), attributes);
                    return obj_guid != Guid.Empty;
                }
            }

            var mesh = JoinedMesh();
            if (mesh == null) return false;
            SetColour(attributes, _colours.FirstOrDefault(c => c != null));
            obj_guid = doc.Objects.AddMesh(mesh, attributes);
            return obj_guid != Guid.Empty;
        }

        private static void SetColour(ObjectAttributes attributes, Colour colour)
        {
            if (colour == null) return;
            attributes.ObjectColor = DocumentGeometry.ToColor(colour);
            attributes.ColorSource = ObjectColorSource.ColorFromObject;
        }
    }
}
