using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Undo;

namespace IfcHopper.Components
{
    /// <summary>Component with collapsible advanced parameters; hiding unused parameters resets their tree controls without removing registered parameters.</summary>
    public abstract class CollapsibleComponent : IfcComponent
    {
        // These optional metadata inputs have no persistent defaults. Main task inputs (including
        // geometry, type, material and placement) remain visible. Never infer relevance from Optional alone.
        private static readonly HashSet<string> AdvancedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Description", "GlobalId", "Tag", "Attribute Names", "Attribute Values",
            "Classifications", "Property Sets", "Contexts", "Units", "Georeference"
        };

        private static readonly HashSet<string> AdvancedOutputNames = new HashSet<string>(AdvancedNames, StringComparer.Ordinal)
        {
            "Author", "Organization", "Source", "Geodetic Datum", "Vertical Datum", "Map Zone",
            "Identifiers", "Target Views", "From Type", "Method", "Formulas"
        };

        private bool _expanded;
        private readonly Dictionary<Guid, bool> _visibility = new Dictionary<Guid, bool>();

        protected CollapsibleComponent(string name, string nickname, string description, string category, string subcategory)
            : base(name, nickname, description + " Use the bottom arrow to show advanced parameters and the top arrow to collapse them; connected parameters and configured inputs stay visible.", category, subcategory) { }

        internal bool Expanded => _expanded;
        internal bool HasAdvancedOutputs => Params.Output.Any(IsAdvancedOutput);
        internal bool HasAdvancedParameters => Params.Input.Any(IsAdvancedInput) || HasAdvancedOutputs;
        internal bool CanCollapseParameters => Params.Input.Any(p => CanHide(p) && IsInputVisible(p))
            || Params.Output.Any(p => CanHide(p) && IsOutputVisible(p));
        internal string AdvancedMenuText => HasAdvancedOutputs ? "Show advanced parameters" : "Show advanced inputs";

        /// <summary>Identifies optional metadata inputs eligible to collapse when unused; overrides can add component-specific metadata.</summary>
        protected virtual bool IsAdvancedInput(IGH_Param parameter) => parameter.Optional && AdvancedNames.Contains(parameter.Name);

        /// <summary>Identifies secondary metadata outputs eligible to collapse when they have no recipients.</summary>
        protected virtual bool IsAdvancedOutput(IGH_Param parameter) => AdvancedOutputNames.Contains(parameter.Name);

        internal bool IsOutputVisible(IGH_Param parameter) =>
            !IsAdvancedOutput(parameter) || parameter.Recipients.Count > 0 || ChosenVisibility(parameter);

        private bool ChosenVisibility(IGH_Param parameter) =>
            _visibility.TryGetValue(parameter.InstanceGuid, out var visible) ? visible : _expanded;

        internal bool IsEligible(IGH_Param parameter) => parameter.Kind == GH_ParamKind.input
            ? IsAdvancedInput(parameter) : IsAdvancedOutput(parameter);

        internal bool CanHide(IGH_Param parameter) => IsEligible(parameter) && (parameter.Kind == GH_ParamKind.input
            ? !InputConfigured(parameter) : parameter.Recipients.Count == 0);

        internal bool IsInputVisible(IGH_Param parameter)
        {
            return !IsAdvancedInput(parameter) || InputConfigured(parameter) || ChosenVisibility(parameter);
        }

        private static bool InputConfigured(IGH_Param parameter)
        {
            if (parameter.SourceCount > 0) return true;
            // Preserve configured inputs before the first solve as well as after data collection.
            if (parameter is Param_String text && !text.PersistentData.IsEmpty) return true;
            if (parameter is Param_GenericObject generic && !generic.PersistentData.IsEmpty) return true;
            return parameter.DataType == GH_ParamData.local;
        }

        internal void ToggleAdvancedInputs()
        {
            SetAllParametersExpanded(!_expanded);
        }

        internal void CollapseAdvancedParameters() => SetAllParametersExpanded(false);

        private void SetAllParametersExpanded(bool expanded)
        {
            var before = CaptureVisibility();
            var after = new VisibilityState(expanded, new Dictionary<Guid, bool>(), new Dictionary<Guid, ParameterControls>(before.Controls));
            if (!expanded)
                foreach (var parameter in Params.Input.Concat(Params.Output).Where(CanHide))
                    after.Controls[parameter.InstanceGuid] = new ParameterControls(GH_DataMapping.None, false, false);
            RecordUndoEvent(expanded ? "Expand advanced parameters" : "Collapse advanced parameters", new AdvancedInputsUndoAction(InstanceGuid, before, after));
            ApplyVisibility(after);
        }

        internal void SetParameterVisible(IGH_Param parameter, bool visible)
        {
            if (!IsEligible(parameter) || !visible && !CanHide(parameter)) return;
            var after = CaptureVisibility();
            after.Choices[parameter.InstanceGuid] = visible;
            if (!visible)
                after.Controls[parameter.InstanceGuid] = new ParameterControls(GH_DataMapping.None, false, false);
            RecordUndoEvent((visible ? "Show " : "Hide ") + parameter.Name, new AdvancedInputsUndoAction(InstanceGuid, CaptureVisibility(), after));
            ApplyVisibility(after);
        }

