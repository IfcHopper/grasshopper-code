using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Components;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;

namespace SampleBuilder
{
    internal static class CustomPreviewChecks
    {
        public static void Run()
        {
            var canvas = new Canvas();
            var previous = RhinoDoc.ActiveDoc;
            using var doc = RhinoDoc.CreateHeadless(null);
            doc.ModelUnitSystem = UnitSystem.Meters;
            RhinoDoc.ActiveDoc = doc;
            try
            {
                using (canvas.Document)
                using (var body = Brep.CreateFromBox(new BoundingBox(0, 0, 0, 1, 1, 1)))
                {
                    var material = canvas.Ifc("Material", 0, 0);
                    Canvas.Set(material, "Name", "Custom Preview regression material");
                    Canvas.Set(material, "Colour", Color.Red);
                    var type = canvas.Ifc("Element Type", 0, 0);
                    Canvas.Set(type, "Name", "Custom Preview regression type");
                    Canvas.Set(type, "Geometry", body);
                    canvas.Wire(material, type, "Material");
                    var objects = Enumerable.Range(0, 4).Select(_ => (GH_Component)canvas.Ifc("Object", 0, 0)).ToList();
                    foreach (var obj in objects)
                    {
                        Canvas.Set(obj, "Geometry", body);
                        obj.Hidden = true; // Exclude upstream overlay as a cause of the wrong colour.
                    }
                    canvas.Wire(material, objects[1], "Material");
                    canvas.Wire(type, objects[2], "Element Type");
                    Canvas.Set(objects[3], "Colour", Color.Red);
                    var previews = objects.Select(obj =>
                    {
                        var preview = (GH_CustomPreviewComponent)canvas.Native("Custom Preview", 300, 0);
                        canvas.Wire(obj, preview, "Geometry");
                        return preview;
                    }).ToList();
                    var overrideMaterial = canvas.Ifc("Material", 0, 0);
                    Canvas.Set(overrideMaterial, "Name", "Explicit preview material");
                    var materialPreview = (GH_CustomPreviewComponent)canvas.Native("Custom Preview", 300, 0);
                    canvas.Wire(objects[3], materialPreview, "Geometry");
                    canvas.Wire(overrideMaterial, materialPreview, "Material");
                    var deconstruct = canvas.Ifc("Deconstruct Object", 0, 0);
                    canvas.Wire(objects[3], deconstruct, "Object");
                    var geometryPreview = (GH_CustomPreviewComponent)canvas.Native("Custom Preview", 300, 0);
                    canvas.Wire(deconstruct, geometryPreview, "Geometry", "Geometry");
                    var stylePreview = (GH_CustomPreviewComponent)canvas.Native("Custom Preview", 300, 0);
                    canvas.Wire(deconstruct, stylePreview, "Geometry", "Geometry");
                    canvas.Wire(deconstruct, stylePreview, "Material", "Colour");
                    var deType = canvas.Ifc("Deconstruct Element Type", 0, 0);
                    canvas.Wire(type, deType, "Element Type");
                    var typePreview = (GH_CustomPreviewComponent)canvas.Native("Custom Preview", 300, 0);
                    canvas.Wire(deType, typePreview, "Geometry", "Geometry");
                    canvas.Wire(overrideMaterial, typePreview, "Material");
                    canvas.Document.Enabled = true;
                    for (int solve = 0; solve < 2; solve++)
                    {
                        var requested = solve == 0 ? Color.Lime : Color.FromArgb(80, 30, 70, 220);
                        var ifcColour = solve == 0 ? Color.Red : Color.Orange;
                        Canvas.Set(material, "Colour", ifcColour);
                        Canvas.Set(objects[3], "Colour", ifcColour);
                        Canvas.Set(overrideMaterial, "Colour", requested);
                        foreach (var preview in previews) Canvas.Set(preview, "Material", requested);
                        Canvas.Set(geometryPreview, "Material", requested);
                        canvas.Document.NewSolution(true, GH_SolutionMode.Silent);
                        foreach (var component in canvas.Document.Objects.OfType<IGH_Component>())
                            foreach (var message in component.RuntimeMessages(GH_RuntimeMessageLevel.Error))
                                throw new InvalidOperationException(component.Name + ": " + message);
                        for (int i = 0; i < objects.Count; i++)
                        {
                            var expected = i == 0 ? (Color?)null : ifcColour;
                            MaterialPreviewChecks.CheckColour(objects[i], expected);
                            var items = previews[i].DisplayItems.ToList();
                            if (items.Count != 1) throw new InvalidOperationException("Native Custom Preview must draw exactly one object.");
                            var item = items[0];
                            if (!(item.Geometry is GH_Mesh mesh) || mesh.Value.VertexColors.Count != 0)
                                throw new InvalidOperationException("IFC vertex colours must not override the native Custom Preview material.");
                            if (item.Shader.Diffuse.ToArgb() != Color.FromArgb(255, requested).ToArgb()
                                || Math.Abs(item.Shader.Transparency - (1 - requested.A / 255.0)) > 1e-6)
                                throw new InvalidOperationException("Native Custom Preview lost the requested colour or transparency.");
                            if (!mesh.Value.IsClosed || (mesh.Value.GetBoundingBox(true).Diagonal - new Vector3d(1, 1, 1)).Length > 1e-8)
                                throw new InvalidOperationException("Custom Preview conversion must preserve body geometry.");

                            var goo = Canvas.Output(objects[i]).VolatileData.AllData(true).Single();
                            var bake = (IGH_BakeAwareData)goo;
                            if (!bake.BakeGeometry(doc, doc.CreateDefaultAttributes(), out var id))
                                throw new InvalidOperationException("The source element could not be baked.");
                            var baked = doc.Objects.FindId(id);
                            if (!(baked.Geometry is Mesh bakedMesh)) throw new InvalidOperationException("Expected a baked mesh.");
                            if (expected.HasValue && (baked.Attributes.ObjectColor.ToArgb() != expected.Value.ToArgb()
                                || bakedMesh.VertexColors.Count != bakedMesh.Vertices.Count
                                || bakedMesh.VertexColors.Any(c => c.ToArgb() != expected.Value.ToArgb())))
                                throw new InvalidOperationException("Baking must retain the original IFC colour after Custom Preview conversion.");
                            if (!expected.HasValue && bakedMesh.VertexColors.Count != 0)
                                throw new InvalidOperationException("Baking an unstyled element must not invent a colour.");
                            doc.Objects.Delete(id, true);
                            MaterialPreviewChecks.CheckColour(objects[i], expected);
                        }
                        foreach (var pair in new[] { (materialPreview, requested), (geometryPreview, requested), (stylePreview, ifcColour), (typePreview, requested) })
                        {
                            var item = pair.Item1.DisplayItems.Single();
                            if (!(item.Geometry is GH_Mesh plain) || plain.Value.VertexColors.Count != 0
                                || item.Shader.Diffuse.ToArgb() != Color.FromArgb(255, pair.Item2).ToArgb()
                                || Math.Abs(item.Shader.Transparency - (1 - pair.Item2.A / 255.0)) > 1e-6)
                                throw new InvalidOperationException("Explicit geometry/IFC material/Colour preview must respect the requested shader.");
                        }
                    }
                    Console.WriteLine("  Native Custom Preview: colour-free object/type geometry, requested material/transparency, direct IFC materials, explicit Colour, repeated solves and unchanged IFC styles/baking passed.");
                }
            }
            finally { RhinoDoc.ActiveDoc = previous; }
        }
    }
}
