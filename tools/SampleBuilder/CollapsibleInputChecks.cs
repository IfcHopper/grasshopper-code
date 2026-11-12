using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace SampleBuilder
{
    /// <summary>Exercises compact input layout through actual component menus, arrow clicks and GH_IO.</summary>
    internal static class CollapsibleInputChecks
    {
        public static int Run(string output)
        {
            Directory.CreateDirectory(output);
            var canvas = new Canvas(expandAdvancedInputs: false);
            using (canvas.Document)
            {
                var component = (GH_Component)canvas.Ifc("Object", 180, 100);
                // The runner loads the GHA dynamically, so inspect our internal calculation here.
                // These expectations were independently measured by executing Rhino 8.37's native
                // annotation renderer at the same distances; no private Rhino APIs are used by the plugin.
                var radiusMethod = component.Attributes.GetType().GetMethod("ButtonRadius",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Missing input-arrow hover calculation.");
                foreach (var sample in new[] { (0f, 3f), (3f, 3f), (4.5f, 2.625f), (6f, 2.25f), (7.5f, 1.875f), (9f, 1.5f), (12f, 1.5f) })
                    Require((float)radiusMethod.Invoke(null, new object[] { sample.Item1 }) == sample.Item2, "Arrow hover size differs from measured native behavior.");
                canvas.Ifc("Deconstruct Object", 460, 100);
                var id = component.InstanceGuid;
                var inputs = component.Params.Input.Select(p => (p.InstanceGuid, p.Name, p.Access, Type: p.GetType())).ToArray();
                var collapsedHeight = component.Attributes.Bounds.Height;
                Require(Canvas.Input(component, "Description").Attributes.Bounds.IsEmpty, "Unused Description must start collapsed.");
                Require(!Canvas.Input(component, "Geometry").Attributes.Bounds.IsEmpty, "Geometry must remain visible.");
                canvas.Document.Enabled = true;
                canvas.Document.NewSolution(true, GH_SolutionMode.Silent);
                var originalOutput = Canvas.Output(component).VolatileData.AllData(true).Single();
                var solves = 0;
                canvas.Document.SolutionEnd += (sender, args) => solves++;
                var undoBefore = canvas.Document.UndoServer.UndoCount;
                ClickArrow(component);
                component.Attributes.PerformLayout();
                Require(Expanded(component), "The bottom arrow must expand inputs.");
                Require(component.Attributes.Bounds.Height > collapsedHeight, "Expanding must increase canvas height.");
                Require(canvas.Document.UndoServer.UndoCount > undoBefore, "The arrow must record undo.");
                Require(solves == 0, "Changing input visibility must not solve the definition.");
                Capture(canvas.Document, Path.Combine(output, "expanded.png"));

                canvas.Document.UndoServer.PerformUndo();
                component = (GH_Component)canvas.Document.Objects.Single(o => o.InstanceGuid == id);
                Require(!Expanded(component), "Undo must restore compact mode.");
                canvas.Document.UndoServer.PerformRedo();
                component = (GH_Component)canvas.Document.Objects.Single(o => o.InstanceGuid == id);
                Require(Expanded(component), "Redo must restore expanded mode.");
                // Grasshopper's undo server may announce a solution cycle even for display-only
                // actions. Existing computed data must survive without recomputing this component.
                Require(ReferenceEquals(originalOutput, Canvas.Output(component).VolatileData.AllData(true).Single()),
                    "Undo/redo of visibility must not expire computed data or retrigger IO.");
                ClickArrow(component);
                component.Attributes.PerformLayout();
                Capture(canvas.Document, Path.Combine(output, "collapsed.png"));

                Canvas.Set(component, "Description", "Configured description");
                var source = canvas.Panel(0, 20, 100, 30, "0000000000000000000000");
                canvas.Wire(source, component, "GlobalId");
                component.Attributes.ExpireLayout();
                component.Attributes.PerformLayout();
                Require(!Canvas.Input(component, "Description").Attributes.Bounds.IsEmpty, "A persistent value must stay visible.");
                Require(!Canvas.Input(component, "GlobalId").Attributes.Bounds.IsEmpty, "A connected input must stay visible.");
                Canvas.Set(component, "Attribute Values", "Configured generic value");
                component.Attributes.ExpireLayout();
                component.Attributes.PerformLayout();
                Require(!Canvas.Input(component, "Attribute Values").Attributes.Bounds.IsEmpty, "Generic persistent data must stay visible.");
                Canvas.Set(component, "Attribute Values");
                Require(inputs.SequenceEqual(component.Params.Input.Select(p => (p.InstanceGuid, p.Name, p.Access, Type: p.GetType()))),
                    "Toggling must retain every parameter identity, index, name, type and access mode.");

                var legacy = new GH_LooseChunk("Component");
                Require(component.Write(legacy), "Could not serialize component options.");
                legacy.RemoveItem("AdvancedInputsExpanded");
                var legacyCanvas = new Canvas(expandAdvancedInputs: false);
                using (legacyCanvas.Document)
                {
                    var old = (GH_Component)legacyCanvas.Ifc("Object", 0, 0);
                    Require(old.Read(legacy), "Could not read a component without the new option.");
                    Require(Expanded(old), "An older definition must default to expanded.");
                }

                CheckSaved(false);
                ClickArrow(component);
                CheckSaved(true);
                Console.WriteLine("Compact inputs: arrow, height, persistent/connected visibility, identities, undo/redo, legacy defaults and GH_IO roundtrips passed.");

                void CheckSaved(bool expanded)
                {
                    var file = Path.Combine(output, expanded ? "expanded.gh" : "collapsed.gh");
                    Require(new GH_DocumentIO(canvas.Document).SaveQuiet(file), "Could not save compact-input test definition.");
                    var io = new GH_DocumentIO();
                    Require(io.Open(file), "Could not reopen compact-input test definition.");
                    using (io.Document)
                    {
                        var read = (GH_Component)io.Document.Objects.Single(o => o.InstanceGuid == id);
                        Require(Expanded(read) == expanded, "Saved expansion state was not restored.");
                        Require(inputs.SequenceEqual(read.Params.Input.Select(p => (p.InstanceGuid, p.Name, p.Access, Type: p.GetType()))), "Saved parameter identities changed.");
                        Require(Canvas.Input(read, "GlobalId").SourceCount == 1, "Saved wires were lost.");
                        io.Document.Enabled = true;
                        for (int solve = 0; solve < 2; solve++)
                        {
                            io.Document.NewSolution(true, GH_SolutionMode.Silent);
                            Require(Canvas.Output(read).VolatileDataCount == 1, "The restored component must solve normally.");
                        }
                        var description = Canvas.Input(read, "Description").VolatileData.AllData(true).Single();
                        Require(description.ToString() == "Configured description", "Saved persistent data changed.");
                        read.Attributes.PerformLayout();
                        Require(!Canvas.Input(read, "GlobalId").Attributes.Bounds.IsEmpty, "A restored wire must stay visible.");
                    }
                }
            }
            foreach (var proxy in Instances.ComponentServer.ObjectProxies.Where(p => !p.Obsolete && p.Desc.Category == "IfcHopper" && p.Desc.SubCategory != "7 - Params"))
            {
                var family = new Canvas(expandAdvancedInputs: false);
                using (family.Document)
                {
                    if (!(family.Ifc(proxy.Desc.Name, 100, 100) is GH_Component component)) continue;
                    using (var menu = new ContextMenuStrip())
                    {
                        component.AppendMenuItems(menu);
                        if (!menu.Items.OfType<ToolStripMenuItem>().Any(item => item.Text == "Show advanced inputs")) continue;
                    }
                    var ids = component.Params.Input.Select(p => p.InstanceGuid).ToArray();
                    ClickArrow(component);
                    component.Attributes.PerformLayout();
                    Require(component.Params.Input.All(p => !p.Attributes.Bounds.IsEmpty), $"{component.Name}: expanded inputs must have bounds.");
                    var inputAnchors = component.Params.Input.ToDictionary(p => p.InstanceGuid, p => p.Attributes.Bounds.Right);
                    var outputAnchors = component.Params.Output.ToDictionary(p => p.InstanceGuid, p => p.Attributes.Bounds.Left);
                    ClickArrow(component);
                    component.Attributes.PerformLayout();
                    Require(ids.SequenceEqual(component.Params.Input.Select(p => p.InstanceGuid)), $"{component.Name}: input identities changed.");
                    foreach (var parameter in component.Params.Input.Where(p => !p.Attributes.Bounds.IsEmpty))
                    {
                        Require(component.Attributes.Bounds.Contains(parameter.Attributes.Bounds), $"{component.Name}: input is outside component bounds.");
                        Require(parameter.Attributes.Bounds.Right == inputAnchors[parameter.InstanceGuid], $"{component.Name}: input label anchor moved.");
                    }
                    foreach (var parameter in component.Params.Output)
                        Require(parameter.Attributes.Bounds.Left == outputAnchors[parameter.InstanceGuid], $"{component.Name}: output label anchor moved.");
                }
            }
            CheckCanvasHover();
            Console.WriteLine("All affected component families: arrow toggles, registered input identities and compact bounds passed.");
            return 0;
        }

        private static void CheckCanvasHover()
        {
            var definition = new Canvas(expandAdvancedInputs: false);
            using (definition.Document)
            {
                var component = (GH_Component)definition.Ifc("Object", 180, 100);
                var attributes = component.Attributes;
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                // Observe the same hover radius the renderer consumes, not just the distance formula.
                var radius = attributes.GetType().GetField("_hoverRadius", flags);
                var subscription = attributes.GetType().GetField("_hoverCanvas", flags);
                Require(radius != null && subscription != null, "Missing canvas-hover state.");
                var box = ((GH_ComponentAttributes)attributes).ContentBox;
                var center = new PointF(box.Left + box.Width * 0.5f, box.Bottom - 2);
                using (var canvas = new HoverProbeCanvas { Size = new Size(800, 650), Document = definition.Document })
                {
                    canvas.CreateControl();
                    canvas.Viewport.Zoom = 4;
                    canvas.Viewport.Focus(center);
                    DrawFrame();
                    Require(ReferenceEquals(subscription.GetValue(attributes), canvas), "Rendering must attach canvas-level hover tracking.");
                    using (var export = new GH_Canvas { Size = new Size(800, 650), Document = definition.Document })
                    {
                        export.CreateControl();
                        export.Viewport.Focus(center);
                        using (export.GetCanvasScreenBuffer(GH_CanvasMode.Export)) { }
                        Require(ReferenceEquals(subscription.GetValue(attributes), canvas), "An export must not replace the interactive canvas subscription.");
                        export.Document = null;
                    }
                    Require(ReferenceEquals(subscription.GetValue(attributes), canvas), "Export cleanup must not detach the interactive canvas.");
                    foreach (var sample in new[]
                    {
                        (12f, 0f, 1.5f), (-12f, 0f, 1.5f), (0f, 12f, 1.5f), (0f, -12f, 1.5f),
                        (6f, 0f, 2.25f), (-6f, 0f, 2.25f), (0f, 6f, 2.25f), (0f, -6f, 2.25f),
                        (3f, 4f, 2.5f), (-3f, 4f, 2.5f), (3f, -4f, 2.5f), (-3f, -4f, 2.5f), (0f, 3f, 3f), (0f, 0f, 3f)
                    })
                    {
                        var point = new PointF(center.X + sample.Item1, center.Y + sample.Item2);
                        canvas.MoveTo(point);
                        Require(Math.Abs((float)radius.GetValue(attributes) - sample.Item3) < 0.02f,
                            $"Canvas hover radius {(float)radius.GetValue(attributes)} is incorrect for approach offset ({sample.Item1}, {sample.Item2}); expected {sample.Item3}.");
                    }
                    var below = new PointF(center.X, center.Y + 6);
                    Require(!attributes.Bounds.Contains(below), "The hover regression must include a point outside the component.");
                    canvas.MoveTo(below);
                    Require(Math.Abs((float)radius.GetValue(attributes) - 2.25f) < 0.02f, "Approaching from below must grow the button outside component bounds.");
                    canvas.LeaveCanvas();
                    Require((float)radius.GetValue(attributes) == 1.5f, "Leaving the canvas must restore resting size.");

                    ClickArrow(component);
                    attributes.PerformLayout();
                    box = ((GH_ComponentAttributes)attributes).ContentBox;
                    center = new PointF(box.Left + box.Width * 0.5f, box.Top);
                    canvas.Viewport.Focus(center);
                    DrawFrame();
                    var above = new PointF(center.X, center.Y - 6);
                    Require(!attributes.Bounds.Contains(above), "The collapse-hover regression must approach from outside the top edge.");
                    canvas.MoveTo(above);
                    Require(Math.Abs((float)radius.GetValue(attributes) - 2.25f) < 0.02f, "The top collapse button must grow when approached from above.");

                    using (var other = new GH_Document())
                    {
                        canvas.Document = other;
                        Require(subscription.GetValue(attributes) == null, "Document switching must detach hover handlers.");
                        canvas.Document = definition.Document;
                    }
                    DrawFrame();
                    definition.Document.RemoveObject(component, false);
                    Require(subscription.GetValue(attributes) == null, "Removing a component must detach hover handlers.");
                    definition.Document.AddObject(component, false);
                    DrawFrame();
                    Require(ReferenceEquals(subscription.GetValue(attributes), canvas), "A rendered component must reattach after re-addition.");

                    void DrawFrame()
                    {
                        using (canvas.GetCanvasScreenBuffer(GH_CanvasMode.Control)) { }
                    }
                }
                Require(subscription.GetValue(attributes) == null, "Disposing the canvas must detach hover handlers.");
            }
            Console.WriteLine("Canvas hover: cardinal/diagonal approaches, outside bottom/top edge growth, mouse leave and subscription cleanup passed.");
        }

        private sealed class HoverProbeCanvas : GH_Canvas
        {
            public void MoveTo(PointF point)
            {
                var projected = Viewport.ProjectPoint(point);
                OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, (int)Math.Round(projected.X), (int)Math.Round(projected.Y), 0));
            }
            public void LeaveCanvas() => OnMouseLeave(EventArgs.Empty);
        }

        private static void Capture(GH_Document document, string path)
        {
            using (var canvas = new GH_Canvas { Size = new Size(800, 650), Document = document })
            {
                canvas.CreateControl();
                canvas.Viewport.Zoom = 1;
                canvas.Viewport.Focus(new PointF(250, 230));
                using (var image = canvas.GetCanvasScreenBuffer(GH_CanvasMode.Export))
                    image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                canvas.Document = null;
            }
        }

        private static void ClickArrow(GH_Component component)
        {
            component.Attributes.PerformLayout();
            var content = ((GH_ComponentAttributes)component.Attributes).ContentBox;
            var point = new PointF(content.Left + content.Width * 0.5f, Expanded(component) ? content.Top : content.Bottom - 2);
            var button = (RectangleF)component.Attributes.GetType().GetField("_button",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(component.Attributes);
            Require(button.Left + button.Width * 0.5f == point.X && button.Top + button.Height * 0.5f == point.Y,
                "Expand must sit two canvas units above the content-box bottom; collapse must stay at its top.");
            var e = new GH_CanvasMouseEvent(Point.Empty, point, MouseButtons.Left, 1, 0);
            Require(component.Attributes.RespondToMouseDown(null, e) == GH_ObjectResponse.Handled, "The bottom arrow must handle the click.");
        }

        private static bool Expanded(GH_Component component)
        {
            using (var menu = new ContextMenuStrip())
            {
                component.AppendMenuItems(menu);
                return menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "Show advanced inputs").Checked;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
