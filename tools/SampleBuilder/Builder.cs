using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Runtime.InProcess;

namespace SampleBuilder
{
    internal static class Builder
    {
        private static bool Verbose;

        /// <summary>Every sample, in order.</summary>
        private static IEnumerable<Sample> Samples => typeof(Sample).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Sample)) && !t.IsAbstract).Select(t => (Sample)Activator.CreateInstance(t)).OrderBy(s => s.Number);

        public static int Run(string[] args)
        {
            var root = FindRepositoryRoot();
            var output = Path.Combine(root, "samples");
            // IfcHopper is loaded as a package from its build output, like the Rhino 8 launch profile of the plugin.
            Environment.SetEnvironmentVariable("RHINO_PACKAGE_DIRS", Path.Combine(root, "IfcHopper", "bin", "Debug", "net8.0"));

            using (new RhinoCore(new[] { "/netcore", "/nosplash" }, WindowStyle.NoWindow))
            {
                var grasshopper = RhinoApp.GetPlugInObject("Grasshopper") as Grasshopper.Plugin.GH_RhinoScriptInterface
                    ?? throw new InvalidOperationException("Grasshopper could not be loaded.");
                grasshopper.RunHeadless();

                Verbose = args.Contains("--verbose");
                var filters = args.Where(a => !a.StartsWith("--")).ToList();
                var selected = Samples.Where(s => filters.Count == 0 || filters.Any(f => s.Number.ToString() == f.TrimStart('0') || s.Title.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                var find = Array.IndexOf(args, "--find");
                if (find >= 0 && find + 1 < args.Length)
                {
                    foreach (var proxy in Instances.ComponentServer.ObjectProxies.Where(p => !p.Obsolete && p.Desc.Name.IndexOf(args[find + 1], StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        var component = proxy.CreateInstance() as IGH_Component;
                        Console.WriteLine($"{proxy.Desc.Category}/{proxy.Desc.SubCategory}/{proxy.Desc.Name}" + (component == null ? "" :
                            $": {string.Join(", ", component.Params.Input.Select(p => p.Name))} -> {string.Join(", ", component.Params.Output.Select(p => p.Name))}"));
                    }
                    return 0;
                }
                if (args.Contains("--describe"))
                {
                    foreach (var proxy in Instances.ComponentServer.ObjectProxies.Where(p => p.Desc.Category == "IfcHopper").OrderBy(p => p.Desc.SubCategory))
                        if (proxy.CreateInstance() is IGH_Component component)
                            Console.WriteLine($"{proxy.Desc.Name}: {string.Join(", ", component.Params.Input.Select(p => p.Name))} -> {string.Join(", ", component.Params.Output.Select(p => p.Name))}");
                    return 0;
                }
                var docs = Array.IndexOf(args, "--docs");
                if (docs >= 0 && docs + 1 < args.Length)
                {
                    // Path of a clone of the documentation site; pages go to docs/components, icons to static/img/components.
                    var site = Path.GetFullPath(args[docs + 1]);
                    return Docs.Write(Path.Combine(site, "docs", "components"), Path.Combine(site, "static", "img", "components"), "/img/components");
                }
                if (args.Contains("--list"))
                {
                    foreach (var sample in selected) Console.WriteLine(sample.FileName);
                    return 0;
                }

                Directory.CreateDirectory(output);
                var failed = 0;
                foreach (var sample in selected)
                {
                    try
                    {
                        if (!Build(sample, Path.Combine(output, sample.FileName))) failed++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Console.WriteLine($"FAILED {sample.FileName}: {ex}");
                    }
                }
                Console.WriteLine(failed == 0 ? $"Built {selected.Count} samples in {output}." : $"{failed} of {selected.Count} samples failed.");
                return failed == 0 ? 0 : 1;
            }
        }

        /// <summary>Builds, solves and saves a sample; false when a component reports an error or an unexpected warning.</summary>
        private static bool Build(Sample sample, string path)
        {
            // Each sample is solved with an emptied Rhino document in metres (one headless document: only the first can become active).
            var rhinoDoc = RhinoDocument();
            if (sample.PrepareRhino(rhinoDoc))
            {
                var model = Path.ChangeExtension(path, ".3dm");
                if (!WriteAnonymous(rhinoDoc, model)) throw new IOException($"Could not save '{model}'.");
            }

            var canvas = new Canvas();
            sample.Build(canvas);
            var document = canvas.Document;
            // Relative paths in the sample resolve against its folder.
            document.FilePath = path;
            document.Enabled = true;
            document.NewSolution(true, GH_SolutionMode.Silent);

            var ok = true;
            Console.WriteLine($"{sample.FileName}: {document.ObjectCount} objects");
            foreach (var obj in document.ActiveObjects())
                foreach (var level in new[] { GH_RuntimeMessageLevel.Error, GH_RuntimeMessageLevel.Warning, GH_RuntimeMessageLevel.Remark })
                    foreach (var message in obj.RuntimeMessages(level))
                    {
                        var expected = sample.ExpectedMessages.Any(m => message.Contains(m));
                        // Remarks fail too unless expected, except the time stamp of Write IFC.
                        if (level == GH_RuntimeMessageLevel.Error || !expected && !message.StartsWith("Written ")) ok = false;
                        Console.WriteLine($"  {(expected ? "expected " : "")}{level} {obj.NickName}: {message}");
                    }

            if (Verbose)
                foreach (var component in document.Objects.OfType<IGH_Component>())
                    Console.WriteLine($"  {component.Name} '{component.NickName}': " +
                        string.Join(", ", component.Params.Output.Select(p => $"{p.Name}={p.VolatileDataCount}")));

            // Layout check: objects (not groups) must not overlap.
            var placed = document.Objects.Where(o => !(o is Grasshopper.Kernel.Special.GH_Group)).ToList();
            foreach (var obj in placed) obj.Attributes.PerformLayout();
            for (int i = 0; i < placed.Count; i++)
                for (int j = i + 1; j < placed.Count; j++)
                {
                    var a = placed[i].Attributes.Bounds;
                    var b = placed[j].Attributes.Bounds;
                    a.Inflate(-1, -1);
                    if (!a.IntersectsWith(b)) continue;
                    ok = false;
                    Console.WriteLine($"  Overlap: {placed[i].NickName} {Rect(placed[i])} and {placed[j].NickName} {Rect(placed[j])}");
                }

            foreach (var action in canvas.BeforeSave) action();
            var io = new GH_DocumentIO(document);
            if (!io.SaveQuiet(path)) throw new IOException($"Could not save '{path}'.");
            document.Enabled = false;
            document.Dispose();
            return ok;
        }

        private static RhinoDoc _rhinoDoc;

        /// <summary>The headless Rhino document, emptied of objects and block definitions, in metres.</summary>
        /// <summary>
        /// Saves the units, block definitions and objects of a document as a new 3dm file. Rhino stamps the Windows user
        /// on documents it saves, and openNURBS on files without a revision history; a new file with a revision carries none.
        /// </summary>
        private static bool WriteAnonymous(RhinoDoc doc, string path)
        {
            using (var file = new Rhino.FileIO.File3dm())
            {
                file.Settings.ModelUnitSystem = doc.ModelUnitSystem;
                file.Revision = 1;
                foreach (var definition in doc.InstanceDefinitions.Where(d => d != null && !d.IsDeleted))
                {
                    var objects = definition.GetObjects();
                    file.AllInstanceDefinitions.Add(definition.Name, definition.Description, Rhino.Geometry.Point3d.Origin,
                        objects.Select(o => o.Geometry), objects.Select(o => o.Attributes));
                }
                foreach (var obj in doc.Objects) file.Objects.Add(obj.Geometry, obj.Attributes);
                return file.Write(path, 8);
            }
        }

        private static RhinoDoc RhinoDocument()
        {
            if (_rhinoDoc == null)
            {
                _rhinoDoc = RhinoDoc.CreateHeadless(null);
                RhinoDoc.ActiveDoc = _rhinoDoc;
            }
            foreach (var obj in _rhinoDoc.Objects.ToList()) _rhinoDoc.Objects.Delete(obj, true);
            foreach (var definition in _rhinoDoc.InstanceDefinitions.Where(d => d != null && !d.IsDeleted).ToList())
                _rhinoDoc.InstanceDefinitions.Delete(definition.Index, true, true);
            _rhinoDoc.InstanceDefinitions.Compact(true);
            _rhinoDoc.ModelUnitSystem = UnitSystem.Meters;
            return _rhinoDoc;
        }
        private static string Rect(IGH_DocumentObject obj)
        {
            var b = obj.Attributes.Bounds;
            return $"[{b.X:0},{b.Y:0} {b.Width:0}×{b.Height:0}]";
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "IfcHopper.sln"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("IfcHopper.sln not found above the builder.");
        }
    }
}
