using System.Collections.Generic;
using IfcHopper.Core.Model;

namespace IfcHopper.Core.IO
{
    /// <summary>Backend-neutral IFC writer.</summary>
    public interface IIfcWriter
    {
        /// <summary>
        /// Writes the model. <paramref name="schema"/> defaults to IFC4X3 for new models and to the source schema for models read
        /// from a file; a read model written in another schema is rebuilt as a new file.
        /// Returns warnings about data that was downgraded or not saved because the schema does not support it.
        /// </summary>
        IReadOnlyList<string> Write(IfcHopperModel model, string path, IfcSchema? schema = null);
    }
}
