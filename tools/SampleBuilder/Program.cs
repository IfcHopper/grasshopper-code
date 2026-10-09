using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace SampleBuilder
{
    /// <summary>
    /// Builds the sample definitions with Rhino 8 running inside this process (Rhino.Inside): Grasshopper runs headless and loads IfcHopper
    /// from its build output, each sample is built in code, solved (errors and warnings are reported) and saved as .gh.
    /// Usage: SampleBuilder [--list] [sample number or name filter ...]
    /// </summary>
    internal static class Program
    {
        internal static readonly string RhinoSystem = Environment.GetEnvironmentVariable("RHINO_SYSTEM_DIR") ?? @"C:\Program Files\Rhino 8\System";

        [STAThread]
        private static int Main(string[] args)
        {
            if (!File.Exists(Path.Combine(RhinoSystem, "Rhino.exe")))
            {
                Console.Error.WriteLine($"Rhino 8 not found in '{RhinoSystem}'; set RHINO_SYSTEM_DIR.");
                return 2;
            }
            Environment.SetEnvironmentVariable("PATH", RhinoSystem + ";" + Environment.GetEnvironmentVariable("PATH"));
            AssemblyLoadContext.Default.Resolving += ResolveFromRhino;
            return Run(args);
        }

        /// <summary>Loads Rhino's assemblies (RhinoCommon, Grasshopper, Eto, ...) from the installation.</summary>
        private static Assembly ResolveFromRhino(AssemblyLoadContext context, AssemblyName name)
        {
            var folders = new[] { Path.Combine(RhinoSystem, "netcore"), RhinoSystem, Path.Combine(Path.GetDirectoryName(RhinoSystem), "Plug-ins", "Grasshopper") };
            var file = folders.Select(f => Path.Combine(f, name.Name + ".dll")).FirstOrDefault(File.Exists);
            return file == null ? null : context.LoadFromAssemblyPath(file);
        }

        // Kept out of Main so that RhinoCommon types are only resolved once the resolver is installed.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Run(string[] args) => Builder.Run(args);
    }
}
