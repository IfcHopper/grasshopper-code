using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace IfcHopper.Components
{
    /// <summary>Compacts unused metadata parameters and draws expand/collapse controls without altering the parameter server.</summary>
    internal sealed class CollapsibleComponentAttributes : GH_ComponentAttributes
    {
        // GH1 8.20 stores the rendered tag list internally; IGH_Param.StateTags returns
        // a fresh list, so updating it cannot move the icons used by rendering/tooltips.
        // Verified on Rhino 8; replace this bridge when GH exposes cached tag layout publicly.
        private static readonly System.Reflection.FieldInfo RenderTagsField = typeof(GH_LinkedParamAttributes)
            .GetField("m_renderTags", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Grasshopper linked-parameter state-tag cache is unavailable.");
        private readonly CollapsibleComponent _component;
        private RectangleF _button;
        private RectangleF _collapseButton;
        private float _collapseRadius = 1.5f;
        private bool _compact;
        private GH_Canvas _hoverCanvas;
        private Point _cursorControl;
        private bool _hasCursor;
        private float _hoverRadius = 1.5f;
        private readonly List<ParameterControl> _parameterControls = new List<ParameterControl>();
        private float[] _parameterRadii = Array.Empty<float>();

        private sealed class ParameterControl
        {
            public IGH_Param Parameter { get; }
            public bool Show { get; }
            public PointF Center { get; }
            public ParameterControl(IGH_Param parameter, bool show, PointF center) { Parameter = parameter; Show = show; Center = center; }
        }
        private PointF ButtonCenter => new PointF(_button.Left + _button.Width * 0.5f, _button.Top + _button.Height * 0.5f);

        // Measured from Rhino 8.37's annotation ZUI renderer (IOComponents.ZuiComponent):
        // radius 1.5 at/above distance 9, linear growth down to distance 3, then radius 3.
        // The renderer is private to IOComponents.gha, so do not depend on it or reflect into it at runtime.
        internal static float ButtonRadius(float distance) => Math.Clamp(3.75f - distance * 0.25f, 1.5f, 3f);

        public CollapsibleComponentAttributes(CollapsibleComponent component) : base(component) => _component = component;

        protected override void Layout()
        {
            base.Layout();
            _button = RectangleF.Empty;
            _collapseButton = RectangleF.Empty;
            _parameterControls.Clear();
            _parameterRadii = Array.Empty<float>();
            if (!_component.HasAdvancedParameters) return;
            var inputs = Owner.Params.Input.Where(_component.IsInputVisible).ToList();
            var outputs = Owner.Params.Output.Where(_component.IsOutputVisible).ToList();
            _compact = inputs.Count < Owner.Params.Input.Count || outputs.Count < Owner.Params.Output.Count;
            if (_compact)
            {
                // Derive row spacing from the native layout instead of imposing a model-unit tolerance
                // or a second font scale. The icon sets the same minimum body height as a normal component.
                // A lone input spans a tall output column on deconstruct components. Use the
                // smallest native row from either side, rather than that stretched input row.
                var rowHeight = Owner.Params.Input.Concat(Owner.Params.Output).Select(p => p.Attributes.Bounds.Height)
                    .Where(height => height > 0).DefaultIfEmpty(GH_FontServer.Standard.Height + 4).Min();
                var rows = Math.Max(inputs.Count, outputs.Count);
                var nativeRows = Math.Max(Owner.Params.Input.Count, Owner.Params.Output.Count);
                m_innerBounds.Height = Math.Max(32, m_innerBounds.Height - (nativeRows - rows) * rowHeight);
                LayoutRows(inputs, rowHeight, true);
                LayoutRows(outputs, rowHeight, false);
                foreach (var hidden in Owner.Params.Input.Where(p => !_component.IsInputVisible(p))
                    .Concat(Owner.Params.Output.Where(p => !_component.IsOutputVisible(p))))
                {
                    hidden.Attributes.Bounds = RectangleF.Empty;
                    hidden.Attributes.Pivot = PointF.Empty;
                    MoveStateTags(hidden.Attributes, 0, true);
                }
                Bounds = m_innerBounds;
                foreach (var parameter in inputs.Concat(outputs))
                    Bounds = RectangleF.Union(Bounds, parameter.Attributes.Bounds);
                // Match GH_ComponentAttributes.LayoutBounds' native capsule padding, while
                // excluding hidden zero-sized rows from the union.
                Bounds.Inflate(2, 2);
            }

            // Keep the maximum-size hit region fixed while the drawing responds to cursor distance.
            // Keep the expand control inside the bottom capsule stroke, as in the annotation
            // components. The collapse control already aligns with the native top position.
            var y = _component.Expanded ? m_innerBounds.Top : m_innerBounds.Bottom - 2;
            _button = new RectangleF(m_innerBounds.Left + m_innerBounds.Width * 0.5f - 3, y - 3, 6, 6);
            // Individually revealed rows need a collapse action even while the global mode
            // remains compact. Keep the bottom expand arrow available for the other rows.
            if (!_component.Expanded && _component.CanCollapseParameters)
                _collapseButton = new RectangleF(m_innerBounds.Left + m_innerBounds.Width * .5f - 3, m_innerBounds.Top - 3, 6, 6);
            LayoutParameterControls(Owner.Params.Input, true);
            LayoutParameterControls(Owner.Params.Output, false);
            _parameterRadii = Enumerable.Repeat(1.5f, _parameterControls.Count).ToArray();
        }

        private void LayoutParameterControls(List<IGH_Param> parameters, bool input)
        {
            var visible = parameters.Where(p => input ? _component.IsInputVisible(p) : _component.IsOutputVisible(p)).ToList();
            var edge = input ? m_innerBounds.Left : m_innerBounds.Right;
            IGH_Param previous = null;
            bool gapHasControl = false;
            foreach (var parameter in parameters)
            {
                if (!visible.Contains(parameter))
                {
                    if (_component.IsEligible(parameter) && !gapHasControl)
                    {
                        // Match GH 8.37's native variable-parameter UI: plus at the gap after
                        // the previous row (or above the first); reveal the next hidden row.
                        var gap = previous?.Attributes.Bounds.Bottom ?? (visible.Count > 0 ? visible[0].Attributes.Bounds.Top
                            : m_innerBounds.Top + m_innerBounds.Height * .5f);
                        // The trailing gap belongs to the capsule bottom even when this
                        // column is shorter. Match the expand arrow's inset from the stroke.
                        if (visible.Count == 0 || ReferenceEquals(previous, visible[visible.Count - 1]))
                            gap = m_innerBounds.Bottom - 2;
                        _parameterControls.Add(new ParameterControl(parameter, true, new PointF(edge, gap)));
                        gapHasControl = true;
                    }
                    continue;
                }
                if (_component.CanHide(parameter))
                    _parameterControls.Add(new ParameterControl(parameter, false,
                        new PointF(edge, parameter.Attributes.Bounds.Top + parameter.Attributes.Bounds.Height * .5f)));
                previous = parameter;
                gapHasControl = false;
            }
        }

        private void LayoutRows(System.Collections.Generic.List<IGH_Param> parameters, float rowHeight, bool input)
        {
            var top = m_innerBounds.Top + (m_innerBounds.Height - parameters.Count * rowHeight) * 0.5f;
            for (int i = 0; i < parameters.Count; i++)
            {
                var attributes = parameters[i].Attributes;
                var box = attributes.Bounds;
                var oldCenter = box.Top + box.Height * .5f;
                box.Y = top + i * rowHeight;
                box.Height = rowHeight;
                attributes.Bounds = box;
                attributes.Pivot = new PointF(input ? box.Left : box.Right, box.Top + rowHeight * 0.5f);
                // GH_LinkedParamAttributes caches the state-tag stages during Layout. Move
                // them after moving the row so mapping/simplify icons and tooltips follow it.
                MoveStateTags(attributes, box.Top + box.Height * .5f - oldCenter, false);
            }
        }

        private static void MoveStateTags(IGH_Attributes attributes, float deltaY, bool hidden)
        {
            if (RenderTagsField.GetValue(attributes) is not GH_StateTagList tags) return;
            foreach (IGH_StateTag tag in tags)
            {
                var stage = tag.Stage;
                stage.Offset(0, (int)Math.Round(deltaY));
                tag.Stage = hidden ? Rectangle.Empty : stage;
            }
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            if (channel != GH_CanvasChannel.Objects || _button.IsEmpty)
            {
                base.Render(canvas, graphics, channel);
                return;
            }
            var interactive = canvas.DrawingMode == GH_CanvasMode.Control;
            if (interactive) AttachHoverCanvas(canvas);
            if (!_compact) base.Render(canvas, graphics, channel);
            else
            {
                // The native label renderer skips zero-width rows, preserving justification, font,
                // state tags, theme and zoom fading exactly. Only grips need filtering separately.
                RenderComponentCapsule(canvas, graphics, true, true, true, false, true, true);
                foreach (var parameter in Owner.Params.Input.Where(_component.IsInputVisible)
                    .Concat(Owner.Params.Output.Where(_component.IsOutputVisible)))
                {
                    if (parameter.Kind == GH_ParamKind.input) GH_CapsuleRenderEngine.RenderInputGrip(graphics, canvas.Viewport.Zoom, parameter.Attributes.InputGrip, false);
                    else GH_CapsuleRenderEngine.RenderOutputGrip(graphics, canvas.Viewport.Zoom, parameter.Attributes.OutputGrip, false);
                }
            }

            // Canvas decorations (for example Sunglasses' rich capsules) repaint objects
            // after this channel. Draw interactive controls after those decorations.
            // Exports retain the existing arrows and omit interactive +/- controls.
            if (!interactive) RenderControls(canvas, graphics, false);
        }

        private void CanvasPrePaintOverlay(GH_Canvas canvas)
        {
            if (canvas.DrawingMode != GH_CanvasMode.Control || _button.IsEmpty
                || !ReferenceEquals(canvas.Document, _component.OnPingDocument())) return;
            var bounds = Bounds;
            if (!canvas.Viewport.IsVisible(ref bounds, 10)) return;
            var graphics = canvas.Graphics;
            var state = graphics.Save();
            try
            {
                // Rich-capsule overlays can paint across the capsule's rounded edge too.
                // Use GH's outline renderer to retain its palette, corners and zoom rules.
                var palette = GH_CapsuleRenderEngine.GetImpliedPalette(Owner);
                if (palette == GH_Palette.Normal && !Owner.IsPreviewCapable) palette = GH_Palette.Hidden;
                using (var capsule = GH_Capsule.CreateCapsule(Bounds, palette))
                {
                    capsule.SetJaggedEdges(Owner.Params.Input.Count == 0, Owner.Params.Output.Count == 0);
                    var style = GH_CapsuleRenderEngine.GetImpliedStyle(palette, Selected, Owner.Locked, Owner.Hidden);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    capsule.RenderEngine.RenderOutlines(graphics, canvas.Viewport.Zoom, style);
                }
                RenderControls(canvas, graphics, true);
            }
            finally { graphics.Restore(state); }
        }

        private void RenderControls(GH_Canvas canvas, Graphics graphics, bool interactive)
        {
            var center = ButtonCenter;
            // Re-evaluate on paint as well: zooming or moving the component changes its distance
            // from the last canvas mouse position even without another mouse event.
            var radius = interactive && _hasCursor ? ButtonRadius(Distance(center, canvas.Viewport.UnprojectPoint(_cursorControl))) : 1.5f;
            if (interactive) _hoverRadius = radius;
            RenderArrow(graphics, center, _component.Expanded, radius);
            if (!_collapseButton.IsEmpty)
            {
                var collapseCenter = CenterOf(_collapseButton);
                var collapseRadius = interactive && _hasCursor ? ButtonRadius(Distance(collapseCenter, canvas.Viewport.UnprojectPoint(_cursorControl))) : 1.5f;
                if (interactive) _collapseRadius = collapseRadius;
                RenderArrow(graphics, collapseCenter, true, collapseRadius);
            }
            if (interactive && GH_Canvas.ZoomFadeHigh > 0)
            {
                var cursor = _hasCursor ? canvas.Viewport.UnprojectPoint(_cursorControl) : new PointF(float.MaxValue, float.MaxValue);
                for (int i = 0; i < _parameterControls.Count; i++)
                {
                    var control = _parameterControls[i];
                    var controlRadius = _hasCursor ? ButtonRadius(Distance(control.Center, cursor)) : 1.5f;
                    _parameterRadii[i] = controlRadius;
                    RenderParameterControl(graphics, control, controlRadius, GH_Canvas.ZoomFadeHigh);
                }
            }
        }

        private static PointF CenterOf(RectangleF box) => new PointF(box.Left + box.Width * .5f, box.Top + box.Height * .5f);

        private static void RenderArrow(Graphics graphics, PointF center, bool collapse, float radius)
        {
            var x = center.X;
            var y = center.Y;
            var direction = collapse ? -1 : 1;
            var smoothing = graphics.SmoothingMode;
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var circle = new RectangleF(x - radius, y - radius, radius * 2, radius * 2);
                graphics.FillEllipse(collapse ? Brushes.White : Brushes.Black, circle);
                using (var edge = new Pen(Color.Black, 0.5f)) graphics.DrawEllipse(edge, circle);
                using (var pen = new Pen(collapse ? Color.Black : Color.White, radius * 0.25f)
                    { LineJoin = LineJoin.Miter, StartCap = LineCap.Flat, EndCap = LineCap.Flat })
                    graphics.DrawLines(pen, new[]
                    {
                        new PointF(x - radius * 0.45f, y - direction * radius * 0.2f),
                        new PointF(x, y + direction * radius * 0.2f),
                        new PointF(x + radius * 0.45f, y - direction * radius * 0.2f)
                    });
            }
            finally { graphics.SmoothingMode = smoothing; }
        }

        private static void RenderParameterControl(Graphics graphics, ParameterControl control, float radius, int alpha)
        {
            var smoothing = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                using var fill = new SolidBrush(Color.FromArgb(alpha, control.Show ? Color.Black : Color.White));
                using var edge = new Pen(Color.FromArgb(alpha, Color.Black), .5f);
                using var glyph = new Pen(Color.FromArgb(alpha, control.Show ? Color.White : Color.Black), radius * .25f);
                var center = control.Center;
                var circle = new RectangleF(center.X - radius, center.Y - radius, 2 * radius, 2 * radius);
                graphics.FillEllipse(fill, circle);
                graphics.DrawEllipse(edge, circle);
                var half = .5f * radius;
                graphics.DrawLine(glyph, center.X - half, center.Y, center.X + half, center.Y);
                if (control.Show) graphics.DrawLine(glyph, center.X, center.Y - half, center.X, center.Y + half);
            }
            finally { graphics.SmoothingMode = smoothing; }
        }

        public override void SetupTooltip(PointF point, GH_TooltipDisplayEventArgs e)
        {
            if (!_collapseButton.IsEmpty && Distance(CenterOf(_collapseButton), point) <= 3)
            {
                e.Title = "Hide advanced parameters";
                e.Text = "Collapse unused metadata parameters, including individually revealed rows.";
                e.Description = "Connected parameters and configured inputs remain visible.";
                return;
            }
            var control = _parameterControls.FirstOrDefault(c => Distance(c.Center, point) <= 3);
            if (control != null && GH_Canvas.ZoomFadeHigh > 5)
            {
                e.Title = (control.Show ? "Show " : "Hide ") + control.Parameter.Name;
                e.Text = control.Show ? "Reveal the next hidden parameter at this gap." : "Hide this unused parameter.";
                e.Description = "Hiding unused parameters resets their tree controls. Connected parameters and inputs with persistent values stay visible.";
                return;
            }
            if (!_button.IsEmpty && Distance(ButtonCenter, point) <= 3)
            {
                e.Title = _component.Expanded ? "Hide advanced parameters" : _component.AdvancedMenuText;
                e.Text = _component.Expanded ? "Collapse unused metadata parameters." : "Show all available parameters.";
                e.Description = "Connected parameters and configured inputs remain visible.";
                return;
            }
            base.SetupTooltip(point, e);
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && !_collapseButton.IsEmpty && Distance(CenterOf(_collapseButton), e.CanvasLocation) <= 3)
            {
                _component.CollapseAdvancedParameters();
                sender?.Invalidate();
                return GH_ObjectResponse.Handled;
            }
            if (e.Button == MouseButtons.Left && (sender == null || GH_Canvas.ZoomFadeHigh > 5))
            {
                var control = _parameterControls.FirstOrDefault(c => Distance(c.Center, e.CanvasLocation) <= 3);
                if (control != null)
                {
                    _component.SetParameterVisible(control.Parameter, control.Show);
                    sender?.Invalidate();
                    return GH_ObjectResponse.Handled;
                }
            }
            if (e.Button == MouseButtons.Left && !_button.IsEmpty && Distance(ButtonCenter, e.CanvasLocation) <= 3)
            {
                _component.ToggleAdvancedInputs();
                sender?.Invalidate();
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDown(sender, e);
        }

        private void AttachHoverCanvas(GH_Canvas canvas)
        {
            if (ReferenceEquals(canvas, _hoverCanvas)) return;
            DetachHoverCanvas();
            if (!ReferenceEquals(canvas.Document, _component.OnPingDocument())) return;
            _hoverCanvas = canvas;
            _cursorControl = canvas.PointToClient(Cursor.Position);
            _hasCursor = canvas.ClientRectangle.Contains(_cursorControl);
            // Canvas events cover the approach region outside the component's hit bounds.
            // Component-level RespondToMouseMove is deliberately not used for hover refresh.
            canvas.MouseMove += CanvasMouseMove;
            canvas.MouseLeave += CanvasMouseLeave;
            canvas.CanvasPrePaintOverlay += CanvasPrePaintOverlay;
            canvas.DocumentChanged += CanvasDocumentChanged;
            canvas.Disposed += CanvasDisposed;
        }

        internal void DetachHoverCanvas()
        {
            if (_hoverCanvas != null)
            {
                _hoverCanvas.MouseMove -= CanvasMouseMove;
                _hoverCanvas.MouseLeave -= CanvasMouseLeave;
                _hoverCanvas.CanvasPrePaintOverlay -= CanvasPrePaintOverlay;
                _hoverCanvas.DocumentChanged -= CanvasDocumentChanged;
                _hoverCanvas.Disposed -= CanvasDisposed;
                _hoverCanvas = null;
            }
            _hasCursor = false;
            _hoverRadius = 1.5f;
            _collapseRadius = 1.5f;
            for (int i = 0; i < _parameterRadii.Length; i++) _parameterRadii[i] = 1.5f;
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (_hoverCanvas == null) return;
            if (!ReferenceEquals(_hoverCanvas.Document, _component.OnPingDocument()))
            {
                DetachHoverCanvas();
                return;
            }
            _cursorControl = e.Location;
            _hasCursor = true;
            UpdateHoverRadius(ButtonRadius(Distance(ButtonCenter, _hoverCanvas.Viewport.UnprojectPoint(e.Location))));
            UpdateParameterRadii(_hoverCanvas.Viewport.UnprojectPoint(e.Location));
        }

        private void CanvasMouseLeave(object sender, EventArgs e)
        {
            _hasCursor = false;
            UpdateHoverRadius(1.5f);
            UpdateParameterRadii(null);
        }

        private void UpdateParameterRadii(PointF? cursor)
        {
            bool changed = false;
            var collapseRadius = cursor.HasValue && !_collapseButton.IsEmpty ? ButtonRadius(Distance(CenterOf(_collapseButton), cursor.Value)) : 1.5f;
            if (_collapseRadius != collapseRadius) { _collapseRadius = collapseRadius; changed = true; }
            for (int i = 0; i < _parameterControls.Count; i++)
            {
                var radius = cursor.HasValue ? ButtonRadius(Distance(_parameterControls[i].Center, cursor.Value)) : 1.5f;
                if (_parameterRadii[i] != radius) { _parameterRadii[i] = radius; changed = true; }
            }
            if (changed) _hoverCanvas?.Invalidate();
        }

        private void UpdateHoverRadius(float value)
        {
            if (_hoverRadius == value) return;
            _hoverRadius = value;
            _hoverCanvas?.Invalidate();
        }

        private void CanvasDocumentChanged(GH_Canvas sender, GH_CanvasDocumentChangedEventArgs e) => DetachHoverCanvas();
        private void CanvasDisposed(object sender, EventArgs e) => DetachHoverCanvas();

        private static float Distance(PointF a, PointF b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
