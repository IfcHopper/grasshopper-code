using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace IfcHopper.Core.Backends.Xbim
{
    /// <summary>Conservative read-time coordinate screening without converting objects or faceting bodies.</summary>
    internal static class XbimCoordinateChecks
    {
        // At 100 km, single-precision coordinates have a spacing of 7.8 mm. This is
        // a screening threshold, not a claim that every Rhino operation is safe below it.
        private const double LargeCoordinateMetres = 100000;

        public static IReadOnlyList<string> Warnings(IModel model)
        {
            var scale = model.ModelFactors.LengthToMetresConversionFactor;
            bool Large(IEnumerable<double> coordinates) => coordinates.Any(v => Math.Abs(v * scale) >= LargeCoordinateMetres);
            // A map conversion's Eastings/Northings are not geometry coordinates and do
            // not trigger this warning. Raw coordinates can belong to unused representations;
            // screening them is intentionally conservative and does not force lazy geometry.
            var large = model.Instances.OfType<IIfcCartesianPoint>().Any(p => Large(p.Coordinates.Select(v => (double)v)))
                || model.Instances.OfType<IIfcCartesianPointList3D>().Any(p => p.CoordList.Any(row => Large(row.Select(v => (double)v))))
                || model.Instances.OfType<IIfcCartesianPointList2D>().Any(p => p.CoordList.Any(row => Large(row.Select(v => (double)v))));
            if (!large)
            {
                // Individually small relative translations can add up to a large world
                // placement. Screen local chains only; unsupported placements are reported
                // by the geometry reader when accessed, not evaluated during this check.
                var resolver = new XbimAlignments();
                foreach (var placement in model.Instances.OfType<IIfcLocalPlacement>())
                {
                    var chain = new HashSet<IIfcObjectPlacement>();
                    IIfcObjectPlacement current = placement;
                    while (current is IIfcLocalPlacement local && chain.Add(current)) current = local.PlacementRelTo;
                    if (current != null) continue; // Cycle or non-local placement.
                    if (Large(resolver.Resolve(placement).Origin)) { large = true; break; }
                }
            }
            return large ? new[]
            {
                "Large placement or geometry coordinates detected (an absolute coordinate of 100 km or more). " +
                "IFC meshes use double precision to preserve small details; coordinates and georeferencing are unchanged. " +
                "Viewport display and mesh booleans may still be inaccurate far from the origin. " +
                "Use Move Model to work near the origin; Keep Map Location compensates an existing georeference for supported files. " +
                "Otherwise re-export with a local model origin and preserved georeference from the authoring application."
            } : Array.Empty<string>();
        }
    }
}
