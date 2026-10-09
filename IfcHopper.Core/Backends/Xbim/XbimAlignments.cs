using System;
using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.Geometry;
using IfcHopper.Core.Model;
using Xbim.Ifc.Extensions;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4x3.GeometricConstraintResource;
using Xbim.Ifc4x3.GeometryResource;
using static IfcHopper.Core.Backends.Xbim.XbimGeometryReader;

namespace IfcHopper.Core.Backends.Xbim
{
    /// <summary>
    /// Resolves object placements to world transforms (file units), including IFC4X3 linear placements along alignment curves
    /// (IfcGradientCurve, IfcCompositeCurve of IfcCurveSegment with line, circle, clothoid and polynomial parent curves).
    /// Results are cached per instance; use one instance per model.
    /// </summary>
    internal class XbimAlignments
    {
        private readonly Dictionary<IIfcObjectPlacement, Affine> _placements = new Dictionary<IIfcObjectPlacement, Affine>();
        private readonly Dictionary<IfcCurve, AlignmentCurve> _curves = new Dictionary<IfcCurve, AlignmentCurve>();

        /// <summary>World transform of a placement; throws <see cref="NotSupportedException"/> for unsupported linear placements.</summary>
        public Affine Resolve(IIfcObjectPlacement placement)
        {
            if (placement == null) return Affine.Identity;
            if (_placements.TryGetValue(placement, out var cached)) return cached;

            Affine result;
            switch (placement)
            {
                case IfcLinearPlacement linear:
                    result = Resolve(linear.PlacementRelTo).Compose(Frame(linear.RelativePlacement));
                    break;
                case IIfcLocalPlacement local when local.RelativePlacement is IIfcAxis2Placement axis:
                    result = Resolve(local.PlacementRelTo).Compose(Axis(axis));
                    break;
                default:
                    result = FromMatrix(placement.ToMatrix3D());
                    break;
            }
            return _placements[placement] = result;
        }

        /// <summary>
        /// Frame of an IfcAxis2PlacementLinear: origin on the curve plus offsets; X along the horizontal tangent, Y to the left, Z up,
        /// unless Axis and RefDirection (given in that frame) say otherwise.
        /// </summary>
        public Affine Frame(IfcAxis2PlacementLinear placement)
        {
            var (curve, distance, lateral, vertical) = Expression(placement);
            var frame = CurveFrame(curve, distance, lateral, vertical);
            if (placement.Axis == null && placement.RefDirection == null) return frame;

            var z = placement.Axis != null ? frame.ApplyVector(Direction(placement.Axis)) : frame.Z;
            var x = placement.RefDirection != null ? frame.ApplyVector(Direction(placement.RefDirection)) : frame.X;
            var axes = new Placement(frame.Origin, x, z);
            return new Affine(axes.Origin, axes.XAxis, axes.YAxis, axes.ZAxis);
        }

        /// <summary>Curve-aligned frame at <paramref name="distance"/>, moved by lateral (to the left) and vertical offsets.</summary>
        public static Affine CurveFrame(AlignmentCurve curve, double distance, double lateral = 0, double vertical = 0)
        {
            var (point, angle) = curve.At(distance);
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            return new Affine(new[] { point[0] - lateral * sin, point[1] + lateral * cos, point[2] + vertical },
                new[] { cos, sin, 0.0 }, new[] { -sin, cos, 0.0 }, new[] { 0.0, 0, 1 });
        }

        /// <summary>Curve, distance along (with the longitudinal offset) and lateral and vertical offsets of a linear placement.</summary>
        public (AlignmentCurve Curve, double Distance, double Lateral, double Vertical) Expression(IfcAxis2PlacementLinear placement)
        {
            if (!(placement.Location is IfcPointByDistanceExpression point)) throw Unsupported(placement.Location);
            var distance = Double(point.DistanceAlong) + (point.OffsetLongitudinal.HasValue ? Double(point.OffsetLongitudinal.Value) : 0);
            return (Curve(point.BasisCurve), distance,
                point.OffsetLateral.HasValue ? Double(point.OffsetLateral.Value) : 0,
                point.OffsetVertical.HasValue ? Double(point.OffsetVertical.Value) : 0);
        }

