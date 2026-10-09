using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel.Types;
using Grasshopper.Rhinoceros.Model;
using IfcHopper.Core.Geometry;
using IfcHopper.Core.Model;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace IfcHopper.Components
{
    /// <summary>Maps element types to Rhino block definitions and their occurrences to block instances (document units).</summary>
    internal static class DocumentBlocks
    {
        public const string IfcClassKey = "IfcClass";
        public const string GlobalIdKey = "GlobalId";

        /// <summary>
        /// Index of the block definition of <paramref name="type"/>: the definition whose GlobalId user text matches, else a new one
        /// named after the type (with a number when the name is taken, never replacing a block). -1 when the type has no geometry
        /// or the definition cannot be added.
        /// </summary>
        public static int Definition(RhinoDoc doc, ElementType type)
        {
            if (!string.IsNullOrEmpty(type.GlobalId))
                foreach (var existing in doc.InstanceDefinitions)
                    if (existing != null && !existing.IsDeleted && existing.GetUserString(GlobalIdKey) == type.GlobalId) return existing.Index;
            if (type.Geometry.Count == 0) return -1;

            var geometry = new List<GeometryBase>();
            var attributes = new List<ObjectAttributes>();
            foreach (var mesh in type.Geometry)
            {
                // Meshes without a style of their own take the colour of the instance, which carries the element's material colour.
                var attribute = new ObjectAttributes { ColorSource = ObjectColorSource.ColorFromParent };
                if (mesh.Colour != null)
                {
                    attribute.ObjectColor = DocumentGeometry.ToColor(mesh.Colour);
                    attribute.ColorSource = ObjectColorSource.ColorFromObject;
                }
                geometry.Add(DocumentGeometry.ToRhino(mesh));
                attributes.Add(attribute);
            }

            var baseName = string.IsNullOrWhiteSpace(type.Name) || !Rhino.DocObjects.ModelComponent.IsValidComponentName(type.Name.Trim()) ? type.IfcClass : type.Name.Trim();
            var name = baseName;
            for (int i = 2; doc.InstanceDefinitions.Find(name) != null; i++) name = $"{baseName} {i}";

            var index = doc.InstanceDefinitions.Add(name, type.Description ?? "", Point3d.Origin, geometry, attributes);
            if (index < 0) return -1;
            var definition = doc.InstanceDefinitions[index];
            definition.SetUserString(IfcClassKey, type.IfcClass ?? "");
            if (!string.IsNullOrEmpty(type.GlobalId)) definition.SetUserString(GlobalIdKey, type.GlobalId);
            return index;
        }

        /// <summary>The document block definition of a block instance, a Grasshopper block definition or a Rhino one; null for other data.</summary>
        public static InstanceDefinition Find(object data)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return null;
            switch (data)
            {
                case InstanceDefinition definition: return definition;
                case GH_InstanceReference instance: return doc.InstanceDefinitions.FindId(instance.Value?.ParentIdefId ?? Guid.Empty);
                // Definitions from Query Model Block Definitions may have no Id; their name identifies them in the document.
                case ModelInstanceDefinition definition:
                    return definition.Id is Guid id ? doc.InstanceDefinitions.FindId(id) : doc.InstanceDefinitions.Find(definition.Name.ToString());
                case IGH_Goo goo when goo.ScriptVariable() is object inner && inner != data: return Find(inner);
                default: return null;
            }
        }

        /// <summary>
        /// Meshes (metres) of the objects of a block definition moved by <paramref name="transform"/> (document units); nested blocks are
        /// flattened. Objects coloured by object keep their colour; others take the colour of the element.
        /// </summary>
        public static List<MeshGeometry> Meshes(InstanceDefinition definition, Transform transform)
        {
            var meshes = new List<MeshGeometry>();
            foreach (var obj in definition.GetObjects())
            {
                if (obj is InstanceObject nested)
                {
                    meshes.AddRange(Meshes(nested.InstanceDefinition, transform * nested.InstanceXform));
                    continue;
                }
                var geometry = obj.Geometry.Duplicate();
                geometry.Transform(transform);
                var mesh = DocumentGeometry.ToMesh(geometry);
                if (mesh == null) continue;
                meshes.Add(obj.Attributes.ColorSource == ObjectColorSource.ColorFromObject ? mesh.WithColour(DocumentGeometry.ToColour(obj.Attributes.ObjectColor)) : mesh);
            }
            return meshes;
        }

        /// <summary>
        /// The element type of a block definition: named after it, with its geometry in block coordinates. Its GlobalId is the one baked on
        /// the definition, else derived from the definition, so every type made from the same block is written as one type.
        /// </summary>
        public static ElementType TypeOf(InstanceDefinition definition, string ifcClass) => new ElementType(definition.Name, ifcClass)
        {
            Description = string.IsNullOrWhiteSpace(definition.Description) ? null : definition.Description,
            GlobalId = GlobalIdOf(definition),
        }.WithGeometry(Meshes(definition, Transform.Identity));

        /// <summary>The GlobalId baked on a block definition, else one derived from the definition.</summary>
        public static string GlobalIdOf(InstanceDefinition definition) =>
            GlobalIds.TryParse(definition.GetUserString(GlobalIdKey), out var baked) ? baked : GlobalIds.FromSeed($"block/{definition.Id:N}");

        private static ElementType WithGeometry(this ElementType type, IEnumerable<MeshGeometry> meshes)
        {
            type.Geometry.AddRange(meshes);
            return type;
        }

        /// <summary>Core transform (metres) of a Rhino transform (document units).</summary>
        public static Affine ToAffine(Transform transform)
        {
            var scale = DocumentUnits.MetresPerUnit();
            double[] Column(int column) => new[] { transform[0, column], transform[1, column], transform[2, column] };
            return new Affine(Column(3).Select(v => v * scale).ToArray(), Column(0), Column(1), Column(2));
        }

        /// <summary>Placement at the origin of a transform, along its X and Z axes; null when they are degenerate.</summary>
        public static Placement PlacementOf(Affine transform)
        {
            try
            {
                return new Placement(transform.Origin, transform.X, transform.Z);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>Rhino transform (document units) of a Core transform (metres).</summary>
        public static Transform ToTransform(Affine affine)
        {
            var scale = 1.0 / DocumentUnits.MetresPerUnit();
            var transform = Transform.Identity;
            for (int row = 0; row < 3; row++)
            {
                transform[row, 0] = affine.X[row];
                transform[row, 1] = affine.Y[row];
                transform[row, 2] = affine.Z[row];
                transform[row, 3] = affine.Origin[row] * scale;
            }
            return transform;
        }
    }
}
