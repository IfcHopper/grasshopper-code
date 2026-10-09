using Grasshopper.Kernel;
using IfcHopper.Core.Model;

namespace IfcHopper.Components
{
    /// <summary>Optional GlobalId input of the create components.</summary>
    internal static class ComponentGlobalId
    {
        public const string Name = "GlobalId";
        public const string NickName = "Id";
        public const string Description =
            "IFC GlobalId (22 characters) or GUID. Empty: a stable id derived from this component and the data branch/item, " +
            "so writing again keeps the same ids.";

        /// <summary>
        /// Reads the GlobalId input at <paramref name="index"/>; when empty, <paramref name="fallback"/> or a stable id derived from the component
        /// instance and solve iteration.
        /// Returns null and adds an error when the input is not a valid id.
        /// </summary>
        public static string Resolve(GH_Component component, IGH_DataAccess DA, int index, string fallback = null)
        {
            string text = null;
            if (!DA.GetData(index, ref text) || string.IsNullOrWhiteSpace(text))
                return fallback ?? GlobalIds.FromSeed($"{component.InstanceGuid:N}/{DA.Iteration}");

            if (GlobalIds.TryParse(text, out var globalId)) return globalId;
            component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"'{text}' is not a valid GlobalId (22 IFC characters or a GUID).");
            return null;
        }
    }
}
