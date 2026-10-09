using System;
using System.Collections.Generic;
using System.Linq;

namespace IfcHopper.Core.Model
{
    /// <summary>
    /// Polygon mesh in world coordinates (metres), written as an IfcPolygonalFaceSet in the element's Body representation.
    /// Every element geometry is faceted to meshes, on write and on read.
    /// </summary>
    public sealed class MeshGeometry
    {
        /// <summary>Vertex coordinates (X, Y, Z) in metres.</summary>
        public IReadOnlyList<double[]> Vertices { get; }

        /// <summary>Faces as 0-based vertex indices, at least 3 per face, in counter-clockwise order seen from outside.</summary>
        public IReadOnlyList<int[]> Faces { get; }

        /// <summary>True when the mesh encloses a volume.</summary>
        public bool IsClosed { get; }

        /// <summary>Surface colour, written as an IfcStyledItem; null when not styled.</summary>
        public Colour Colour { get; }

        /// <summary>Creates a mesh; throws when a vertex has no 3 coordinates or a face is too small or out of range.</summary>
        public MeshGeometry(IEnumerable<double[]> vertices, IEnumerable<int[]> faces, bool isClosed = false, Colour colour = null)
        {
            Colour = colour;
            Vertices = vertices.Select(v => v != null && v.Length == 3 ? (double[])v.Clone() : throw new ArgumentException("Vertices need 3 coordinates.", nameof(vertices))).ToList();
            Faces = faces.Select(f => (int[])f.Clone()).ToList();
            if (Faces.Count == 0) throw new ArgumentException("Mesh has no faces.", nameof(faces));
            foreach (var face in Faces)
            {
                if (face.Length < 3) throw new ArgumentException("Faces need at least 3 vertices.", nameof(faces));
                if (face.Any(i => i < 0 || i >= Vertices.Count)) throw new ArgumentException("Face index out of range.", nameof(faces));
            }
            IsClosed = isClosed;
        }

        /// <summary>A copy with every vertex mapped by <paramref name="map"/>; faces, closure and colour are kept.</summary>
        public MeshGeometry Transform(Func<double[], double[]> map) => new MeshGeometry(Vertices.Select(map), Faces, IsClosed, Colour);

        /// <summary>A copy with every face reversed.</summary>
        public MeshGeometry Flip() => new MeshGeometry(Vertices, Faces.Select(f => f.Reverse().ToArray()), IsClosed, Colour);

        /// <summary>A copy with another colour (null removes it).</summary>
        public MeshGeometry WithColour(Colour colour) => new MeshGeometry(Vertices, Faces, IsClosed, colour);

        /// <summary>Signed enclosed volume in m³ (divergence theorem): positive when the faces of a closed mesh point outwards.</summary>
        public double SignedVolume()
        {
            double volume = 0;
            foreach (var f in Faces)
            {
                var a = Vertices[f[0]];
                for (int i = 1; i < f.Length - 1; i++)
                {
                    double[] b = Vertices[f[i]], c = Vertices[f[i + 1]];
                    volume += a[0] * (b[1] * c[2] - b[2] * c[1]) - a[1] * (b[0] * c[2] - b[2] * c[0]) + a[2] * (b[0] * c[1] - b[1] * c[0]);
                }
            }
            return volume / 6;
        }
    }
}
