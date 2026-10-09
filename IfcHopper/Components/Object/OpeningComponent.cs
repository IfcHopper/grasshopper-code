using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class OpeningComponent : GH_Component
    {
        public OpeningComponent()
          : base("Opening", "Opening",
              "Creates an opening or recess (IfcOpeningElement). Connect it to the Openings input of Object or Modify Object: it voids that object, " +
              "whose geometry stays uncut in the file. Doors and windows filling it must also be in a storey or space.",
              ComponentCategory.Tab, ComponentCategory.Object)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Opening name.", GH_ParamAccess.item, Opening.DefaultOpeningName);
            pManager.AddTextParameter("Description", "D", "Opening description.", GH_ParamAccess.item);
            pManager.AddTextParameter("Type", "T", "OPENING (through the element) or RECESS (part of its thickness). Empty: not defined.", GH_ParamAccess.item);
            pManager.AddGeometryParameter(ComponentGeometry.Name, ComponentGeometry.NickName,
                "Volume subtracted from the element, in world coordinates. Meshes are kept; Breps, extrusions, surfaces and SubDs are faceted.", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Fills", "Fi", "Elements filling the opening, e.g. doors and windows (IfcRelFillsElement).", GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.Description, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.Description);
            ComponentPlacement.RegisterInput(this, "World placement plane. Empty: the placement of the element it voids.");
            pManager.AddTextParameter(ComponentGlobalId.Name, ComponentGlobalId.NickName, ComponentGlobalId.Description, GH_ParamAccess.item);
            foreach (var i in new[] { 1, 2, 4, 5, 7, 8 }) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new ElementParam(), "Opening", "Op", "IfcHopper opening.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = null, description = null, type = null;
            if (!DA.GetData(0, ref name)) return;
            if (string.IsNullOrWhiteSpace(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name cannot be empty.");
                return;
            }
            DA.GetData(1, ref description);
            DA.GetData(2, ref type);

            var opening = new Opening(name.Trim()) { Description = description };
            if (!string.IsNullOrWhiteSpace(type) && !opening.SetType(type))
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"'{type}' is not a predefined type of {opening.IfcClass} ({string.Join(", ", opening.PredefinedTypes)}); written as USERDEFINED.");

            var globalId = ComponentGlobalId.Resolve(this, DA, 8);
            if (globalId == null) return;
            opening.GlobalId = globalId;
            opening.Placement = ComponentPlacement.Read(DA, 7);
            opening.Geometry.AddRange(ComponentGeometry.Read(this, DA, 3));
            if (opening.Geometry.Count == 0) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The opening has no geometry, so it voids nothing.");

            var fills = new List<ElementGoo>();
            DA.GetDataList(4, fills);
            opening.Fills.AddRange(fills.Where(f => f != null && f.IsValid).Select(f => f.Value));
            ComponentPropertySets.Apply(this, DA, 5, opening);
            ComponentClassifications.Apply(this, DA, opening);
            DA.SetData(0, new ElementGoo(opening));
        }

        protected override Bitmap Icon => Properties.Resources.Opening;

        public override Guid ComponentGuid => new Guid("4199aef7-86e1-4879-bee4-8c87ebc888ee");
    }
}
