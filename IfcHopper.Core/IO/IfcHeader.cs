using System.Collections.Generic;

namespace IfcHopper.Core.IO
{
    /// <summary>
    /// STEP header of an IFC file (FILE_DESCRIPTION, FILE_NAME, FILE_SCHEMA) plus the project name.
    /// </summary>
    public class IfcHeader
    {
        // FILE_DESCRIPTION
        public IReadOnlyList<string> Description { get; set; } = new List<string>();
        public string ImplementationLevel { get; set; }

        // FILE_NAME
        public string Name { get; set; }
        public string TimeStamp { get; set; }
        public IReadOnlyList<string> Author { get; set; } = new List<string>();
        public IReadOnlyList<string> Organization { get; set; } = new List<string>();
        public string PreprocessorVersion { get; set; }
        public string OriginatingSystem { get; set; }
        public string Authorization { get; set; }

        // FILE_SCHEMA
        public IReadOnlyList<string> Schemas { get; set; } = new List<string>();

        /// <summary>Name of the IfcProject, null if the file has none.</summary>
        public string ProjectName { get; set; }
    }
}
