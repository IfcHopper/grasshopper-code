using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace IfcHopper.Components
{
    public class ClassColoursComponent : IfcComponent
    {
        public ClassColoursComponent()
            : base("Class Colours", "ClassCol",
                "One display colour per IFC object, based on its exact IFC class. Connect geometry and these Colours to native Custom Preview. " +
                "Automatic colours stay stable across solves and reordering. Objects, IFC styles and default preview are unchanged.",
                ComponentCategory.Tab, ComponentCategory.Utilities) { }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Objects", "O", "IFC objects to colour, including spatial objects and element types. Tree paths, order and empty positions are preserved; unsupported/null items produce empty colours.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Classes", "C", "Exact IFC classes to override, paired by index with Colours. Ignore case; no subclass expansion. * sets the colour for all other classes. Exact overrides win over *. Empty: automatic colours for every class.", GH_ParamAccess.list);
            pManager.AddColourParameter("Colours", "Col", "One colour per override Class (including *); alpha/transparency is preserved. Classes with no exact or * override keep automatic colours.", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager) =>
            pManager.AddColourParameter("Colours", "Col", "One colour per input object, with the same tree paths and ordering. Empty entries mark unsupported/null objects. Wire to native Custom Preview's Material input.", GH_ParamAccess.tree);

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> objects)) return;
            var classes = new List<string>();
            var colourGoo = new List<GH_Colour>();
            DA.GetDataList(1, classes);
            DA.GetDataList(2, colourGoo);
            var colours = new List<Colour>();
            foreach (var goo in colourGoo)
                colours.Add(goo == null || !goo.IsValid || goo.Value.IsEmpty ? null : DocumentGeometry.ToColour(goo.Value));
            ClassColourPalette palette;
            try { palette = new ClassColourPalette(classes, colours); }
            catch (ArgumentException ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                return;
            }
            var result = new GH_Structure<GH_Colour>();
            for (int branch = 0; branch < objects.PathCount; branch++)
            {
                var path = objects.Paths[branch];
                result.EnsurePath(path);
                for (int index = 0; index < objects.Branches[branch].Count; index++)
                {
                    var goo = objects.Branches[branch][index];
                    string ifcClass = goo?.ScriptVariable() switch
                    {
                        ElementType type => type.IfcClass,
                        ModelObject obj => SpatialRules.IfcClassOf(obj),
                        _ => null
                    };
                    GH_Colour colour = null;
                    try
                    {
                        if (ifcClass == null) throw new ArgumentException("Expected an IFC object or element type.");
                        colour = new GH_Colour(DocumentGeometry.ToColor(palette.GetColour(ifcClass)));
                    }
                    catch (ArgumentException ex)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Objects {path}[{index}]: {ex.Message} Colour left empty to preserve correspondence.");
                    }
                    result.Append(colour, path);
                }
            }
            DA.SetDataTree(0, result);
        }

        protected override Bitmap Icon => Properties.Resources.ClassColours;
        public override Guid ComponentGuid => new Guid("43915dc4-91d5-4978-b5e0-5f28b737fe2e");
    }
}
