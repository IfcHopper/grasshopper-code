using System.IO;
using Grasshopper.Kernel;

namespace IfcHopper.Components
{
    /// <summary>Resolves file paths given relative to the folder of the Grasshopper definition, so definitions can ship with their files.</summary>
    internal static class DocumentPaths
    {
        public const string Note = " A relative path is taken from the folder of the saved definition.";

        /// <summary>
        /// The full path of <paramref name="path"/>: unchanged when rooted, else relative to the folder of the component's saved definition.
        /// Null (with an error) for a relative path in an unsaved definition.
        /// </summary>
        public static string Resolve(GH_Component component, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return path;
            var definition = component.OnPingDocument()?.FilePath;
            if (string.IsNullOrEmpty(definition))
            {
                component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"'{path}' is relative: save the definition first, or give a full path.");
                return null;
            }
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(definition), path));
        }
    }
}
