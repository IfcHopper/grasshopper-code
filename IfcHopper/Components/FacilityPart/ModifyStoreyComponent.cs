using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class ModifyStoreyComponent : ModifyComponentBase
    {
        public ModifyStoreyComponent()
          : base("Modify Storey", "ModStorey",
              "Edits an IFC building storey. Unconnected inputs keep their value; the GlobalId is kept.",
              ComponentCategory.FacilityPart)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new StoreyParam(), "Storey", "St", "IfcHopper building storey to edit.", GH_ParamAccess.item);
            RegisterCommonInputs(pManager);
            pManager.AddNumberParameter("Elevation", "El", "New elevation in document units; the storey and its contents move with it. Unconnected: unchanged.", GH_ParamAccess.item);
            pManager.AddParameter(new SpaceParam(), "Spaces", "Sp", "Spaces." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "Contained objects." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.ModifyDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.ModifyDescription);
            for (int i = 1; i < 7; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new StoreyParam(), "Storey", "St", "Edited IfcHopper building storey.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            StoreyGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var storey = (Storey)goo.Value.Copy();
            if (!ApplyCommon(DA, storey)) return;
            double elevation = 0;
            if (DA.GetData(3, ref elevation)) storey.Elevation = DocumentUnits.ToMetres(elevation);
            ReplaceList(DA, 4, storey.Spaces);
            ReplaceList(DA, 5, storey.Elements);
            ComponentPropertySets.Apply(this, DA, 6, storey);
            ComponentClassifications.Apply(this, DA, storey);

            DA.SetData(0, new StoreyGoo(storey));
        }

        protected override Bitmap Icon => Properties.Resources.ModifyStorey;

        public override Guid ComponentGuid => new Guid("6f2b8c4e-a175-4d39-8e6a-c0b4d9f3e218");
    }
}
