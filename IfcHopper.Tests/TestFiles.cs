using System;
using System.IO;
using Xunit;

namespace IfcHopper.Tests
{
    /// <summary>Paths for test output and the local (not committed) sample models.</summary>
    internal static class TestFiles
    {
        public static readonly string RepositoryRoot = FindRepositoryRoot();

        public static readonly string BridgeSample = Sample("03_Bridge_BEST_IFC4x3.ifc");

        /// <summary>Alignment-based IFC4X3 bridge (linear placements, sectioned solids).</summary>
        public static readonly string AcernoSample = Sample("Viadotto Acerno.ifc");

        public static string Sample(string fileName) => Path.Combine(RepositoryRoot, "SampleModels", fileName);

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

    /// <summary>Fact that is skipped when a local sample model is not available (SampleModels is gitignored).</summary>
    public sealed class SampleModelFactAttribute : FactAttribute
    {
        /// <param name="fileName">Sample file the test needs; the bridge sample by default.</param>
        public SampleModelFactAttribute(string fileName = "03_Bridge_BEST_IFC4x3.ifc")
        {
            if (!File.Exists(TestFiles.Sample(fileName))) Skip = $"Sample model '{fileName}' not available.";
        }
    }
}
