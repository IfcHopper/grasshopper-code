using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class LayerSetComponent : GH_Component
    {
        public LayerSetComponent()
          : base("Layer Set", "LayerSet",
              "Creates an IFC material layer set (IfcMaterialLayerSet), e.g. the build-up of a wall or slab, from the outside in.",
              ComponentCategory.Tab, ComponentCategory.Object)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Layer set name, e.g. Cavity wall 300.", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "Layer set description (IFC4 and later).", GH_ParamAccess.item);
            pManager.AddParameter(new MaterialParam(), "Materials", "M", "Material of each layer (single materials).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thicknesses", "T", "Thickness of each layer in document units.", GH_ParamAccess.list);
            pManager.AddTextParameter("Layer Names", "LN", "Name of each layer, e.g. Core, Insulation (IFC4 and later).", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new MaterialParam(), "Layer Set", "LS", "IfcHopper material layer set.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = null, description = null;
            var materials = new List<MaterialGoo>();
            var thicknesses = new List<double>();
            var names = new List<string>();
            if (!DA.GetData(0, ref name) || !DA.GetDataList(2, materials) || !DA.GetDataList(3, thicknesses)) return;
            DA.GetData(1, ref description);
            DA.GetDataList(4, names);

            if (materials.Count != thicknesses.Count || names.Count > 0 && names.Count != materials.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Materials, Thicknesses and Layer Names (when given) need one item per layer.");
                return;
            }
            if (materials.Any(m => !(m?.Value is Material)))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Each layer needs a single material, not a material set.");
                return;
            }
            if (thicknesses.Any(t => t < 0))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Thicknesses cannot be negative.");
                return;
            }

            var set = new MaterialLayerSet(string.IsNullOrWhiteSpace(name) ? null : name.Trim()) { Description = string.IsNullOrWhiteSpace(description) ? null : description };
            set.Layers.AddRange(materials.Select((m, i) => new MaterialLayer
            {
                Material = (Material)m.Value,
                Thickness = DocumentUnits.ToMetres(thicknesses[i]),
                Name = names.Count > 0 && !string.IsNullOrWhiteSpace(names[i]) ? names[i] : null,
            }));
            DA.SetData(0, new MaterialGoo(set));
        }

        protected override Bitmap Icon => Properties.Resources.LayerSet;

        public override Guid ComponentGuid => new Guid("1f6d83b5-c247-4e09-8a5d-b2e7f40c9a18");
    }
}
