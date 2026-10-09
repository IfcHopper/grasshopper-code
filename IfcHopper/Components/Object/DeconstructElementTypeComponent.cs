using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class DeconstructElementTypeComponent : DeconstructComponentBase
    {
        public DeconstructElementTypeComponent()
          : base("Deconstruct Element Type", "DeType",
              "Deconstructs an IFC element type. Its geometry is in type coordinates, like a block definition; occurrences place it.",
              ComponentCategory.Object)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new ElementTypeParam(), "Element Type", "ET", "IfcHopper element type.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            RegisterCommonOutputs(pManager);
            pManager.AddTextParameter("Class", "C", "IFC class, e.g. IfcWallType.", GH_ParamAccess.item);
            pManager.AddTextParameter("Type", "T", "Predefined type, or the element type when user defined.", GH_ParamAccess.item);
            pManager.AddMeshParameter("Geometry", "G", "Body geometry of the representation maps as meshes in type coordinates. Read solids are faceted.", GH_ParamAccess.list);
            pManager.AddColourParameter("Colour", "Col", "Surface colour of each mesh: its own style (IfcStyledItem), else the colour of the type's material; empty when neither is styled. Alpha holds the transparency.", GH_ParamAccess.list);
            pManager.AddParameter(new MaterialParam(), "Material", "Mat", "Material of the type (IfcRelAssociatesMaterial).", GH_ParamAccess.item);
            pManager.AddParameter(new PropertySetParam(), ComponentPropertySets.Name, ComponentPropertySets.NickName, ComponentPropertySets.OutputDescription, GH_ParamAccess.list);
            ComponentClassifications.RegisterOutput(this);
            ComponentAttributes.RegisterOutputs(this);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            ElementTypeGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            var type = goo.Value;
            SetCommonOutputs(DA, type);
            DA.SetData(3, type.IfcClass);
            DA.SetData(4, type.HasPredefinedType ? TypeOf(type) : type.ObjectType);
            DA.SetDataList(5, type.Geometry.Select(m => DocumentGeometry.ToRhino(m.WithColour(type.DisplayColour(m)))));
            DA.SetDataList(6, type.Geometry.Select(type.DisplayColour).Select(c => c == null ? null : new GH_Colour(DocumentGeometry.ToColor(c))));
            if (type.Material != null) DA.SetData(7, new MaterialGoo(type.Material));
            ComponentPropertySets.SetOutput(DA, 8, type);
            ComponentClassifications.SetOutput(this, DA, type);
            ComponentAttributes.SetOutputs(DA, 10, type.Tag, type.Attributes, type.IfcClass);

            if (type.SkippedGeometry.Count > 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Geometry not converted: " +
                    string.Join(", ", type.SkippedGeometry.GroupBy(s => s).Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key)));
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructElementType;

        public override Guid ComponentGuid => new Guid("a3e6b9d1-2c47-4f8a-b05e-7d93c1f4e268");
    }
}
