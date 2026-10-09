using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Grasshopper.Kernel;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using IfcHopper.Types;
using Rhino;

namespace IfcHopper.Components
{
    public class ReadIfcComponent : GH_Component
    {
        /// <summary>Rhino units matching the last read file, when they differ from the document units.</summary>
        private UnitSystem? _fileUnits;

        public ReadIfcComponent()
          : base("Read IFC", "ReadIFC",
              "Reads an IFC file into an IfcHopper model. Only the project is converted; deconstruct components load the rest on demand. Lengths are converted to Rhino units.",
              ComponentCategory.Tab, ComponentCategory.File)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Path", "P", "Path of the IFC file." + DocumentPaths.Note, GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new ModelParam(), "Model", "M", "IfcHopper model read from the file.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            _fileUnits = null;
            string path = null;
            if (!DA.GetData(0, ref path)) return;
            path = DocumentPaths.Resolve(this, path);
            if (path == null) return;

            try
            {
                var model = IfcBackend.Reader.Read(path);
                DA.SetData(0, new ModelGoo(model));
                CheckUnits(model.Project);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        private void CheckUnits(Project project)
        {
            var fileLength = project.Units?.Length;
            if (fileLength != null && fileLength != DocumentUnits.LengthUnit)
            {
                _fileUnits = DocumentUnits.ToRhino(fileLength);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                    $"File length unit: {fileLength.Name}; Rhino: {DocumentUnits.System}. Lengths are converted." +
                    (_fileUnits.HasValue ? " Right-click to match Rhino units to the file." : ""));
            }

            // Only warn when Rhino's tolerance is coarse in absolute terms too: most files use a very fine precision (1e-5 m).
            var precision = project.Contexts.FirstOrDefault(c => c.Dimension == 3)?.Precision;
            var tolerance = DocumentUnits.ToleranceMetres;
            if (precision.HasValue && tolerance > 1e-3 && tolerance > 100 * precision.Value)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"Rhino absolute tolerance ({tolerance:G3} m) is much coarser than the file precision ({precision.Value:G3} m); geometry may lose detail.");
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            var label = _fileUnits.HasValue ? $"Match Rhino units to file ({_fileUnits.Value})" : "Match Rhino units to file";
            Menu_AppendItem(menu, label, OnMatchUnits, _fileUnits.HasValue);
        }

        private void OnMatchUnits(object sender, EventArgs e)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null || !_fileUnits.HasValue) return;

            // Scale existing geometry so it keeps its real size in the new units.
            doc.AdjustModelUnitSystem(_fileUnits.Value, true);
            // Every component converting lengths depends on the document units.
            OnPingDocument()?.NewSolution(true);
        }

        protected override Bitmap Icon => Properties.Resources.ReadIfc;

        public override Guid ComponentGuid => new Guid("d93a6e1f-2b7c-4a58-8e04-f1c5b9a3d762");
    }
}
