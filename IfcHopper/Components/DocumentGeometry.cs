using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using IfcHopper.Core.Geometry;
using IfcHopper.Core.Model;
using Rhino;
using Rhino.Geometry;

namespace IfcHopper.Components
{
    /// <summary>Converts Rhino geometry (document units) to and from Core meshes (metres). Everything that is not a mesh is faceted.</summary>
    internal static class DocumentGeometry
    {
        /// <summary>The Core mesh of <paramref name="geometry"/>, or null when it is not a mesh, Brep, Extrusion, Surface or SubD, or meshes to nothing.</summary>
        public static MeshGeometry ToMesh(GeometryBase geometry)
        {
            var mesh = Facet(geometry);
            if (mesh == null) return null;

            mesh.Vertices.CombineIdentical(true, true);
            mesh.Faces.CullDegenerateFaces();
            if (mesh.Faces.Count == 0) return null;

            var scale = DocumentUnits.MetresPerUnit();
            var vertices = mesh.Vertices.Select(v => new[] { v.X * scale, v.Y * scale, v.Z * scale });
            var faces = mesh.Faces.Select(f => f.IsQuad ? new[] { f.A, f.B, f.C, f.D } : new[] { f.A, f.B, f.C });
            return new MeshGeometry(vertices, faces, mesh.IsClosed);
        }

        /// <summary>Rhino mesh (document units) of a Core mesh. Polygons with more than 4 vertices are triangulated; creases above 30° are unwelded for flat shading.</summary>
        public static Mesh ToRhino(MeshGeometry geometry) => Finish(Welded(geometry), geometry.Colour);

        /// <summary>Welded Rhino mesh (document units) of a Core mesh, e.g. for mesh booleans; <see cref="Finish"/> prepares it for display.</summary>
        public static Mesh Welded(MeshGeometry geometry)
        {
            var scale = 1.0 / DocumentUnits.MetresPerUnit();
            var mesh = new Mesh();
            foreach (var v in geometry.Vertices) mesh.Vertices.Add(v[0] * scale, v[1] * scale, v[2] * scale);
            foreach (var face in geometry.Faces)
            {
                if (face.Length == 3) mesh.Faces.AddFace(face[0], face[1], face[2]);
                else if (face.Length == 4) mesh.Faces.AddFace(face[0], face[1], face[2], face[3]);
                else
                    foreach (var t in Triangulator.Triangulate(face.Select(i => geometry.Vertices[i]).ToList()))
                        mesh.Faces.AddFace(face[t[0]], face[t[1]], face[t[2]]);
            }

            mesh.Vertices.CombineIdentical(true, true);
            mesh.Faces.CullDegenerateFaces();
            return mesh;
        }

        /// <summary>Unwelds creases above 30° for flat shading and colours the mesh (null: no vertex colours).</summary>
        public static Mesh Finish(Mesh mesh, Colour colour)
        {
            mesh.Unweld(RhinoMath.ToRadians(30), true);
            mesh.Normals.ComputeNormals();
            mesh.Compact();
            if (colour != null) mesh.VertexColors.CreateMonotoneMesh(ToColor(colour));
            return mesh;
        }

        /// <summary>Core colour of a display colour; alpha becomes transparency.</summary>
        public static Colour ToColour(Color color) => new Colour(color.R / 255.0, color.G / 255.0, color.B / 255.0, 1 - color.A / 255.0);

        /// <summary>Display colour of a Core colour; transparency becomes alpha.</summary>
        public static Color ToColor(Colour colour) =>
            Color.FromArgb((int)Math.Round((1 - colour.Transparency) * 255), (int)Math.Round(colour.Red * 255), (int)Math.Round(colour.Green * 255), (int)Math.Round(colour.Blue * 255));

        private static Mesh Facet(GeometryBase geometry)
        {
            switch (geometry)
            {
                case Mesh mesh: return mesh.DuplicateMesh();
                case SubD subD: return Mesh.CreateFromSubD(subD, 3);
                case Extrusion extrusion: return Join(Mesh.CreateFromBrep(extrusion.ToBrep(), MeshingParameters.Default));
                case Surface surface: return Join(Mesh.CreateFromBrep(surface.ToBrep(), MeshingParameters.Default));
                case Brep brep: return Join(Mesh.CreateFromBrep(brep, MeshingParameters.Default));
                default: return null;
            }
        }

        private static Mesh Join(IEnumerable<Mesh> meshes)
        {
            if (meshes == null) return null;
            var joined = new Mesh();
            joined.Append(meshes);
            return joined;
        }
    }
}
