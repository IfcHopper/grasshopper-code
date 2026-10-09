using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;
using Rhino.Geometry;

namespace IfcHopper.Components
{
    public class DeconstructContextComponent : GH_Component
    {
        public DeconstructContextComponent()
          : base("Deconstruct Context", "DeContext",
              "Deconstructs an IFC representation context and its subcontexts.",
              ComponentCategory.Tab, ComponentCategory.Project)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new ContextParam(), "Context", "Cx", "IfcHopper representation context.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Type", "T", "Context type, e.g. Model or Plan.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Dimension", "Dim", "Coordinate space dimension.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Precision", "P", "Geometric precision, in metres.", GH_ParamAccess.item);
            pManager.AddVectorParameter("True North", "N", "True north direction. Empty when not set (+Y).", GH_ParamAccess.item);
            pManager.AddTextParameter("Identifiers", "Id", "Subcontext identifiers.", GH_ParamAccess.list);
            pManager.AddTextParameter("Target Views", "V", "Target view of each subcontext.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            ContextGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var context = goo.Value;
            DA.SetData(0, context.ContextType);
            DA.SetData(1, context.Dimension);
            DA.SetData(2, context.Precision);
            if (context.TrueNorth != null && context.TrueNorth.Length >= 2)
                DA.SetData(3, new Vector3d(context.TrueNorth[0], context.TrueNorth[1], 0));
            DA.SetDataList(4, context.SubContexts.Select(s => s.Identifier));
            DA.SetDataList(5, context.SubContexts.Select(s =>
                s.TargetView == RepresentationSubContext.UserDefined ? s.UserDefinedTargetView : s.TargetView));
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructContext;

        public override Guid ComponentGuid => new Guid("e1a8d4f7-5b29-4c63-9f0e-b3c6a2d8e715");
    }
}
