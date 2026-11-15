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
                        if (!menu.Items.OfType<ToolStripMenuItem>().Any(IsAdvancedMenu)) continue;
                    }
                    var ids = component.Params.Input.Select(p => p.InstanceGuid).ToArray();
                    ClickArrow(component);
                    component.Attributes.PerformLayout();
                    Require(component.Params.Input.All(p => !p.Attributes.Bounds.IsEmpty), $"{component.Name}: expanded inputs must have bounds.");
                    Require(component.Params.Output.All(p => !p.Attributes.Bounds.IsEmpty), $"{component.Name}: expanded outputs must have bounds.");
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
                    foreach (var parameter in component.Params.Output.Where(p => !p.Attributes.Bounds.IsEmpty))
                        Require(parameter.Attributes.Bounds.Left == outputAnchors[parameter.InstanceGuid], $"{component.Name}: output label anchor moved.");
                    CheckTrailingPlusAlignment(component);
                    CheckPlusOpenedCollapse(component, family.Document);
                }
            }
            CheckCanvasHover();
            CheckZoomRendering(output);
            CheckStateTags(output);
            CheckInputControlCollapse();
            CheckDeconstructOutputs(output);
            CheckIndividualControls(output);
            Console.WriteLine("All affected component families: arrow toggles, top collapse of all plus-opened inputs/outputs, undo/redo, registered identities and compact bounds passed.");
            return 0;
        }

        private static void CheckStateTags(string output)
        {
            var canvas = new Canvas(expandAdvancedInputs: false);
            using (canvas.Document)
            {
                var component = (GH_Component)canvas.Ifc("Object", 180, 100);
                var input = Canvas.Input(component, "Geometry");
                var outputParameter = Canvas.Output(component);
                foreach (var mapping in new[] { GH_DataMapping.Flatten, GH_DataMapping.Graft })
                {
                    input.DataMapping = mapping;
                    outputParameter.DataMapping = mapping;
                    input.Simplify = outputParameter.Simplify = true;
                    for (var i = 0; i < 4; i++)
                    {
                        component.Attributes.ExpireLayout();
                        component.Attributes.PerformLayout();
                        foreach (var parameter in new[] { input, outputParameter })
                        {
                            var field = typeof(GH_LinkedParamAttributes).GetField("m_renderTags",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            var tags = (GH_StateTagList)field.GetValue(parameter.Attributes);
                            Require(tags != null && tags.Count > 0, "Configured parameters must have state tags.");
                            var bounds = tags.BoundingBox;
                            var row = parameter.Attributes.Bounds;
                            Require(Math.Abs(bounds.Top + bounds.Height * .5f - (row.Top + row.Height * .5f)) <= 1,
                                $"{parameter.Name}: mapping/simplify icons must follow the parameter row. Tags={bounds}, row={row}, expanded={Expanded(component)}");
                        }
                        ClickArrow(component);
                    }
                }
                component.Attributes.PerformLayout();
                Capture(canvas.Document, Path.Combine(output, "state-tags.png"));
            }
            Console.WriteLine("Input/output flatten, graft and simplify state tags follow repeated compact/expanded layout changes.");
        }

        private static void CheckPlusOpenedCollapse(GH_Component component, GH_Document document)
        {
            var parameters = component.Params.Input.Concat(component.Params.Output).ToArray();
            var hidden = parameters.Where(p => p.Attributes.Bounds.IsEmpty).ToArray();
            if (hidden.Length == 0) return;
            // Reveal every available row via real plus clicks, including the last row that
            // removes the compact layout. The global expanded flag must still remain false.
            while (true)
            {
                var plus = ParameterControls(component).FirstOrDefault(c => c.Show);
                if (plus.Parameter == null) break;
                var visibleCount = parameters.Count(p => !p.Attributes.Bounds.IsEmpty);
                var e = new GH_CanvasMouseEvent(Point.Empty, plus.Center, MouseButtons.Left, 1, 0);
                Require(component.Attributes.RespondToMouseDown(null, e) == GH_ObjectResponse.Handled,
                    $"{component.Name}: plus did not reveal its parameter.");
                component.Attributes.PerformLayout();
                Require(parameters.Count(p => !p.Attributes.Bounds.IsEmpty) == visibleCount + 1,
                    $"{component.Name}: each plus click must reveal exactly one parameter.");
            }
            Require(!Expanded(component) && hidden.All(p => !p.Attributes.Bounds.IsEmpty),
                $"{component.Name}: plus must reveal all hidden input/output rows without changing the global flag.");
            foreach (var parameter in hidden)
            {
                parameter.DataMapping = GH_DataMapping.Graft;
                parameter.Reverse = parameter.Simplify = true;
            }
            var identities = parameters.Select(p => p.InstanceGuid).ToArray();
            ClickPartialCollapse(component);
            Require(!Expanded(component) && hidden.All(p => p.Attributes.Bounds.IsEmpty),
                $"{component.Name}: top arrow must collapse all plus-opened inputs and outputs, including rows with tree controls.");
            Require(hidden.All(p => p.DataMapping == GH_DataMapping.None && !p.Reverse && !p.Simplify),
                $"{component.Name}: collapsing must reset tree controls on both hidden inputs and outputs.");
            Require(identities.SequenceEqual(component.Params.Input.Concat(component.Params.Output).Select(p => p.InstanceGuid)),
                $"{component.Name}: collapsing plus-opened rows must preserve parameter identities.");
            document.UndoServer.PerformUndo();
            component.Attributes.PerformLayout();
            Require(!Expanded(component) && hidden.All(p => !p.Attributes.Bounds.IsEmpty),
                $"{component.Name}: undo must restore every plus-opened input/output row.");
            Require(hidden.All(p => p.DataMapping == GH_DataMapping.Graft && p.Reverse && p.Simplify),
                $"{component.Name}: undo must restore input and output tree controls.");
            document.UndoServer.PerformRedo();
            component.Attributes.PerformLayout();
            Require(hidden.All(p => p.Attributes.Bounds.IsEmpty),
                $"{component.Name}: redo must collapse every plus-opened input/output row.");
            Require(hidden.All(p => p.DataMapping == GH_DataMapping.None && !p.Reverse && !p.Simplify),
                $"{component.Name}: redo must reset input and output tree controls.");
        }

        private static void CheckTrailingPlusAlignment(GH_Component component)
        {
            var controls = ParameterControls(component);
            var body = ((GH_ComponentAttributes)component.Attributes).ContentBox;
            foreach (var column in new[] { component.Params.Input, component.Params.Output })
            {
                var lastVisible = column.FindLastIndex(p => !p.Attributes.Bounds.IsEmpty);
                foreach (var control in controls.Where(c => c.Show && column.Contains(c.Parameter) && column.IndexOf(c.Parameter) > lastVisible))
                    Require(control.Center.Y == body.Bottom - 2,
                        $"{component.Name}/{control.Parameter.Name}: trailing plus must align with the bottom expand arrow.");
            }
        }

        private static void CheckInputControlCollapse()
        {
            CheckControlCollapse(true);
            CheckControlCollapse(false);
        }

        private static void CheckControlCollapse(bool inputSide)
        {
            var canvas = new Canvas(expandAdvancedInputs: false);
            using (canvas.Document)
            {
                var component = (GH_Component)canvas.Ifc(inputSide ? "Object" : "Deconstruct Object", 180, 100);
                var name = inputSide ? "Tag" : "Description";
                var input = inputSide ? Canvas.Input(component, name) : Canvas.Output(component, name);
                var wired = inputSide ? Canvas.Input(component, "GlobalId") : Canvas.Output(component, "GlobalId");
                var source = canvas.Panel(0, 0, 100, 30, "Connected");
                if (inputSide) canvas.Wire(source, component, "GlobalId");
                else canvas.Wire(component, source, null, "GlobalId");
                foreach (var mapping in new[] { GH_DataMapping.Flatten, GH_DataMapping.Graft, GH_DataMapping.None })
                {
                    ClickArrow(component);
                    foreach (var parameter in new[] { input, wired })
                    {
                        parameter.DataMapping = mapping;
                        parameter.Reverse = parameter.Simplify = true;
                    }
                    ClickArrow(component);
                    component.Attributes.PerformLayout();
                    Require(input.Attributes.Bounds.IsEmpty && input.DataMapping == GH_DataMapping.None && !input.Reverse && !input.Simplify,
                        "Collapsing an unused input must hide it and clear its tree controls.");
                    Require(!wired.Attributes.Bounds.IsEmpty && wired.DataMapping == mapping && wired.Reverse && wired.Simplify,
                        "Connected input tree controls must survive collapse.");
                    canvas.Document.UndoServer.PerformUndo();
                    component.Attributes.PerformLayout();
                    Require(Expanded(component) && !input.Attributes.Bounds.IsEmpty
                        && input.DataMapping == mapping && input.Reverse && input.Simplify,
                        "Undo must restore the input and its original tree controls.");
                    canvas.Document.UndoServer.PerformRedo();
                    component.Attributes.PerformLayout();
                    Require(input.Attributes.Bounds.IsEmpty && input.DataMapping == GH_DataMapping.None && !input.Reverse && !input.Simplify,
                        "Redo must hide the input and clear its controls again.");
                    ClickParameter(component, name, true);
                    input.DataMapping = mapping;
                    input.Reverse = input.Simplify = true;
                    ClickParameter(component, name, false);
                    component.Attributes.PerformLayout();
                    Require(input.Attributes.Bounds.IsEmpty && input.DataMapping == GH_DataMapping.None && !input.Reverse && !input.Simplify,
                        "The individual minus control must also clear unused input tree controls.");
                }
            }
            Console.WriteLine($"Unused {(inputSide ? "input" : "output")} tree controls: global/individual collapse resets controls, undo/redo restores them, connected parameters retain controls.");
        }

        private static void CheckIndividualControls(string output)
        {
            var definition = new Canvas(expandAdvancedInputs: false);
            using (definition.Document)
            {
                var component = (GH_Component)definition.Ifc("Object", 100, 100);
                var description = Canvas.Input(component, "Description");
                var ids = component.Params.Input.Concat(component.Params.Output).Select(p => p.InstanceGuid).ToArray();
                definition.Document.Enabled = true;
                definition.Document.NewSolution(true, GH_SolutionMode.Silent);
                var computed = Canvas.Output(component).VolatileData.AllData(true).Single();
                var solves = 0;
                definition.Document.SolutionEnd += (sender, args) => solves++;
                var plus = ParameterControls(component).Single(c => c.Parameter == description && c.Show);
                Require(plus.Center.X == ((GH_ComponentAttributes)component.Attributes).ContentBox.Left
                    && plus.Center.Y == Canvas.Input(component, "Name").Attributes.Bounds.Bottom,
                    "Plus must sit at the native gap after the previous visible row.");
                ClickParameter(component, "Description", true);
                Require(!description.Attributes.Bounds.IsEmpty && !Expanded(component), "Plus must reveal only the chosen input in compact mode.");
                Require(Canvas.Input(component, "GlobalId").Attributes.Bounds.IsEmpty, "Plus must not reveal unrelated inputs.");
                ClickPartialCollapse(component);
                Require(description.Attributes.Bounds.IsEmpty && !Expanded(component), "Top arrow must collapse a single plus-opened input without expanding other inputs.");
                definition.Document.UndoServer.PerformUndo();
                component.Attributes.PerformLayout();
                Require(!description.Attributes.Bounds.IsEmpty && !Expanded(component), "Undo must restore the individually opened input.");
                var control = ParameterControls(component).Single(c => c.Parameter == description && !c.Show);
                var body = ((GH_ComponentAttributes)component.Attributes).ContentBox;
                Require(control.Center.X == body.Left && control.Center.Y == description.Attributes.Bounds.Top + description.Attributes.Bounds.Height * .5f,
                    "Minus must sit on the content-box edge at the row centre, matching native dimension components.");
                ClickParameter(component, "Description", false);
                Require(description.Attributes.Bounds.IsEmpty, "Minus must hide the chosen unused input.");
                definition.Document.UndoServer.PerformUndo();
                component.Attributes.PerformLayout();
                Require(!description.Attributes.Bounds.IsEmpty, "Individual undo must restore the previous choice.");
                definition.Document.UndoServer.PerformRedo();
                component.Attributes.PerformLayout();
                Require(description.Attributes.Bounds.IsEmpty, "Individual redo must restore the hidden choice.");
                ClickParameter(component, "Description", true);
                ClickArrow(component);
                definition.Document.UndoServer.PerformUndo();
                component.Attributes.PerformLayout();
                Require(!description.Attributes.Bounds.IsEmpty && Canvas.Input(component, "GlobalId").Attributes.Bounds.IsEmpty,
                    "Undo of show-all must restore individual choices.");
                definition.Document.UndoServer.PerformRedo();
                component.Attributes.PerformLayout();
                ClickParameter(component, "Description", false);
                Require(Expanded(component) && description.Attributes.Bounds.IsEmpty, "An individual minus must work in expanded mode.");
                Require(solves == 0 && ReferenceEquals(computed, Canvas.Output(component).VolatileData.AllData(true).Single()),
                    "Individual controls/undo must retain computed data without solving.");
                Require(ids.SequenceEqual(component.Params.Input.Concat(component.Params.Output).Select(p => p.InstanceGuid)), "Individual controls changed parameter identities.");

                var file = Path.Combine(output, "individual-visibility.gh");
                Require(new GH_DocumentIO(definition.Document).SaveQuiet(file), "Could not save individual visibility.");
                var io = new GH_DocumentIO();
                Require(io.Open(file), "Could not reopen individual visibility.");
                using (io.Document)
                {
                    var read = (GH_Component)io.Document.Objects.Single(o => o.InstanceGuid == component.InstanceGuid);
                    read.Attributes.PerformLayout();
                    Require(Expanded(read) && Canvas.Input(read, "Description").Attributes.Bounds.IsEmpty,
                        "Individual hidden choice in expanded mode must survive save/reopen.");
                }
                var chunk = new GH_LooseChunk("Component");
                Require(component.Write(chunk), "Could not write individual options.");
                chunk.RemoveChunk("IndividualVisibility");
                var legacy = new Canvas(expandAdvancedInputs: false);
                using (legacy.Document)
                {
                    var read = (GH_Component)legacy.Ifc("Object", 0, 0);
                    Require(read.Read(chunk), "Could not read earlier compact definitions.");
                    read.Attributes.PerformLayout();
                    Require(!Canvas.Input(read, "Description").Attributes.Bounds.IsEmpty, "Missing per-parameter options must use the saved global mode.");
                }

                ClickArrow(component);
                ClickParameter(component, "Description", true);
                var shownFile = Path.Combine(output, "individual-shown-compact.gh");
                Require(new GH_DocumentIO(definition.Document).SaveQuiet(shownFile), "Could not save individual shown choice.");
                var shownIO = new GH_DocumentIO();
                Require(shownIO.Open(shownFile), "Could not reopen individual shown choice.");
                using (shownIO.Document)
                {
                    var read = (GH_Component)shownIO.Document.Objects.Single(o => o.InstanceGuid == component.InstanceGuid);
                    read.Attributes.PerformLayout();
                    Require(!Expanded(read) && !Canvas.Input(read, "Description").Attributes.Bounds.IsEmpty
                        && Canvas.Input(read, "GlobalId").Attributes.Bounds.IsEmpty, "Individual shown choice must survive in compact mode.");
                }

                Canvas.Set(component, "Description", "Keep me");
                component.Attributes.ExpireLayout();
                component.Attributes.PerformLayout();
                Require(!description.Attributes.Bounds.IsEmpty && !ParameterControls(component).Any(c => c.Parameter == description && !c.Show),
                    "Configured inputs must stay visible and have no minus.");
                var source = definition.Panel(0, 0, 100, 30, "Connected");
                definition.Wire(source, component, "GlobalId");
                component.Attributes.ExpireLayout();
                component.Attributes.PerformLayout();
                Require(!ParameterControls(component).Any(c => c.Parameter == Canvas.Input(component, "GlobalId") && !c.Show), "Wired inputs must have no minus.");
                var deconstruct = (GH_Component)definition.Ifc("Deconstruct Object", 400, 100);
                ClickParameter(deconstruct, "Description", true);
                ClickPartialCollapse(deconstruct);
                Require(Canvas.Output(deconstruct, "Description").Attributes.Bounds.IsEmpty, "Top arrow must collapse a single plus-opened output.");
                ClickParameter(deconstruct, "Description", true);
                ClickParameter(deconstruct, "Description", false);
                Require(Canvas.Output(deconstruct, "Description").Attributes.Bounds.IsEmpty, "Output minus must hide unused metadata.");
                ClickParameter(deconstruct, "Description", true);
                var recipient = definition.Panel(700, 0, 100, 30);
                definition.Wire(deconstruct, recipient, null, "Description");
                deconstruct.Attributes.ExpireLayout();
                deconstruct.Attributes.PerformLayout();
                Require(!ParameterControls(deconstruct).Any(c => c.Parameter == Canvas.Output(deconstruct, "Description") && !c.Show), "Wired outputs must have no minus.");
            }
            Console.WriteLine("Individual parameter controls: placement, reveal/hide, undo/redo, global reset undo, saved choices, legacy defaults and wired/configured protection passed.");
        }

        private static (IGH_Param Parameter, bool Show, PointF Center)[] ParameterControls(GH_Component component)
        {
            component.Attributes.PerformLayout();
            var controls = (System.Collections.IEnumerable)component.Attributes.GetType().GetField("_parameterControls",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(component.Attributes);
            return controls.Cast<object>().Select(c => ((IGH_Param)c.GetType().GetProperty("Parameter").GetValue(c),
                (bool)c.GetType().GetProperty("Show").GetValue(c), (PointF)c.GetType().GetProperty("Center").GetValue(c))).ToArray();
        }

        private static void ClickParameter(GH_Component component, string name, bool show)
        {
            var control = ParameterControls(component).Single(c => c.Parameter.Name == name && c.Show == show);
            var e = new GH_CanvasMouseEvent(Point.Empty, control.Center, MouseButtons.Left, 1, 0);
            Require(component.Attributes.RespondToMouseDown(null, e) == GH_ObjectResponse.Handled, "Parameter control did not handle its click.");
            component.Attributes.PerformLayout();
        }

        private static void ClickPartialCollapse(GH_Component component)
        {
            component.Attributes.PerformLayout();
            var box = (RectangleF)component.Attributes.GetType().GetField("_collapseButton",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(component.Attributes);
            var content = ((GH_ComponentAttributes)component.Attributes).ContentBox;
            Require(!box.IsEmpty && box.Top + box.Height * .5f == content.Top,
                "A top collapse arrow must appear when even one unused parameter is plus-opened.");
            var point = new PointF(box.Left + box.Width * .5f, box.Top + box.Height * .5f);
            var e = new GH_CanvasMouseEvent(Point.Empty, point, MouseButtons.Left, 1, 0);
            Require(component.Attributes.RespondToMouseDown(null, e) == GH_ObjectResponse.Handled, "Partial-layout top arrow must handle its click.");
            component.Attributes.PerformLayout();
        }

        private static void CheckDeconstructOutputs(string output)
        {
            var canvas = new Canvas(expandAdvancedInputs: false);
            using (canvas.Document)
            {
                var source = canvas.Ifc("Object", 0, 0);
                var component = (GH_Component)canvas.Ifc("Deconstruct Object", 300, 0);
                canvas.Wire(source, component, "Object");
                var ids = component.Params.Output.Select(p => (p.InstanceGuid, p.Name, p.Access, Type: p.GetType())).ToArray();
                var compactHeight = component.Attributes.Bounds.Height;
                Require(Canvas.Output(component, "Description").Attributes.Bounds.IsEmpty, "Deconstruct metadata must start collapsed.");
                Require(!Canvas.Output(component, "Geometry").Attributes.Bounds.IsEmpty, "Deconstruct geometry must remain visible.");
                canvas.Document.Enabled = true;
                canvas.Document.NewSolution(true, GH_SolutionMode.Silent);
                var name = Canvas.Output(component, "Name").VolatileData.AllData(true).Single();
                ClickArrow(component);
                component.Attributes.PerformLayout();
                Require(component.Attributes.Bounds.Height > compactHeight, $"Deconstruct expansion must increase height (compact {compactHeight}, expanded {component.Attributes.Bounds.Height}, metadata {Canvas.Output(component, "Description").Attributes.Bounds}).");
                var recipient = canvas.Panel(600, 0, 150, 30);
                canvas.Wire(component, recipient, null, "Description");
                var solves = 0;
                canvas.Document.SolutionEnd += (sender, args) => solves++;
                ClickArrow(component);
                component.Attributes.PerformLayout();
                Require(!Canvas.Output(component, "Description").Attributes.Bounds.IsEmpty, "Wired empty metadata output must remain visible.");
                Require(Canvas.Output(component, "GlobalId").Attributes.Bounds.IsEmpty, "Unwired metadata output must collapse.");
                Require(Canvas.Output(component, "GlobalId").VolatileDataCount == 1, "Hidden metadata must still be computed.");
                Require(solves == 0, "Output visibility must not solve the document.");
                canvas.Document.UndoServer.PerformUndo();
                Require(Expanded(component), "Output layout undo must expand.");
                canvas.Document.UndoServer.PerformRedo();
                Require(!Expanded(component), "Output layout redo must collapse.");
                Require(ReferenceEquals(name, Canvas.Output(component, "Name").VolatileData.AllData(true).Single()), "Output layout undo must retain computed data.");
                Require(ids.SequenceEqual(component.Params.Output.Select(p => (p.InstanceGuid, p.Name, p.Access, Type: p.GetType()))), "Output identities or contracts changed.");
                var file = Path.Combine(output, "deconstruct-collapsed.gh");
                Require(new GH_DocumentIO(canvas.Document).SaveQuiet(file), "Could not save compact deconstruct definition.");
                var io = new GH_DocumentIO();
                Require(io.Open(file), "Could not reopen compact deconstruct definition.");
                using (io.Document)
                {
                    var read = (GH_Component)io.Document.Objects.Single(o => o.InstanceGuid == component.InstanceGuid);
                    Require(!Expanded(read), "Deconstruct compact state must survive save/reopen.");
                    Require(ids.SequenceEqual(read.Params.Output.Select(p => (p.InstanceGuid, p.Name, p.Access, Type: p.GetType()))), "Saved output identities changed.");
                    Require(Canvas.Output(read, "Description").Recipients.Count == 1, "Output wire must survive save/reopen.");
                    read.Attributes.PerformLayout();
                    Require(!Canvas.Output(read, "Description").Attributes.Bounds.IsEmpty, "Reopened connected output must remain visible.");
                    for (int solve = 0; solve < 2; solve++)
                    {
                        io.Document.Enabled = true;
                        io.Document.NewSolution(true, GH_SolutionMode.Silent);
                        Require(Canvas.Output(read, "Name").VolatileDataCount == 1, "Reopened deconstruct must solve.");
                    }
                }
                var legacy = new GH_LooseChunk("Component");
                Require(component.Write(legacy), "Could not serialize deconstruct options.");
                legacy.RemoveItem("AdvancedInputsExpanded");
                var old = (GH_Component)canvas.Ifc("Deconstruct Object", 300, 300);
                Require(old.Read(legacy) && Expanded(old), "Legacy deconstruct definitions must open expanded.");
                var modify = (GH_Component)canvas.Ifc("Modify Material", 0, 300);
                Require(Canvas.Input(modify, "Description").Attributes.Bounds.IsEmpty, "Modify Material must start compact.");
                ClickArrow(modify);
                modify.Attributes.PerformLayout();
                Require(!Canvas.Input(modify, "Description").Attributes.Bounds.IsEmpty, "Modify Material metadata must expand.");
                // Creating an export canvas can service pending solutions from wire edits.
                // Capture only after assertions about layout-only actions and computed data.
                Capture(canvas.Document, Path.Combine(output, "deconstruct-collapsed.png"));
                ClickArrow(component);
                component.Attributes.PerformLayout();
                Capture(canvas.Document, Path.Combine(output, "deconstruct-expanded.png"));
            }
            Console.WriteLine("Deconstruct outputs and Modify Material: compact layout, wired visibility, identities, undo, persistence and legacy defaults passed.");
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
                    CheckParameterHover(true);
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
                    CheckParameterHover(false);
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

                    void CheckParameterHover(bool show)
                    {
                        var controls = ParameterControls(component);
                        var index = Array.FindIndex(controls, control => control.Parameter.Name == "Description" && control.Show == show);
                        Require(index >= 0, "Missing native-position parameter control.");
                        var point = controls[index].Center;
                        var radii = attributes.GetType().GetField("_parameterRadii", flags);
                        foreach (var offset in new[] { (6f, 0f), (-6f, 0f), (0f, 6f), (0f, -6f) })
                        {
                            canvas.MoveTo(new PointF(point.X + offset.Item1, point.Y + offset.Item2));
                            Require(Math.Abs(((float[])radii.GetValue(attributes))[index] - 2.25f) < .02f,
                                "Parameter +/- hover sizing must match native controls from every direction.");
                        }
                        canvas.MoveTo(point);
                        Require(((float[])radii.GetValue(attributes))[index] == 3f, "Parameter +/- maximum radius must be 3.");
                        canvas.LeaveCanvas();
                        Require(((float[])radii.GetValue(attributes))[index] == 1.5f, "Parameter +/- must reset on mouse leave.");
                    }
                }
                Require(subscription.GetValue(attributes) == null, "Disposing the canvas must detach hover handlers.");
            }
            Console.WriteLine("Canvas hover: cardinal/diagonal approaches, outside bottom/top edge growth, mouse leave and subscription cleanup passed.");
        }

        private static void CheckZoomRendering(string output)
        {
            var sunglassesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Grasshopper", "Libraries", "sunglasses.gha");
            System.Reflection.MethodInfo richCapsules = null;
            if (File.Exists(sunglassesPath))
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name.Equals("sunglasses", StringComparison.OrdinalIgnoreCase))
                    ?? System.Reflection.Assembly.Load(File.ReadAllBytes(sunglassesPath));
                richCapsules = assembly.GetType("Sunglasses.Drawing")?.GetMethod("PaintRichedCapsules");
                Require(richCapsules != null, "Installed Sunglasses has no rich-capsule rendering entry point.");
            }
            var definition = new Canvas(expandAdvancedInputs: false);
            using (definition.Document)
            {
                foreach (var name in new[] { "Object", "Deconstruct Object" })
                {
                    var component = (GH_Component)definition.Ifc(name, 180, 100);
                    foreach (var expanded in new[] { false, true })
                    {
                        if (expanded) ClickArrow(component);
                        var content = ((GH_ComponentAttributes)component.Attributes).ContentBox;
                        var control = expanded ? ParameterControls(component).Last(c => !c.Show) : ParameterControls(component).Last(c => c.Show);
                        using (var canvas = new HoverProbeCanvas { Size = new Size(1000, 700), Document = definition.Document })
                        {
                            canvas.CreateControl();
                            foreach (var zoom in new[] { 1f, 4f, 6f, 10f, 20f })
                            {
                                canvas.Viewport.Zoom = zoom;
                                canvas.Viewport.Focus(new PointF(control.Center.X * zoom, control.Center.Y * zoom));
                                canvas.MoveTo(new PointF(control.Center.X + 20, control.Center.Y + 20));
                                // GH fades interactive controls over successive frames. Compare
                                // settled frames, rather than two different animation alphas.
                                for (var frame = 0; frame < 64; frame++)
                                {
                                    using (canvas.GetCanvasScreenBuffer(GH_CanvasMode.Control)) { }
                                    if (GH_Canvas.ZoomFadeHigh == 255) break;
                                    System.Threading.Thread.Sleep(10);
                                    Application.DoEvents();
                                }
                                using (var baseline = canvas.GetCanvasScreenBuffer(GH_CanvasMode.Control))
                                {
                                    canvas.CanvasPostPaintObjects += PaintDecoration;
                                    if (richCapsules != null)
                                    {
                                        var fade = (GH_FadeAnimation)richCapsules.DeclaringType.GetProperty("Fade").GetValue(null);
                                        for (var frame = 0; zoom >= fade.Threshold && frame < 128; frame++)
                                        {
                                            using (canvas.GetCanvasScreenBuffer(GH_CanvasMode.Control)) { }
                                            if (fade.FadeAlpha == 255) break;
                                            System.Threading.Thread.Sleep(10);
                                            Application.DoEvents();
                                        }
                                        Require(zoom < fade.Threshold || fade.FadeAlpha == 255,
                                            $"Sunglasses high-zoom decoration must finish fading in: zoom={zoom}, threshold={fade.Threshold}, alpha={fade.FadeAlpha}, duration={fade.Duration}.");
                                    }
                                    using (var decorated = canvas.GetCanvasScreenBuffer(GH_CanvasMode.Control))
                                    {
                                        // The circle's two sides must survive the later decoration,
                                        // including a minus that lies on the edge of a parameter panel.
                                        foreach (var offset in new[] { -.9f, .9f })
                                        {
                                            var point = canvas.Viewport.ProjectPoint(new PointF(control.Center.X + offset, control.Center.Y));
                                            var x = (int)Math.Round(point.X);
                                            var y = (int)Math.Round(point.Y);
                                            Require(x >= 0 && x < baseline.Width && y >= 0 && y < baseline.Height,
                                                $"Zoom probe outside bitmap: {name}, expanded={expanded}, zoom={zoom}, point={point}, size={baseline.Size}, center={control.Center}.");
                                            Require(baseline.GetPixel(x, y).ToArgb() == decorated.GetPixel(x, y).ToArgb(),
                                                $"{name}: {(expanded ? "minus" : "plus")} is obscured at zoom {zoom}, at {point}, baseline={baseline.GetPixel(x, y)}, decorated={decorated.GetPixel(x, y)}.");
                                        }
                                        var border = canvas.Viewport.ProjectPoint(new PointF(control.Center.X + 4, component.Attributes.Bounds.Bottom));
                                        var borderX = (int)Math.Round(border.X);
                                        var borderY = (int)Math.Round(border.Y);
                                        if (borderX >= 0 && borderX < baseline.Width && borderY >= 0 && borderY < baseline.Height)
                                            Require(baseline.GetPixel(borderX, borderY).ToArgb() == decorated.GetPixel(borderX, borderY).ToArgb(),
                                                $"{name}: capsule bottom border is damaged at zoom {zoom}.");
                                        if (zoom == 20)
                                            decorated.Save(Path.Combine(output, $"zoom-{name.Replace(' ', '-')}-{expanded}.png"), System.Drawing.Imaging.ImageFormat.Png);
                                    }
                                    canvas.CanvasPostPaintObjects -= PaintDecoration;
                                }
                            }
                            canvas.Document = null;
                        }
                        if (expanded) ClickArrow(component);

                        void PaintDecoration(GH_Canvas canvas)
                        {
                            // Exercise the installed overlay without changing its user settings.
                            // A simple opaque repaint also guarantees coverage without Sunglasses.
                            if (richCapsules != null)
                                richCapsules.Invoke(null, new object[] { canvas, new IGH_DocumentObject[] { component } });
                            else
                                canvas.Graphics.FillRectangle(Brushes.LightGray, content);
                        }
                    }
                    definition.Document.RemoveObject(component, false);
                }
            }
            Console.WriteLine($"Zoom controls: input/output plus/minus survive post-object decoration at 100–2000% ({(richCapsules == null ? "synthetic overlay; Sunglasses unavailable" : "installed Sunglasses")}).");
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
                return menu.Items.OfType<ToolStripMenuItem>().Single(IsAdvancedMenu).Checked;
            }
        }

        private static bool IsAdvancedMenu(ToolStripMenuItem item) =>
            item.Text == "Show advanced inputs" || item.Text == "Show advanced parameters";

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
