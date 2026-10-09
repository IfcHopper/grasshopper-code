using Grasshopper.Kernel;
using IfcHopper.Core.Model;

namespace IfcHopper.Components
{
    /// <summary>Base for deconstruct components: Name, Description and GlobalId outputs at indices 0-2.</summary>
    public abstract class DeconstructComponentBase : GH_Component
    {
        protected DeconstructComponentBase(string name, string nickname, string description, string subCategory)
          : base(name, nickname, description, ComponentCategory.Tab, subCategory)
        {
        }

        protected static void RegisterCommonOutputs(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Name.", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "Description.", GH_ParamAccess.item);
            pManager.AddTextParameter("GlobalId", "Id", "IFC GlobalId. Empty for objects not yet written.", GH_ParamAccess.item);
        }

        protected static void SetCommonOutputs(IGH_DataAccess DA, ModelObject obj)
        {
            DA.SetData(0, obj.Name);
            DA.SetData(1, obj.Description);
            DA.SetData(2, obj.GlobalId);
        }

        protected static void RegisterPlacementOutput(GH_OutputParamManager pManager) =>
            pManager.AddPlaneParameter("Placement", "Pl", "World placement plane in document units. Empty when unknown.", GH_ParamAccess.item);

        protected static void SetPlacement(IGH_DataAccess DA, int index, ModelObject obj)
        {
            if (obj.Placement != null) DA.SetData(index, DocumentUnits.ToPlane(obj.Placement));
        }

        /// <summary>Predefined type, or the object type when the predefined type is USERDEFINED.</summary>
        protected static string TypeOf(TypedModelObject obj) =>
            obj.PredefinedType == TypedModelObject.UserDefined ? obj.ObjectType : obj.PredefinedType;
    }
}
