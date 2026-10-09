using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class ModifyElementTypeComponent : ModifyComponentBase
    {
        public ModifyElementTypeComponent()
          : base("Modify Element Type", "ModType",
              "Edits an IFC element type. Unconnected inputs keep their value; the GlobalId and class are kept. Written back to its file " +
              "(through Apply Edits, or as the type of an object), the type changes for all its occurrences, like editing a block definition.",
              ComponentCategory.Object)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new ElementTypeParam(), "Element Type", "ET", "IfcHopper element type to edit.", GH_ParamAccess.item);
            RegisterCommonInputs(pManager);
            pManager.AddTextParameter("Type", "T", "New predefined type of the class. Unconnected: unchanged.", GH_ParamAccess.item);
            pManager.AddGenericParameter(ComponentGeometry.Name, ComponentGeometry.NickName,
                ComponentGeometry.TypeDescription + " Replaces the geometry of every instance when written back. Unconnected: unchanged.", GH_ParamAccess.list);
            pManager.AddColourParameter(ComponentGeometry.ColourName, ComponentGeometry.ColourNickName,
                "New surface colour of all geometry; alpha sets the transparency. Unconnected: unchanged (new geometry keeps its block colours).", GH_ParamAccess.item);
            pManager.AddParameter(new MaterialParam(), ComponentMaterial.Name, ComponentMaterial.NickName, ComponentMaterial.ModifyTypeDescription, GH_ParamAccess.item);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.ModifyDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.ModifyDescription);
            for (int i = 1; i < 8; i++) pManager[i].Optional = true;
            ComponentAttributes.RegisterInputs(this, true);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new ElementTypeParam(), "Element Type", "ET", "Edited IfcHopper element type.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            ElementTypeGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var type = (ElementType)goo.Value.Copy();
            if (!ApplyCommon(DA, type)) return;
            ApplyType(DA, 3, type);
            if (Params.Input[4].SourceCount > 0) type.ReplaceGeometry(ComponentGeometry.ReadType(this, DA, 4, out _));
            var colour = ComponentGeometry.ReadColour(DA, 5);
            if (colour != null) type.SetColour(colour);
            ComponentMaterial.Modify(this, DA, 6, type.SetMaterial);
            ComponentPropertySets.Apply(this, DA, 7, type);
            ComponentClassifications.Apply(this, DA, type);

            if (!ComponentAttributes.Apply(this, DA, 9, type.IfcClass, t => type.Tag = t, type.Attributes)) return;
            DA.SetData(0, new ElementTypeGoo(type));
        }

        protected override Bitmap Icon => Properties.Resources.ModifyElementType;

        public override Guid ComponentGuid => new Guid("7b2e9c4d-6f18-4a35-9d0b-e85c1a3f7d42");
    }
}
