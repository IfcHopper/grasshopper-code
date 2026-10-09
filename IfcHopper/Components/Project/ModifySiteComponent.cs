using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class ModifySiteComponent : ModifyComponentBase
    {
        public ModifySiteComponent()
          : base("Modify Site", "ModSite",
              "Edits an IFC site. Unconnected inputs keep their value; the GlobalId is kept.",
              ComponentCategory.Project)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new SiteParam(), "Site", "Si", "IfcHopper site to edit.", GH_ParamAccess.item);
            RegisterCommonInputs(pManager);
            pManager.AddParameter(new SiteParam(), "Sites", "Si", "Nested sites." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new FacilityParam(), "Facilities", "F", "Facilities." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new SpaceParam(), "Spaces", "Sp", "Spaces directly on the site." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "Objects contained directly in the site." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.ModifyDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.ModifyDescription);
            ComponentPlacement.RegisterInput(this, ComponentPlacement.ModifyDescription);
            for (int i = 1; i < 10; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new SiteParam(), "Site", "Si", "Edited IfcHopper site.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            SiteGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var site = (Site)goo.Value.Copy();
            if (!ApplyCommon(DA, site)) return;
            ReplaceList(DA, 3, site.Sites);
            ReplaceList(DA, 4, site.Facilities);
            ReplaceList(DA, 5, site.Spaces);
            ReplaceList(DA, 6, site.Elements);
            ComponentPropertySets.Apply(this, DA, 7, site);
            ComponentClassifications.Apply(this, DA, site);
            ApplyPlacement(DA, 9, site);

            DA.SetData(0, new SiteGoo(site));
        }

        protected override Bitmap Icon => Properties.Resources.ModifySite;

        public override Guid ComponentGuid => new Guid("8b4d1f6e-2a93-4c75-9e08-d7c3b5a1f264");
    }
}
