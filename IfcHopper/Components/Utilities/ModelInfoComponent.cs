using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class ModelInfoComponent : GH_Component
    {
        public ModelInfoComponent()
          : base("Model Info", "Info",
              "Overview of a model or of the objects below an object: schema and units, the number of objects per IFC class and the spatial tree. " +
              "Objects read from a file are loaded to count them (not their geometry).",
              ComponentCategory.Tab, ComponentCategory.Utilities)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", ComponentObjects.RootDescription, GH_ParamAccess.item);
            pManager.AddBooleanParameter("List Elements", "LE", "True: the tree lists every element (with parts and openings); false: their number per class.", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Schema", "S", "Schema of the file the model was read from; empty for models built in Grasshopper.", GH_ParamAccess.item);
            pManager.AddParameter(new UnitsParam(), "Units", "U", "Units of the project; empty when not set.", GH_ParamAccess.item);
            pManager.AddTextParameter("Classes", "C", "IFC classes of the objects, alphabetically.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Counts", "N", "Number of objects of each class.", GH_ParamAccess.list);
            pManager.AddTextParameter("Tree", "T", "The spatial tree, one object per line, indented by level.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            IGH_Goo goo = null;
            if (!DA.GetData(0, ref goo)) return;
            var root = ComponentObjects.Root(this, goo, out var model);
            if (root == null) return;
            bool listElements = false;
            DA.GetData(1, ref listElements);

            if (model?.SourcePath != null)
            {
                try
                {
                    DA.SetData(0, string.Join(", ", IfcBackend.Reader.ReadHeader(model.SourcePath).Schemas));
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"The schema could not be read: {ex.Message}");
                }
            }
            if (root is Project project && project.Units != null) DA.SetData(1, new UnitsGoo(project.Units));

            var counts = ModelSearch.CountByClass(root);
            DA.SetDataList(2, counts.Select(c => c.IfcClass));
            DA.SetDataList(3, counts.Select(c => c.Count));
            DA.SetData(4, ModelSearch.Tree(root, listElements));
        }

        protected override Bitmap Icon => Properties.Resources.ModelInfo;

        public override Guid ComponentGuid => new Guid("76c3cfa6-79cc-4519-9742-fe9235234f7b");
    }
}
