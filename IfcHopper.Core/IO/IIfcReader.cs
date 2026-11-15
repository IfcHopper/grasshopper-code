using System.Collections.Generic;
using IfcHopper.Core.Model;

namespace IfcHopper.Core.IO
{
    /// <summary>Backend-neutral IFC reader.</summary>
    public interface IIfcReader
    {
        IfcHeader ReadHeader(string path);

        /// <summary>
        /// Reads a model. Only the project is converted; children are loaded on first access,
        /// and each object keeps a link to its source entity for in-place writing.
        /// </summary>
        IfcHopperModel Read(string path);

        /// <summary>
        /// GlobalIds of the ancestors of an object read from a file, from the project down to its parent,
        /// or null when the object was not read from a file.
        /// </summary>
        IReadOnlyList<string> GetAncestorIds(ModelObject obj);

        /// <summary>Explains source-file features that prevent translating every product safely in place.</summary>
        /// <param name="model">Model whose source is inspected; authored models need no source validation.</param>
        /// <returns>Empty when supported, otherwise actionable limitations. No objects or geometry are converted or changed.</returns>
        /// <remarks>Backends must opt into source translation; this prevents silently moving only the mapped portion of a file.</remarks>
        IReadOnlyList<string> GetTranslationLimitations(IfcHopperModel model) => model.Project.Source == null
            ? System.Array.Empty<string>() : new[] { "This backend has not implemented whole-model translation validation." };
    }
}
