using System;
using System.Collections.Generic;
using System.Linq;

namespace IfcHopper.Core.Geometry
{
    /// <summary>Planar parent curve of an alignment segment, in its own coordinates and parameterised by arc length.</summary>
    internal abstract class PlanarCurve
    {
        public abstract double[] Point(double u);

        /// <summary>Tangent direction in radians.</summary>
        public abstract double Angle(double u);

        /// <summary>Smallest radius of curvature for u in [u0, u1]; infinity when straight.</summary>
        public abstract double MinRadius(double u0, double u1);

        protected static double[] Rotate(double[] v, double angle) =>
            new[] { v[0] * Math.Cos(angle) - v[1] * Math.Sin(angle), v[0] * Math.Sin(angle) + v[1] * Math.Cos(angle) };
    }

    internal sealed class LineCurve : PlanarCurve
    {
        private readonly double[] _origin;
        private readonly double _angle;

        public LineCurve(double[] origin, double angle)
        {
            _origin = origin;
            _angle = angle;
        }

        public override double[] Point(double u) => new[] { _origin[0] + u * Math.Cos(_angle), _origin[1] + u * Math.Sin(_angle) };
        public override double Angle(double u) => _angle;
        public override double MinRadius(double u0, double u1) => double.PositiveInfinity;
    }

    /// <summary>Counter-clockwise circle; u = 0 is the point on the reference direction.</summary>
    internal sealed class CircleCurve : PlanarCurve
    {
        private readonly double[] _center;
        private readonly double _refAngle, _radius;

        public CircleCurve(double[] center, double refAngle, double radius)
        {
            _center = center;
            _refAngle = refAngle;
            _radius = radius;
        }

        public override double[] Point(double u)
        {
            var phi = _refAngle + u / _radius;
            return new[] { _center[0] + _radius * Math.Cos(phi), _center[1] + _radius * Math.Sin(phi) };
        }

        public override double Angle(double u) => _refAngle + u / _radius + Math.PI / 2;
        public override double MinRadius(double u0, double u1) => _radius;
    }

    /// <summary>Clothoid with curvature u / (A·|A|): zero at u = 0, turning left for positive A.</summary>
    internal sealed class ClothoidCurve : PlanarCurve
    {
        private readonly double[] _origin;
        private readonly double _refAngle, _a2;

        public ClothoidCurve(double[] origin, double refAngle, double a)
        {
            _origin = origin;
            _refAngle = refAngle;
            _a2 = a * Math.Abs(a);
        }

        private double Theta(double u) => u * u / (2 * _a2);

        public override double[] Point(double u)
        {
            // Simpson integration of (cos θ, sin θ) from 0 to u.
            var n = 2 * Math.Max(8, (int)Math.Ceiling(Math.Abs(u) / 0.5));
            var h = u / n;
            double x = 0, y = 0;
            for (int i = 0; i <= n; i++)
            {
                var w = i == 0 || i == n ? 1 : i % 2 == 1 ? 4 : 2;
                var theta = Theta(i * h);
                x += w * Math.Cos(theta);
                y += w * Math.Sin(theta);
            }
            var local = Rotate(new[] { x * h / 3, y * h / 3 }, _refAngle);
            return new[] { _origin[0] + local[0], _origin[1] + local[1] };
        }

        public override double Angle(double u) => _refAngle + Theta(u);

        public override double MinRadius(double u0, double u1)
        {
            var u = Math.Max(Math.Abs(u0), Math.Abs(u1));
            return u == 0 ? double.PositiveInfinity : Math.Abs(_a2) / u;
        }
    }

    /// <summary>Planar polynomial curve (x, y) = (Σ cx·p^i, Σ cy·p^i), parameterised here by arc length from p = 0.</summary>
    internal sealed class PolynomialCurve : PlanarCurve
    {
        private readonly double[] _origin;
        private readonly double _refAngle;
        private readonly double[] _cx, _cy;

        public PolynomialCurve(double[] origin, double refAngle, double[] cx, double[] cy)
        {
            _origin = origin;
            _refAngle = refAngle;
            _cx = cx;
            _cy = cy;
        }

