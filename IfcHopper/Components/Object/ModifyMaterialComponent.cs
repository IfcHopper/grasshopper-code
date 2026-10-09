using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class ModifyMaterialComponent : GH_Component
    {
        public ModifyMaterialComponent()
          : base("Modify Material", "ModMaterial",
              "Edits an IFC material or material set. Unconnected inputs keep their value. Written back to its file (through Apply Edits, or as the " +
              "material of an object or type), the material changes for every element using it. To change the layers of a set, build a new set.",
              ComponentCategory.Tab, ComponentCategory.Object)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new MaterialParam(), "Material", "Mat", "IfcHopper material or material set to edit.", GH_ParamAccess.item);
            pManager.AddTextParameter("Name", "N", "New name. Unconnected: unchanged.", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "New description (IFC4 and later). Unconnected: unchanged.", GH_ParamAccess.item);
            pManager.AddTextParameter("Category", "Cat", "New category of a single material (IFC4 and later). Unconnected: unchanged.", GH_ParamAccess.item);
            pManager.AddColourParameter(ComponentGeometry.ColourName, ComponentGeometry.ColourNickName,
                "New surface colour of a single material; alpha sets the transparency. Unconnected: unchanged.", GH_ParamAccess.item);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.ModifyMaterialDescription, GH_ParamAccess.list);
            for (int i = 1; i < 6; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new MaterialParam(), "Material", "Mat", "Edited IfcHopper material or material set.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            MaterialGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var definition = goo.Value.Copy();
            string name = null, description = null, category = null;
            if (DA.GetData(1, ref name))
            {
                if (definition is Material && string.IsNullOrWhiteSpace(name))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name cannot be empty.");
                    return;
                }
                definition.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            }
            if (DA.GetData(2, ref description)) definition.Description = string.IsNullOrWhiteSpace(description) ? null : description;

            var colour = ComponentGeometry.ReadColour(DA, 4);
            var hasCategory = DA.GetData(3, ref category);
            if (definition is Material material)
            {
                if (hasCategory) material.Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
                if (colour != null) material.Colour = colour;
            }
            else if (hasCategory || colour != null)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Category and Colour belong to single materials; edit the materials of the set instead.");

            ComponentPropertySets.ApplyToMaterial(this, DA, 5, definition);
            DA.SetData(0, new MaterialGoo(definition));
        }

        protected override Bitmap Icon => Properties.Resources.ModifyMaterial;

        public override Guid ComponentGuid => new Guid("4a7c2e91-b836-4f0d-95e3-d17b6a8c2f05");
    }
}
