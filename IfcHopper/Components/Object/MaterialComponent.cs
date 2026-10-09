using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class MaterialComponent : GH_Component
    {
        public MaterialComponent()
          : base("Material", "Material",
              "Creates an IFC material (IfcMaterial). Connect it to objects, types or material sets; on write an existing material with the same " +
              "name and values is reused.",
              ComponentCategory.Tab, ComponentCategory.Object)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Material name, e.g. Concrete C30/37.", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "Material description (IFC4 and later).", GH_ParamAccess.item);
            pManager.AddTextParameter("Category", "Cat", "Material category, e.g. concrete, steel, aluminium, block, brick, stone, wood, glass, gypsum, plastic, earth (IFC4 and later).", GH_ParamAccess.item);
            pManager.AddColourParameter(ComponentGeometry.ColourName, ComponentGeometry.ColourNickName,
                "Surface colour of the material (IfcMaterialDefinitionRepresentation), shown on geometry without a colour of its own; alpha sets the transparency.", GH_ParamAccess.item);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.MaterialDescription, GH_ParamAccess.list);
            for (int i = 1; i < 5; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new MaterialParam(), "Material", "Mat", "IfcHopper material.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = null, description = null, category = null;
            if (!DA.GetData(0, ref name)) return;
            if (string.IsNullOrWhiteSpace(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name cannot be empty.");
                return;
            }
            DA.GetData(1, ref description);
            DA.GetData(2, ref category);

            var material = new Material(name.Trim())
            {
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
                Colour = ComponentGeometry.ReadColour(DA, 3),
            };
            ComponentPropertySets.ApplyToMaterial(this, DA, 4, material);
            DA.SetData(0, new MaterialGoo(material));
        }

        protected override Bitmap Icon => Properties.Resources.Material;

        public override Guid ComponentGuid => new Guid("e8a14c27-5b39-4d6f-a2c8-0f73d9b4e156");
    }
}
