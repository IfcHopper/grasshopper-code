using System;
using System.IO;

namespace IfcHopper.Core.IO
{
    public static class IfcFilePath
    {
        public const string Extension = ".ifc";
        public const string DefaultFileName = "IfcHopperFile" + Extension;

        /// <summary>%USERPROFILE%/IfcHopper</summary>
        public static string DefaultDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "IfcHopper");

        /// <summary>
        /// Full path from a file name and directory, using defaults for empty values
        /// and appending ".ifc" when missing.
        /// </summary>
        public static string Resolve(string fileName, string directory)
        {
            if (string.IsNullOrWhiteSpace(fileName)) fileName = DefaultFileName;
            if (string.IsNullOrWhiteSpace(directory)) directory = DefaultDirectory;

            fileName = fileName.Trim();
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException($"Invalid file name: '{fileName}'.", nameof(fileName));
            if (!fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                fileName += Extension;

            return Path.GetFullPath(Path.Combine(directory.Trim(), fileName));
        }
    }
}
