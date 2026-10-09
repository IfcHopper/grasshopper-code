using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class SiteComponent : GH_Component
    {
        public SiteComponent()
          : base("Site", "Site",
              "Creates an IFC site (IfcSite) from nested sites, facilities and elements.",
              ComponentCategory.Tab, ComponentCategory.Project)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Site name.", GH_ParamAccess.item, Site.DefaultName);
            pManager.AddTextParameter("Description", "D", "Site description.", GH_ParamAccess.item);
            pManager.AddParameter(new SiteParam(), "Sites", "Si", "Nested sites, e.g. a building site within an environment site.", GH_ParamAccess.list);
            pManager.AddParameter(new FacilityParam(), "Facilities", "F", "Facilities on the site (buildings, roads, bridges, ...).", GH_ParamAccess.list);
            pManager.AddParameter(new SpaceParam(), "Spaces", "Sp", "Spaces directly on the site, e.g. outdoor areas.", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "Objects contained directly in the site, e.g. terrain or landscaping.", GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.Description, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.Description);
            ComponentPlacement.RegisterInput(this, ComponentPlacement.Description);
            pManager.AddTextParameter(ComponentGlobalId.Name, ComponentGlobalId.NickName, ComponentGlobalId.Description, GH_ParamAccess.item);
            for (int i = 1; i < 10; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new SiteParam(), "Site", "Si", "IfcHopper site.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = null, description = null;
            var sites = new List<SiteGoo>();
            var facilities = new List<FacilityGoo>();
            var spaces = new List<SpaceGoo>();
            var elements = new List<ElementGoo>();
            if (!DA.GetData(0, ref name)) return;
            DA.GetData(1, ref description);
            DA.GetDataList(2, sites);
            DA.GetDataList(3, facilities);
            DA.GetDataList(4, spaces);
            DA.GetDataList(5, elements);

            if (string.IsNullOrWhiteSpace(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name cannot be empty.");
                return;
            }

            var site = new Site(name.Trim()) { Description = description };
            site.Sites.AddRange(sites.Where(s => s != null && s.IsValid).Select(s => s.Value));
            site.Facilities.AddRange(facilities.Where(f => f != null && f.IsValid).Select(f => f.Value));
            site.Spaces.AddRange(spaces.Where(s => s != null && s.IsValid).Select(s => s.Value));
            site.Elements.AddRange(elements.Where(e => e != null && e.IsValid).Select(e => e.Value));
            var globalId = ComponentGlobalId.Resolve(this, DA, 9);
            if (globalId == null) return;
            site.GlobalId = globalId;
            site.Placement = ComponentPlacement.Read(DA, 8);
            ComponentPropertySets.Apply(this, DA, 6, site);
            ComponentClassifications.Apply(this, DA, site);
            DA.SetData(0, new SiteGoo(site));
        }

        protected override Bitmap Icon => Properties.Resources.Site;

        public override Guid ComponentGuid => new Guid("7c5d2a9e-0f64-4b18-a3c7-d8e1f6b4029a");
    }
}
