using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class DeconstructSiteComponent : DeconstructComponentBase
    {
        public DeconstructSiteComponent()
          : base("Deconstruct Site", "DeSite",
              "Deconstructs an IFC site. Children of a site read from a file are loaded on demand.",
              ComponentCategory.Project)
        {
        }

        // Below Context and Georeference (secondary).
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new SiteParam(), "Site", "Si", "IfcHopper site.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            RegisterCommonOutputs(pManager);
            pManager.AddParameter(new SiteParam(), "Sites", "Si", "Nested sites.", GH_ParamAccess.list);
            pManager.AddParameter(new FacilityParam(), "Facilities", "F", "Facilities on the site.", GH_ParamAccess.list);
            pManager.AddParameter(new SpaceParam(), "Spaces", "Sp", "Spaces directly on the site.", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "Elements contained directly in the site.", GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.OutputDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterOutput(this);
            RegisterPlacementOutput(pManager);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            SiteGoo site = null;
            if (!DA.GetData(0, ref site) || !site.IsValid) return;

            SetCommonOutputs(DA, site.Value);
            DA.SetDataList(3, site.Value.Sites.Select(s => new SiteGoo(s)));
            DA.SetDataList(4, site.Value.Facilities.Select(f => new FacilityGoo(f)));
            DA.SetDataList(5, site.Value.Spaces.Select(s => new SpaceGoo(s)));
            DA.SetDataList(6, site.Value.Elements.Select(e => new ElementGoo(e)));
            ComponentPropertySets.SetOutput(DA, 7, site.Value);
            ComponentClassifications.SetOutput(this, DA, site.Value);
            SetPlacement(DA, 9, site.Value);
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructSite;

        public override Guid ComponentGuid => new Guid("3c9e1f6b-a4d8-4275-9b3e-e6a0c2d7f491");
    }
}