        private static double Eval(double[] c, double p) => c.Reverse().Aggregate(0.0, (acc, k) => acc * p + k);
        private static double Derivative(double[] c, double p) => c.Select((k, i) => i * k * Math.Pow(p, Math.Max(0, i - 1))).Sum();
        private static double Second(double[] c, double p) => c.Select((k, i) => i * (i - 1) * k * Math.Pow(p, Math.Max(0, i - 2))).Sum();

        private double Speed(double p) => Math.Sqrt(Math.Pow(Derivative(_cx, p), 2) + Math.Pow(Derivative(_cy, p), 2));

        private double ArcLength(double p)
        {
            const int n = 64;
            var h = p / n;
            double sum = 0;
            for (int i = 0; i <= n; i++) sum += (i == 0 || i == n ? 1 : i % 2 == 1 ? 4 : 2) * Speed(i * h);
            return sum * h / 3;
        }

        /// <summary>Parameter at arc length <paramref name="u"/> (Newton iteration).</summary>
        private double Parameter(double u)
        {
            var p = u;
            for (int i = 0; i < 30; i++)
            {
                var speed = Speed(p);
                if (speed < 1e-12) break;
                var step = (ArcLength(p) - u) / speed;
                p -= step;
                if (Math.Abs(step) < 1e-10) break;
            }
            return p;
        }

        public override double[] Point(double u)
        {
            var p = Parameter(u);
            var local = Rotate(new[] { Eval(_cx, p), Eval(_cy, p) }, _refAngle);
            return new[] { _origin[0] + local[0], _origin[1] + local[1] };
        }

        public override double Angle(double u)
        {
            var p = Parameter(u);
            return _refAngle + Math.Atan2(Derivative(_cy, p), Derivative(_cx, p));
        }

        public override double MinRadius(double u0, double u1)
        {
            var radius = double.PositiveInfinity;
            for (int i = 0; i <= 8; i++)
            {
                var p = Parameter(u0 + (u1 - u0) * i / 8);
                double dx = Derivative(_cx, p), dy = Derivative(_cy, p), ddx = Second(_cx, p), ddy = Second(_cy, p);
                var curvature = Math.Abs(dx * ddy - dy * ddx) / Math.Pow(dx * dx + dy * dy, 1.5);
                if (curvature > 1e-12) radius = Math.Min(radius, 1 / curvature);
            }
            return radius;
        }
    }

    /// <summary>
    /// IfcCurveSegment: the parent curve from Start over Length (negative: backwards), moved so that its start point and
    /// travel direction coincide with the segment placement.
    /// </summary>
    internal sealed class CurveSegment2D
    {
        private readonly PlanarCurve _curve;
        private readonly double _start, _direction, _rotation;
        private readonly double[] _origin, _startPoint;

        public double Length { get; }

        public CurveSegment2D(PlanarCurve curve, double start, double length, double[] origin, double angle)
        {
            _curve = curve;
            _start = start;
            _direction = length < 0 ? -1 : 1;
            Length = Math.Abs(length);
            _origin = origin;
            _startPoint = curve.Point(start);
            _rotation = angle - TravelAngle(start);
        }

        private double TravelAngle(double u) => _curve.Angle(u) + (_direction < 0 ? Math.PI : 0);

        /// <summary>Point and tangent angle at distance <paramref name="t"/> from the segment start.</summary>
        public (double[] Point, double Angle) At(double t)
        {
            var u = _start + _direction * t;
            var p = _curve.Point(u);
            double dx = p[0] - _startPoint[0], dy = p[1] - _startPoint[1], cos = Math.Cos(_rotation), sin = Math.Sin(_rotation);
            return (new[] { _origin[0] + dx * cos - dy * sin, _origin[1] + dx * sin + dy * cos }, TravelAngle(u) + _rotation);
        }

        public double MinRadius => _curve.MinRadius(_start, _start + _direction * Length);
    }

    /// <summary>Planar curve made of segments, evaluated by arc length; extended along the end tangents beyond its ends.</summary>
    internal sealed class Polycurve2D
    {
        private readonly List<CurveSegment2D> _segments;
        private readonly double[] _starts;

        public double Length { get; }

        public Polycurve2D(IEnumerable<CurveSegment2D> segments)
        {
            _segments = segments.Where(s => s.Length > 0).ToList();
            if (_segments.Count == 0) throw new ArgumentException("The curve has no segments.");
            _starts = new double[_segments.Count];
            for (int i = 1; i < _segments.Count; i++) _starts[i] = _starts[i - 1] + _segments[i - 1].Length;
            Length = _starts[_segments.Count - 1] + _segments[_segments.Count - 1].Length;
        }

