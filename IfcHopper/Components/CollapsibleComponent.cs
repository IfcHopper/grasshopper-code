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
    /// <summary>Component with a presentation-only advanced-parameter toggle; registered parameters and their data remain unchanged.</summary>
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

        protected CollapsibleComponent(string name, string nickname, string description, string category, string subcategory)
            : base(name, nickname, description + " Use the bottom arrow to show advanced parameters and the top arrow to collapse them; connected parameters and configured inputs stay visible.", category, subcategory) { }

        internal bool Expanded => _expanded;
        internal bool HasAdvancedOutputs => Params.Output.Any(IsAdvancedOutput);
        internal bool HasAdvancedParameters => Params.Input.Any(IsAdvancedInput) || HasAdvancedOutputs;
        internal string AdvancedMenuText => HasAdvancedOutputs ? "Show advanced parameters" : "Show advanced inputs";

        /// <summary>Identifies optional metadata inputs eligible to collapse when unused; overrides can add component-specific metadata.</summary>
        protected virtual bool IsAdvancedInput(IGH_Param parameter) => parameter.Optional && AdvancedNames.Contains(parameter.Name);

        /// <summary>Identifies secondary metadata outputs eligible to collapse when they have no recipients.</summary>
        protected virtual bool IsAdvancedOutput(IGH_Param parameter) => AdvancedOutputNames.Contains(parameter.Name);

        internal bool IsOutputVisible(IGH_Param parameter) =>
            _expanded || !IsAdvancedOutput(parameter) || parameter.Recipients.Count > 0;

        internal bool IsInputVisible(IGH_Param parameter)
        {
            if (_expanded || !IsAdvancedInput(parameter) || parameter.SourceCount > 0) return true;
            // Preserve configured inputs before the first solve as well as after data collection.
            if (parameter is Param_String text && !text.PersistentData.IsEmpty) return true;
            if (parameter is Param_GenericObject generic && !generic.PersistentData.IsEmpty) return true;
            return parameter.DataType == GH_ParamData.local || parameter.DataMapping != GH_DataMapping.None
                || parameter.Reverse || parameter.Simplify;
        }

        internal void ToggleAdvancedInputs()
        {
            RecordUndoEvent(_expanded ? "Collapse advanced parameters" : "Expand advanced parameters", new AdvancedInputsUndoAction(InstanceGuid, _expanded));
            SetExpanded(!_expanded);
        }

        private void SetExpanded(bool value)
        {
            _expanded = value;
            Attributes.ExpireLayout();
            // Layout-only changes must not solve a definition or retrigger Write IFC.
            OnObjectChanged(GH_ObjectEventType.Layout);
        }

        // Capture only a document-local identity and a bool. A generic object undo would deserialize
        // all parameters and expire the solution; undoing this layout option must not retrigger IO.
        private sealed class AdvancedInputsUndoAction : IGH_UndoAction
        {
            private Guid _id;
            private bool _before;
            public AdvancedInputsUndoAction() { }
            public AdvancedInputsUndoAction(Guid id, bool before) { _id = id; _before = before; }
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
                Apply(doc, !_before);
                State = GH_UndoState.undo;
            }
            private void Apply(GH_Document doc, bool value)
            {
                var component = doc.FindObject(_id, false) as CollapsibleComponent
                    ?? throw new InvalidOperationException("The component for this advanced-input undo action no longer exists.");
                component.SetExpanded(value);
            }
            public bool Write(GH_IWriter writer)
            {
                writer.SetGuid("ComponentId", _id);
                writer.SetBoolean("Before", _before);
                writer.SetInt32("State", (int)State);
                return true;
            }
            public bool Read(GH_IReader reader)
            {
                if (!reader.ItemExists("ComponentId") || !reader.ItemExists("Before") || !reader.ItemExists("State")) return false;
                _id = reader.GetGuid("ComponentId");
                _before = reader.GetBoolean("Before");
                State = (GH_UndoState)reader.GetInt32("State");
                return _id != Guid.Empty && Enum.IsDefined(typeof(GH_UndoState), State);
            }
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
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            // Existing definitions predate compact mode; preserve their fully expanded layout.
            _expanded = !reader.ItemExists("AdvancedInputsExpanded") || reader.GetBoolean("AdvancedInputsExpanded");
            var success = base.Read(reader);
            Attributes?.ExpireLayout();
            return success;
        }
    }
}
