using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.Geometry;
using IfcHopper.Core.Model;
using Xbim.Common;
using Xbim.Ifc.Extensions;
using Xbim.Ifc4.Interfaces;

namespace IfcHopper.Core.Backends.Xbim
{
    /// <summary>
    /// Facets the Body representation of a product into world meshes (metres): face sets, faceted Breps and surface models,
    /// extruded area solids, half space clippings and mapped items. Other items are listed as skipped, by IFC class.
    /// </summary>
    internal class XbimGeometryReader
    {
        private const string BodyIdentifier = "Body";
        private const int CircleSegments = 24;

        /// <summary>Representation types of 3D bodies, used when a file does not label its representations.</summary>
        private static readonly HashSet<string> SolidTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Tessellation", "Brep", "SweptSolid", "AdvancedSweptSolid", "SurfaceModel", "MappedRepresentation", "AdvancedBrep", "CSG", "Clipping", "SolidModel",
        };

        /// <summary>Largest distance between sampled alignment curves and the true curve, in metres.</summary>
        private const double ChordTolerance = 0.005;

        private readonly double _toMetres;
        private readonly XbimAlignments _alignments;
        private readonly ICollection<string> _skipped;

        public XbimGeometryReader(double toMetres, XbimAlignments alignments, ICollection<string> skipped)
        {
            _toMetres = toMetres;
            _alignments = alignments;
            _skipped = skipped;
        }

        public List<MeshGeometry> Read(IIfcProduct product)
        {
            var meshes = new List<MeshGeometry>();
            var items = BodyRepresentations(product).SelectMany(r => r.Items).ToList();
            if (items.Count == 0) return meshes;

            Affine world;
            try
            {
                world = _alignments.Resolve(product.ObjectPlacement);
            }
            catch (NotSupportedException e)
            {
                _skipped.Add($"{product.ObjectPlacement.ExpressType.Name} ({e.Message})");
                return meshes;
            }
            catch (Exception)
            {
                _skipped.Add($"{product.ObjectPlacement.ExpressType.Name} (invalid)");
                return meshes;
            }

            foreach (var item in items) AddItem(item, world, null, meshes);
            return meshes;
        }

        /// <summary>Meshes of the Body representation maps of a type, in type coordinates (metres).</summary>
        public List<MeshGeometry> Read(IIfcTypeProduct type)
        {
            var meshes = new List<MeshGeometry>();
            foreach (var map in BodyMaps(type))
                foreach (var item in map.MappedRepresentation.Items) AddItem(item, Axis(map.MappingOrigin), null, meshes);
            return meshes;
        }

