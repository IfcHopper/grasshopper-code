using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class ModifySpaceComponent : ModifyComponentBase
    {
        public ModifySpaceComponent()
          : base("Modify Space", "ModSpace",
              "Edits an IFC space. Unconnected inputs keep their value; the GlobalId is kept.",
              ComponentCategory.FacilityPart)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new SpaceParam(), "Space", "Sp", "IfcHopper space to edit.", GH_ParamAccess.item);
            RegisterCommonInputs(pManager);
            pManager.AddTextParameter("Type", "T", "New predefined type. Unconnected: unchanged.", GH_ParamAccess.item);
            pManager.AddParameter(new SpaceParam(), "Spaces", "Sp", "Nested spaces." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "Contained objects." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.ModifyDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.ModifyDescription);
            ComponentPlacement.RegisterInput(this, ComponentPlacement.ModifyDescription);
            for (int i = 1; i < 9; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new SpaceParam(), "Space", "Sp", "Edited IfcHopper space.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            SpaceGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var space = (Space)goo.Value.Copy();
            if (!ApplyCommon(DA, space)) return;
            ApplyType(DA, 3, space);
            ReplaceList(DA, 4, space.Spaces);
            ReplaceList(DA, 5, space.Elements);
            ComponentPropertySets.Apply(this, DA, 6, space);
            ComponentClassifications.Apply(this, DA, space);
            ApplyPlacement(DA, 8, space);

            DA.SetData(0, new SpaceGoo(space));
        }

        protected override Bitmap Icon => Properties.Resources.ModifySpace;

        public override Guid ComponentGuid => new Guid("2d4598b0-4d8b-431a-b491-76cb62797204");
    }
}