        public IReadOnlyList<CurveSegment2D> Segments => _segments;

        public (double[] Point, double Angle) At(double s)
        {
            if (s <= 0) return Extend(_segments[0].At(0), s);
            if (s >= Length) return Extend(_segments[_segments.Count - 1].At(_segments[_segments.Count - 1].Length), s - Length);
            var i = Array.BinarySearch(_starts, s);
            if (i < 0) i = ~i - 1;
            return _segments[i].At(s - _starts[i]);
        }

        private static (double[] Point, double Angle) Extend((double[] Point, double Angle) end, double distance) =>
            (new[] { end.Point[0] + distance * Math.Cos(end.Angle), end.Point[1] + distance * Math.Sin(end.Angle) }, end.Angle);

        /// <summary>Y at X = <paramref name="x"/>, for curves whose X grows along the curve (vertical alignments: X distance, Y height).</summary>
        public double YAtX(double x)
        {
            var first = _segments[0].At(0);
            if (x <= first.Point[0]) return first.Point[1] + (x - first.Point[0]) * Math.Tan(first.Angle);

            var segment = _segments.FirstOrDefault(s => s.At(s.Length).Point[0] >= x);
            if (segment == null)
            {
                var last = _segments[_segments.Count - 1];
                var end = last.At(last.Length);
                return end.Point[1] + (x - end.Point[0]) * Math.Tan(end.Angle);
            }

            double lo = 0, hi = segment.Length;
            for (int k = 0; k < 60 && hi - lo > 1e-9; k++)
            {
                var mid = (lo + hi) / 2;
                if (segment.At(mid).Point[0] < x) lo = mid;
                else hi = mid;
            }
            return segment.At((lo + hi) / 2).Point[1];
        }

        /// <summary>
        /// Stations (in arc length, or X when <paramref name="byX"/>) in [from, to] at segment ends and along curved segments,
        /// spaced so chords deviate less than <paramref name="tolerance"/>.
        /// </summary>
        public IEnumerable<double> Stations(double from, double to, double tolerance, bool byX)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                double a = byX ? segment.At(0).Point[0] : _starts[i], b = byX ? segment.At(segment.Length).Point[0] : _starts[i] + segment.Length;
                if (b < from || a > to) continue;
                yield return a;
                var radius = segment.MinRadius;
                if (double.IsInfinity(radius)) continue;
                var step = Math.Max(tolerance * 10, Math.Sqrt(8 * radius * tolerance));
                for (var s = a + step; s < b; s += step) yield return s;
            }
        }
    }

    /// <summary>
    /// 3D alignment curve: a horizontal curve with an optional vertical curve (heights by horizontal distance).
    /// Distances along are measured on the horizontal curve.
    /// </summary>
    internal sealed class AlignmentCurve
    {
        private readonly Polycurve2D _horizontal;
        private readonly Polycurve2D _vertical;

        public AlignmentCurve(Polycurve2D horizontal, Polycurve2D vertical = null)
        {
            _horizontal = horizontal;
            _vertical = vertical;
        }

        /// <summary>Point (x, y, z) and horizontal tangent angle at distance <paramref name="s"/>.</summary>
        public (double[] Point, double Angle) At(double s)
        {
            var h = _horizontal.At(s);
            return (new[] { h.Point[0], h.Point[1], _vertical?.YAtX(s) ?? 0 }, h.Angle);
        }

        /// <summary>Sorted sampling stations from <paramref name="from"/> to <paramref name="to"/>, including both.</summary>
        public List<double> Stations(double from, double to, double tolerance)
        {
            var stations = _horizontal.Stations(from, to, tolerance, false);
            if (_vertical != null) stations = stations.Concat(_vertical.Stations(from, to, tolerance, true));
            var sorted = stations.Where(s => s > from && s < to).Append(from).Append(to).OrderBy(s => s).ToList();
            var result = new List<double>();
            foreach (var s in sorted)
                if (result.Count == 0 || s - result[result.Count - 1] > tolerance) result.Add(s);
            if (result[result.Count - 1] < to) result[result.Count - 1] = to;
            return result;
        }
    }
}
