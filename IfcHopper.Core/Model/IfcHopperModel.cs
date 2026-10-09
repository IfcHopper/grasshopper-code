using System.Collections.Generic;
using System.Linq;

namespace IfcHopper.Core.Model
{
    /// <summary>
    /// Root of the backend-neutral IfcHopper model: Model > Project > children (sites, facilities, storeys, facility parts,
    /// spaces and elements, nested as allowed by <see cref="SpatialRules"/>).
    /// Built just before writing and converted to IFC by an <see cref="IO.IIfcWriter"/>.
    /// All lengths are in metres.
    /// </summary>
    public class IfcHopperModel
    {
        public Project Project { get; set; }

        /// <summary>File the model was read from; null for models created in Grasshopper.</summary>
        public string SourcePath { get; internal set; }

        /// <summary>Written to the FILE_NAME author of the IFC header.</summary>
        public string Author { get; set; }

        /// <summary>Written to the FILE_NAME organization of the IFC header.</summary>
        public string Organization { get; set; }

        /// <summary>
        /// Edited types read from the model's file (Apply Edits), written back onto the file's types by GlobalId, so they change for all
        /// their occurrences. Types used by the model's objects are written as well and need not be listed.
        /// </summary>
        public List<ElementType> Types { get; } = new List<ElementType>();

        /// <summary>
        /// Edited materials and material sets read from the model's file (Apply Edits), written back onto the file's materials, so they
        /// change for every element using them. Edited materials of the model's objects are written as well and need not be listed.
        /// </summary>
        public List<MaterialDefinition> Materials { get; } = new List<MaterialDefinition>();

        public IfcHopperModel(Project project = null)
        {
            Project = project ?? new Project();
        }

        /// <summary>
        /// True when the project was created in Grasshopper but contains objects read from a file: the model is written as a
        /// new file, so data of those objects that IfcHopper does not map is not copied. Only loaded child lists are checked.
        /// </summary>
        public bool RebuildsReadObjects => Project != null && Project.Source == null && LoadedDescendants(Project).Any(o => o.Source != null);

        private static IEnumerable<ModelObject> LoadedDescendants(ModelObject parent)
        {
            if (!parent.ChildrenLoaded) yield break;
            foreach (var child in parent.Children)
            {
                yield return child;
                foreach (var descendant in LoadedDescendants(child)) yield return descendant;
            }
        }

        /// <summary>Model with one default Site, Building and Storey.</summary>
        public static IfcHopperModel CreateDefault()
        {
            var storey = new Storey();
            var building = new Building();
            building.Storeys.Add(storey);
            var site = new Site();
            site.Facilities.Add(building);
            var project = new Project();
            project.Sites.Add(site);
            return new IfcHopperModel(project);
        }
    }
}
