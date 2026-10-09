using System;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    /// <summary>Material input of object and type components.</summary>
    internal static class ComponentMaterial
    {
        public const string Name = "Material";
        public const string NickName = "Mat";
        public const string Description =
            "Material of the object (IfcRelAssociatesMaterial): a material, layer set or constituent set. Layer sets are written with a layer set " +
            "usage (along the thickness of slabs, plates, coverings and roofs, across that of other elements). Empty: the material of its type.";
        public const string ModifyDescription =
            "New material of the object (a material, layer set or constituent set). An edited material read from the file changes for every " +
            "element using it. Connected but empty: removes the material, so the object shows its type's. Unconnected: unchanged.";
        public const string TypeDescription = "Material of the type (IfcRelAssociatesMaterial), shown by its objects without one of their own.";
        public const string ModifyTypeDescription = "New material of the type. Connected but empty: removes the material. Unconnected: unchanged.";

        /// <summary>Sets the material at <paramref name="index"/> with <paramref name="set"/>; does nothing when the input is empty.</summary>
        public static void Apply(IGH_DataAccess DA, int index, Action<MaterialDefinition> set)
        {
            MaterialGoo material = null;
            if (DA.GetData(index, ref material) && material != null && material.IsValid) set(material.Value);
        }

        /// <summary>
        /// For modify components: sets the material at <paramref name="index"/>; an empty item on a connected input removes the material
        /// (<paramref name="set"/> with null). Does nothing when the input is not connected.
        /// </summary>
        public static void Modify(GH_Component component, IGH_DataAccess DA, int index, Action<MaterialDefinition> set)
        {
            if (component.Params.Input[index].SourceCount == 0) return;
            MaterialGoo material = null;
            set(DA.GetData(index, ref material) && material != null && material.IsValid ? material.Value : null);
        }
    }
}
