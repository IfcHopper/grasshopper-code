using System;
using IfcHopper.Core.Geometry;

namespace IfcHopper.Core.Model
{
    /// <summary>
    /// Right-handed orthonormal coordinate system: origin in metres, unit X and Z axes (Y = Z × X).
    /// Used both for world placements (Core model) and for placements relative to a parent (IFC).
    /// </summary>
    public sealed class Placement
    {
        private const double Tolerance = 1e-9;

        public static readonly Placement World = new Placement(new[] { 0.0, 0.0, 0.0 }, new[] { 1.0, 0.0, 0.0 }, new[] { 0.0, 0.0, 1.0 });

        public double[] Origin { get; }
        public double[] XAxis { get; }
        public double[] ZAxis { get; }
        public double[] YAxis => Cross(ZAxis, XAxis);

        /// <summary>Creates a placement; X is made orthogonal to Z. Throws when the axes are zero or parallel.</summary>
        public Placement(double[] origin, double[] xAxis, double[] zAxis)
        {
            if (origin == null || origin.Length != 3) throw new ArgumentException("Origin needs 3 coordinates.", nameof(origin));
            var z = Normalize(zAxis, nameof(zAxis));
            var x = Normalize(Subtract(xAxis, Scale(z, Dot(xAxis, z))), nameof(xAxis));
            Origin = (double[])origin.Clone();
            XAxis = x;
            ZAxis = z;
        }

        public static Placement Translation(double x, double y, double z) =>
            new Placement(new[] { x, y, z }, World.XAxis, World.ZAxis);

        /// <summary>This placement expressed in the coordinate system of <paramref name="parent"/>.</summary>
        public Placement RelativeTo(Placement parent) =>
            new Placement(parent.ToLocal(Subtract(Origin, parent.Origin)), parent.ToLocal(XAxis), parent.ToLocal(ZAxis));

        /// <summary>The world placement of <paramref name="relative"/>, given in this coordinate system.</summary>
        public Placement Compose(Placement relative) =>
            new Placement(Add(Origin, ToWorld(relative.Origin)), ToWorld(relative.XAxis), ToWorld(relative.ZAxis));

        /// <summary>A world point expressed in this coordinate system.</summary>
        public double[] PointToLocal(double[] point) => ToLocal(Subtract(point, Origin));

        /// <summary>A point given in this coordinate system, expressed in world coordinates.</summary>
        public double[] PointToWorld(double[] point) => Add(Origin, ToWorld(point));

        /// <summary>The transform from this coordinate system to world.</summary>
        public Affine ToAffine() => new Affine((double[])Origin.Clone(), (double[])XAxis.Clone(), YAxis, (double[])ZAxis.Clone());

        public bool IsWorld => IsClose(Origin, World.Origin) && IsClose(XAxis, World.XAxis) && IsClose(ZAxis, World.ZAxis);

        public bool HasDefaultAxes => IsClose(XAxis, World.XAxis) && IsClose(ZAxis, World.ZAxis);

        private double[] ToLocal(double[] v) => new[] { Dot(v, XAxis), Dot(v, YAxis), Dot(v, ZAxis) };

        private double[] ToWorld(double[] v)
        {
            var y = YAxis;
            return new[]
            {
                v[0] * XAxis[0] + v[1] * y[0] + v[2] * ZAxis[0],
                v[0] * XAxis[1] + v[1] * y[1] + v[2] * ZAxis[1],
                v[0] * XAxis[2] + v[1] * y[2] + v[2] * ZAxis[2],
            };
        }

        private static double[] Normalize(double[] v, string name)
        {
            if (v == null || v.Length != 3) throw new ArgumentException("Axis needs 3 components.", name);
            var length = Math.Sqrt(Dot(v, v));
            if (length < Tolerance) throw new ArgumentException("Axes must be non-zero and not parallel.", name);
            return Scale(v, 1.0 / length);
        }

        private static bool IsClose(double[] a, double[] b) =>
            Math.Abs(a[0] - b[0]) < Tolerance && Math.Abs(a[1] - b[1]) < Tolerance && Math.Abs(a[2] - b[2]) < Tolerance;

        private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        private static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        private static double[] Add(double[] a, double[] b) => new[] { a[0] + b[0], a[1] + b[1], a[2] + b[2] };
        private static double[] Subtract(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
        private static double[] Scale(double[] a, double s) => new[] { a[0] * s, a[1] * s, a[2] * s };
    }
}