        /// <summary>Alignment curve of a gradient curve, segmented reference curve (cant ignored) or composite curve of curve segments.</summary>
        public AlignmentCurve Curve(IfcCurve curve)
        {
            if (_curves.TryGetValue(curve, out var cached)) return cached;
            AlignmentCurve result;
            switch (curve)
            {
                case IfcSegmentedReferenceCurve reference:
                    result = Curve(reference.BaseCurve);
                    break;
                case IfcGradientCurve gradient:
                    result = new AlignmentCurve(Polycurve(gradient.BaseCurve), Polycurve(gradient.Segments));
                    break;
                case IfcCompositeCurve composite:
                    result = new AlignmentCurve(Polycurve(composite.Segments));
                    break;
                case IfcPolyline polyline:
                    result = Polyline(polyline.Points.Select(p => Point(p)).ToList(), polyline.Points.All(p => p.Coordinates.Count == 3));
                    break;
                default:
                    throw Unsupported(curve);
            }
            return _curves[curve] = result;
        }

        /// <summary>Polyline as straight horizontal segments with straight grades between the point heights (3D only).</summary>
        private static AlignmentCurve Polyline(List<double[]> points, bool threeD)
        {
            var horizontal = new List<CurveSegment2D>();
            var vertical = new List<CurveSegment2D>();
            var distance = 0.0;
            for (int i = 0; i + 1 < points.Count; i++)
            {
                double[] a = points[i], b = points[i + 1];
                double dx = b[0] - a[0], dy = b[1] - a[1], dz = b[2] - a[2], length = Math.Sqrt(dx * dx + dy * dy);
                if (length == 0) continue;
                horizontal.Add(new CurveSegment2D(new LineCurve(new[] { 0.0, 0 }, 0), 0, length, new[] { a[0], a[1] }, Math.Atan2(dy, dx)));
                vertical.Add(new CurveSegment2D(new LineCurve(new[] { 0.0, 0 }, 0), 0, Math.Sqrt(length * length + dz * dz), new[] { distance, a[2] }, Math.Atan2(dz, length)));
                distance += length;
            }
            return new AlignmentCurve(new Polycurve2D(horizontal), threeD ? new Polycurve2D(vertical) : null);
        }

        private static Polycurve2D Polycurve(IfcBoundedCurve curve) =>
            curve is IfcCompositeCurve composite ? Polycurve(composite.Segments) : throw Unsupported(curve);

        internal static Polycurve2D Polycurve(IEnumerable<IfcSegment> segments) =>
            new Polycurve2D(segments.Select(s => s is IfcCurveSegment segment ? Segment(segment) : throw Unsupported(s)));

        private static CurveSegment2D Segment(IfcCurveSegment segment)
        {
            var (origin, angle) = Plane(segment.Placement);
            var parent = Parent(segment.ParentCurve, out var parameterScale);
            double Measure(object value) => Double(value) * (value is global::Xbim.Ifc4x3.MeasureResource.IfcParameterValue ? parameterScale : 1);
            return new CurveSegment2D(parent, Measure(segment.SegmentStart), Measure(segment.SegmentLength), origin, angle);
        }

        /// <summary>Parent curve; <paramref name="parameterScale"/> converts IfcParameterValue measures to arc length.</summary>
        private static PlanarCurve Parent(IfcCurve curve, out double parameterScale)
        {
            parameterScale = 1;
            switch (curve)
            {
                case IfcLine line:
                    {
                        var direction = Direction(line.Dir.Orientation);
                        parameterScale = Double(line.Dir.Magnitude);
                        return new LineCurve(Point(line.Pnt), Math.Atan2(direction[1], direction[0]));
                    }
                case IfcCircle circle:
                    {
                        var (center, angle) = Plane(circle.Position);
                        parameterScale = Double(circle.Radius);
                        return new CircleCurve(center, angle, Double(circle.Radius));
                    }
                case IfcClothoid clothoid:
                    {
                        var (origin, angle) = Plane(clothoid.Position);
                        return new ClothoidCurve(origin, angle, Double(clothoid.ClothoidConstant));
                    }
                case IfcPolynomialCurve polynomial:
                    {
                        var (origin, angle) = Plane(polynomial.Position);
                        return new PolynomialCurve(origin, angle,
                            polynomial.CoefficientsX?.Select(c => Double(c)).ToArray() ?? new double[0],
                            polynomial.CoefficientsY?.Select(c => Double(c)).ToArray() ?? new double[0]);
                    }
                default:
                    throw Unsupported(curve);
            }
        }

        /// <summary>Origin (X, Y) and X axis angle of a 2D or 3D placement.</summary>
        private static (double[] Origin, double Angle) Plane(object placement)
        {
            var axes = Axis(placement as IIfcAxis2Placement);
            return (new[] { axes.Origin[0], axes.Origin[1] }, Math.Atan2(axes.X[1], axes.X[0]));
        }
    }
}
