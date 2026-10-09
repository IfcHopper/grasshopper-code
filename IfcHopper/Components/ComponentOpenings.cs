using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    /// <summary>Openings input of Object and Modify Object.</summary>
    internal static class ComponentOpenings
    {
        public const string Name = "Openings";
        public const string NickName = "Op";
        public const string Description = "Openings and recesses voiding the object (Opening component, IfcRelVoidsElement). Its geometry stays uncut in the file.";
        public const string ModifyDescription = "Openings voiding the object. Connected: replaces the list (empty removes all openings). Unconnected: unchanged.";

        /// <summary>The openings at <paramref name="index"/>; other elements are skipped with a warning.</summary>
        public static List<Opening> Read(GH_Component component, IGH_DataAccess DA, int index)
        {
            var items = new List<ElementGoo>();
            DA.GetDataList(index, items);
            var valid = items.Where(i => i != null && i.IsValid).Select(i => i.Value).ToList();
            foreach (var other in valid.Where(e => !(e is Opening)))
                component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"'{other.Name}' ({other.IfcClass}) is no opening; use the Opening component.");
            return valid.OfType<Opening>().ToList();
        }
    }
}
