using System;
using System.Collections.Generic;
using System.Linq;

namespace IfcHopper.Core.Geometry
{
    /// <summary>Ear-clipping triangulation of planar 3D polygons, optionally with holes.</summary>
    public static class Triangulator
    {
        /// <summary>
        /// Triangles of a planar polygon, as indices into <paramref name="outer"/> followed by the points of each hole in order.
        /// Triangles keep the winding of <paramref name="outer"/>; holes may have any winding. Degenerate polygons give no triangles.
        /// </summary>
        public static List<int[]> Triangulate(IReadOnlyList<double[]> outer, IReadOnlyList<IReadOnlyList<double[]>> holes = null)
        {
            holes = holes ?? Array.Empty<IReadOnlyList<double[]>>();
            var normal = Newell(outer);
            var length = Math.Sqrt(Dot(normal, normal));
            if (length < 1e-30) return new List<int[]>();
            normal = Scale(normal, 1.0 / length);

            // 2D basis (u, v) with u × v = normal, so the outer loop is counter-clockwise in 2D.
            var u = Normalize(Math.Abs(normal[0]) < 0.9 ? Cross(new[] { 1.0, 0, 0 }, normal) : Cross(new[] { 0, 1.0, 0 }, normal));
            var v = Cross(normal, u);
            var points = outer.Concat(holes.SelectMany(h => h)).Select(p => new[] { Dot(p, u), Dot(p, v) }).ToList();
            var size = Math.Max(points.Max(p => p[0]) - points.Min(p => p[0]), points.Max(p => p[1]) - points.Min(p => p[1]));
            var eps = 1e-12 * size * size;

            var polygon = Loop(points, 0, outer.Count, eps, true);
            var holeLoops = new List<List<int>>();
            var offset = outer.Count;
            foreach (var hole in holes)
            {
                var loop = Loop(points, offset, hole.Count, eps, false);
                if (loop.Count >= 3) holeLoops.Add(loop);
                offset += hole.Count;
            }

            // Bridge holes into the outer loop, rightmost hole first.
            foreach (var hole in holeLoops.OrderByDescending(h => h.Max(i => points[i][0])).ToList())
            {
                holeLoops.Remove(hole);
                Bridge(points, polygon, hole, holeLoops);
            }
            return EarClip(points, polygon, eps);
        }

        /// <summary>Indices of a loop without consecutive duplicates, counter-clockwise when <paramref name="ccw"/>, else clockwise.</summary>
        private static List<int> Loop(List<double[]> points, int start, int count, double eps, bool ccw)
        {
            var loop = new List<int>();
            for (int i = start; i < start + count; i++)
                if (loop.Count == 0 || Distance2(points[loop[loop.Count - 1]], points[i]) > eps) loop.Add(i);
            while (loop.Count > 1 && Distance2(points[loop[0]], points[loop[loop.Count - 1]]) <= eps) loop.RemoveAt(loop.Count - 1);
            if ((Area(points, loop) > 0) != ccw) loop.Reverse();
            return loop;
        }

        /// <summary>Joins <paramref name="hole"/> to <paramref name="polygon"/> through the nearest polygon vertex the hole can see.</summary>
        private static void Bridge(List<double[]> points, List<int> polygon, List<int> hole, List<List<int>> otherHoles)
        {
            var h = hole.IndexOf(hole.OrderByDescending(i => points[i][0]).First());
            var m = points[hole[h]];
            int best = -1;
            double bestDistance = double.MaxValue;
            for (int k = 0; k < polygon.Count; k++)
            {
                var p = points[polygon[k]];
                var distance = Distance2(p, m);
                if (distance >= bestDistance) continue;
                var prev = points[polygon[(k + polygon.Count - 1) % polygon.Count]];
                var next = points[polygon[(k + 1) % polygon.Count]];
                if (!LocallyInside(prev, p, next, m)) continue;
                if (Crosses(points, polygon, p, m) || Crosses(points, hole, p, m) || otherHoles.Any(o => Crosses(points, o, p, m))) continue;
                best = k;
                bestDistance = distance;
            }
            if (best < 0) best = Enumerable.Range(0, polygon.Count).OrderBy(k => Distance2(points[polygon[k]], m)).First();

            var bridge = new List<int>();
            for (int i = 0; i <= hole.Count; i++) bridge.Add(hole[(h + i) % hole.Count]);
            bridge.Add(polygon[best]);
            polygon.InsertRange(best + 1, bridge);
        }

