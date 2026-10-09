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
    public class ApplyEditsComponent : GH_Component
    {
        public ApplyEditsComponent()
          : base("Apply Edits", "Apply",
              "Puts edited objects (from Modify components) back into a model by GlobalId, so their parents do not have to be rebuilt.",
              ComponentCategory.Tab, ComponentCategory.File)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new ModelParam(), "Model", "M", "IfcHopper model read from a file.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Edits", "E", "Edited projects, sites, facilities, parts, storeys, objects, element types or materials (types and materials are written onto the file's, for everything using them).", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new ModelParam(), "Model", "M", "IfcHopper model with the edits applied.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            ModelGoo model = null;
            var items = new List<IGH_Goo>();
            if (!DA.GetData(0, ref model) || !model.IsValid) return;
            DA.GetDataList(1, items);

            var edits = new List<ModelObject>();
            var materials = new List<MaterialDefinition>();
            foreach (var item in items.Where(i => i != null))
            {
                var value = item.ScriptVariable();
                if (value is ModelObject edit) edits.Add(edit);
                else if (value is MaterialDefinition material) materials.Add(material);
                else AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{item.TypeName} is not an IfcHopper object and was skipped.");
            }

            var result = ModelEdits.Apply(model.Value, edits, out var notFound);
            result = ModelEdits.ApplyMaterials(result, materials, out var materialsNotFound);
            foreach (var name in notFound.Select(e => e.Name).Concat(materialsNotFound.Select(m => m.Name)))
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"'{name}' was not found in the model (only objects read from its file can be applied).");

            DA.SetData(0, new ModelGoo(result));
        }

        protected override Bitmap Icon => Properties.Resources.ApplyEdits;

        public override Guid ComponentGuid => new Guid("f2c86b1e-9d47-4a3b-b5e0-1c7d4a9e8f36");
    }
}
