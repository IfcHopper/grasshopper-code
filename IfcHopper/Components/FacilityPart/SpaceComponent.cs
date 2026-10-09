using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class SpaceComponent : GH_Component
    {
        public SpaceComponent()
          : base("Space", "Space",
              "Creates an IFC space (IfcSpace), e.g. a room. Connect it to a site, facility, storey, facility part or space.",
              ComponentCategory.Tab, ComponentCategory.FacilityPart)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Space name.", GH_ParamAccess.item, Space.DefaultName);
            pManager.AddTextParameter("Description", "D", "Space description.", GH_ParamAccess.item);
            pManager.AddTextParameter("Type", "T",
                $"Predefined type: {string.Join(", ", new Space().PredefinedTypes)}. Other values are written as USERDEFINED with the value as object type.",
                GH_ParamAccess.item);
            pManager.AddParameter(new SpaceParam(), "Spaces", "Sp", "Nested (partial) spaces.", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "Objects contained in the space.", GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.Description, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.Description);
            ComponentPlacement.RegisterInput(this, ComponentPlacement.Description);
            pManager.AddTextParameter(ComponentGlobalId.Name, ComponentGlobalId.NickName, ComponentGlobalId.Description, GH_ParamAccess.item);
            for (int i = 1; i < 9; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new SpaceParam(), "Space", "Sp", "IfcHopper space.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = null, description = null, type = null;
            var spaces = new List<SpaceGoo>();
            var elements = new List<ElementGoo>();
            if (!DA.GetData(0, ref name)) return;
            DA.GetData(1, ref description);
            DA.GetData(2, ref type);
            DA.GetDataList(3, spaces);
            DA.GetDataList(4, elements);

            if (string.IsNullOrWhiteSpace(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name cannot be empty.");
                return;
            }

            var space = new Space(name.Trim()) { Description = description };
            if (!space.SetType(type))
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"'{type}' is not a predefined type; written as USERDEFINED.");
            space.Spaces.AddRange(spaces.Where(s => s != null && s.IsValid).Select(s => s.Value));
            space.Elements.AddRange(elements.Where(e => e != null && e.IsValid).Select(e => e.Value));
            var globalId = ComponentGlobalId.Resolve(this, DA, 8);
            if (globalId == null) return;
            space.GlobalId = globalId;
            space.Placement = ComponentPlacement.Read(DA, 7);
            ComponentPropertySets.Apply(this, DA, 5, space);
            ComponentClassifications.Apply(this, DA, space);
            DA.SetData(0, new SpaceGoo(space));
        }

        protected override Bitmap Icon => Properties.Resources.Space;

        public override Guid ComponentGuid => new Guid("e7113307-9566-4c50-8150-a5f8f24551a2");
    }
}
