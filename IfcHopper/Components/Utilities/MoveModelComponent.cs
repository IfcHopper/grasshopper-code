using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class MoveModelComponent : GH_Component
    {
        public MoveModelComponent() : base("Move Model", "MoveModel",
            "Translates all objects of a supported IfcHopper model in world X/Y, including parts, openings and fills. " +
            "Geometry and placement origins move together. Rotations, Z and storey elevations stay unchanged. " +
            "Keep Map Location compensates an existing georeference. Copies the object tree; geometry remains lazy. Does not write files.",
            ComponentCategory.Tab, ComponentCategory.Utilities) { }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new ModelParam(), "Model", "M", "IfcHopper model to translate. Source files with unsupported products or non-local placements are rejected rather than partly moved.", GH_ParamAccess.item);
            pManager.AddNumberParameter("X", "X", "Signed world X translation in Rhino document units. Default: 0.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Y", "Y", "Signed world Y translation in Rhino document units. Default: 0.", GH_ParamAccess.item, 0.0);
            pManager.AddBooleanParameter("Keep Map Location", "K", "True (default): adjust an existing georeference to preserve projected map positions. False: leave it unchanged, moving map positions too. No georeference: translate only; no CRS is inferred.", GH_ParamAccess.item, true);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager) =>
            pManager.AddParameter(new ModelParam(), "Model", "M", "Translated model copy. Connect to Find Objects to preview or Write IFC to save explicitly. Upstream model values are unchanged.", GH_ParamAccess.item);

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            ModelGoo goo = null;
            double x = 0, y = 0;
            bool keep = true;
            if (!DA.GetData(0, ref goo) || goo == null || !goo.IsValid
                || !DA.GetData(1, ref x) || !DA.GetData(2, ref y) || !DA.GetData(3, ref keep)) return;
            try
            {
                var result = ModelTranslation.Move(goo.Value, DocumentUnits.ToMetres(x), DocumentUnits.ToMetres(y), keep);
                DA.SetData(0, new ModelGoo(result));
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, keep && result.Project.Georeference != null
                    ? "Model geometry and placement origins translated; georeference compensated to keep map positions unchanged."
                    : result.Project.Georeference == null ? "Model translated. No georeference exists; no projected map location can be preserved and no CRS was inferred."
                    : "Model translated; georeference unchanged. Projected map positions move with the model.");
            }
            catch (ArgumentException ex) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message); }
            catch (NotSupportedException ex) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message); }
        }

        protected override Bitmap Icon => Properties.Resources.MoveModel;
        public override Guid ComponentGuid => new Guid("bb380b92-6d5d-44d8-bb01-60627aa826dd");
    }
}
