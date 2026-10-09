using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    /// <summary>Common inputs and output for the facility part components (Road Part, Bridge Part, ...).</summary>
    public abstract class FacilityPartComponentBase : GH_Component
    {
        protected FacilityPartComponentBase(string name, string description)
          : base(name, name, description, ComponentCategory.Tab, ComponentCategory.FacilityPart)
        {
        }

        /// <summary>Facility kind the part belongs to; <see cref="FacilityType.Facility"/> for IfcFacilityPartCommon.</summary>
        protected abstract FacilityType PartKind { get; }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Part name.", GH_ParamAccess.item, FacilityPart.GetDefaultName(PartKind));
            pManager.AddTextParameter("Description", "D", "Part description.", GH_ParamAccess.item);
            pManager.AddTextParameter("Type", "T",
                $"Predefined type: {string.Join(", ", FacilityPart.GetPredefinedTypes(PartKind))}. Other values are written as USERDEFINED with the value as object type.",
                GH_ParamAccess.item);
            pManager.AddTextParameter("Usage", "U",
                $"How the part divides the facility: {string.Join(", ", Enum.GetNames(typeof(FacilityUsage)))}.",
                GH_ParamAccess.item, FacilityUsage.NotDefined.ToString());
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager.AddParameter(new FacilityPartParam(), "Parts", "FP", $"Nested parts ({FacilityPart.GetIfcClass(PartKind)} or IfcFacilityPartCommon).", GH_ParamAccess.list);
            pManager[4].Optional = true;
            pManager.AddParameter(new SpaceParam(), "Spaces", "Sp", "Spaces in the part.", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "Objects contained in the part.", GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.Description, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.Description);
            ComponentPlacement.RegisterInput(this, ComponentPlacement.Description);
            pManager.AddTextParameter(ComponentGlobalId.Name, ComponentGlobalId.NickName, ComponentGlobalId.Description, GH_ParamAccess.item);
            for (int i = 5; i < 11; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new FacilityPartParam(), Name, "FP", $"IfcHopper facility part ({FacilityPart.GetIfcClass(PartKind)}).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = null, description = null, type = null, usageText = null;
            if (!DA.GetData(0, ref name)) return;
            DA.GetData(1, ref description);
            DA.GetData(2, ref type);
            DA.GetData(3, ref usageText);

            if (string.IsNullOrWhiteSpace(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name cannot be empty.");
                return;
            }

            var usage = FacilityUsage.NotDefined;
            if (!string.IsNullOrWhiteSpace(usageText) &&
                !(Enum.TryParse(usageText.Trim(), true, out usage) && Enum.IsDefined(typeof(FacilityUsage), usage)))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Unknown usage '{usageText}'. Use one of: {string.Join(", ", Enum.GetNames(typeof(FacilityUsage)))}.");
                return;
            }

            var part = new FacilityPart(PartKind, name.Trim()) { Description = description, Usage = usage };
            if (!part.SetType(type))
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"'{type}' is not a predefined type; written as USERDEFINED.");
            var nested = new List<FacilityPartGoo>();
            DA.GetDataList(4, nested);
            foreach (var child in nested.Where(p => p != null && p.IsValid).Select(p => p.Value))
            {
                if (part.Accepts(child)) part.Parts.Add(child);
                else AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{child.IfcClass} '{child.Name}' cannot be part of {part.IfcClass} and was skipped.");
            }
            var globalId = ComponentGlobalId.Resolve(this, DA, 10);
            if (globalId == null) return;
            part.GlobalId = globalId;
            part.Placement = ComponentPlacement.Read(DA, 9);
            ComponentPropertySets.Apply(this, DA, 7, part);
            ComponentClassifications.Apply(this, DA, part);
            var spaces = new List<SpaceGoo>();
            DA.GetDataList(5, spaces);
            part.Spaces.AddRange(spaces.Where(s => s != null && s.IsValid).Select(s => s.Value));
            var elements = new List<ElementGoo>();
            DA.GetDataList(6, elements);
            part.Elements.AddRange(elements.Where(e => e != null && e.IsValid).Select(e => e.Value));
            DA.SetData(0, new FacilityPartGoo(part));
        }
    }
}
