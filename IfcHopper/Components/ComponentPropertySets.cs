using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    /// <summary>Property Sets input and output of object and spatial components.</summary>
    internal static class ComponentPropertySets
    {
        public const string Name = "Property Sets";
        public const string NickName = "PS";
        public const string Description = "Property and quantity sets (Pset and Qto components). Sets with the same name: the last one is kept.";
        public const string ModifyDescription =
            "Property and quantity sets put in place of the sets with the same name, or added; a set without properties deletes the set with its name. Unconnected: unchanged.";
        public const string OutputDescription = "Property and quantity sets. Sets of the type are included unless the object has its own set with the same name (From Type on Deconstruct Pset and Deconstruct Qto).";

        /// <summary>Merges the sets of the input at <paramref name="index"/> into the object; does nothing when the input is not connected.</summary>
        public static void Apply(GH_Component component, IGH_DataAccess DA, int index, ModelObject obj)
        {
            if (component.Params.Input[index].SourceCount == 0) return;
            var sets = new List<PropertySetGoo>();
            DA.GetDataList(index, sets);
            obj.MergePropertySets(sets.Where(s => s != null && s.IsValid).Select(s => s.Value));
        }

        public const string MaterialDescription = "Property sets of the material (IfcMaterialProperties, IFC4 and later), e.g. Pset_MaterialCommon. Sets with the same name: the last one is kept.";
        public const string ModifyMaterialDescription =
            "Property sets put in place of the material's sets with the same name, or added; a set without properties deletes the set with its name. Unconnected: unchanged.";

        /// <summary>Merges the property sets at <paramref name="index"/> into a material; quantity sets are skipped with a warning. Nothing when not connected.</summary>
        public static void ApplyToMaterial(GH_Component component, IGH_DataAccess DA, int index, MaterialDefinition material)
        {
            if (component.Params.Input[index].SourceCount == 0) return;
            var sets = new List<PropertySetGoo>();
            DA.GetDataList(index, sets);
            foreach (var set in sets.Where(s => s != null && s.IsValid && !(s.Value is PropertySet)))
                component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{set.Value.Name} is a quantity set; materials only take property sets.");
            material.MergePropertySets(sets.Where(s => s != null && s.IsValid).Select(s => s.Value).OfType<PropertySet>());
        }

        public static void SetOutput(IGH_DataAccess DA, int index, ModelObject obj) =>
            DA.SetDataList(index, obj.PropertySets.Select(s => new PropertySetGoo(s)));
    }
}
