using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class ElementTypeComponent : GH_Component
    {
        public ElementTypeComponent()
          : base("Element Type", "Type",
              "Creates an IFC element type (e.g. IfcWallType), shared by the objects placing it, like a block definition. " +
              "Connect it to the Element Type input of objects.",
              ComponentCategory.Tab, ComponentCategory.Object)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Class", "C", "IFC type class (e.g. IfcWallType) or the element class it types (e.g. IfcWall).", GH_ParamAccess.item, "IfcBuildingElementProxyType");
            pManager.AddTextParameter("Name", "N", "Type name. Empty: the name of the block, else Hopper Type (with a warning).", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "Type description.", GH_ParamAccess.item);
            pManager.AddTextParameter("Type", "T", "Predefined type of the class (e.g. SOLIDWALL for IfcWallType). Other values are written as USERDEFINED with the value as element type.", GH_ParamAccess.item);
            pManager.AddGenericParameter(ComponentGeometry.Name, ComponentGeometry.NickName, ComponentGeometry.TypeDescription, GH_ParamAccess.list);
            pManager.AddColourParameter(ComponentGeometry.ColourName, ComponentGeometry.ColourNickName, "Surface colour of the geometry (IfcStyledItem); alpha sets the transparency. Empty: block objects keep their object colours.", GH_ParamAccess.item);
            pManager.AddParameter(new MaterialParam(), ComponentMaterial.Name, ComponentMaterial.NickName, ComponentMaterial.TypeDescription, GH_ParamAccess.item);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.Description, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.Description);
            pManager.AddTextParameter(ComponentGlobalId.Name, ComponentGlobalId.NickName,
                ComponentGlobalId.Description + " With a block, empty means the GlobalId of the block, so objects made from its instances share this type.", GH_ParamAccess.item);
            for (int i = 1; i < 10; i++) pManager[i].Optional = true;
            ComponentAttributes.RegisterInputs(this, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new ElementTypeParam(), "Element Type", "ET", "IfcHopper element type.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string className = null, name = null, description = null, type = null;
            if (!DA.GetData(0, ref className)) return;
            DA.GetData(1, ref name);
            DA.GetData(2, ref description);
            DA.GetData(3, ref type);

            var typeClass = IfcBackend.Schema.FindTypeClass(className) ?? IfcBackend.Schema.TypeClassOf(className);
            if (typeClass == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, IfcBackend.Schema.FindElementClass(className) is string elementClass
                    ? $"{elementClass} has no type class; use IfcBuildingElementProxyType."
                    : $"'{className}' is not an IFC element type class (e.g. IfcWallType, IfcSlabType, IfcColumnType).");
                return;
            }

            var meshes = ComponentGeometry.ReadType(this, DA, 4, out var firstBlock);

            name = string.IsNullOrWhiteSpace(name) ? firstBlock?.Name : name.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = ElementType.DefaultName;
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Name is empty; the type is named '{name}'.");
            }

            var elementType = new ElementType(name, typeClass) { Description = description ?? (string.IsNullOrWhiteSpace(firstBlock?.Description) ? null : firstBlock.Description) };
            if (!string.IsNullOrWhiteSpace(type))
            {
                if (!elementType.HasPredefinedType)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{typeClass} has no predefined type; Type is ignored.");
                else if (!elementType.SetType(type))
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                        $"'{type}' is not a predefined type of {typeClass} ({string.Join(", ", elementType.PredefinedTypes)}); written as USERDEFINED.");
            }

            var globalId = ComponentGlobalId.Resolve(this, DA, 9, firstBlock == null ? null : DocumentBlocks.GlobalIdOf(firstBlock));
            if (globalId == null) return;
            elementType.GlobalId = globalId;
            var colour = ComponentGeometry.ReadColour(DA, 5);
            elementType.Geometry.AddRange(meshes.Select(m => colour == null ? m : m.WithColour(colour)));
            ComponentMaterial.Apply(DA, 6, elementType.SetMaterial);
            ComponentPropertySets.Apply(this, DA, 7, elementType);
            ComponentClassifications.Apply(this, DA, elementType);
            if (!ComponentAttributes.Apply(this, DA, 10, elementType.IfcClass, t => elementType.Tag = t, elementType.Attributes)) return;
            DA.SetData(0, new ElementTypeGoo(elementType));
        }

        protected override Bitmap Icon => Properties.Resources.ElementType;

        public override Guid ComponentGuid => new Guid("c48f2a6e-91d3-4b57-8e0c-3a7d5f1b9e64");
    }
}
