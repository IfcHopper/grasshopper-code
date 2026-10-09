using System;
using System.Drawing;
using IfcHopper.Core.IO;
using IfcHopper.Types;
using Grasshopper.Kernel;

namespace IfcHopper.Components
{
    public class WriteIfcComponent : GH_Component
    {
        public WriteIfcComponent()
          : base("Write IFC", "WriteIFC",
              "Writes an IfcHopper model to an IFC file. The path is output even when Write is false.",
              ComponentCategory.Tab, ComponentCategory.File)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new ModelParam(), "Model", "M", "IfcHopper model to write.", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "File name. '.ifc' is appended when missing.", GH_ParamAccess.item, IfcFilePath.DefaultFileName);
            pManager.AddTextParameter("Directory", "D", "Output directory. Defaults to the IfcHopper folder in the user folder." + DocumentPaths.Note, GH_ParamAccess.item);
            pManager.AddBooleanParameter("Write", "W", "Set to true to write the file.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Schema", "S",
                $"IFC schema to write: {string.Join(", ", IfcSchemas.Names)}. Empty: IFC4X3_ADD2 for new models, the schema of the source file for models read from a file. " +
                "What the schema does not support is downgraded or left out, with a warning.", GH_ParamAccess.item);
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Path", "P", "Full path of the IFC file.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            ModelGoo model = null;
            string name = null, directory = null;
            bool write = false;
            DA.GetData(0, ref model);
            DA.GetData(1, ref name);
            DA.GetData(2, ref directory);
            DA.GetData(3, ref write);
            string schemaText = null;
            DA.GetData(4, ref schemaText);
            var schema = IfcSchemas.Parse(schemaText);
            if (!string.IsNullOrWhiteSpace(schemaText) && schema == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Unknown schema '{schemaText}'. Use one of: {string.Join(", ", IfcSchemas.Names)}.");
                return;
            }

            string path;
            try
            {
                if (!string.IsNullOrWhiteSpace(directory) && (directory = DocumentPaths.Resolve(this, directory)) == null) return;
                path = IfcFilePath.Resolve(name, directory);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                return;
            }
            DA.SetData(0, path);

            if (!write) return;
            if (model == null || !model.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No valid model to write.");
                return;
            }
            try
            {
                var warnings = IfcBackend.Writer.Write(model.Value, path, schema);
                foreach (var warning in warnings) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, warning);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Written {DateTime.Now:T}");
                if (model.Value.RebuildsReadObjects)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                        "Objects read from a file were written into a new project: data IfcHopper does not model (e.g. alignments, other representations) is not copied. Edit the read project to keep it.");
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        protected override Bitmap Icon => Properties.Resources.WriteIfc;

        public override Guid ComponentGuid => new Guid("b4e9a1d2-7c3f-4e86-a05b-9d2f6c1e7a34");
    }
}