        private sealed class VisibilityState
        {
            public readonly bool Expanded;
            public readonly Dictionary<Guid, bool> Choices;
            public readonly Dictionary<Guid, ParameterControls> Controls;
            public VisibilityState(bool expanded, Dictionary<Guid, bool> choices, Dictionary<Guid, ParameterControls> controls = null)
            {
                Expanded = expanded;
                Choices = choices;
                Controls = controls ?? new Dictionary<Guid, ParameterControls>();
            }
        }

        private sealed class ParameterControls
        {
            public readonly GH_DataMapping Mapping;
            public readonly bool Reverse;
            public readonly bool Simplify;
            public ParameterControls(GH_DataMapping mapping, bool reverse, bool simplify)
            { Mapping = mapping; Reverse = reverse; Simplify = simplify; }
        }

        private VisibilityState CaptureVisibility() => new VisibilityState(_expanded, new Dictionary<Guid, bool>(_visibility),
            Params.Input.Concat(Params.Output).Where(IsEligible).ToDictionary(p => p.InstanceGuid,
                p => new ParameterControls(p.DataMapping, p.Reverse, p.Simplify)));

        private void ApplyVisibility(VisibilityState state)
        {
            _expanded = state.Expanded;
            _visibility.Clear();
            foreach (var choice in state.Choices) _visibility.Add(choice.Key, choice.Value);
            foreach (var parameter in Params.Input.Concat(Params.Output))
                if (state.Controls.TryGetValue(parameter.InstanceGuid, out var controls))
                {
                    parameter.DataMapping = controls.Mapping;
                    parameter.Reverse = controls.Reverse;
                    parameter.Simplify = controls.Simplify;
                }
            Attributes.ExpireLayout();
            // Only unused parameters have their controls reset; do not solve or retrigger Write IFC.
            OnObjectChanged(GH_ObjectEventType.Layout);
        }

        // Capture document-local identities, visibility and parameter controls. A generic object undo would deserialize
        // all parameters and expire the solution; undoing this layout option must not retrigger IO.
        private sealed class AdvancedInputsUndoAction : IGH_UndoAction
        {
            private Guid _id;
            private VisibilityState _before;
            private VisibilityState _after;
            public AdvancedInputsUndoAction() { }
            public AdvancedInputsUndoAction(Guid id, VisibilityState before, VisibilityState after) { _id = id; _before = before; _after = after; }
            public bool ExpiresSolution => false;
            public bool ExpiresDisplay => false;
            public GH_UndoState State { get; private set; } = GH_UndoState.undo;
            public void Undo(GH_Document doc)
            {
                if (State != GH_UndoState.undo) throw new InvalidOperationException("This input-layout action cannot be undone twice.");
                Apply(doc, _before);
                State = GH_UndoState.redo;
            }
            public void Redo(GH_Document doc)
            {
                if (State != GH_UndoState.redo) throw new InvalidOperationException("This input-layout action must be undone before redo.");
                Apply(doc, _after);
                State = GH_UndoState.undo;
            }
            private void Apply(GH_Document doc, VisibilityState value)
            {
                var component = doc.FindObject(_id, false) as CollapsibleComponent
                    ?? throw new InvalidOperationException("The component for this advanced-input undo action no longer exists.");
                component.ApplyVisibility(value);
            }
            public bool Write(GH_IWriter writer)
            {
                writer.SetGuid("ComponentId", _id);
                WriteVisibility(writer.CreateChunk("Before"), _before, true);
                WriteVisibility(writer.CreateChunk("After"), _after, true);
                writer.SetInt32("State", (int)State);
                return true;
            }
            public bool Read(GH_IReader reader)
            {
                if (!reader.ItemExists("ComponentId") || !reader.ItemExists("State")) return false;
                _id = reader.GetGuid("ComponentId");
                // Older layout-only undo records contained only the previous global flag.
                if (reader.ItemExists("Before"))
                {
                    var before = reader.GetBoolean("Before");
                    _before = new VisibilityState(before, new Dictionary<Guid, bool>());
                    _after = new VisibilityState(!before, new Dictionary<Guid, bool>());
                }
                else
                {
                    var before = reader.FindChunk("Before");
                    var after = reader.FindChunk("After");
                    if (before == null || after == null) return false;
                    _before = ReadVisibility(before);
                    _after = ReadVisibility(after);
                }
                State = (GH_UndoState)reader.GetInt32("State");
                return _id != Guid.Empty && Enum.IsDefined(typeof(GH_UndoState), State);
            }
        }

