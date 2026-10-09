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
    }
}
