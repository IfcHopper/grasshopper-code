using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class DeconstructProjectComponent : DeconstructComponentBase
    {
        public DeconstructProjectComponent()
          : base("Deconstruct Project", "DeProject",
              "Deconstructs an IFC project. Children of a project read from a file are loaded on demand.",
              ComponentCategory.Project)
        {
        }

        // Below Context and Georeference (secondary).
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new ProjectParam(), "Project", "Pr", "IfcHopper project.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            RegisterCommonOutputs(pManager);
            pManager.AddParameter(new SiteParam(), "Sites", "Si", "Sites of the project.", GH_ParamAccess.list);
            pManager.AddParameter(new FacilityParam(), "Facilities", "F", "Facilities directly under the project, without a site.", GH_ParamAccess.list);
            pManager.AddParameter(new ContextParam(), "Contexts", "Cx", "Representation contexts of the project.", GH_ParamAccess.list);
            pManager.AddParameter(new UnitsParam(), "Units", "U", "Units of the project.", GH_ParamAccess.item);
            pManager.AddParameter(new GeoreferenceParam(), "Georeference", "Geo", "Map conversion of the project. Empty when not georeferenced.", GH_ParamAccess.item);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.OutputDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterOutput(this);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            ProjectGoo project = null;
            if (!DA.GetData(0, ref project) || !project.IsValid) return;

            SetCommonOutputs(DA, project.Value);
            DA.SetDataList(3, project.Value.Sites.Select(s => new SiteGoo(s)));
            DA.SetDataList(4, project.Value.Facilities.Select(f => new FacilityGoo(f)));
            DA.SetDataList(5, project.Value.Contexts.Select(c => new ContextGoo(c)));
            if (project.Value.Units != null) DA.SetData(6, new UnitsGoo(project.Value.Units));
            if (project.Value.Georeference != null) DA.SetData(7, new GeoreferenceGoo(project.Value.Georeference));
            ComponentPropertySets.SetOutput(DA, 8, project.Value);
            ComponentClassifications.SetOutput(this, DA, project.Value);
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructProject;

        public override Guid ComponentGuid => new Guid("a2f7d5c8-1e93-4b6a-8d4f-07b3e9c1a586");
    }
}
