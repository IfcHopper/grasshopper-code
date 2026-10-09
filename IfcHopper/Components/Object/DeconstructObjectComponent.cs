using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class DeconstructObjectComponent : DeconstructComponentBase
    {
        public DeconstructObjectComponent()
          : base("Deconstruct Object", "DeObject",
              "Deconstructs an IFC element.",
              ComponentCategory.Object)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new ElementParam(), "Object", "O", "IfcHopper element.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            RegisterCommonOutputs(pManager);
            pManager.AddTextParameter("Class", "C", "IFC class, e.g. IfcWall.", GH_ParamAccess.item);
            pManager.AddTextParameter("Type", "T", "Predefined type, or the object type when user defined.", GH_ParamAccess.item);
            pManager.AddMeshParameter("Geometry", "G",
                "Body geometry as meshes in world coordinates, with the openings subtracted (the body stored in the file is uncut). Read solids are faceted.", GH_ParamAccess.list);
            pManager.AddColourParameter("Colour", "Col", "Surface colour of each mesh: its own style (IfcStyledItem), else the colour of the element's material; empty when neither is styled. Alpha holds the transparency.", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Parts", "P", "Elements this object is made of (IfcRelAggregates).", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Openings", "Op", "Openings and recesses voiding the element (IfcRelVoidsElement); deconstruct them as objects.", GH_ParamAccess.list);
            pManager.AddParameter(new ElementParam(), "Fills", "Fi", "For an opening: the elements filling it, e.g. doors and windows (IfcRelFillsElement).", GH_ParamAccess.list);
            pManager.AddParameter(new MaterialParam(), "Material", "Mat", "Material of the element, or of its type when the element has none (IfcRelAssociatesMaterial).", GH_ParamAccess.item);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.OutputDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterOutput(this);
            pManager.AddParameter(new ElementTypeParam(), "Element Type", "ET", "Type of the element (IfcRelDefinesByType).", GH_ParamAccess.item);
            RegisterPlacementOutput(pManager);
            ComponentAttributes.RegisterOutputs(this);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            ElementGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var element = goo.Value;
            SetCommonOutputs(DA, element);
            DA.SetData(3, element.IfcClass);
            DA.SetData(4, element.HasPredefinedType ? TypeOf(element) : element.ObjectType);
            var body = DocumentOpenings.CutBody(element, out var failed);
            if (failed) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Some openings could not be subtracted; those meshes are uncut.");
            DA.SetDataList(5, body.Select(b => b.Mesh));
            DA.SetDataList(6, body.Select(b => b.Colour == null ? null : new GH_Colour(DocumentGeometry.ToColor(b.Colour))));
            DA.SetDataList(7, element.Parts.Select(p => new ElementGoo(p)));
            DA.SetDataList(8, element.Openings.Select(o => new ElementGoo(o)));
            if (element is Opening opening) DA.SetDataList(9, opening.Fills.Select(f => new ElementGoo(f)));
            if (element.Material != null) DA.SetData(10, new MaterialGoo(element.Material));
            ComponentPropertySets.SetOutput(DA, 11, element);
            ComponentClassifications.SetOutput(this, DA, element);
            if (element.Type != null) DA.SetData(13, new ElementTypeGoo(element.Type));
            SetPlacement(DA, 14, element);
            ComponentAttributes.SetOutputs(DA, 15, element.Tag, element.Attributes, element.IfcClass);

            if (element.SkippedGeometry.Count > 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Geometry not converted: " +
                    string.Join(", ", element.SkippedGeometry.GroupBy(s => s).Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key)));
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructObject;

        public override Guid ComponentGuid => new Guid("e9b4a2d7-3c61-4f58-8d0e-6a2f7c1b5e93");
    }
}
