using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class ProjectComponent : GH_Component
    {
        public ProjectComponent()
          : base("Project", "Project",
              "Creates an IFC project (IfcProject) from sites, or from facilities placed directly under the project.",
              ComponentCategory.Tab, ComponentCategory.Project)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Project name.", GH_ParamAccess.item, Project.DefaultName);
            pManager.AddTextParameter("Description", "D", "Project description.", GH_ParamAccess.item);
            pManager.AddParameter(new SiteParam(), "Sites", "Si", "Sites of the project.", GH_ParamAccess.list);
            pManager.AddParameter(new FacilityParam(), "Facilities", "F", "Facilities directly under the project, without a site.", GH_ParamAccess.list);
            pManager.AddParameter(new ContextParam(), "Contexts", "Cx", "Representation contexts. When empty, a default 3D Model context is written.", GH_ParamAccess.list);
            pManager.AddParameter(new UnitsParam(), "Units", "U", "Units written to the file. Defaults to the Rhino document length unit with m2, m3 and radians.", GH_ParamAccess.item);
            for (int i = 1; i < 6; i++) pManager[i].Optional = true;
            pManager.AddParameter(new GeoreferenceParam(), "Georeference", "Geo", "Map conversion to a projected CRS. Empty: not georeferenced.", GH_ParamAccess.item);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.Description, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.Description);
            pManager[6].Optional = true;
            pManager[7].Optional = true;
            pManager.AddTextParameter(ComponentGlobalId.Name, ComponentGlobalId.NickName, ComponentGlobalId.Description, GH_ParamAccess.item);
            pManager[9].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new ProjectParam(), "Project", "Pr", "IfcHopper project.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = null, description = null;
            var sites = new List<SiteGoo>();
            var facilities = new List<FacilityGoo>();
            var contexts = new List<ContextGoo>();
            UnitsGoo units = null;
            if (!DA.GetData(0, ref name)) return;
            DA.GetData(1, ref description);
            DA.GetDataList(2, sites);
            DA.GetDataList(3, facilities);
            DA.GetDataList(4, contexts);
            DA.GetData(5, ref units);

            if (string.IsNullOrWhiteSpace(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name cannot be empty.");
                return;
            }

            var project = new Project(name.Trim()) { Description = description };
            project.Sites.AddRange(sites.Where(s => s != null && s.IsValid).Select(s => s.Value));
            project.Facilities.AddRange(facilities.Where(f => f != null && f.IsValid).Select(f => f.Value));
            project.Contexts.AddRange(contexts.Where(c => c != null && c.IsValid).Select(c => c.Value));
            project.Units = units != null && units.IsValid ? units.Value : new Units { Length = UnitsComponent.DocumentLength(this) };
            var globalId = ComponentGlobalId.Resolve(this, DA, 9);
            if (globalId == null) return;
            project.GlobalId = globalId;
            GeoreferenceGoo georeference = null;
            if (DA.GetData(6, ref georeference) && georeference.IsValid) project.Georeference = georeference.Value;
            ComponentPropertySets.Apply(this, DA, 7, project);
            ComponentClassifications.Apply(this, DA, project);
            DA.SetData(0, new ProjectGoo(project));
        }

        protected override Bitmap Icon => Properties.Resources.Project;

        public override Guid ComponentGuid => new Guid("e2d5f8a3-91c6-4b7e-8a20-3f6d1c9b5e74");
    }
}
