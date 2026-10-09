using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    /// <summary>Common inputs and output for the facility components (Building, Road, Bridge, ...).</summary>
    public abstract class FacilityComponentBase : GH_Component
    {
        protected FacilityComponentBase(string name, string description)
          : base(name, name, description, ComponentCategory.Tab, ComponentCategory.Facility)
        {
        }

        protected abstract FacilityType FacilityType { get; }

        /// <summary>Nickname of the output parameter.</summary>
        protected abstract string OutputNickName { get; }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>Predefined types of this facility, null when its IFC entity has none (no Type input).</summary>
        private IReadOnlyList<string> PredefinedTypes => Facility.GetPredefinedTypes(FacilityType);

        /// <summary>Index of the children input (parts or storeys), after the optional Type input.</summary>
        protected int ChildrenIndex => PredefinedTypes == null ? 2 : 3;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Facility name.", GH_ParamAccess.item, Facility.GetDefaultName(FacilityType));
            pManager.AddTextParameter("Description", "D", "Facility description.", GH_ParamAccess.item);
            pManager[1].Optional = true;
            if (PredefinedTypes != null)
            {
                var values = PredefinedTypes.Count == 0
                    ? "IFC defines no predefined values; any value"
                    : $"Predefined type: {string.Join(", ", PredefinedTypes)}. Other values";
                pManager.AddTextParameter("Type", "T", $"{values} is written as USERDEFINED with the value as object type.", GH_ParamAccess.item);
                pManager[2].Optional = true;
            }
            RegisterChildrenInput(pManager);
            pManager.AddParameter(new SpaceParam(), "Spaces", "Sp", "Spaces directly in the facility.", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "Objects contained directly in the facility.", GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.Description, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.Description);
            ComponentPlacement.RegisterInput(this, ComponentPlacement.Description);
            pManager.AddTextParameter(ComponentGlobalId.Name, ComponentGlobalId.NickName, ComponentGlobalId.Description, GH_ParamAccess.item);
            for (int i = ChildrenIndex; i <= ChildrenIndex + 6; i++) pManager[i].Optional = true;
        }

        /// <summary>Registers the children input at <see cref="ChildrenIndex"/>. Defaults to facility parts.</summary>
        protected virtual void RegisterChildrenInput(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new FacilityPartParam(), "Parts", "FP", $"Facility parts ({FacilityPart.GetIfcClass(FacilityType)} or IfcFacilityPartCommon).", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new FacilityParam(), Name, OutputNickName, $"IfcHopper facility ({Name}).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = null, description = null, type = null;
            if (!DA.GetData(0, ref name)) return;
            DA.GetData(1, ref description);
            if (PredefinedTypes != null) DA.GetData(2, ref type);

            if (string.IsNullOrWhiteSpace(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name cannot be empty.");
                return;
            }

            var facility = CreateFacility(DA, name.Trim());
            facility.Description = description;
            if (facility.HasPredefinedType && !facility.SetType(type))
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"'{type}' is not a predefined type; written as USERDEFINED.");
            var spaces = new List<SpaceGoo>();
            DA.GetDataList(ChildrenIndex + 1, spaces);
            facility.Spaces.AddRange(spaces.Where(s => s != null && s.IsValid).Select(s => s.Value));
            var elements = new List<ElementGoo>();
            DA.GetDataList(ChildrenIndex + 2, elements);
            facility.Elements.AddRange(elements.Where(e => e != null && e.IsValid).Select(e => e.Value));
            var globalId = ComponentGlobalId.Resolve(this, DA, ChildrenIndex + 6);
            if (globalId == null) return;
            facility.GlobalId = globalId;
            facility.Placement = ComponentPlacement.Read(DA, ChildrenIndex + 5);
            ComponentPropertySets.Apply(this, DA, ChildrenIndex + 3, facility);
            ComponentClassifications.Apply(this, DA, facility);
            DA.SetData(0, new FacilityGoo(facility));
        }

        /// <summary>Creates the facility and reads the children input; overrides can read other inputs.</summary>
        protected virtual Facility CreateFacility(IGH_DataAccess DA, string name)
        {
            var facility = new Facility(FacilityType, name);
            var parts = new List<FacilityPartGoo>();
            DA.GetDataList(ChildrenIndex, parts);
            foreach (var part in parts.Where(p => p != null && p.IsValid).Select(p => p.Value))
            {
                if (facility.Accepts(part)) facility.Parts.Add(part);
                else AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{part.IfcClass} '{part.Name}' cannot be part of {facility.IfcClass} and was skipped.");
            }
            return facility;
        }
    }
}
