using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class ConstituentSetComponent : GH_Component
    {
        public ConstituentSetComponent()
          : base("Constituent Set", "Constituents",
              "Creates an IFC material constituent set (IfcMaterialConstituentSet): named parts of an element and their materials, e.g. the frame " +
              "and glazing of a window. Written as a material list without names and fractions in IFC2X3.",
              ComponentCategory.Tab, ComponentCategory.Object)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Constituent set name.", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "Constituent set description.", GH_ParamAccess.item);
            pManager.AddParameter(new MaterialParam(), "Materials", "M", "Material of each constituent (single materials).", GH_ParamAccess.list);
            pManager.AddTextParameter("Constituent Names", "CN", "Name of each constituent, e.g. Frame, Glazing.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Fractions", "F", "Share of the element made of each constituent, from 0 to 1.", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new MaterialParam(), "Constituent Set", "CS", "IfcHopper material constituent set.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = null, description = null;
            var materials = new List<MaterialGoo>();
            var names = new List<string>();
            var fractions = new List<double>();
            if (!DA.GetData(0, ref name) || !DA.GetDataList(2, materials)) return;
            DA.GetData(1, ref description);
            DA.GetDataList(3, names);
            DA.GetDataList(4, fractions);

            if (names.Count > 0 && names.Count != materials.Count || fractions.Count > 0 && fractions.Count != materials.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Constituent Names and Fractions (when given) need one item per material.");
                return;
            }
            if (materials.Any(m => !(m?.Value is Material)))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Each constituent needs a single material, not a material set.");
                return;
            }
            if (fractions.Any(f => f < 0 || f > 1))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Fractions must be between 0 and 1.");
                return;
            }
            if (fractions.Count > 0 && Math.Abs(fractions.Sum() - 1) > 1e-6)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Fractions add up to {fractions.Sum():0.###}, not 1.");

            var set = new MaterialConstituentSet(string.IsNullOrWhiteSpace(name) ? null : name.Trim()) { Description = string.IsNullOrWhiteSpace(description) ? null : description };
            set.Constituents.AddRange(materials.Select((m, i) => new MaterialConstituent
            {
                Material = (Material)m.Value,
                Name = names.Count > 0 && !string.IsNullOrWhiteSpace(names[i]) ? names[i] : null,
                Fraction = fractions.Count > 0 ? fractions[i] : (double?)null,
            }));
            DA.SetData(0, new MaterialGoo(set));
        }

        protected override Bitmap Icon => Properties.Resources.ConstituentSet;

        public override Guid ComponentGuid => new Guid("9c35e7a1-4d82-4b6f-b1e9-6a0d2f8c5e73");
    }
}
