using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    /// <summary>Converts between model objects and their Grasshopper data, for components taking or giving objects of any kind.</summary>
    internal static class ComponentObjects
    {
        public const string RootDescription = "IfcHopper model, or a project, site, facility, part, storey, space or object to search below.";

        /// <summary>The object to start from: the project of a model, or the object itself; null with an error for other data.</summary>
        public static ModelObject Root(GH_Component component, IGH_Goo goo, out IfcHopperModel model)
        {
            model = null;
            switch (goo?.ScriptVariable())
            {
                case IfcHopperModel m:
                    model = m;
                    return m.Project;
                case ModelObject obj:
                    return obj;
                default:
                    component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"{goo?.TypeName ?? "Null"} is not an IfcHopper model or object.");
                    return null;
            }
        }

        /// <summary>The Grasshopper data of a model object, e.g. a StoreyGoo for a storey.</summary>
        public static IGH_Goo Wrap(ModelObject obj)
        {
            switch (obj)
            {
                case Project project: return new ProjectGoo(project);
                case Site site: return new SiteGoo(site);
                case Facility facility: return new FacilityGoo(facility);
                case Storey storey: return new StoreyGoo(storey);
                case FacilityPart part: return new FacilityPartGoo(part);
                case Space space: return new SpaceGoo(space);
                case Element element: return new ElementGoo(element);
                case ElementType type: return new ElementTypeGoo(type);
                default: return obj == null ? null : new GH_ObjectWrapper(obj);
            }
        }
    }
}