        /// <summary>True when direction a→b points into the polygon interior at vertex a (counter-clockwise polygon).</summary>
        private static bool LocallyInside(double[] prev, double[] a, double[] next, double[] b) =>
            Cross(prev, a, next) >= 0
                ? Cross(a, next, b) >= 0 && Cross(prev, a, b) >= 0
                : Cross(a, next, b) >= 0 || Cross(prev, a, b) >= 0;

        /// <summary>True when segment p-q properly crosses an edge of the loop that does not touch p or q.</summary>
        private static bool Crosses(List<double[]> points, List<int> loop, double[] p, double[] q)
        {
            for (int i = 0; i < loop.Count; i++)
            {
                var a = points[loop[i]];
                var b = points[loop[(i + 1) % loop.Count]];
                if (Same(a, p) || Same(a, q) || Same(b, p) || Same(b, q)) continue;
                if (Math.Sign(Cross(p, q, a)) * Math.Sign(Cross(p, q, b)) < 0 && Math.Sign(Cross(a, b, p)) * Math.Sign(Cross(a, b, q)) < 0) return true;
            }
            return false;
        }

        private static List<int[]> EarClip(List<double[]> points, List<int> polygon, double eps)
        {
            var triangles = new List<int[]>();
            var ring = new List<int>(polygon);
            while (ring.Count > 3)
            {
                var clipped = false;
                for (int i = 0; i < ring.Count && !clipped; i++)
                {
                    int a = ring[(i + ring.Count - 1) % ring.Count], b = ring[i], c = ring[(i + 1) % ring.Count];
                    if (Cross(points[a], points[b], points[c]) <= eps || ContainsOther(points, ring, a, b, c)) continue;
                    triangles.Add(new[] { a, b, c });
                    ring.RemoveAt(i);
                    clipped = true;
                }
                if (clipped) continue;

                // No ear: drop a collinear vertex, or clip anyway so the loop always ends (self-intersecting input).
                var degenerate = Enumerable.Range(0, ring.Count).FirstOrDefault(i =>
                    Math.Abs(Cross(points[ring[(i + ring.Count - 1) % ring.Count]], points[ring[i]], points[ring[(i + 1) % ring.Count]])) <= eps);
                if (Math.Abs(Cross(points[ring[(degenerate + ring.Count - 1) % ring.Count]], points[ring[degenerate]], points[ring[(degenerate + 1) % ring.Count]])) > eps)
                    triangles.Add(new[] { ring[ring.Count - 1], ring[0], ring[1] });
                ring.RemoveAt(degenerate);
            }
            if (ring.Count == 3 && Cross(points[ring[0]], points[ring[1]], points[ring[2]]) > eps) triangles.Add(ring.ToArray());
            return triangles;
        }

        /// <summary>True when a ring vertex other than a, b, c (or copies of them made by bridges) lies in triangle a-b-c.</summary>
        private static bool ContainsOther(List<double[]> points, List<int> ring, int a, int b, int c)
        {
            double[] pa = points[a], pb = points[b], pc = points[c];
            foreach (var i in ring)
            {
                var p = points[i];
                if (Same(p, pa) || Same(p, pb) || Same(p, pc)) continue;
                if (Cross(pa, pb, p) >= 0 && Cross(pb, pc, p) >= 0 && Cross(pc, pa, p) >= 0) return true;
            }
            return false;
        }

        private static double[] Newell(IReadOnlyList<double[]> loop)
        {
            var n = new double[3];
            for (int i = 0; i < loop.Count; i++)
            {
                var a = loop[i];
                var b = loop[(i + 1) % loop.Count];
                n[0] += (a[1] - b[1]) * (a[2] + b[2]);
                n[1] += (a[2] - b[2]) * (a[0] + b[0]);
                n[2] += (a[0] - b[0]) * (a[1] + b[1]);
            }
            return n;
        }

        private static double Area(List<double[]> points, List<int> loop)
        {
            double area = 0;
            for (int i = 0; i < loop.Count; i++)
            {
                var a = points[loop[i]];
                var b = points[loop[(i + 1) % loop.Count]];
                area += a[0] * b[1] - b[0] * a[1];
            }
            return area / 2;
        }

        private static bool Same(double[] a, double[] b) => a[0] == b[0] && a[1] == b[1];
        private static double Cross(double[] a, double[] b, double[] c) => (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]);
        private static double Distance2(double[] a, double[] b) => (a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]);
        private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        private static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        private static double[] Scale(double[] a, double s) => new[] { a[0] * s, a[1] * s, a[2] * s };
        private static double[] Normalize(double[] a) => Scale(a, 1.0 / Math.Sqrt(Dot(a, a)));
    }
}