        /// <summary>
        /// Transform from the coordinates of <paramref name="type"/> to world (metres) when the Body of <paramref name="product"/> maps each
        /// Body map of the type once, with the same target and without a style of its own; otherwise null.
        /// </summary>
        public Affine InstanceTransform(IIfcProduct product, IIfcTypeProduct type)
        {
            var maps = BodyMaps(type).ToList();
            var items = BodyRepresentations(product).SelectMany(r => r.Items).ToList();
            var mapped = items.OfType<IIfcMappedItem>().ToList();
            if (maps.Count == 0 || mapped.Count != items.Count || mapped.Count != maps.Count || mapped.Any(m => m.StyledByItem.Any())
                || maps.Any(map => !mapped.Any(m => m.MappingSource == map)))
                return null;

            try
            {
                var targets = mapped.Select(m => Operator(m.MappingTarget)).ToList();
                if (targets.Any(t => !Close(t, targets[0]))) return null;
                var world = _alignments.Resolve(product.ObjectPlacement).Compose(targets[0]);
                return new Affine(Scale(world.Origin, _toMetres), world.X, world.Y, world.Z);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool Close(Affine a, Affine b) =>
            new[] { (a.Origin, b.Origin), (a.X, b.X), (a.Y, b.Y), (a.Z, b.Z) }.All(p => Distance(p.Item1, p.Item2) <= 1e-9 * (1 + Math.Sqrt(Dot(p.Item1, p.Item1))));

        /// <summary>Representation maps of a type that hold its Body.</summary>
        internal static IEnumerable<IIfcRepresentationMap> BodyMaps(IIfcTypeProduct type)
        {
            var maps = type.RepresentationMaps.Where(m => m.MappedRepresentation is IIfcShapeRepresentation).ToList();
            var body = BodyOf(maps.Select(m => (IIfcShapeRepresentation)m.MappedRepresentation)).ToList();
            return maps.Where(m => body.Contains((IIfcShapeRepresentation)m.MappedRepresentation));
        }

        private static Colour StyleColour(IIfcRepresentationItem item) => SurfaceColour(item.StyledByItem);

        /// <summary>Colour of the first surface style of styled items (IFC4 styles or IFC2X3 style assignments), or null.</summary>
        internal static Colour SurfaceColour(IEnumerable<IIfcStyledItem> styledItems)
        {
            var styles = styledItems.SelectMany(s => s.Styles)
                .SelectMany(s => s is IIfcPresentationStyleAssignment assignment ? assignment.Styles.Cast<object>() : new object[] { s });
            var shading = styles.OfType<IIfcSurfaceStyle>().SelectMany(s => s.Styles).OfType<IIfcSurfaceStyleShading>().FirstOrDefault(s => s.SurfaceColour != null);
            if (shading == null) return null;

            var rgb = shading.SurfaceColour;
            return new Colour(Double(rgb.Red), Double(rgb.Green), Double(rgb.Blue), shading.Transparency.HasValue ? Double(shading.Transparency.Value) : 0);
        }

        internal static IEnumerable<IIfcShapeRepresentation> BodyRepresentations(IIfcProduct product) =>
            BodyOf(product.Representation?.Representations.OfType<IIfcShapeRepresentation>() ?? Enumerable.Empty<IIfcShapeRepresentation>());

        /// <summary>The Body representations, or when none is labelled, the unlabelled ones of a solid representation type.</summary>
        private static IEnumerable<IIfcShapeRepresentation> BodyOf(IEnumerable<IIfcShapeRepresentation> representations)
        {
            var shapes = representations.ToList();
            var body = shapes.Where(IsBody).ToList();
            return body.Count > 0 ? body : shapes.Where(s => s.RepresentationIdentifier == null && SolidTypes.Contains(s.RepresentationType?.ToString() ?? ""));
        }

        private static bool IsBody(IIfcShapeRepresentation shape)
        {
            var identifier = shape.RepresentationIdentifier?.ToString();
            if (!string.IsNullOrEmpty(identifier)) return identifier.Equals(BodyIdentifier, StringComparison.OrdinalIgnoreCase);
            return shape.ContextOfItems is IIfcGeometricRepresentationSubContext sub && BodyIdentifier.Equals(sub.ContextIdentifier?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Adds the meshes of an item; its own style wins over <paramref name="inherited"/> (the style of an enclosing mapped item).</summary>
        private void AddItem(IIfcRepresentationItem item, Affine transform, Colour inherited, List<MeshGeometry> meshes)
        {
            try
            {
                var colour = StyleColour(item) ?? inherited;
                if (item is IIfcMappedItem mapped)
                {
                    var source = mapped.MappingSource;
                    var mappedTransform = transform.Compose(Operator(mapped.MappingTarget)).Compose(Axis(source.MappingOrigin));
                    foreach (var inner in source.MappedRepresentation.Items) AddItem(inner, mappedTransform, colour, meshes);
                    return;
                }

                var mesh = Facet(item);
                if (mesh.Faces.Count > 0) meshes.Add(ToWorld(mesh, transform).WithColour(colour));
            }
            catch (NotSupportedException e)
            {
                _skipped.Add(e.Message == item.ExpressType.Name ? e.Message : $"{item.ExpressType.Name} ({e.Message})");
            }
            catch (Exception)
            {
                _skipped.Add($"{item.ExpressType.Name} (invalid)");
            }
        }

        private LocalMesh Facet(IIfcRepresentationItem item)
        {
            switch (item)
            {
                case IIfcTriangulatedFaceSet faceSet: return TriangulatedFaceSet(faceSet);
                case IIfcPolygonalFaceSet faceSet: return PolygonalFaceSet(faceSet);
                case IIfcFacetedBrep brep: return Faces(brep.Outer.CfsFaces, true);
                case IIfcShellBasedSurfaceModel model: return Faces(model.SbsmBoundary.OfType<IIfcConnectedFaceSet>().SelectMany(s => s.CfsFaces), false);
                case IIfcFaceBasedSurfaceModel model: return Faces(model.FbsmFaces.SelectMany(s => s.CfsFaces), false);
                case IIfcExtrudedAreaSolid solid: return Extrusion(solid);
                case global::Xbim.Ifc4x3.GeometricModelResource.IfcSectionedSolidHorizontal solid: return SectionedSolid(solid);
                case IIfcBooleanResult boolean: return Clipping(boolean);
                default: throw Unsupported(item);
            }
        }

        /// <summary>A solid minus a plain half space (IfcBooleanClippingResult, or an IfcBooleanResult of that form).</summary>
        private LocalMesh Clipping(IIfcBooleanResult boolean)
        {
            if (boolean.Operator != IfcBooleanOperator.DIFFERENCE) throw new NotSupportedException(boolean.Operator.ToString());
            if (!(boolean.SecondOperand is IIfcHalfSpaceSolid halfSpace) || halfSpace is IIfcPolygonalBoundedHalfSpace)
                throw Unsupported(boolean.SecondOperand as IPersistEntity);
            if (!(halfSpace.BaseSurface is IIfcPlane plane)) throw Unsupported(halfSpace.BaseSurface);
            if (!(boolean.FirstOperand is IIfcRepresentationItem first)) throw Unsupported(boolean.FirstOperand as IPersistEntity);

            var solid = Facet(first);
            if (solid.Closed && new MeshGeometry(solid.Vertices, solid.Faces, true).SignedVolume() < 0) solid.Faces.ForEach(Array.Reverse);
            var position = Axis(plane.Position);
            return Clip(solid, position.Origin, halfSpace.AgreementFlag ? position.Z : Scale(position.Z, -1));
        }

        /// <summary>
        /// The part of a mesh behind a plane (signed distance along <paramref name="normal"/> at most zero). Faces across the plane are
        /// triangulated and cut; on closed meshes the cut edges are chained into loops and capped, facing along <paramref name="normal"/>.
        /// </summary>
        private static LocalMesh Clip(LocalMesh mesh, double[] origin, double[] normal)
        {
            var result = new LocalMesh { Closed = mesh.Closed };
            if (mesh.Vertices.Count == 0) return result;
            var extent = Enumerable.Range(0, 3).Max(k => mesh.Vertices.Max(v => v[k]) - mesh.Vertices.Min(v => v[k]));
            var distance = mesh.Vertices.Select(v => Dot(Subtract(v, origin), normal)).Select(d => Math.Abs(d) <= 1e-9 * extent ? 0 : d).ToList();

            // Cut edges run from where a face enters the kept side to where it leaves it: the reverse of the kept face's edge, as the cap needs.
            var cuts = new List<(double[] Entry, double[] Exit)>();
            foreach (var face in mesh.Faces)
            {
                if (face.All(i => distance[i] <= 0)) result.Faces.Add(result.AddLoop(face.Select(i => mesh.Vertices[i])).ToArray());
                if (face.All(i => distance[i] <= 0) || face.All(i => distance[i] > 0)) continue;

                var triangles = face.Length == 3 ? new List<int[]> { new[] { 0, 1, 2 } } : Triangulator.Triangulate(face.Select(i => mesh.Vertices[i]).ToList());
                foreach (var t in triangles)
                {
                    var points = new List<double[]>();
                    double[] entry = null, exit = null;
                    for (int k = 0; k < 3; k++)
                    {
                        int a = face[t[k]], b = face[t[(k + 1) % 3]];
                        if (distance[a] <= 0) points.Add(mesh.Vertices[a]);
                        if ((distance[a] <= 0) == (distance[b] <= 0)) continue;
                        var x = distance[a] == 0 ? mesh.Vertices[a] : distance[b] == 0 ? mesh.Vertices[b]
                            : Add(mesh.Vertices[a], Scale(Subtract(mesh.Vertices[b], mesh.Vertices[a]), distance[a] / (distance[a] - distance[b])));
                        points.Add(x);
                        if (distance[a] <= 0) exit = x;
                        else entry = x;
                    }
                    var polygon = points.Where((p, k) => !Same(p, points[(k + 1) % points.Count])).ToList();
                    if (polygon.Count >= 3) result.Faces.Add(result.AddLoop(polygon).ToArray());
                    if (entry != null && exit != null && !Same(entry, exit)) cuts.Add((entry, exit));
                }
            }

            if (mesh.Closed) Cap(result, cuts, normal, 1e-6 * extent);
            return result;
        }

        /// <summary>Chains cut edges into loops and fills them facing along <paramref name="normal"/>; clockwise loops become holes of the smallest loop around them.</summary>
        private static void Cap(LocalMesh mesh, List<(double[] Entry, double[] Exit)> cuts, double[] normal, double tolerance)
        {
            var loops = new List<List<double[]>>();
            while (cuts.Count > 0)
            {
                var loop = new List<double[]> { cuts[cuts.Count - 1].Entry };
                var end = cuts[cuts.Count - 1].Exit;
                cuts.RemoveAt(cuts.Count - 1);
                while (loop != null && Distance(end, loop[0]) > tolerance)
                {
                    var next = cuts.FindIndex(c => Distance(c.Entry, end) <= tolerance);
                    if (next < 0) loop = null;
                    else
                    {
                        loop.Add(cuts[next].Entry);
                        end = cuts[next].Exit;
                        cuts.RemoveAt(next);
                    }
                }
                if (loop != null && loop.Count >= 3) loops.Add(loop);
            }

            var u = Normalize(Cross(normal, Math.Abs(normal[2]) < 0.9 ? new[] { 0.0, 0, 1 } : new[] { 1.0, 0, 0 }));
            var v = Cross(normal, u);
            var flat = loops.Select(l => l.Select(p => new[] { Dot(p, u), Dot(p, v), 0.0 }).ToList()).ToList();
            var areas = flat.Select(Area).ToList();
            var holes = loops.Select(_ => new List<List<int>>()).ToList();
            for (int j = 0; j < loops.Count; j++)
            {
                if (areas[j] >= 0) continue;
                var owner = -1;
                for (int i = 0; i < loops.Count; i++)
                    if (areas[i] > 0 && (owner < 0 || areas[i] < areas[owner]) && Inside(flat[j][0], flat[i])) owner = i;
                if (owner >= 0) holes[owner].Add(mesh.AddLoop(loops[j]));
            }
            for (int i = 0; i < loops.Count; i++)
                if (areas[i] > 0) mesh.AddFace(mesh.AddLoop(loops[i]), holes[i]);
        }

        /// <summary>Even-odd test of a point against a polygon in the XY plane.</summary>
        private static bool Inside(double[] point, List<double[]> polygon)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                double[] a = polygon[i], b = polygon[j];
                if ((a[1] > point[1]) != (b[1] > point[1]) && point[0] < a[0] + (point[1] - a[1]) * (b[0] - a[0]) / (b[1] - a[1])) inside = !inside;
            }
            return inside;
        }

        /// <summary>World mesh in metres; closed meshes are turned outwards, also when the file has them inside out.</summary>
        private MeshGeometry ToWorld(LocalMesh mesh, Affine transform)
        {
            var mirrored = transform.IsMirrored;
            var world = new MeshGeometry(
                mesh.Vertices.Select(v => transform.Apply(v).Select(c => c * _toMetres).ToArray()),
                mesh.Faces.Select(f => mirrored ? f.Reverse().ToArray() : f),
                mesh.Closed);
            return world.IsClosed && world.SignedVolume() < 0 ? world.Flip() : world;
        }

        private static LocalMesh TriangulatedFaceSet(IIfcTriangulatedFaceSet faceSet)
        {
            var mesh = new LocalMesh { Closed = faceSet.Closed == true };
            mesh.Vertices.AddRange(faceSet.Coordinates.CoordList.Select(Coordinates));
            var map = PointIndexMap(faceSet.PnIndex);
            foreach (var triangle in faceSet.CoordIndex) mesh.AddFace(triangle.Select(i => map(Int(i))).ToList());
            return mesh;
        }

        private static LocalMesh PolygonalFaceSet(IIfcPolygonalFaceSet faceSet)
        {
            var mesh = new LocalMesh { Closed = faceSet.Closed == true };
            mesh.Vertices.AddRange(faceSet.Coordinates.CoordList.Select(Coordinates));
            var map = PointIndexMap(faceSet.PnIndex);
            foreach (var face in faceSet.Faces)
            {
                var holes = (face as IIfcIndexedPolygonalFaceWithVoids)?.InnerCoordIndices.Select(h => h.Select(i => map(Int(i))).ToList()).ToList();
                mesh.AddFace(face.CoordIndex.Select(i => map(Int(i))).ToList(), holes);
            }
            return mesh;
        }

        /// <summary>Maps 1-based face indices to 0-based vertex indices, through PnIndex when present.</summary>
        private static Func<int, int> PointIndexMap(IEnumerable pnIndex)
        {
            var pn = pnIndex?.Cast<object>().Select(Int).ToList();
            return pn != null && pn.Count > 0 ? i => pn[i - 1] - 1 : (Func<int, int>)(i => i - 1);
        }

        private static LocalMesh Faces(IEnumerable<IIfcFace> faces, bool closed)
        {
            var mesh = new LocalMesh { Closed = closed };
            foreach (var face in faces)
            {
                var bounds = face.Bounds.ToList();
                var outer = bounds.FirstOrDefault(b => b is IIfcFaceOuterBound) ?? bounds.FirstOrDefault();
                if (outer == null) continue;
                var holes = bounds.Where(b => b != outer).Select(b => mesh.AddLoop(Loop(b))).ToList();
                mesh.AddFace(mesh.AddLoop(Loop(outer)), holes);
            }
            return mesh;
        }

        private static IEnumerable<double[]> Loop(IIfcFaceBound bound)
        {
            if (!(bound.Bound is IIfcPolyLoop loop)) throw Unsupported(bound.Bound);
            var points = loop.Polygon.Select(Point).ToList();
            if (!bound.Orientation) points.Reverse();
            return points;
        }

        /// <summary>Profile extruded along its direction: caps keep the profile holes, side faces point outwards.</summary>
        private LocalMesh Extrusion(IIfcExtrudedAreaSolid solid)
        {
            var offset = Scale(Normalize(Direction(solid.ExtrudedDirection)), Double(solid.Depth));
            var mesh = new LocalMesh { Closed = true };
            foreach (var region in Profile(solid.SweptArea))
            {
                var loops = new[] { Oriented(region.Outer, true) }.Concat(region.Holes.Select(h => Oriented(h, false))).ToList();
                var bottom = loops.Select(l => mesh.AddLoop(l)).ToList();
                var top = loops.Select(l => mesh.AddLoop(l.Select(p => Add(p, offset)))).ToList();

                mesh.AddFace(top[0], top.Skip(1).ToList());
                mesh.AddFace(Enumerable.Reverse(bottom[0]).ToList(), bottom.Skip(1).ToList());
                for (int k = 0; k < loops.Count; k++)
                    for (int i = 0, n = bottom[k].Count; i < n; i++)
                        mesh.Faces.Add(new[] { bottom[k][i], bottom[k][(i + 1) % n], top[k][(i + 1) % n], top[k][i] });
            }

            if (offset[2] < 0) mesh.Faces.ForEach(Array.Reverse);
            if (solid.Position != null)
            {
                var position = Axis(solid.Position);
                for (int i = 0; i < mesh.Vertices.Count; i++) mesh.Vertices[i] = position.Apply(mesh.Vertices[i]);
            }
            return mesh;
        }

        /// <summary>
        /// Cross sections swept along the directrix: each profile lies in the vertical plane across the horizontal tangent
        /// (profile X to the left, Y up), and consecutive profiles with the same structure are interpolated linearly.
        /// </summary>
        private LocalMesh SectionedSolid(global::Xbim.Ifc4x3.GeometricModelResource.IfcSectionedSolidHorizontal solid)
        {
            var curve = _alignments.Curve(solid.Directrix);
            var positions = solid.CrossSectionPositions.Select(_alignments.Expression).ToList();
            var sections = solid.CrossSections.Select(p => Profile(p).ToList()).ToList();
            if (positions.Count < 2 || positions.Count != sections.Count) throw new NotSupportedException("cross sections and positions do not match");
            if (sections.Any(s => !SameStructure(s, sections[0]))) throw new NotSupportedException("cross sections with different point counts");

            // Orient every section like the first one, so interpolated loops keep their correspondence.
            var reverse = sections[0].Select(r => (Outer: Area(r.Outer) < 0, Holes: r.Holes.Select(h => Area(h) > 0).ToList())).ToList();
            sections = sections.Select(s => s.Select((r, i) => new Region
            {
                Outer = reverse[i].Outer ? Enumerable.Reverse(r.Outer).ToList() : r.Outer,
                Holes = r.Holes.Select((h, j) => reverse[i].Holes[j] ? Enumerable.Reverse(h).ToList() : h).ToList(),
            }).ToList()).ToList();

            var mesh = new LocalMesh { Closed = true };
            var rings = new List<List<(List<int> Outer, List<List<int>> Holes)>>();
            foreach (var s in curve.Stations(positions[0].Distance, positions[positions.Count - 1].Distance, ChordTolerance / _toMetres))
            {
                var span = 0;
                while (span < positions.Count - 2 && positions[span + 1].Distance <= s) span++;
                var (a, b) = (positions[span], positions[span + 1]);
                var f = b.Distance > a.Distance ? Math.Max(0, Math.Min(1, (s - a.Distance) / (b.Distance - a.Distance))) : 0;
                var frame = XbimAlignments.CurveFrame(curve, s, a.Lateral + f * (b.Lateral - a.Lateral), a.Vertical + f * (b.Vertical - a.Vertical));

                List<int> Ring(List<double[]> from, List<double[]> to) =>
                    mesh.AddLoop(from.Select((p, i) => frame.Apply(new[] { 0, p[0] + f * (to[i][0] - p[0]), p[1] + f * (to[i][1] - p[1]) })));
                rings.Add(sections[span].Select((r, i) => (Ring(r.Outer, sections[span + 1][i].Outer),
                    r.Holes.Select((h, j) => Ring(h, sections[span + 1][i].Holes[j])).ToList())).ToList());
            }

            var first = rings[0];
            var last = rings[rings.Count - 1];
            for (int r = 0; r < first.Count; r++)
            {
                mesh.AddFace(Enumerable.Reverse(first[r].Outer).ToList(), first[r].Holes);
                mesh.AddFace(last[r].Outer, last[r].Holes);
            }
            for (int k = 0; k + 1 < rings.Count; k++)
                for (int r = 0; r < rings[k].Count; r++)
                    foreach (var (loop, next) in new[] { (rings[k][r].Outer, rings[k + 1][r].Outer) }.Concat(rings[k][r].Holes.Zip(rings[k + 1][r].Holes, (h, n) => (h, n))))
                        for (int i = 0, n = loop.Count; i < n; i++)
                            mesh.Faces.Add(new[] { loop[i], loop[(i + 1) % n], next[(i + 1) % n], next[i] });
            return mesh;
        }

        private static bool SameStructure(List<Region> a, List<Region> b) =>
            a.Count == b.Count && a.Zip(b, (x, y) => x.Outer.Count == y.Outer.Count && x.Holes.Count == y.Holes.Count &&
                x.Holes.Zip(y.Holes, (h, k) => h.Count == k.Count).All(same => same)).All(same => same);

        private static double Area(List<double[]> loop)
        {
            double area = 0;
            for (int i = 0; i < loop.Count; i++)
            {
                var a = loop[i];
                var b = loop[(i + 1) % loop.Count];
                area += a[0] * b[1] - b[0] * a[1];
            }
            return area / 2;
        }

        private sealed class Region
        {
            public List<double[]> Outer;
            public List<List<double[]>> Holes = new List<List<double[]>>();

            public Region Transform(Affine transform) =>
                new Region { Outer = Outer.Select(transform.Apply).ToList(), Holes = Holes.Select(h => h.Select(transform.Apply).ToList()).ToList() };
        }

        /// <summary>Profile as closed regions in the XY plane of the profile coordinate system.</summary>
        private IEnumerable<Region> Profile(IIfcProfileDef profile)
        {
            switch (profile)
            {
                case IIfcArbitraryProfileDefWithVoids withVoids:
                    return new[] { new Region { Outer = Curve(withVoids.OuterCurve), Holes = withVoids.InnerCurves.Select(Curve).ToList() } };
                case IIfcArbitraryClosedProfileDef arbitrary:
                    return new[] { new Region { Outer = Curve(arbitrary.OuterCurve) } };
                case IIfcDerivedProfileDef derived:
                    var transform = Operator(derived.Operator);
                    return Profile(derived.ParentProfile).Select(r => r.Transform(transform)).ToList();
                case IIfcCompositeProfileDef composite:
                    return composite.Profiles.SelectMany(Profile).ToList();
                case IIfcParameterizedProfileDef parameterized:
                    var region = Parameterized(parameterized);
                    return new[] { parameterized.Position == null ? region : region.Transform(Axis(parameterized.Position)) };
                default:
                    throw Unsupported(profile);
            }
        }

        /// <summary>Parameterized profiles centred on their bounding box. Fillet and edge radii are ignored.</summary>
        private static Region Parameterized(IIfcParameterizedProfileDef profile)
        {
            switch (profile)
            {
                case IIfcRectangleHollowProfileDef hollow:
                    {
                        double x = Double(hollow.XDim) / 2, y = Double(hollow.YDim) / 2, t = Double(hollow.WallThickness);
                        return new Region { Outer = Rectangle(x, y), Holes = { Rectangle(x - t, y - t) } };
                    }
                case IIfcRectangleProfileDef rectangle:
                    return new Region { Outer = Rectangle(Double(rectangle.XDim) / 2, Double(rectangle.YDim) / 2) };
                case IIfcCircleHollowProfileDef hollow:
                    {
                        var r = Double(hollow.Radius);
                        return new Region { Outer = Ellipse(r, r), Holes = { Ellipse(r - Double(hollow.WallThickness), r - Double(hollow.WallThickness)) } };
                    }
                case IIfcCircleProfileDef circle:
                    return new Region { Outer = Ellipse(Double(circle.Radius), Double(circle.Radius)) };
                case IIfcEllipseProfileDef ellipse:
                    return new Region { Outer = Ellipse(Double(ellipse.SemiAxis1), Double(ellipse.SemiAxis2)) };
                case IIfcIShapeProfileDef i:
                    {
                        double b = Double(i.OverallWidth) / 2, h = Double(i.OverallDepth) / 2, w = Double(i.WebThickness) / 2, f = Double(i.FlangeThickness);
                        return Polygon(-b, -h, b, -h, b, -h + f, w, -h + f, w, h - f, b, h - f, b, h, -b, h, -b, h - f, -w, h - f, -w, -h + f, -b, -h + f);
                    }
                case IIfcLShapeProfileDef l:
                    {
                        double d = Double(l.Depth) / 2, b = l.Width.HasValue ? Double(l.Width.Value) / 2 : d, t = Double(l.Thickness);
                        return Polygon(-b, -d, b, -d, b, -d + t, -b + t, -d + t, -b + t, d, -b, d);
                    }
                case IIfcTShapeProfileDef t:
                    {
                        double d = Double(t.Depth) / 2, b = Double(t.FlangeWidth) / 2, w = Double(t.WebThickness) / 2, f = Double(t.FlangeThickness);
                        return Polygon(-w, -d, w, -d, w, d - f, b, d - f, b, d, -b, d, -b, d - f, -w, d - f);
                    }
                case IIfcUShapeProfileDef u:
                    {
                        double d = Double(u.Depth) / 2, b = Double(u.FlangeWidth) / 2, w = Double(u.WebThickness), f = Double(u.FlangeThickness);
                        return Polygon(-b, -d, b, -d, b, -d + f, -b + w, -d + f, -b + w, d - f, b, d - f, b, d, -b, d);
                    }
                case IIfcCShapeProfileDef c:
                    {
                        double d = Double(c.Depth) / 2, b = Double(c.Width) / 2, t = Double(c.WallThickness), g = Double(c.Girth);
                        return Polygon(-b, -d, b, -d, b, -d + g, b - t, -d + g, b - t, -d + t, -b + t, -d + t,
                            -b + t, d - t, b - t, d - t, b - t, d - g, b, d - g, b, d, -b, d);
                    }
                default:
                    throw Unsupported(profile);
            }
        }

        private static Region Polygon(params double[] xy) =>
            new Region { Outer = Enumerable.Range(0, xy.Length / 2).Select(i => new[] { xy[2 * i], xy[2 * i + 1], 0.0 }).ToList() };

        private static List<double[]> Rectangle(double x, double y) =>
            new List<double[]> { new[] { -x, -y, 0.0 }, new[] { x, -y, 0.0 }, new[] { x, y, 0.0 }, new[] { -x, y, 0.0 } };

        private static List<double[]> Ellipse(double a, double b) =>
            Enumerable.Range(0, CircleSegments).Select(i => 2 * Math.PI * i / CircleSegments).Select(t => new[] { a * Math.Cos(t), b * Math.Sin(t), 0.0 }).ToList();

        /// <summary>Closed curve as a polygon, without the closing point.</summary>
        private List<double[]> Curve(IIfcCurve curve)
        {
            List<double[]> points;
            switch (curve)
            {
                case IIfcCircle circle:
                    return Ellipse(Double(circle.Radius), Double(circle.Radius)).Select(Axis(circle.Position).Apply).ToList();
                case IIfcEllipse ellipse:
                    return Ellipse(Double(ellipse.SemiAxis1), Double(ellipse.SemiAxis2)).Select(Axis(ellipse.Position).Apply).ToList();
                default:
                    points = OpenCurve(curve);
                    break;
            }
            if (points.Count > 1 && Same(points[0], points[points.Count - 1])) points.RemoveAt(points.Count - 1);
            return points;
        }

        /// <summary>Points of a polyline-like curve, from start to end.</summary>
        private List<double[]> OpenCurve(IIfcCurve curve)
        {
            switch (curve)
            {
                case IIfcPolyline polyline:
                    return polyline.Points.Select(Point).ToList();
                case IIfcIndexedPolyCurve indexed:
                    return IndexedPolyCurve(indexed);
                case IIfcCompositeCurve composite:
                    {
                        var points = new List<double[]>();
                        foreach (var segment in composite.Segments)
                        {
                            if (!(segment is IIfcCompositeCurveSegment part)) throw Unsupported(segment);
                            var segmentPoints = OpenCurve(part.ParentCurve);
                            if (!part.SameSense) segmentPoints.Reverse();
                            Append(points, segmentPoints);
                        }
                        return points;
                    }
                case IIfcTrimmedCurve trimmed:
                    return TrimmedCurve(trimmed);
                default:
                    throw Unsupported(curve);
            }
        }

        /// <summary>
        /// Points of a trimmed line, circle or ellipse from Trim1 to Trim2. Each trim is its Cartesian point or its parameter
        /// (plane angle units on conics), as MasterRepresentation prefers when both are given; Cartesian trims are kept as exact end points.
        /// </summary>
        private static List<double[]> TrimmedCurve(IIfcTrimmedCurve curve)
        {
            var start = Trim(curve.Trim1, curve.MasterRepresentation);
            var end = Trim(curve.Trim2, curve.MasterRepresentation);
            switch (curve.BasisCurve)
            {
                case IIfcLine line:
                    {
                        var direction = Scale(Normalize(Direction(line.Dir.Orientation)), Double(line.Dir.Magnitude));
                        double[] At(double u) => Add(Point(line.Pnt), Scale(direction, u));
                        return new List<double[]> { start.Point ?? At(start.Parameter), end.Point ?? At(end.Parameter) };
                    }
                case IIfcConic conic:
                    {
                        double a, b;
                        if (conic is IIfcCircle circle) a = b = Double(circle.Radius);
                        else if (conic is IIfcEllipse ellipse) (a, b) = (Double(ellipse.SemiAxis1), Double(ellipse.SemiAxis2));
                        else throw Unsupported(conic);

                        var position = Axis(conic.Position);
                        var toRadians = curve.Model.ModelFactors.AngleToRadiansConversionFactor;
                        double Angle((double[] Point, double Parameter) trim) => trim.Point == null ? trim.Parameter * toRadians
                            : Math.Atan2(Dot(Subtract(trim.Point, position.Origin), position.Y) / b, Dot(Subtract(trim.Point, position.Origin), position.X) / a);

                        // Counter-clockwise from Trim1 to Trim2 when the sense agrees with the conic, else clockwise; equal trims give the full conic.
                        double t1 = Angle(start), sense = curve.SenseAgreement ? 1 : -1;
                        var sweep = sense * (Angle(end) - t1);
                        while (sweep <= 0) sweep += 2 * Math.PI;
                        while (sweep > 2 * Math.PI) sweep -= 2 * Math.PI;

                        var segments = Math.Max(2, (int)Math.Ceiling(sweep / (2 * Math.PI / CircleSegments)));
                        var points = Enumerable.Range(0, segments + 1).Select(i => t1 + sense * sweep * i / segments)
                            .Select(t => position.Apply(new[] { a * Math.Cos(t), b * Math.Sin(t), 0.0 })).ToList();
                        if (start.Point != null) points[0] = start.Point;
                        if (end.Point != null) points[segments] = end.Point;
                        return points;
                    }
                default:
                    throw Unsupported(curve.BasisCurve);
            }
        }

        /// <summary>The Cartesian point of a trim (null when the parameter is preferred or missing) and its parameter.</summary>
        private static (double[] Point, double Parameter) Trim(IEnumerable<IIfcTrimmingSelect> trim, IfcTrimmingPreference preference)
        {
            var point = trim.OfType<IIfcCartesianPoint>().FirstOrDefault();
            var parameter = trim.Where(t => !(t is IIfcCartesianPoint)).OfType<IExpressValueType>().Select(t => (double?)Double(t)).FirstOrDefault();
            if (point == null && parameter == null) throw new NotSupportedException("empty trim");
            return (point == null || (preference == IfcTrimmingPreference.PARAMETER && parameter.HasValue) ? null : Point(point), parameter ?? 0);
        }

        private static List<double[]> IndexedPolyCurve(IIfcIndexedPolyCurve curve)
        {
            var coordinates = curve.Points is IIfcCartesianPointList3D list3D
                ? list3D.CoordList.Select(Coordinates).ToList()
                : ((IIfcCartesianPointList2D)curve.Points).CoordList.Select(Coordinates).ToList();
            if (curve.Segments == null || curve.Segments.Count == 0) return coordinates;

            var points = new List<double[]>();
            foreach (var segment in curve.Segments)
            {
                var indices = ((IEnumerable)((IExpressValueType)segment).Value).Cast<object>().Select(o => Int(o) - 1).ToList();
                var segmentPoints = segment.GetType().Name == "IfcArcIndex"
                    ? Arc(coordinates[indices[0]], coordinates[indices[1]], coordinates[indices[2]])
                    : indices.Select(i => coordinates[i]).ToList();
                Append(points, segmentPoints);
            }
            return points;
        }

        /// <summary>Points of the circular arc from <paramref name="a"/> through <paramref name="b"/> to <paramref name="c"/>.</summary>
        private static List<double[]> Arc(double[] a, double[] b, double[] c)
        {
            double[] u = Subtract(b, a), v = Subtract(c, a), w = Cross(u, v);
            var w2 = Dot(w, w);
            if (w2 < 1e-24) return new List<double[]> { a, c };

            var center = Add(a, Scale(Cross(Subtract(Scale(v, Dot(u, u)), Scale(u, Dot(v, v))), w), 1.0 / (2 * w2)));
            var radius = Math.Sqrt(Dot(Subtract(a, center), Subtract(a, center)));
            var e1 = Scale(Subtract(a, center), 1.0 / radius);
            var e2 = Cross(Normalize(w), e1);
            var end = Subtract(c, center);
            var angle = Math.Atan2(Dot(end, e2), Dot(end, e1));
            if (angle <= 0) angle += 2 * Math.PI;

            var segments = Math.Max(2, (int)Math.Ceiling(angle / (2 * Math.PI / CircleSegments)));
            return Enumerable.Range(0, segments + 1).Select(i => angle * i / segments)
                .Select(t => Add(center, Add(Scale(e1, radius * Math.Cos(t)), Scale(e2, radius * Math.Sin(t))))).ToList();
        }

        private static void Append(List<double[]> points, List<double[]> segment)
        {
            if (points.Count > 0 && segment.Count > 0 && Same(points[points.Count - 1], segment[0])) segment = segment.Skip(1).ToList();
            points.AddRange(segment);
        }

        /// <summary>The loop with counter-clockwise (or clockwise) winding seen from +Z.</summary>
        private static List<double[]> Oriented(List<double[]> loop, bool counterClockwise)
        {
            double area = 0;
            for (int i = 0; i < loop.Count; i++)
            {
                var a = loop[i];
                var b = loop[(i + 1) % loop.Count];
                area += a[0] * b[1] - b[0] * a[1];
            }
            return (area > 0) == counterClockwise ? loop : Enumerable.Reverse(loop).ToList();
        }

        internal static Affine FromMatrix(global::Xbim.Common.Geometry.XbimMatrix3D m) =>
            new Affine(new[] { m.OffsetX, m.OffsetY, m.OffsetZ }, new[] { m.M11, m.M12, m.M13 }, new[] { m.M21, m.M22, m.M23 }, new[] { m.M31, m.M32, m.M33 });

        internal static Affine Axis(IIfcAxis2Placement placement)
        {
            switch (placement)
            {
                case IIfcAxis2Placement3D axis:
                    {
                        var z = axis.Axis != null ? Normalize(Direction(axis.Axis)) : new[] { 0.0, 0, 1 };
                        var x = FirstProjectedAxis(z, axis.RefDirection);
                        return new Affine(Point(axis.Location), x, Cross(z, x), z);
                    }
                case IIfcAxis2Placement2D axis:
                    {
                        var z = new[] { 0.0, 0, 1 };
                        var x = FirstProjectedAxis(z, axis.RefDirection);
                        return new Affine(Point(axis.Location), x, Cross(z, x), z);
                    }
                default:
                    return Affine.Identity;
            }
        }

        /// <summary>IfcCartesianTransformationOperator per the IFC BaseAxis function; may scale non-uniformly and mirror.</summary>
        private static Affine Operator(IIfcCartesianTransformationOperator op)
        {
            var z = op is IIfcCartesianTransformationOperator3D op3D && op3D.Axis3 != null ? Normalize(Direction(op3D.Axis3)) : new[] { 0.0, 0, 1 };
            var x = FirstProjectedAxis(z, op.Axis1);
            var y = op.Axis2 != null ? Direction(op.Axis2) : Cross(z, x);
            y = Normalize(Subtract(Subtract(y, Scale(x, Dot(y, x))), Scale(z, Dot(y, z))));

            var scale = Double(op.Scl);
            var scaleY = op is IIfcCartesianTransformationOperator3DnonUniform n3 ? Double(n3.Scl2)
                : op is IIfcCartesianTransformationOperator2DnonUniform n2 ? Double(n2.Scl2) : scale;
            var scaleZ = op is IIfcCartesianTransformationOperator3DnonUniform n3z ? Double(n3z.Scl3) : scale;
            return new Affine(op.LocalOrigin != null ? Point(op.LocalOrigin) : new[] { 0.0, 0, 0 }, Scale(x, scale), Scale(y, scaleY), Scale(z, scaleZ));
        }

        private static double[] FirstProjectedAxis(double[] z, IIfcDirection reference)
        {
            var v = reference != null ? Direction(reference) : Math.Abs(z[0] - 1) > 1e-9 ? new[] { 1.0, 0, 0 } : new[] { 0.0, 1, 0 };
            return Normalize(Subtract(v, Scale(z, Dot(v, z))));
        }

        private sealed class LocalMesh
        {
            public readonly List<double[]> Vertices = new List<double[]>();
            public readonly List<int[]> Faces = new List<int[]>();
            public bool Closed;

            public List<int> AddLoop(IEnumerable<double[]> points) =>
                points.Select(p => { Vertices.Add(p); return Vertices.Count - 1; }).ToList();

            /// <summary>Adds a polygon; polygons with holes are triangulated.</summary>
            public void AddFace(IReadOnlyList<int> outer, IReadOnlyList<List<int>> holes = null)
            {
                if (outer.Count < 3) return;
                if (holes == null || holes.Count == 0)
                {
                    Faces.Add(outer.ToArray());
                    return;
                }
                var all = outer.Concat(holes.SelectMany(h => h)).ToList();
                var triangles = Triangulator.Triangulate(outer.Select(i => Vertices[i]).ToList(),
                    holes.Select(h => (IReadOnlyList<double[]>)h.Select(i => Vertices[i]).ToList()).ToList());
                Faces.AddRange(triangles.Select(t => t.Select(i => all[i]).ToArray()));
            }
        }

        internal static NotSupportedException Unsupported(IPersistEntity entity) => new NotSupportedException(entity?.ExpressType.Name ?? "missing entity");

        internal static double[] Point(IIfcCartesianPoint point) => Coordinates(point.Coordinates);
        internal static double[] Direction(IIfcDirection direction) => Coordinates(direction.DirectionRatios);
        private static double[] Coordinates(IEnumerable values) => Pad(values.Cast<object>().Select(Double).ToList());
        private static double[] Pad(List<double> values) => new[] { values[0], values[1], values.Count > 2 ? values[2] : 0.0 };
        internal static double Double(object value) => Convert.ToDouble(value is IExpressValueType v ? v.Value : value);
        private static int Int(object value) => Convert.ToInt32(value is IExpressValueType v ? v.Value : value);

        private static bool Same(double[] a, double[] b) => a[0] == b[0] && a[1] == b[1] && a[2] == b[2];
        private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        private static double Distance(double[] a, double[] b) => Math.Sqrt(Dot(Subtract(a, b), Subtract(a, b)));
        private static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        private static double[] Add(double[] a, double[] b) => new[] { a[0] + b[0], a[1] + b[1], a[2] + b[2] };
        private static double[] Subtract(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
        private static double[] Scale(double[] a, double s) => new[] { a[0] * s, a[1] * s, a[2] * s };
        private static double[] Normalize(double[] a) => Scale(a, 1.0 / Math.Sqrt(Dot(a, a)));
    }
}
