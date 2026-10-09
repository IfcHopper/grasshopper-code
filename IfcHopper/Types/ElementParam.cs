using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using IfcHopper.Components;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace IfcHopper.Types
{
    /// <summary>Element parameter; previews and bakes the body geometry of its elements, also as a component output.</summary>
    public class ElementParam : GH_Param<ElementGoo>, IGH_PreviewObject, IGH_BakeAwareObject
    {
        public ElementParam()
          : base("Element", "E", "IfcHopper element.", ComponentCategory.Tab, ComponentCategory.Params, GH_ParamAccess.item)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public bool Hidden { get; set; }

        public bool IsPreviewCapable => true;

        public BoundingBox ClippingBox => Preview_ComputeClippingBox();

        /// <summary>Unselected elements are drawn in their IFC colours; selected ones in the Grasshopper selection colour.</summary>
        public void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            var selected = Attributes.GetTopLevel.Selected;
            var material = selected ? args.ShadeMaterial_Selected : args.ShadeMaterial;
            foreach (var goo in m_data.AllData(true).OfType<ElementGoo>()) goo.DrawShaded(args.Display, material, !selected);
        }

        public void DrawViewportWires(IGH_PreviewArgs args) => Preview_DrawWires(args);

        public bool IsBakeCapable => !m_data.IsEmpty;

        public void BakeGeometry(RhinoDoc doc, List<Guid> obj_ids) => BakeGeometry(doc, null, obj_ids);

        /// <summary>Bakes elements that place their type's geometry as block instances; off bakes every element as a mesh.</summary>
        public bool BakeAsBlocks { get; set; } = true;

        public void BakeGeometry(RhinoDoc doc, ObjectAttributes att, List<Guid> obj_ids)
        {
            foreach (var goo in m_data.AllData(true))
                if (goo is ElementGoo element && element.BakeGeometry(doc, att, BakeAsBlocks, out var id)) obj_ids.Add(id);
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Menu_AppendItem(menu, "Bake as blocks", (s, e) =>
            {
                RecordUndoEvent("Bake as blocks");
                BakeAsBlocks = !BakeAsBlocks;
            }, true, BakeAsBlocks).ToolTipText = "Bake elements that place their type's geometry as instances of a block definition per type.";
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean("Hidden", Hidden);
            writer.SetBoolean("BakeAsBlocks", BakeAsBlocks);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            Hidden = reader.ItemExists("Hidden") && reader.GetBoolean("Hidden");
            BakeAsBlocks = !reader.ItemExists("BakeAsBlocks") || reader.GetBoolean("BakeAsBlocks");
            return base.Read(reader);
        }

        protected override Bitmap Icon => Properties.Resources.ElementParam;

        public override Guid ComponentGuid => new Guid("b7d4e2a9-5f1c-4836-9a0e-c3f8d6b1e275");
    }
}
