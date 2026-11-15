using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace SampleBuilder
{
    internal static class ClassColourChecks
    {
        public static void Run()
        {
            var canvas = new Canvas();
            using (canvas.Document)
            {
                var source = canvas.Add(new Param_GenericObject(), 0, 0);
                var wall = new Element("Wall", "IfcWall");
                var storedStyle = new Colour(.2, .3, .4, .25);
                var material = new Material("Stored material") { Colour = storedStyle };
                wall.SetMaterial(material);
                var placement = Placement.Translation(10, 20, 30);
                wall.Placement = placement;
                var mesh = new MeshGeometry(new[] { new[] { 0.0, 0.0, 0.0 }, new[] { 1.0, 0.0, 0.0 }, new[] { 0.0, 1.0, 0.0 } },
                    new[] { new[] { 0, 1, 2 } }, colour: storedStyle);
                wall.Geometry.Add(mesh);
                var slab = new Element("Slab", "IfcSlab");
                var path = new GH_Path(2, 4);
                source.PersistentData.Append(new GH_ObjectWrapper(wall), path);
                source.PersistentData.Append(null, path);
                source.PersistentData.Append(new GH_String("Unsupported"), path);
                source.PersistentData.Append(new GH_ObjectWrapper(slab), path);
                source.PersistentData.Append(new GH_ObjectWrapper(wall), new GH_Path(7));
                source.PersistentData.Append(new GH_ObjectWrapper(new Project()), new GH_Path(9));
                source.PersistentData.Append(new GH_ObjectWrapper(new ElementType("Type", "IfcWallType")), new GH_Path(9));
                source.PersistentData.EnsurePath(new GH_Path(11));
                var component = (GH_Component)canvas.Ifc("Class Colours", 300, 0);
                canvas.Wire(source, component, "Objects");
                canvas.Document.Enabled = true;
                Color automaticWall = Color.Empty, automaticSlab = Color.Empty;
                for (int solve = 0; solve < 3; solve++)
                {
                    if (solve > 0)
                    {
                        Canvas.Set(component, "Classes", " ifcwall ");
                        Canvas.Set(component, "Colours", Color.FromArgb(100, 240, 20, 30));
                    }
                    canvas.Document.NewSolution(true, GH_SolutionMode.Silent);
                    var result = (GH_Structure<GH_Colour>)Canvas.Output(component).VolatileData;
                    Require(source.PersistentData.Paths.Select(p => p.ToString()).SequenceEqual(result.Paths.Select(p => p.ToString())), "Colour tree paths changed.");
                    Require(result.Branches[0].Count == 4 && result.Branches[0][1] == null && result.Branches[0][2] == null,
                        "Null/unsupported positions must remain empty without shifting correspondence.");
                    Require(result.Branches[3].Count == 0, "Empty branch must be preserved.");
                    Require(result.Branches[0][0].Value == result.Branches[1][0].Value, "Same class must have same colour across branches.");
                    Require(result.Branches[2].Count == 2 && result.Branches[2].All(c => c != null), "Spatial objects and element types must be supported.");
                    if (solve == 0)
                    {
                        automaticWall = result.Branches[0][0].Value;
                        automaticSlab = result.Branches[0][3].Value;
                    }
                    else
                    {
                        Require(result.Branches[0][0].Value == Color.FromArgb(100, 240, 20, 30), "Override/alpha not applied.");
                        Require(result.Branches[0][3].Value == automaticSlab, "Partial override changed another class.");
                    }
                    Require(component.RuntimeMessages(GH_RuntimeMessageLevel.Error).Count == 0, "Valid palette must not produce errors.");
                    Require(component.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Any(m => m.Contains("{2;4}[2]")), "Unsupported item warning must identify its position.");
                }
                Canvas.Set(component, "Classes");
                Canvas.Set(component, "Colours");
                source.PersistentData.Branches[0].Reverse();
                source.ExpireSolution(false);
                canvas.Document.NewSolution(true, GH_SolutionMode.Silent);
                var reordered = (GH_Structure<GH_Colour>)Canvas.Output(component).VolatileData;
                Require(reordered.Branches[0][0].Value == automaticSlab && reordered.Branches[0][3].Value == automaticWall,
                    "Reordering objects changed their automatic colours.");
                Require(wall.IfcClass == "IfcWall" && wall.Geometry.Count == 1 && ReferenceEquals(wall.Geometry[0], mesh)
                    && ReferenceEquals(mesh.Colour, storedStyle) && ReferenceEquals(wall.Material, material)
                    && ReferenceEquals(material.Colour, storedStyle) && ReferenceEquals(wall.Placement, placement), "Palette changed upstream geometry, styles or placement.");

                Canvas.Set(component, "Classes", "*", "IfcWall");
                Canvas.Set(component, "Colours", Color.Gray, Color.Red);
                canvas.Document.NewSolution(true, GH_SolutionMode.Silent);
                var fallback = (GH_Structure<GH_Colour>)Canvas.Output(component).VolatileData;
                Require(fallback.Branches[0][0].Value.ToArgb() == Color.Gray.ToArgb() && fallback.Branches[0][3].Value.ToArgb() == Color.Red.ToArgb(),
                    "Exact overrides must win over the all-other-classes fallback.");

                Invalid(new[] { "IfcWall" }, new[] { Color.Red, Color.Blue }, "one Colour");
                Invalid(new[] { "IfcWall", "ifcwall" }, new[] { Color.Red, Color.Blue }, "repeats");
                Invalid(new[] { "*", " * " }, new[] { Color.Red, Color.Blue }, "repeats");
                Invalid(new[] { "IfcNotAClass" }, new[] { Color.Red }, "not an IFC class");
                Invalid(new[] { " " }, new[] { Color.Red }, "not an IFC class");
                Invalid(new[] { "IfcWall" }, new[] { Color.Empty }, "empty");
                void Invalid(string[] classes, Color[] colours, string expected)
                {
                    Canvas.Set(component, "Classes", classes.Cast<object>().ToArray());
                    Canvas.Set(component, "Colours", colours.Cast<object>().ToArray());
                    canvas.Document.NewSolution(true, GH_SolutionMode.Silent);
                    Require(component.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(m => m.Contains(expected)), "Missing actionable palette error: " + expected);
                    Require(Canvas.Output(component).VolatileDataCount == 0, "Invalid palette retained stale colours.");
                }
            }
            Console.WriteLine("Class Colours: tree/null correspondence, repeated solves, reordering, partial overrides, transparency, spatial/types, invalid inputs and upstream preservation passed.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
