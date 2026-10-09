using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class ModifyFacilityPartComponent : ModifyComponentBase
    {
        public ModifyFacilityPartComponent()
          : base("Modify Facility Part", "ModPart",
              "Edits any IFC facility part. Unconnected inputs keep their value; the GlobalId is kept.",
              ComponentCategory.FacilityPart)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new FacilityPartParam(), "Part", "FP", "IfcHopper facility part to edit.", GH_ParamAccess.item);
            RegisterCommonInputs(pManager);
            pManager.AddTextParameter("Type", "T", "New predefined type. Unconnected: unchanged.", GH_ParamAccess.item);
            pManager.AddTextParameter("Usage", "U", $"New usage: {string.Join(", ", Enum.GetNames(typeof(FacilityUsage)))}. Unconnected: unchanged.", GH_ParamAccess.item);
            pManager.AddParameter(new FacilityPartParam(), "Parts", "FP", "Nested parts." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new SpaceParam(), "Spaces", "Sp", "Spaces." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "Contained objects." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.ModifyDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.ModifyDescription);
            ComponentPlacement.RegisterInput(this, ComponentPlacement.ModifyDescription);
            for (int i = 1; i < 11; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new FacilityPartParam(), "Part", "FP", "Edited IfcHopper facility part.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            FacilityPartGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var part = (FacilityPart)goo.Value.Copy();
            if (!ApplyCommon(DA, part)) return;
            ApplyType(DA, 3, part);

            string usageText = null;
            if (DA.GetData(4, ref usageText))
            {
                if (!Enum.TryParse(usageText?.Trim(), true, out FacilityUsage usage) || !Enum.IsDefined(typeof(FacilityUsage), usage))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Unknown usage '{usageText}'. Use one of: {string.Join(", ", Enum.GetNames(typeof(FacilityUsage)))}.");
                    return;
                }
                part.Usage = usage;
            }

            if (ReplaceList(DA, 5, part.Parts))
                foreach (var nested in part.Parts.Where(p => !part.Accepts(p)).ToList())
                {
                    part.Parts.Remove(nested);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{nested.IfcClass} '{nested.Name}' cannot be part of {part.IfcClass} and was skipped.");
                }
            ReplaceList(DA, 6, part.Spaces);
            ReplaceList(DA, 7, part.Elements);
            ComponentPropertySets.Apply(this, DA, 8, part);
            ComponentClassifications.Apply(this, DA, part);
            ApplyPlacement(DA, 10, part);

            DA.SetData(0, new FacilityPartGoo(part));
        }

        protected override Bitmap Icon => Properties.Resources.ModifyFacilityPart;

        public override Guid ComponentGuid => new Guid("a85e2c7d-4f9b-46e1-b3a0-9d6c1e8f4b72");
    }
}
