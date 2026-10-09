using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;
using Rhino.Geometry;

namespace IfcHopper.Components
{
    public class GeoreferenceComponent : GH_Component
    {
        public GeoreferenceComponent()
          : base("Georeference", "Georef",
              "Georeferences a project: maps the model origin and X axis to a projected CRS (IfcMapConversion, IfcProjectedCRS).",
              ComponentCategory.Tab, ComponentCategory.Project)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("CRS", "CRS", "Projected CRS name, e.g. EPSG:5110.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Eastings", "E", "Map eastings of the model origin, in metres.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Northings", "N", "Map northings of the model origin, in metres.", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Height", "H", "Orthogonal height of the model origin, in metres.", GH_ParamAccess.item, 0.0);
            pManager.AddVectorParameter("X Axis", "X", "Direction of the model X axis in map coordinates (XY). Default: map east.", GH_ParamAccess.item, Vector3d.XAxis);
            pManager.AddNumberParameter("Scale", "S", "Map distance per model distance (1 = no scaling).", GH_ParamAccess.item, 1.0);
            pManager.AddTextParameter("Geodetic Datum", "GD", "Geodetic datum, e.g. EUREF89.", GH_ParamAccess.item);
            pManager.AddTextParameter("Vertical Datum", "VD", "Vertical datum, e.g. NN2000.", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "CRS description.", GH_ParamAccess.item);
            for (int i = 6; i < 9; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GeoreferenceParam(), "Georeference", "Geo", "IfcHopper georeference.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string crs = null, geodeticDatum = null, verticalDatum = null, description = null;
            double eastings = 0, northings = 0, height = 0, scale = 1;
            var xAxis = Vector3d.XAxis;
            if (!DA.GetData(0, ref crs)) return;
            DA.GetData(1, ref eastings);
            DA.GetData(2, ref northings);
            DA.GetData(3, ref height);
            DA.GetData(4, ref xAxis);
            DA.GetData(5, ref scale);
            DA.GetData(6, ref geodeticDatum);
            DA.GetData(7, ref verticalDatum);
            DA.GetData(8, ref description);

            if (string.IsNullOrWhiteSpace(crs))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "CRS cannot be empty.");
                return;
            }
            var direction = new Vector3d(xAxis.X, xAxis.Y, 0);
            if (!direction.Unitize())
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "X Axis needs an X or Y component.");
                return;
            }
            if (scale <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Scale must be positive.");
                return;
            }

            DA.SetData(0, new GeoreferenceGoo(new Georeference(crs.Trim())
            {
                CrsDescription = description,
                GeodeticDatum = geodeticDatum,
                VerticalDatum = verticalDatum,
                Eastings = eastings,
                Northings = northings,
                OrthogonalHeight = height,
                XAxisAbscissa = direction.X,
                XAxisOrdinate = direction.Y,
                Scale = scale,
            }));
        }

        protected override Bitmap Icon => Properties.Resources.Georeference;

        public override Guid ComponentGuid => new Guid("7a2e9c4f-d1b6-4385-a07e-f3c8b5d2e916");
    }
}
