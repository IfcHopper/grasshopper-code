using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace SampleBuilder
{
    /// <summary>A sample definition, saved as samples/"NN Title.gh", with helpers for the parts samples share.</summary>
    internal abstract class Sample
    {
        public abstract int Number { get; }
        public abstract string Title { get; }

        /// <summary>One or two sentences shown under the title.</summary>
        public abstract string Summary { get; }

        public string FileName => $"{Number:00} {Title}.gh";

        /// <summary>Name of the IFC file the sample writes (without extension), in samples/ifc.</summary>
        public string IfcName => $"{Number:00} {Title}";

        /// <summary>Text of the warnings the sample shows on purpose (e.g. schema downgrades); others fail the build.</summary>
        public virtual IEnumerable<string> ExpectedMessages => new string[0];

        /// <summary>
        /// Prepares the (new, empty, metre) Rhino document the sample is solved with, e.g. adds block definitions. Returns true when the document
        /// must be saved next to the definition as "NN Title.3dm", for users to open before the definition.
        /// </summary>
        public virtual bool PrepareRhino(Rhino.RhinoDoc doc) => false;
        /// <summary>Builds the definition below the title (from y = <see cref="Top"/>).</summary>
        protected abstract void BuildBody(Canvas canvas);

        public const float Top = 110;

        public void Build(Canvas canvas)
        {
            canvas.Note($"{Number:00} {Title}", 0, 0, 20, true);
            canvas.Note(Summary, 0, 36);
            BuildBody(canvas);
        }

        /// <summary>A Center Box (half sizes, metres).</summary>
        protected static IGH_DocumentObject Box(Canvas c, float x, float y, Point3d centre, Vector3d half)
        {
            var box = c.Native("Center Box", x, y);
            Canvas.Set(box, "Base", new Plane(centre, Vector3d.ZAxis));
            Canvas.Set(box, "X", half.X);
            Canvas.Set(box, "Y", half.Y);
            Canvas.Set(box, "Z", half.Z);
            return box;
        }

        /// <summary>An Object with class, name and optional predefined type; its geometry wired from <paramref name="geometry"/> when given.</summary>
        protected static IGH_DocumentObject Object(Canvas c, float x, float y, IGH_DocumentObject geometry, string ifcClass, string name, string type = null)
        {
            var obj = c.Ifc("Object", x, y);
            Canvas.Set(obj, "Class", ifcClass);
            Canvas.Set(obj, "Name", name);
            if (type != null) Canvas.Set(obj, "Type", type);
            if (geometry != null) c.Wire(geometry, obj, "Geometry");
            return obj;
        }

        /// <summary>Storey (with the elements) in a building on a site in a project, left to right from (x, y); returns the project.</summary>
        protected static IGH_DocumentObject House(Canvas c, float x, float y, string projectName, params IGH_DocumentObject[] elements)
        {
            var storey = c.Ifc("Storey", x, y);
            Canvas.Set(storey, "Name", "Ground floor");
            foreach (var element in elements) c.Wire(element, storey, "Elements");
            var building = c.Ifc("Building", Canvas.RightOf(storey), y);
            c.Wire(storey, building, "Storeys");
            var site = c.Ifc("Site", Canvas.RightOf(building), y);
            c.Wire(building, site, "Facilities");
            var project = c.Ifc("Project", Canvas.RightOf(site), y);
            Canvas.Set(project, "Name", projectName);
            c.Wire(site, project, "Sites");
            c.Group("Spatial structure: storey > building > site > project", Canvas.Colours.Ifc, storey, building, site, project);
            return project;
        }

        /// <summary>
        /// Model and Write IFC to ifc/<paramref name="fileName"/> next to the definition, with a Write toggle (on while building, saved off)
        /// and a panel with the path; returns Write IFC.
        /// </summary>
        protected static IGH_DocumentObject WriteFile(Canvas c, float x, float y, IGH_DocumentObject project, string fileName, string schema = null, bool group = true)
        {
            var model = c.Ifc("Model", x, y);
            c.Wire(project, model, "Project");
            var write = c.Ifc("Write IFC", Canvas.RightOf(model), y);
            c.Wire(model, write, "Model");
            Canvas.Set(write, "Name", fileName);
            Canvas.Set(write, "Directory", "ifc");
            if (schema != null) Canvas.Set(write, "Schema", schema);
            var toggle = c.Toggle("Write", true, x, Canvas.Below(model));
            c.Wire(toggle, write, "Write");
            var path = c.Panel(Canvas.RightOf(write), y, 300, 40);
            c.Wire(write, path, null);
            if (group) c.Group("Write: set Write to true; the file goes to the ifc folder next to this definition", Canvas.Colours.Output, model, write, toggle, path);
            c.BeforeSave.Add(() => toggle.Value = false);
            return write;
        }

        /// <summary>A panel showing an output, right of the component.</summary>
        protected static IGH_DocumentObject Show(Canvas c, IGH_DocumentObject from, string output, float x, float y, float width = 220, float height = 40)
        {
            var panel = c.Panel(x, y, width, height);
            c.Wire(from, panel, null, output);
            return panel;
        }

        protected static IGH_DocumentObject[] All(params IEnumerable<IGH_DocumentObject>[] groups) => groups.SelectMany(g => g).ToArray();
    }
}
