namespace IfcHopper.Core.Geometry
{
    /// <summary>Affine 3D transform: p' = Origin + X·p.x + Y·p.y + Z·p.z. Axes may be scaled, non-orthogonal or mirrored.</summary>
    public sealed class Affine
    {
        public static readonly Affine Identity = new Affine(new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0, 1.0, 0 }, new[] { 0, 0, 1.0 });

        public double[] Origin { get; }
        public double[] X { get; }
        public double[] Y { get; }
        public double[] Z { get; }

        public Affine(double[] origin, double[] x, double[] y, double[] z)
        {
            Origin = origin;
            X = x;
            Y = y;
            Z = z;
        }

        public double[] Apply(double[] p) => Add(Origin, ApplyVector(p));

        public double[] ApplyVector(double[] v) => new[]
        {
            X[0] * v[0] + Y[0] * v[1] + Z[0] * v[2],
            X[1] * v[0] + Y[1] * v[1] + Z[1] * v[2],
            X[2] * v[0] + Y[2] * v[1] + Z[2] * v[2],
        };

        /// <summary>The transform that applies <paramref name="inner"/> first, then this one.</summary>
        public Affine Compose(Affine inner) => new Affine(Apply(inner.Origin), ApplyVector(inner.X), ApplyVector(inner.Y), ApplyVector(inner.Z));

        /// <summary>True when the transform flips handedness, so face windings must be reversed.</summary>
        public bool IsMirrored =>
            X[0] * (Y[1] * Z[2] - Y[2] * Z[1]) - X[1] * (Y[0] * Z[2] - Y[2] * Z[0]) + X[2] * (Y[0] * Z[1] - Y[1] * Z[0]) < 0;

        private static double[] Add(double[] a, double[] b) => new[] { a[0] + b[0], a[1] + b[1], a[2] + b[2] };
    }
}
