using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Types;
using Rhino.Geometry;

namespace IfcHopper.Components
{
    public class DeconstructGeoreferenceComponent : GH_Component
    {
        public DeconstructGeoreferenceComponent()
          : base("Deconstruct Georeference", "DeGeoref",
              "Deconstructs an IFC georeference.",
              ComponentCategory.Tab, ComponentCategory.Project)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new GeoreferenceParam(), "Georeference", "Geo", "IfcHopper georeference.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("CRS", "CRS", "Projected CRS name.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Eastings", "E", "Map eastings of the model origin, in metres.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Northings", "N", "Map northings of the model origin, in metres.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Height", "H", "Orthogonal height of the model origin, in metres.", GH_ParamAccess.item);
            pManager.AddVectorParameter("X Axis", "X", "Direction of the model X axis in map coordinates.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Scale", "S", "Map distance per model distance.", GH_ParamAccess.item);
            pManager.AddTextParameter("Geodetic Datum", "GD", "Geodetic datum.", GH_ParamAccess.item);
            pManager.AddTextParameter("Vertical Datum", "VD", "Vertical datum.", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "CRS description.", GH_ParamAccess.item);
            pManager.AddTextParameter("Map Zone", "Z", "Map zone.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GeoreferenceGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var georeference = goo.Value;
            DA.SetData(0, georeference.CrsName);
            DA.SetData(1, georeference.Eastings);
            DA.SetData(2, georeference.Northings);
            DA.SetData(3, georeference.OrthogonalHeight);
            DA.SetData(4, new Vector3d(georeference.XAxisAbscissa, georeference.XAxisOrdinate, 0));
            DA.SetData(5, georeference.Scale);
            DA.SetData(6, georeference.GeodeticDatum);
            DA.SetData(7, georeference.VerticalDatum);
            DA.SetData(8, georeference.CrsDescription);
            DA.SetData(9, georeference.MapZone);
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructGeoreference;

        public override Guid ComponentGuid => new Guid("2b8f5e1d-a94c-4d67-b3f0-8e1a6c9d4b52");
    }
}
