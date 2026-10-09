using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    /// <summary>Optional Placement input of spatial components and objects (world plane in document units).</summary>
    internal static class ComponentPlacement
    {
        public const string Name = "Placement";
        public const string NickName = "Pl";
        public const string Description = "World placement plane. Empty: the placement of the parent.";
        public const string ModifyDescription = "New world placement plane. Unconnected: unchanged.";

        /// <summary>Adds the Placement input; call from RegisterInputParams.</summary>
        public static void RegisterInput(GH_Component component, string description) =>
            component.Params.RegisterInputParam(new PlacementParam { Name = Name, NickName = NickName, Description = description, Access = GH_ParamAccess.item });

        /// <summary>The placement at <paramref name="index"/>, or null when the input is empty.</summary>
        public static Placement Read(IGH_DataAccess DA, int index)
        {
            PlacementGoo goo = null;
            return DA.GetData(index, ref goo) && goo != null && goo.IsValid ? DocumentUnits.ToPlacement(goo.Value) : null;
        }
    }
}
