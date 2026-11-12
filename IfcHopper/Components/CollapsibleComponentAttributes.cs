using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace IfcHopper.Components
{
    /// <summary>Compacts unused inputs and draws native-position expand/collapse controls without altering the parameter server.</summary>
    internal sealed class CollapsibleComponentAttributes : GH_ComponentAttributes
    {
        private readonly CollapsibleComponent _component;
        private RectangleF _button;
        private bool _compact;
        private GH_Canvas _hoverCanvas;
        private Point _cursorControl;
        private bool _hasCursor;
        private float _hoverRadius = 1.5f;
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
            if (!_component.HasAdvancedInputs) return;
            var inputs = Owner.Params.Input.Where(_component.IsInputVisible).ToList();
            _compact = inputs.Count < Owner.Params.Input.Count;
            if (_compact)
            {
                // Derive row spacing from the native layout instead of imposing a model-unit tolerance
                // or a second font scale. The icon sets the same minimum body height as a normal component.
                var rowHeight = Owner.Params.Input[0].Attributes.Bounds.Height;
                if (rowHeight <= 0) rowHeight = GH_FontServer.Standard.Height + 4;
                var rows = Math.Max(inputs.Count, Owner.Params.Output.Count);
                var nativeRows = Math.Max(Owner.Params.Input.Count, Owner.Params.Output.Count);
                m_innerBounds.Height = Math.Max(32, m_innerBounds.Height - (nativeRows - rows) * rowHeight);
                LayoutRows(inputs, rowHeight, true);
                LayoutRows(Owner.Params.Output.ToList(), rowHeight, false);
                foreach (var hidden in Owner.Params.Input.Where(p => !_component.IsInputVisible(p)))
                {
                    hidden.Attributes.Bounds = RectangleF.Empty;
                    hidden.Attributes.Pivot = PointF.Empty;
                }
                Bounds = m_innerBounds;
                foreach (var parameter in inputs.Concat(Owner.Params.Output))
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
        }

        private void LayoutRows(System.Collections.Generic.List<IGH_Param> parameters, float rowHeight, bool input)
        {
            var top = m_innerBounds.Top + (m_innerBounds.Height - parameters.Count * rowHeight) * 0.5f;
            for (int i = 0; i < parameters.Count; i++)
            {
                var attributes = parameters[i].Attributes;
                var box = attributes.Bounds;
                box.Y = top + i * rowHeight;
                box.Height = rowHeight;
                attributes.Bounds = box;
                attributes.Pivot = new PointF(input ? box.Left : box.Right, box.Top + rowHeight * 0.5f);
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
                foreach (var parameter in Owner.Params.Input.Where(_component.IsInputVisible).Concat(Owner.Params.Output))
                {
                    if (parameter.Kind == GH_ParamKind.input) GH_CapsuleRenderEngine.RenderInputGrip(graphics, canvas.Viewport.Zoom, parameter.Attributes.InputGrip, false);
                    else GH_CapsuleRenderEngine.RenderOutputGrip(graphics, canvas.Viewport.Zoom, parameter.Attributes.OutputGrip, false);
                }
            }

            var center = ButtonCenter;
            // Re-evaluate on paint as well: zooming or moving the component changes its distance
            // from the last canvas mouse position even without another mouse event.
            var radius = interactive && _hasCursor ? ButtonRadius(Distance(center, canvas.Viewport.UnprojectPoint(_cursorControl))) : 1.5f;
            if (interactive) _hoverRadius = radius;
            var x = center.X;
            var y = center.Y;
            var direction = _component.Expanded ? -1 : 1;
            var smoothing = graphics.SmoothingMode;
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var circle = new RectangleF(x - radius, y - radius, radius * 2, radius * 2);
                graphics.FillEllipse(_component.Expanded ? Brushes.White : Brushes.Black, circle);
                using (var edge = new Pen(Color.Black, 0.5f)) graphics.DrawEllipse(edge, circle);
                using (var pen = new Pen(_component.Expanded ? Color.Black : Color.White, radius * 0.25f)
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

        public override void SetupTooltip(PointF point, GH_TooltipDisplayEventArgs e)
        {
            if (!_button.IsEmpty && Distance(ButtonCenter, point) <= 3)
            {
                e.Title = _component.Expanded ? "Hide advanced inputs" : "Show advanced inputs";
                e.Text = _component.Expanded ? "Collapse unused metadata inputs." : "Show all available inputs.";
                e.Description = "Connected and configured inputs remain visible.";
                return;
            }
            base.SetupTooltip(point, e);
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
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
            canvas.DocumentChanged += CanvasDocumentChanged;
            canvas.Disposed += CanvasDisposed;
        }

        internal void DetachHoverCanvas()
        {
            if (_hoverCanvas != null)
            {
                _hoverCanvas.MouseMove -= CanvasMouseMove;
                _hoverCanvas.MouseLeave -= CanvasMouseLeave;
                _hoverCanvas.DocumentChanged -= CanvasDocumentChanged;
                _hoverCanvas.Disposed -= CanvasDisposed;
                _hoverCanvas = null;
            }
            _hasCursor = false;
            _hoverRadius = 1.5f;
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
        }

        private void CanvasMouseLeave(object sender, EventArgs e)
        {
            _hasCursor = false;
            UpdateHoverRadius(1.5f);
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