        private static void WriteVisibility(GH_IWriter writer, VisibilityState state, bool includeControls = false)
        {
            writer.SetInt32("Version", 1);
            writer.SetBoolean("Expanded", state.Expanded);
            writer.SetInt32("Count", state.Choices.Count);
            var index = 0;
            foreach (var choice in state.Choices.OrderBy(pair => pair.Key))
            {
                var chunk = writer.CreateChunk("Choice", index++);
                chunk.SetGuid("Parameter", choice.Key);
                chunk.SetBoolean("Visible", choice.Value);
            }
            // Definition persistence already writes these settings through the parameters.
            // Undo records need their own snapshots to restore a collapsed parameter's controls.
            if (includeControls)
            {
                writer.SetInt32("ControlCount", state.Controls.Count);
                index = 0;
                foreach (var control in state.Controls.OrderBy(pair => pair.Key))
                {
                    var chunk = writer.CreateChunk("Control", index++);
                    chunk.SetGuid("Parameter", control.Key);
                    chunk.SetInt32("Mapping", (int)control.Value.Mapping);
                    chunk.SetBoolean("Reverse", control.Value.Reverse);
                    chunk.SetBoolean("Simplify", control.Value.Simplify);
                }
            }
        }

        private static VisibilityState ReadVisibility(GH_IReader reader)
        {
            var choices = new Dictionary<Guid, bool>();
            if (reader.ItemExists("Version") && reader.GetInt32("Version") != 1)
                return new VisibilityState(false, choices);
            var count = reader.ItemExists("Count") ? Math.Min(reader.GetInt32("Count"), reader.ChunkCount) : 0;
            for (int i = 0; i < count; i++)
            {
                var choice = reader.FindChunk("Choice", i);
                if (choice != null && choice.ItemExists("Parameter") && choice.ItemExists("Visible"))
                    choices[choice.GetGuid("Parameter")] = choice.GetBoolean("Visible");
            }
            var controls = new Dictionary<Guid, ParameterControls>();
            var controlCount = reader.ItemExists("ControlCount") ? Math.Min(reader.GetInt32("ControlCount"), reader.ChunkCount) : 0;
            for (int i = 0; i < controlCount; i++)
            {
                var chunk = reader.FindChunk("Control", i);
                if (chunk == null || !chunk.ItemExists("Parameter") || !chunk.ItemExists("Mapping")
                    || !chunk.ItemExists("Reverse") || !chunk.ItemExists("Simplify")) continue;
                var mapping = (GH_DataMapping)chunk.GetInt32("Mapping");
                if (Enum.IsDefined(typeof(GH_DataMapping), mapping))
                    controls[chunk.GetGuid("Parameter")] = new ParameterControls(mapping, chunk.GetBoolean("Reverse"), chunk.GetBoolean("Simplify"));
            }
            return new VisibilityState(reader.ItemExists("Expanded") && reader.GetBoolean("Expanded"), choices, controls);
        }

        public override void CreateAttributes()
        {
            (m_attributes as CollapsibleComponentAttributes)?.DetachHoverCanvas();
            m_attributes = new CollapsibleComponentAttributes(this);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            (m_attributes as CollapsibleComponentAttributes)?.DetachHoverCanvas();
            base.RemovedFromDocument(document);
        }

        public override void MovedBetweenDocuments(GH_Document oldDocument, GH_Document newDocument)
        {
            (m_attributes as CollapsibleComponentAttributes)?.DetachHoverCanvas();
            base.MovedBetweenDocuments(oldDocument, newDocument);
        }

        public override void DocumentContextChanged(GH_Document document, GH_DocumentContext context)
        {
            // A temporary export canvas can mark the same document Unloaded while the live
            // canvas still displays it. Actual canvas switches detach through DocumentChanged.
            if (context == GH_DocumentContext.Close)
                (m_attributes as CollapsibleComponentAttributes)?.DetachHoverCanvas();
            base.DocumentContextChanged(document, context);
        }

        protected override void AfterSolveInstance()
        {
            base.AfterSolveInstance();
            Attributes.ExpireLayout();
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            if (!HasAdvancedParameters) return;
            Menu_AppendItem(menu, AdvancedMenuText, (sender, args) => ToggleAdvancedInputs(), true, _expanded)
                .ToolTipText = "Show secondary metadata parameters. Connected parameters and configured inputs stay visible when collapsed. Expand with the bottom arrow and collapse with the top arrow.";
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean("AdvancedInputsExpanded", _expanded);
            WriteVisibility(writer.CreateChunk("IndividualVisibility"), CaptureVisibility());
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            // Existing definitions predate compact mode; preserve their fully expanded layout.
            _expanded = !reader.ItemExists("AdvancedInputsExpanded") || reader.GetBoolean("AdvancedInputsExpanded");
            var success = base.Read(reader);
            _visibility.Clear();
            var visibility = reader.FindChunk("IndividualVisibility");
            if (visibility != null)
            {
                var eligible = new HashSet<Guid>(Params.Input.Concat(Params.Output).Where(IsEligible).Select(p => p.InstanceGuid));
                foreach (var choice in ReadVisibility(visibility).Choices)
                    if (eligible.Contains(choice.Key)) _visibility[choice.Key] = choice.Value;
            }
            Attributes?.ExpireLayout();
            return success;
        }
    }
}
