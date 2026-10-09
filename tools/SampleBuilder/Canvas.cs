using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace SampleBuilder
{
    /// <summary>
    /// Builds a Grasshopper document in code: components found by name (IfcHopper or Grasshopper's own), wires and internalised input values
    /// by parameter name, notes, groups, sliders and toggles. Positions are canvas coordinates of the component's top-left corner.
    /// </summary>
    internal sealed class Canvas
    {
        public GH_Document Document { get; } = new GH_Document();

        /// <summary>Actions run after the solution and before saving, e.g. switching off a Write toggle used to produce files.</summary>
        public List<Action> BeforeSave { get; } = new List<Action>();

        private static readonly string[] NativeCategories = { "Params", "Maths", "Sets", "Vector", "Curve", "Surface", "Mesh", "Intersect", "Transform", "Display", "Rhino" };

        /// <summary>An IfcHopper component or parameter by its name, e.g. "Object" or "Write IFC".</summary>
        public IGH_DocumentObject Ifc(string name, float x, float y, bool parameter = false) =>
            Add(Find(p => p.Desc.Category == "IfcHopper" && p.Desc.Name == name && (p.Desc.SubCategory == "7 - Params") == parameter, $"IfcHopper '{name}'"), x, y);

        /// <summary>A Grasshopper component by its name, e.g. "Center Box".</summary>
        public IGH_DocumentObject Native(string name, float x, float y) =>
            Add(Find(p => p.Desc.Name == name && NativeCategories.Contains(p.Desc.Category), $"Grasshopper '{name}'"), x, y);

        private static IGH_ObjectProxy Find(Func<IGH_ObjectProxy, bool> match, string what)
        {
            var found = Instances.ComponentServer.ObjectProxies.Where(p => !p.Obsolete && match(p)).ToList();
            if (found.Count == 0) throw new ArgumentException($"{what} not found.");
            return found[0];
        }

        public T Add<T>(T obj, float x, float y) where T : IGH_DocumentObject
        {
            obj.NewInstanceGuid();
            obj.CreateAttributes();
            obj.Attributes.Pivot = new PointF(x, y);
            Document.AddObject(obj, false);
            obj.Attributes.ExpireLayout();
            obj.Attributes.PerformLayout();
            // Pivot is the centre for most objects; shift so (x, y) is the top-left corner.
            var bounds = obj.Attributes.Bounds;
            obj.Attributes.Pivot = new PointF(obj.Attributes.Pivot.X + x - bounds.X, obj.Attributes.Pivot.Y + y - bounds.Y);
            obj.Attributes.ExpireLayout();
            obj.Attributes.PerformLayout();
            return obj;
        }

        /// <summary>
        /// A component or parameter from the component server, shown with full names (like placing it with Display > Draw Full Names on):
        /// its nickname and those of its inputs and outputs are their names.
        /// </summary>
        private IGH_DocumentObject Add(IGH_ObjectProxy proxy, float x, float y)
        {
            var obj = proxy.CreateInstance();
            obj.NickName = obj.Name;
            if (obj is IGH_Component component)
                foreach (var param in component.Params.Input.Concat(component.Params.Output)) param.NickName = param.Name;
            return Add(obj, x, y);
        }

        public static IGH_Param Input(IGH_DocumentObject obj, string name)
        {
            if (obj is IGH_Param param) return param;
            var inputs = ((IGH_Component)obj).Params.Input;
            return inputs.FirstOrDefault(p => p.Name == name) ?? inputs.FirstOrDefault(p => p.NickName == name)
                ?? throw new ArgumentException($"{obj.Name} has no input '{name}' ({string.Join(", ", inputs.Select(p => p.Name))}).");
        }

        public static IGH_Param Output(IGH_DocumentObject obj, string name = null)
        {
            if (obj is IGH_Param param) return param;
            var outputs = ((IGH_Component)obj).Params.Output;
            if (name == null) return outputs[0];
            return outputs.FirstOrDefault(p => p.Name == name) ?? outputs.FirstOrDefault(p => p.NickName == name)
                ?? throw new ArgumentException($"{obj.Name} has no output '{name}' ({string.Join(", ", outputs.Select(p => p.Name))}).");
        }

        /// <summary>Wires an output (the first when <paramref name="output"/> is null, or a parameter itself) to an input.</summary>
        public void Wire(IGH_DocumentObject from, IGH_DocumentObject to, string input, string output = null) => Input(to, input).AddSource(Output(from, output));

        /// <summary>Internalises values in an input, e.g. text, numbers, booleans, planes or geometry goo.</summary>
        public static void Set(IGH_DocumentObject obj, string input, params object[] values)
        {
            var param = Input(obj, input);
            var method = param.GetType().GetMethod("SetPersistentData", new[] { typeof(object[]) })
                ?? throw new ArgumentException($"Input '{input}' of {obj.Name} cannot hold values.");
            // Inputs with a default value already hold it.
            (param.GetType().GetProperty("PersistentData")?.GetValue(param) as IGH_Structure)?.Clear();
            method.Invoke(param, new object[] { values.Select(Goo).ToArray() });
        }

        private static object Goo(object value)
        {
            switch (value)
            {
                case Brep brep: return new GH_Brep(brep);
                case Mesh mesh: return new GH_Mesh(mesh);
                case Plane plane: return new GH_Plane(plane);
                case Point3d point: return new GH_Point(point);
                case Vector3d vector: return new GH_Vector(vector);
                case Curve curve: return new GH_Curve(curve);
                case Color colour: return new GH_Colour(colour);
                default: return value;
            }
        }

        /// <summary>The y below an object, for stacking objects by their real height.</summary>
        public static float Below(IGH_DocumentObject obj, float gap = 30) => obj.Attributes.Bounds.Bottom + gap;

        /// <summary>The x right of an object.</summary>
        public static float RightOf(IGH_DocumentObject obj, float gap = 50) => obj.Attributes.Bounds.Right + gap;

        /// <summary>A text note on the canvas (scribble).</summary>
        public GH_Scribble Note(string text, float x, float y, float size = 12, bool bold = false)
        {
            var scribble = new GH_Scribble { Text = text, Font = new Font(GH_FontServer.Script.FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular) };
            return Add(scribble, x, y);
        }

        /// <summary>A panel showing what is wired into it, or a fixed text.</summary>
        public GH_Panel Panel(float x, float y, float width = 200, float height = 60, string text = "")
        {
            var panel = new GH_Panel { UserText = text };
            panel.NewInstanceGuid();
            panel.CreateAttributes();
            panel.Attributes.Pivot = new PointF(x, y);
            panel.Attributes.Bounds = new RectangleF(x, y, width, height);
            Document.AddObject(panel, false);
            panel.Attributes.ExpireLayout();
            panel.Attributes.PerformLayout();
            // Panels lay out from their pivot; move it so the panel's top-left corner is at (x, y).
            var bounds = panel.Attributes.Bounds;
            panel.Attributes.Pivot = new PointF(panel.Attributes.Pivot.X + x - bounds.X, panel.Attributes.Pivot.Y + y - bounds.Y);
            panel.Attributes.Bounds = new RectangleF(x, y, width, height);
            panel.Attributes.ExpireLayout();
            panel.Attributes.PerformLayout();
            return panel;
        }
        public GH_NumberSlider Slider(string name, double min, double max, double value, float x, float y, int decimals = 2)
        {
            var slider = new GH_NumberSlider { NickName = name };
            slider.Slider.DecimalPlaces = decimals;
            slider.Slider.Minimum = (decimal)min;
            slider.Slider.Maximum = (decimal)max;
            slider.SetSliderValue((decimal)value);
            return Add(slider, x, y);
        }

        public GH_BooleanToggle Toggle(string name, bool value, float x, float y) => Add(new GH_BooleanToggle { NickName = name, Value = value }, x, y);

        public GH_ValueList ValueList(string name, float x, float y, int selected, params string[] values)
        {
            var list = new GH_ValueList { NickName = name };
            list.ListItems.Clear();
            foreach (var value in values) list.ListItems.Add(new GH_ValueListItem(value, $"\"{value}\""));
            list.SelectItem(selected);
            return Add(list, x, y);
        }

        /// <summary>A coloured group around objects, with a title.</summary>
        public GH_Group Group(string title, Color colour, params IGH_DocumentObject[] objects)
        {
            var group = new GH_Group { NickName = title, Colour = colour, Border = GH_GroupBorder.Blob };
            group.NewInstanceGuid();
            foreach (var obj in objects) group.AddObject(obj.InstanceGuid);
            group.CreateAttributes();
            Document.AddObject(group, false);
            return group;
        }

        /// <summary>Group colours: geometry inputs, IFC objects, file output, reading and editing.</summary>
        public static class Colours
        {
            public static readonly Color Geometry = Color.FromArgb(150, 230, 230, 230);
            public static readonly Color Ifc = Color.FromArgb(150, 170, 200, 240);
            public static readonly Color Output = Color.FromArgb(150, 170, 230, 170);
            public static readonly Color Read = Color.FromArgb(150, 240, 210, 160);
        }
    }
}
