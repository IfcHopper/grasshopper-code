using System;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class ModifyObjectComponent : ModifyComponentBase
    {
        public ModifyObjectComponent()
          : base("Modify Object", "ModObject",
              "Edits an IFC element. Unconnected inputs keep their value; the GlobalId and class are kept.",
              ComponentCategory.Object)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new ElementParam(), "Object", "O", "IfcHopper element to edit.", GH_ParamAccess.item);
            RegisterCommonInputs(pManager);
            pManager.AddTextParameter("Type", "T", "New predefined type of the class. Unconnected: unchanged.", GH_ParamAccess.item);
            pManager.AddGeometryParameter(ComponentGeometry.Name, ComponentGeometry.NickName,
                "New body geometry in world coordinates, faceted to meshes; replaces the body when written back. Unconnected: unchanged (moves with a new placement).", GH_ParamAccess.list);
            pManager.AddColourParameter(ComponentGeometry.ColourName, ComponentGeometry.ColourNickName,
                "New surface colour of all geometry; alpha sets the transparency. A changed colour replaces the body when written back. Unconnected: unchanged (new geometry has no colour).", GH_ParamAccess.item);
            pManager.AddParameter(new ElementParam(), "Parts", "P", "Elements this object is made of." + ChildListNote, GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), ComponentOpenings.Name, ComponentOpenings.NickName, ComponentOpenings.ModifyDescription, GH_ParamAccess.list);
            pManager.AddParameter(new MaterialParam(), ComponentMaterial.Name, ComponentMaterial.NickName, ComponentMaterial.ModifyDescription, GH_ParamAccess.item);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.ModifyDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterInput(this, ComponentClassifications.ModifyDescription);
            pManager.AddParameter(new ElementTypeParam(), "Element Type", "ET",
                "New type of the object (IfcRelDefinesByType). An instance of its type becomes an instance of the new type in the same place, " +
                "like replacing a block; other objects keep their geometry. Connected but empty: removes the type (an instance keeps its geometry). " +
                "Unconnected: unchanged.", GH_ParamAccess.item);
            ComponentPlacement.RegisterInput(this, ComponentPlacement.ModifyDescription);
            for (int i = 1; i < 13; i++) pManager[i].Optional = true;
            ComponentAttributes.RegisterInputs(this, true);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new ElementParam(), "Object", "O", "Edited IfcHopper element.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            ElementGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var element = (Element)goo.Value.Copy();
            if (!ApplyCommon(DA, element)) return;
            ApplyType(DA, 3, element);

            var oldPlacement = element.Placement;
            ApplyPlacement(DA, 12, element);
            if (Params.Input[4].SourceCount > 0) element.ReplaceGeometry(ComponentGeometry.Read(this, DA, 4));
            else if (oldPlacement != null && element.Placement != oldPlacement) element.MoveGeometry(oldPlacement, element.Placement);

            var colour = ComponentGeometry.ReadColour(DA, 5);
            if (colour != null) element.SetColour(colour);
            ElementTypeGoo type = null;
            if (DA.GetData(11, ref type) && type != null && type.IsValid)
            {
                if (element.TypeTransform != null) element.PlaceType(type.Value, element.TypeTransform);
                else element.AssignType(type.Value);
            }
            // An instance keeps the geometry of its old type as its own.
            else if (Params.Input[11].SourceCount > 0) element.AssignType(null);
            ReplaceList(DA, 6, element.Parts);
            if (Params.Input[7].SourceCount > 0)
            {
                element.Openings.Clear();
                element.Openings.AddRange(ComponentOpenings.Read(this, DA, 7));
            }
            ComponentMaterial.Modify(this, DA, 8, element.SetMaterial);
            ComponentPropertySets.Apply(this, DA, 9, element);
            ComponentClassifications.Apply(this, DA, element);

            if (!ComponentAttributes.Apply(this, DA, 13, element.IfcClass, t => element.Tag = t, element.Attributes)) return;
            DA.SetData(0, new ElementGoo(element));
        }

        protected override Bitmap Icon => Properties.Resources.ModifyObject;

        public override Guid ComponentGuid => new Guid("3e7a1d9c-b2f6-48c5-a1e3-6b9f0d4c7e28");
    }
}
