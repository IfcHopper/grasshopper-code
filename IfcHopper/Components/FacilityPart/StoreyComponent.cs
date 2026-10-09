using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class StoreyComponent : GH_Component
    {
        public StoreyComponent()
          : base("Storey", "Storey",
              "Creates an IFC building storey (IfcBuildingStorey).",
              ComponentCategory.Tab, ComponentCategory.FacilityPart)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Storey name.", GH_ParamAccess.item, Storey.DefaultName);
            pManager.AddTextParameter("Description", "D", "Storey description.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Elevation", "El", "Storey elevation in document units.", GH_ParamAccess.item, 0.0);
            pManager[1].Optional = true;
            pManager.AddParameter(new SpaceParam(), "Spaces", "Sp", "Spaces in the storey, e.g. rooms.", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Elements", "E", "Objects contained in the storey.", GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.Description, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.Description);
            pManager.AddTextParameter(ComponentGlobalId.Name, ComponentGlobalId.NickName, ComponentGlobalId.Description, GH_ParamAccess.item);
            for (int i = 3; i < 8; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new StoreyParam(), "Storey", "St", "IfcHopper building storey.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = null, description = null;
            double elevation = 0.0;
            if (!DA.GetData(0, ref name)) return;
            DA.GetData(1, ref description);
            DA.GetData(2, ref elevation);

            if (string.IsNullOrWhiteSpace(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name cannot be empty.");
                return;
            }

            var storey = new Storey(name.Trim(), DocumentUnits.ToMetres(elevation)) { Description = description };
            var globalId = ComponentGlobalId.Resolve(this, DA, 7);
            if (globalId == null) return;
            storey.GlobalId = globalId;
            var spaces = new List<SpaceGoo>();
            DA.GetDataList(3, spaces);
            storey.Spaces.AddRange(spaces.Where(s => s != null && s.IsValid).Select(s => s.Value));
            var elements = new List<ElementGoo>();
            DA.GetDataList(4, elements);
            storey.Elements.AddRange(elements.Where(e => e != null && e.IsValid).Select(e => e.Value));
            ComponentPropertySets.Apply(this, DA, 5, storey);
            ComponentClassifications.Apply(this, DA, storey);
            DA.SetData(0, new StoreyGoo(storey));
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override Bitmap Icon => Properties.Resources.Storey;

        public override Guid ComponentGuid => new Guid("a6c3d9e2-4b81-4f57-9d0e-28f5b7c1a4d6");
    }
}
