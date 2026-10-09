using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.IO;

namespace IfcHopper.Core.Model
{
    /// <summary>Puts edited objects back into a model by GlobalId, without rebuilding the chain of parents by hand.</summary>
    public static class ModelEdits
    {
        /// <summary>
        /// Returns a model where every object with the GlobalId of an edit is replaced by that edit; edited types read from a file are
        /// added to <see cref="IfcHopperModel.Types"/> (in place of an earlier edit of the same type).
        /// Only the branches leading to the edits are loaded and copied; the input model is not changed.
        /// Edits that are not part of the model (no source, other file, removed) are returned in <paramref name="notFound"/>.
        /// </summary>
        public static IfcHopperModel Apply(IfcHopperModel model, IEnumerable<ModelObject> edits, out IReadOnlyList<ModelObject> notFound)
        {
            var missing = new List<ModelObject>();
            var byId = new Dictionary<string, ModelObject>();
            var onPath = new HashSet<string>();
            var types = model.Types.ToList();
            foreach (var edit in edits.Where(e => e != null))
            {
                if (edit is ElementType type)
                {
                    if (type.Source == null || type.GlobalId == null) missing.Add(type);
                    else
                    {
                        types.RemoveAll(t => t.GlobalId == type.GlobalId);
                        types.Add(type);
                    }
                    continue;
                }
                var ancestors = edit.GlobalId == null ? null : IfcBackend.Reader.GetAncestorIds(edit);
                if (ancestors == null)
                {
                    missing.Add(edit);
                    continue;
                }
                byId[edit.GlobalId] = edit;
                onPath.UnionWith(ancestors);
            }

            var visitor = new Visitor(byId, onPath);
            var project = (Project)visitor.Visit(model.Project);
            missing.AddRange(byId.Values.Where(e => !visitor.Applied.Contains(e.GlobalId)));
            notFound = missing;

            var result = new IfcHopperModel(project)
            {
                Author = model.Author,
                Organization = model.Organization,
                SourcePath = model.SourcePath,
            };
            result.Types.AddRange(types);
            result.Materials.AddRange(model.Materials);
            return result;
        }

        /// <summary>
        /// Returns a model with edited materials read from a file added to <see cref="IfcHopperModel.Materials"/> (in place of an earlier edit
        /// of the same material). Materials without a source are returned in <paramref name="notFound"/>.
        /// </summary>
        public static IfcHopperModel ApplyMaterials(IfcHopperModel model, IEnumerable<MaterialDefinition> edits, out IReadOnlyList<MaterialDefinition> notFound)
        {
            var result = new IfcHopperModel(model.Project)
            {
                Author = model.Author,
                Organization = model.Organization,
                SourcePath = model.SourcePath,
            };
            result.Types.AddRange(model.Types);
            result.Materials.AddRange(model.Materials);
            var missing = new List<MaterialDefinition>();
            foreach (var edit in edits.Where(e => e != null))
            {
                if (edit.Source == null)
                {
                    missing.Add(edit);
                    continue;
                }
                result.Materials.RemoveAll(m => m.Source == edit.Source);
                result.Materials.Add(edit);
            }
            notFound = missing;
            return result;
        }

        private sealed class Visitor
        {
            private readonly Dictionary<string, ModelObject> _edits;
            private readonly HashSet<string> _onPath;

            public HashSet<string> Applied { get; } = new HashSet<string>();

            public Visitor(Dictionary<string, ModelObject> edits, HashSet<string> onPath)
            {
                _edits = edits;
                _onPath = onPath;
            }

            /// <summary>The edit of <paramref name="node"/> if any; when it is an ancestor of other edits, a copy with its children visited.</summary>
            public ModelObject Visit(ModelObject node)
            {
                var result = node;
                if (node.GlobalId != null && _edits.TryGetValue(node.GlobalId, out var edit))
                {
                    result = edit;
                    Applied.Add(node.GlobalId);
                }
                if (node.GlobalId == null || !_onPath.Contains(node.GlobalId)) return result;

                result = result.Copy();
                var children = result.Children;
                for (int i = 0; i < children.Count; i++) children[i] = Visit(children[i]);
                return result;
            }
        }
    }
}
