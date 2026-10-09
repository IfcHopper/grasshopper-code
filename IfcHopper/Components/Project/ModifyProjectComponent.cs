using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class ModifyProjectComponent : ModifyComponentBase
    {
        public ModifyProjectComponent()
          : base("Modify Project", "ModProject",
              "Edits an IFC project. Unconnected inputs keep their value; the GlobalId is kept.",
              ComponentCategory.Project)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new ProjectParam(), "Project", "Pr", "IfcHopper project to edit.", GH_ParamAccess.item);
            RegisterCommonInputs(pManager);
            pManager.AddParameter(new SiteParam(), "Sites", "Si", "Sites." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new FacilityParam(), "Facilities", "F", "Facilities directly under the project." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new ContextParam(), "Contexts", "Cx", "Representation contexts. Connected: replaces the list.", GH_ParamAccess.list);
            pManager.AddParameter(new UnitsParam(), "Units", "U", "Units. Changing the units of a file read from disk is not supported.", GH_ParamAccess.item);
            pManager.AddParameter(new GeoreferenceParam(), "Georeference", "Geo", "Map conversion.", GH_ParamAccess.item);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.ModifyDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.ModifyDescription);
            for (int i = 1; i < 9; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new ProjectParam(), "Project", "Pr", "Edited IfcHopper project.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            ProjectGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var project = (Project)goo.Value.Copy();
            if (!ApplyCommon(DA, project)) return;
            ReplaceList(DA, 3, project.Sites);
            ReplaceList(DA, 4, project.Facilities);
            ReplaceList(DA, 5, project.Contexts);
            UnitsGoo units = null;
            if (DA.GetData(6, ref units) && units.IsValid) project.Units = units.Value;
            GeoreferenceGoo georeference = null;
            if (DA.GetData(7, ref georeference) && georeference.IsValid) project.Georeference = georeference.Value;
            ComponentPropertySets.Apply(this, DA, 8, project);
            ComponentClassifications.Apply(this, DA, project);

            DA.SetData(0, new ProjectGoo(project));
        }

        protected override Bitmap Icon => Properties.Resources.ModifyProject;

        public override Guid ComponentGuid => new Guid("0c7e3a95-f218-4b6d-a4e9-b5d1c8f2a637");
    }
}
