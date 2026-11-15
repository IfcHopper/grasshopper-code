using System;
using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.IO;

namespace IfcHopper.Core.Model
{
    /// <summary>Translates a complete supported model horizontally, optionally preserving its projected map location.</summary>
    public static class ModelTranslation
    {
        /// <summary>Creates a translated copy without modifying upstream object values or the source file.</summary>
        /// <param name="model">Model to copy, including its project, child objects, parts, openings and fills.</param>
        /// <param name="x">Translation along model world X, in metres; negative values move toward negative X.</param>
        /// <param name="y">Translation along model world Y, in metres; Z, orientation and storey elevations are unchanged.</param>
        /// <param name="keepMapLocation">Compensate an existing map conversion so projected positions remain unchanged. Without one, no CRS is inferred.</param>
        /// <returns>A model with preserved identities/source links and translated world placements/geometry. Type-local geometry is unchanged.</returns>
        /// <exception cref="ArgumentNullException">The model or project is null.</exception>
        /// <exception cref="ArgumentException">Offsets or the map conversion being compensated are non-finite or invalid.</exception>
        /// <exception cref="NotSupportedException">The source contains products or placements that cannot all be translated safely.</exception>
        /// <remarks>
        /// Traverses and copies the object tree, openings and fills, but keeps body/type geometry lazy. Property data, materials and type definitions
        /// retain their existing sharing contracts. Geometry is translated on first access. The returned tree is independently editable through the
        /// normal copy-based edit APIs; this operation does not make shared metadata deeply immutable. Use on the owning model thread, not concurrently
        /// with mutation. Read-time warnings are preserved as source diagnostics, not recomputed bounds. Writing still requires an explicit writer action.
        /// </remarks>
        public static IfcHopperModel Move(IfcHopperModel model, double x, double y, bool keepMapLocation = true)
        {
            if (model?.Project == null) throw new ArgumentNullException(nameof(model), "A model with a project is required.");
            if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentException("X and Y must be finite metre offsets.");
            var limitations = IfcBackend.Reader.GetTranslationLimitations(model);
            if (limitations.Count > 0) throw new NotSupportedException(string.Join(" ", limitations));
            var geo = model.Project.Georeference;
            Georeference adjusted = geo;
            if (keepMapLocation && geo != null)
            {
                var norm = Math.Sqrt(geo.XAxisAbscissa * geo.XAxisAbscissa + geo.XAxisOrdinate * geo.XAxisOrdinate);
                if (!double.IsFinite(norm) || norm == 0 || !double.IsFinite(geo.Scale) || geo.Scale <= 0
                    || !double.IsFinite(geo.Eastings) || !double.IsFinite(geo.Northings))
                    throw new ArgumentException("The map conversion needs finite offsets, a nonzero X axis and a positive finite scale.");
                // IFC map = offset + scale * rotation * model. For model' = model + d,
                // offset' = offset - scale * rotation * d. Height is unaffected by XY translation.
                // https://standards.buildingsmart.org/IFC/RELEASE/IFC4_3/HTML/lexical/IfcMapConversion.htm
                adjusted = new Georeference(geo.CrsName)
                {
                    CrsDescription = geo.CrsDescription, GeodeticDatum = geo.GeodeticDatum, VerticalDatum = geo.VerticalDatum,
                    MapProjection = geo.MapProjection, MapZone = geo.MapZone,
                    Eastings = geo.Eastings - geo.Scale * (geo.XAxisAbscissa * x - geo.XAxisOrdinate * y) / norm,
                    Northings = geo.Northings - geo.Scale * (geo.XAxisOrdinate * x + geo.XAxisAbscissa * y) / norm,
                    OrthogonalHeight = geo.OrthogonalHeight, XAxisAbscissa = geo.XAxisAbscissa,
                    XAxisOrdinate = geo.XAxisOrdinate, Scale = geo.Scale,
                };
                if (!double.IsFinite(adjusted.Eastings) || !double.IsFinite(adjusted.Northings))
                    throw new ArgumentException("The translated map conversion overflows. Use smaller offsets.");
            }
            var copies = new Dictionary<ModelObject, ModelObject>();
            var active = new HashSet<ModelObject>();
            var from = Placement.World;
            var to = Placement.Translation(x, y, 0);
            ModelObject Visit(ModelObject node, bool projectChild = false)
            {
                if (active.Contains(node)) throw new NotSupportedException($"'{node.Name}' forms a cycle in the model hierarchy or opening relationships.");
                if (copies.TryGetValue(node, out var cached)) return cached;
                active.Add(node);
                var copy = node.Copy();
                copies.Add(node, copy);
                if (!(node is Project))
                {
                    var original = node.Placement;
                    if (original != null)
                    {
                        if (!double.IsFinite(original.Origin[0] + x) || !double.IsFinite(original.Origin[1] + y))
                            throw new ArgumentException($"The translated placement of '{node.Name}' overflows. Use smaller offsets.");
                        copy.Placement = new Placement(new[] { original.Origin[0] + x, original.Origin[1] + y, original.Origin[2] }, original.XAxis, original.ZAxis);
                    }
                    else if (projectChild) copy.Placement = to;
                }
                if (copy is Element element && node is Element originalElement)
                {
                    element.MoveGeometry(from, to);
                    // MoveGeometry handles openings for a single-element edit. Here the
                    // visitor owns that traversal, so replace its loader to avoid moving twice.
                    element.LoadOpenings(() => originalElement.Openings.Select(opening => (Opening)Visit(opening)));
                    _ = element.Openings;
                    if (element is Opening resultOpening && node is Opening originalOpening)
                    {
                        resultOpening.Fills.Clear();
                        foreach (var fill in originalOpening.Fills) resultOpening.Fills.Add((Element)Visit(fill));
                    }
                }
                copy.Children.Clear();
                foreach (var child in node.Children) copy.Children.Add(Visit(child, node is Project));
                active.Remove(node);
                return copy;
            }
            var project = (Project)Visit(model.Project);
            project.Georeference = adjusted;
            var result = new IfcHopperModel(project)
            {
                SourcePath = model.SourcePath, Author = model.Author, Organization = model.Organization, ReadWarnings = model.ReadWarnings,
            };
            result.Types.AddRange(model.Types);
            result.Materials.AddRange(model.Materials);
            return result;
        }
    }
}
