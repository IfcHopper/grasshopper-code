using IfcHopper.Core.Model;
using Rhino;
using Rhino.Geometry;

namespace IfcHopper.Components
{
    /// <summary>Converts lengths between the active Rhino document units and the Core model (metres), and maps Rhino units to IFC units.</summary>
    internal static class DocumentUnits
    {
        public static double ToMetres(double value) => value * MetresPerUnit();

        public static double FromMetres(double value) => value / MetresPerUnit();

        /// <summary>A value of a length dimension (1 length, 2 area, 3 volume) in document units to metres, m² or m³; other dimensions are unchanged.</summary>
        public static double ToSi(double value, int dimension) => dimension == 0 ? value : value * global::System.Math.Pow(MetresPerUnit(), dimension);

        public static double FromSi(double value, int dimension) => dimension == 0 ? value : value / global::System.Math.Pow(MetresPerUnit(), dimension);

        public static double MetresPerUnit()
        {
            var doc = RhinoDoc.ActiveDoc;
            return doc == null ? 1.0 : RhinoMath.UnitScale(doc.ModelUnitSystem, UnitSystem.Meters);
        }

        public static UnitSystem System => RhinoDoc.ActiveDoc?.ModelUnitSystem ?? UnitSystem.Meters;

        /// <summary>Rhino plane (document units) to a Core placement (metres).</summary>
        public static Placement ToPlacement(Plane plane)
        {
            var scale = MetresPerUnit();
            return new Placement(
                new[] { plane.OriginX * scale, plane.OriginY * scale, plane.OriginZ * scale },
                new[] { plane.XAxis.X, plane.XAxis.Y, plane.XAxis.Z },
                new[] { plane.ZAxis.X, plane.ZAxis.Y, plane.ZAxis.Z });
        }

        /// <summary>Core placement (metres) to a Rhino plane (document units).</summary>
        public static Plane ToPlane(Placement placement)
        {
            var scale = 1.0 / MetresPerUnit();
            var origin = new Point3d(placement.Origin[0] * scale, placement.Origin[1] * scale, placement.Origin[2] * scale);
            var y = placement.YAxis;
            return new Plane(origin, new Vector3d(placement.XAxis[0], placement.XAxis[1], placement.XAxis[2]), new Vector3d(y[0], y[1], y[2]));
        }

        /// <summary>Rhino absolute tolerance in metres.</summary>
        public static double ToleranceMetres => (RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001) * MetresPerUnit();

        /// <summary>IFC length unit of the document, or null when IFC has no matching unit (e.g. microns, yards).</summary>
        public static UnitDefinition LengthUnit => Units.FromFactor(UnitKind.Length, RhinoMath.UnitScale(System, UnitSystem.Meters));

        /// <summary>Rhino unit system of an IFC length unit, or null when Rhino has no matching unit.</summary>
        public static UnitSystem? ToRhino(UnitDefinition length)
        {
            foreach (var system in new[] { UnitSystem.Millimeters, UnitSystem.Centimeters, UnitSystem.Decimeters, UnitSystem.Meters, UnitSystem.Kilometers, UnitSystem.Inches, UnitSystem.Feet })
                if (Units.FromFactor(UnitKind.Length, RhinoMath.UnitScale(system, UnitSystem.Meters)) == length) return system;
            return null;
        }
    }
}
