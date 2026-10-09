using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class ModifyFacilityComponent : ModifyComponentBase
    {
        public ModifyFacilityComponent()
          : base("Modify Facility", "ModFacility",
              "Edits any IFC facility. Unconnected inputs keep their value; the GlobalId is kept.",
              ComponentCategory.Facility)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new FacilityParam(), "Facility", "F", "IfcHopper facility to edit.", GH_ParamAccess.item);
            RegisterCommonInputs(pManager);
            pManager.AddTextParameter("Type", "T", "New predefined type (bridges, roads, railways, marine facilities). Unconnected: unchanged.", GH_ParamAccess.item);
            pManager.AddParameter(new StoreyParam(), "Storeys", "St", "Storeys (buildings only)." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new FacilityPartParam(), "Parts", "FP", "Facility parts." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new SpaceParam(), "Spaces", "Sp", "Spaces directly in the facility." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "Objects contained directly in the facility." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.ModifyDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.ModifyDescription);
            ComponentPlacement.RegisterInput(this, ComponentPlacement.ModifyDescription);
            for (int i = 1; i < 11; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new FacilityParam(), "Facility", "F", "Edited IfcHopper facility.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            FacilityGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var facility = (Facility)goo.Value.Copy();
            if (!ApplyCommon(DA, facility)) return;
            ApplyType(DA, 3, facility);

            if (facility is Building building) ReplaceList(DA, 4, building.Storeys);
            else if (Params.Input[4].SourceCount > 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{facility.IfcClass} has no storeys; Storeys is ignored.");

            if (ReplaceList(DA, 5, facility.Parts))
                foreach (var part in facility.Parts.Where(p => !facility.Accepts(p)).ToList())
                {
                    facility.Parts.Remove(part);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{part.IfcClass} '{part.Name}' cannot be part of {facility.IfcClass} and was skipped.");
                }
            ReplaceList(DA, 6, facility.Spaces);
            ReplaceList(DA, 7, facility.Elements);
            ComponentPropertySets.Apply(this, DA, 8, facility);
            ComponentClassifications.Apply(this, DA, facility);
            ApplyPlacement(DA, 10, facility);

            DA.SetData(0, new FacilityGoo(facility));
        }

        protected override Bitmap Icon => Properties.Resources.ModifyFacility;

        public override Guid ComponentGuid => new Guid("d3a96f2b-5e17-4c08-b4d2-1f8e7a6c9b45");
    }
}
