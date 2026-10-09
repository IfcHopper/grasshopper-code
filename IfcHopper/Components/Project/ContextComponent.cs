using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;
using Rhino.Geometry;

namespace IfcHopper.Components
{
    public class ContextComponent : GH_Component
    {
        public ContextComponent()
          : base("Context", "Context",
              "Creates an IFC representation context (IfcGeometricRepresentationContext) with subcontexts, e.g. Model with Body and Axis.",
              ComponentCategory.Tab, ComponentCategory.Project)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Type", "T", "Context type: Model (3D) or Plan (2D).", GH_ParamAccess.item, RepresentationContext.ModelType);
            pManager.AddTextParameter("Identifiers", "Id", "Subcontext identifiers, e.g. Body, Axis, Box, FootPrint, Annotation.", GH_ParamAccess.list);
            pManager.AddTextParameter("Target Views", "V",
                $"Target view per identifier: {string.Join(", ", RepresentationSubContext.TargetViews)}. The last value is repeated for the remaining identifiers.",
                GH_ParamAccess.list);
            pManager.AddNumberParameter("Precision", "P", "Geometric precision, in metres.", GH_ParamAccess.item, RepresentationContext.DefaultPrecision);
            pManager.AddVectorParameter("True North", "N", "True north direction in the XY plane. Empty means +Y.", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new ContextParam(), "Context", "Cx", "IfcHopper representation context.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string type = null;
            var identifiers = new List<string>();
            var views = new List<string>();
            double precision = RepresentationContext.DefaultPrecision;
            var north = Vector3d.Unset;
            if (!DA.GetData(0, ref type)) return;
            DA.GetDataList(1, identifiers);
            DA.GetDataList(2, views);
            DA.GetData(3, ref precision);
            DA.GetData(4, ref north);

            if (string.IsNullOrWhiteSpace(type))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Type cannot be empty.");
                return;
            }
            if (precision <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Precision must be positive.");
                return;
            }

            var context = new RepresentationContext(type.Trim()) { Precision = precision };
            if (north.IsValid)
            {
                if (north.X == 0 && north.Y == 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "True North needs an X or Y component.");
                    return;
                }
                var xy = new Vector3d(north.X, north.Y, 0);
                xy.Unitize();
                context.TrueNorth = new[] { xy.X, xy.Y };
            }

            for (int i = 0; i < identifiers.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(identifiers[i])) continue;
                var view = views.Count == 0 ? null : views[Math.Min(i, views.Count - 1)];
                var sub = new RepresentationSubContext(identifiers[i].Trim());
                if (!sub.SetTargetView(view))
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"'{view}' is not a predefined target view; written as USERDEFINED.");
                context.SubContexts.Add(sub);
            }

            DA.SetData(0, new ContextGoo(context));
        }

        protected override Bitmap Icon => Properties.Resources.Context;

        public override Guid ComponentGuid => new Guid("9c4e7a1b-3d58-4f26-b8e0-7a1d5c3f9e62");
    }
}
