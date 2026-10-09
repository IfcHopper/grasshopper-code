using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    /// <summary>
    /// Base for modify components: they output an edited copy that keeps the GlobalId and source of the input object.
    /// Unconnected inputs keep the original value. A connected child list replaces the children; children left out are
    /// deleted when the model is written back to its source file.
    /// </summary>
    public abstract class ModifyComponentBase : GH_Component
    {
        protected const string ChildListNote = " Connected: replaces the list; objects left out are deleted on write. Unconnected: unchanged.";

        protected ModifyComponentBase(string name, string nickname, string description, string subCategory)
          : base(name, nickname, description, ComponentCategory.Tab, subCategory)
        {
        }

        protected static void RegisterCommonInputs(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "New name. Unconnected: unchanged.", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "New description. Unconnected: unchanged.", GH_ParamAccess.item);
        }

        /// <summary>Applies Name and Description from inputs 1 and 2. Returns false (with an error) for an empty name.</summary>
        protected bool ApplyCommon(IGH_DataAccess DA, ModelObject obj)
        {
            string name = null, description = null;
            if (DA.GetData(1, ref name))
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name cannot be empty.");
                    return false;
                }
                obj.Name = name.Trim();
            }
            if (DA.GetData(2, ref description)) obj.Description = description;
            return true;
        }

        protected void ApplyType(IGH_DataAccess DA, int index, TypedModelObject obj)
        {
            string type = null;
            if (!DA.GetData(index, ref type)) return;
            if (!obj.HasPredefinedType)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "This object has no predefined type; Type is ignored.");
            else if (!obj.SetType(type))
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"'{type}' is not a predefined type; written as USERDEFINED.");
        }

        protected static void ApplyPlacement(IGH_DataAccess DA, int index, ModelObject obj)
        {
            var placement = ComponentPlacement.Read(DA, index);
            if (placement != null) obj.Placement = placement;
        }

        /// <summary>Replaces <paramref name="target"/> (e.g. a typed view of the children) with the input list when the input is connected.</summary>
        protected bool ReplaceList<TModel>(IGH_DataAccess DA, int index, IList<TModel> target) where TModel : class
        {
            if (Params.Input[index].SourceCount == 0) return false;
            var items = new List<IfcHopperGoo<TModel>>();
            DA.GetDataList(index, items);
            target.Clear();
            foreach (var item in items.Where(i => i != null && i.IsValid)) target.Add(item.Value);
            return true;
        }
    }
}
