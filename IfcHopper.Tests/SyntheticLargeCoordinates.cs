using IfcHopper.Core.Model;

namespace IfcHopper.Tests
{
    // Authored here, not an external fixture. A 20 mm wall at survey coordinates.
    internal static class SyntheticLargeCoordinates
    {
        public static IfcHopperModel Create(bool placed, string unit = "m", double northing = 5000000)
        {
            const double easting = 500000;
            var vertices = new[]
            {
                new[] { 0.0, 0, 0 }, new[] { 4.0, 0, 0 }, new[] { 4.0, 0.02, 0 }, new[] { 0.0, 0.02, 0 },
                new[] { 0.0, 0, 3 }, new[] { 4.0, 0, 3 }, new[] { 4.0, 0.02, 3 }, new[] { 0.0, 0.02, 3 },
            };
            foreach (var p in vertices) { p[0] += easting; p[1] += northing; }
            var wall = new Element("20 mm survey wall", "IfcWall")
            {
                Placement = placed ? Placement.Translation(easting, northing, 0) : Placement.World,
            };
            wall.Geometry.Add(new MeshGeometry(vertices, new[]
            {
                new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 },
                new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 },
            }, true));
            var model = IfcHopperModel.CreateDefault();
            model.Project.Units = new Units { Length = Units.Find(UnitKind.Length, unit) };
            var site = model.Project.Sites[0];
            site.Placement = placed ? Placement.Translation(easting - 10, northing - 20, 0) : Placement.World;
            ((Building)site.Facilities[0]).Storeys[0].Elements.Add(wall);
            return model;
        }
    }
}
