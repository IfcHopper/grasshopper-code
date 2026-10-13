using System;
using System.IO;

namespace IfcHopper.Tests
{
    /// <summary>Paths for test output and public specification fixtures.</summary>
    internal static class TestFiles
    {
        public static readonly string RepositoryRoot = FindRepositoryRoot();

        public static string PublicFixture(string fileName)
        {
            var path = Path.Combine(RepositoryRoot, "IfcHopper.Tests", "Fixtures", "cache", fileName);
            if (!File.Exists(path))
                throw new FileNotFoundException("Fetch public fixtures first: powershell -File tools/FetchTestFixtures.ps1", path);
            return path;
        }

        /// <summary>A unique path in a fresh temporary folder.</summary>
        public static string NewPath(string fileName = "test.ifc")
        {
            var directory = Path.Combine(Path.GetTempPath(), "IfcHopper.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "IfcHopper.sln")))
                directory = directory.Parent;
            return directory?.FullName ?? AppContext.BaseDirectory;
        }
    }

}
