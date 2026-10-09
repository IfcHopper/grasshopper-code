using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.Model;
using Rhino.Geometry;

namespace IfcHopper.Components
{
    /// <summary>
    /// Subtracts openings from element bodies with Rhino mesh booleans, for preview, bake and Deconstruct Object. Core keeps the uncut body,
    /// as IFC stores it.
    /// </summary>
    internal static class DocumentOpenings
    {
        /// <summary>
        /// Body meshes of the element (document units) with its openings subtracted, each in its display colour. <paramref name="failed"/> is
        /// true when a boolean failed; that mesh is kept uncut.
        /// </summary>
        public static List<(Mesh Mesh, Colour Colour)> CutBody(Element element, out bool failed)
        {
            failed = false;
            var result = new List<(Mesh, Colour)>();
            var cutters = element.Openings.SelectMany(o => o.Geometry).Select(DocumentGeometry.Welded).Where(m => m.Faces.Count > 0).ToList();
            foreach (var body in element.Geometry)
            {
                var colour = element.DisplayColour(body);
                var mesh = DocumentGeometry.Welded(body);
                var box = mesh.GetBoundingBox(false);
                var hits = cutters.Where(c => BoundingBox.Intersection(box, c.GetBoundingBox(false)).IsValid).ToList();
                if (hits.Count > 0)
                {
                    var cut = Mesh.CreateBooleanDifference(new[] { mesh }, hits);
                    if (cut != null && cut.Length > 0)
                    {
                        result.AddRange(cut.Select(piece => (DocumentGeometry.Finish(piece, colour), colour)));
                        continue;
                    }
                    failed = true;
                }
                result.Add((DocumentGeometry.Finish(mesh, colour), colour));
            }
            return result;
        }
    }
}
