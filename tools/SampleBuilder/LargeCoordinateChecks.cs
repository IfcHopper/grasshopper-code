using System;
using System.IO;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using IfcHopper.Tests;
using Rhino;
using Rhino.Geometry;

namespace SampleBuilder
{
    internal static class LargeCoordinateChecks
    {
        public static int Run(string output)
        {
            Directory.CreateDirectory(output);
            using (var doc = RhinoDoc.CreateHeadless(null))
            {
                doc.ModelUnitSystem = UnitSystem.Meters;
                RhinoDoc.ActiveDoc = doc;
                Console.WriteLine($"Rhino {RhinoApp.Version}; {System.Runtime.InteropServices.RuntimeInformation.OSDescription}; .NET {Environment.Version}");
                foreach (var schema in new[] { IfcSchema.Ifc2x3, IfcSchema.Ifc4, IfcSchema.Ifc4x3 })
                foreach (var unit in new[] { UnitSystem.Meters, UnitSystem.Millimeters })
                foreach (var placed in new[] { true, false })
                {
                    doc.ModelUnitSystem = unit;
                    var scale = RhinoMath.UnitScale(UnitSystem.Meters, unit);
                    var path = Path.Combine(output, $"survey-{schema}-{unit}-{(placed ? "placement" : "vertices")}.ifc");
                    IfcBackend.Writer.Write(SyntheticLargeCoordinates.Create(placed, unit == UnitSystem.Meters ? "m" : "mm"), path, schema);
                    var canvas = new Canvas();
                    using (canvas.Document)
                    {
                        var read = (GH_Component)canvas.Ifc("Read IFC", 0, 0);
                        Canvas.Set(read, "Path", path);
                        var find = (GH_Component)canvas.Ifc("Find Objects", 250, 0);
                        canvas.Wire(read, find, "Model");
                        Canvas.Set(find, "Classes", "IfcWall");
                        var deconstruct = (GH_Component)canvas.Ifc("Deconstruct Object", 500, 0);
                        canvas.Wire(find, deconstruct, "Object", "Elements");
                        var recreated = (GH_Component)canvas.Ifc("Object", 750, 0);
                        canvas.Wire(deconstruct, recreated, "Geometry", "Geometry");
                        var move = (GH_Component)canvas.Ifc("Move Model", 0, 450);
                        canvas.Wire(read, move, "Model");
                        Canvas.Set(move, "X", -500000 * scale);
                        Canvas.Set(move, "Y", -5000000 * scale);
                        var movedFind = (GH_Component)canvas.Ifc("Find Objects", 300, 450);
                        canvas.Wire(move, movedFind, "Model");
                        Canvas.Set(movedFind, "Classes", "IfcWall");
                        canvas.Document.Enabled = true;
                        for (int solve = 0; solve < 2; solve++)
                        {
                            canvas.Document.NewSolution(true, GH_SolutionMode.Silent);
                            foreach (var component in new[] { read, find, deconstruct, recreated, move, movedFind })
                                if (component.RuntimeMessages(GH_RuntimeMessageLevel.Error).Count > 0)
                                    throw new InvalidOperationException(component.Name + ": " + string.Join("; ", component.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
                            if (!read.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Any(m => m.StartsWith("Large placement or geometry coordinates")))
                                throw new InvalidOperationException("Read IFC must warn about the survey coordinates.");
                            var geometry = deconstruct.Params.Output.First(p => p.Name == "Geometry");
                            var meshes = geometry.VolatileData.AllData(true).OfType<GH_Mesh>().ToList();
                            if (meshes.Count != 1) throw new InvalidOperationException("Expected one wall mesh.");
                            using (var mesh = meshes[0].Value.DuplicateMesh())
                            {
                                var bounds = mesh.GetBoundingBox(true);
                                var thickness = (bounds.Max.Y - bounds.Min.Y) / scale;
                                Console.WriteLine($"{Path.GetFileName(path)}: thickness={thickness:G17} m; faces={mesh.Faces.Count}; closed={mesh.IsClosed}");
                                if (Math.Abs(thickness - 0.02) > 1e-8 || !mesh.IsClosed)
                                    throw new InvalidOperationException("Survey coordinates destroyed the 20 mm wall thickness.");
                                if (Math.Abs(bounds.Min.Y / scale - 5000000) > 1e-8)
                                    throw new InvalidOperationException("The world northing changed.");
                            }
                            var goo = recreated.Params.Output[0].VolatileData.AllData(true).Single();
                            // The runner deliberately loads the selected GHA at runtime; read its
                            // public typed Value without taking a build-time plugin dependency.
                            var value = (Element)goo.GetType().GetProperty("Value").GetValue(goo);
                            var body = value.Geometry.Single();
                            if (Math.Abs(body.Vertices.Max(p => p[1]) - body.Vertices.Min(p => p[1]) - 0.02) > 1e-8)
                                throw new InvalidOperationException("Rhino-to-Core conversion rounded the wall thickness.");
                            GH_Mesh cast = null;
                            if (!find.Params.Output[1].VolatileData.AllData(true).Single().CastTo(out cast) || !cast.Value.IsClosed)
                                throw new InvalidOperationException("The element mesh cast lost the wall.");
                            cast.Value.Dispose();
                            var movedGoo = movedFind.Params.Output[1].VolatileData.AllData(true).Single();
                            if (!movedGoo.CastTo(out cast)) throw new InvalidOperationException("Moved wall has no mesh.");
                            using (cast.Value)
                            {
                                var box = cast.Value.GetBoundingBox(true);
                                if (Math.Abs(box.Min.X) > 1e-7 * scale || Math.Abs(box.Min.Y) > 1e-7 * scale
                                    || Math.Abs(box.Diagonal.Y / scale - 0.02) > 1e-8)
                                    throw new InvalidOperationException("Move Model must translate once in Rhino document units without losing thickness.");
                            }
                        }
                        var io = new GH_DocumentIO { Document = canvas.Document };
                        if (!io.SaveQuiet(Path.ChangeExtension(path, ".gh")))
                            throw new InvalidOperationException("Could not save the reproduction definition.");
                    }
                }
            }
            Console.WriteLine("Large-coordinate checks passed.");
            return 0;
        }
    }
}
