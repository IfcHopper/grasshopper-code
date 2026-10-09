using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class DeconstructMaterialComponent : GH_Component
    {
        public DeconstructMaterialComponent()
          : base("Deconstruct Material", "DeMaterial",
              "Deconstructs an IFC material or material set. Part outputs list the layers, constituents or profiles of a set in order; a single material is its own only part.",
              ComponentCategory.Tab, ComponentCategory.Object)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new MaterialParam(), "Material", "Mat", "IfcHopper material.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Class", "C", "IFC class: IfcMaterial, IfcMaterialLayerSet, IfcMaterialConstituentSet (also IFC2X3 material lists) or IfcMaterialProfileSet.", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "Name of the material or set.", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "Description.", GH_ParamAccess.item);
            pManager.AddTextParameter("Category", "Cat", "Category of a single material, e.g. concrete or steel.", GH_ParamAccess.item);
            pManager.AddColourParameter("Colour", "Col", "Surface colour of the material, or of the first part with one; empty when not styled. Alpha holds the transparency.", GH_ParamAccess.item);
            pManager.AddParameter(new MaterialParam(), "Materials", "M", "Material of each part; empty items for parts without material.", GH_ParamAccess.list);
            pManager.AddTextParameter("Part Names", "PN", "Name of each layer, constituent or profile.", GH_ParamAccess.list);
            pManager.AddTextParameter("Part Categories", "PC", "Category of each layer, constituent or profile.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thicknesses", "T", "Thickness of each layer in document units (layer sets only).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Fractions", "F", "Share of each constituent from 0 to 1; empty items when not given (constituent sets only).", GH_ParamAccess.list);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, "Property sets of the material or set (IfcMaterialProperties).", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            MaterialGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var definition = goo.Value;
            DA.SetData(0, definition.IfcClass);
            DA.SetData(1, definition.Name);
            DA.SetData(2, definition.Description);
            DA.SetData(3, (definition as Material)?.Category);
            if (definition.DisplayColour != null) DA.SetData(4, DocumentGeometry.ToColor(definition.DisplayColour));
            DA.SetDataList(5, definition.Materials.Select(m => m == null ? null : new MaterialGoo(m)));
            DA.SetDataList(10, definition.PropertySets.Select(s => new PropertySetGoo(s)));

            switch (definition)
            {
                case MaterialLayerSet set:
                    SetParts(DA, set.Layers.Select(l => (l.Name, l.Category)));
                    DA.SetDataList(8, set.Layers.Select(l => DocumentUnits.FromMetres(l.Thickness)));
                    break;
                case MaterialConstituentSet set:
                    SetParts(DA, set.Constituents.Select(c => (c.Name, c.Category)));
                    DA.SetDataList(9, set.Constituents.Select(c => c.Fraction.HasValue ? new GH_Number(c.Fraction.Value) : null));
                    break;
                case MaterialProfileSet set:
                    SetParts(DA, set.Profiles.Select(p => (p.Name, p.Category)));
                    break;
                case Material material:
                    SetParts(DA, new[] { (material.Name, material.Category) });
                    break;
            }
        }

        private static void SetParts(IGH_DataAccess DA, IEnumerable<(string Name, string Category)> parts)
        {
            var list = parts.ToList();
            DA.SetDataList(6, list.Select(p => p.Name));
            DA.SetDataList(7, list.Select(p => p.Category));
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructMaterial;

        public override Guid ComponentGuid => new Guid("49820339-b276-477f-9eb1-c8f27cffb136");
    }
}
