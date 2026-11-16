using System;
using System.Linq;
using System.IO;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;

namespace SampleBuilder
{
    internal static class RuntimeMessageChecks
    {
        public static void Run()
        {
            var canvas = new Canvas();
            using (canvas.Document)
            {
                var element = canvas.Ifc("Object", 0, 0);
                var site = canvas.Ifc("Site", 0, 0);
                var project = canvas.Ifc("Project", 0, 0);
                var source = canvas.Add(new Param_GenericObject(), 0, 0);
                var deconstruct = (GH_Component)canvas.Ifc("Deconstruct Object", 300, 0);
                canvas.Wire(source, deconstruct, "Object");
                var material = (GH_Component)canvas.Ifc("Material", 300, 200);
                canvas.Document.Enabled = true;

                for (int pass = 0; pass < 4; pass++)
                {
                    source.RemoveAllSources();
                    if (pass != 2) source.AddSource(Canvas.Output(element));
                    if (pass == 1 || pass == 2)
                    {
                        source.AddSource(Canvas.Output(site));
                        source.AddSource(Canvas.Output(project));
                    }
                    Canvas.Set(material, "Name", pass == 1 ? new object[] { "Valid material", " " }
                        : pass == 2 ? new object[] { " " } : new object[] { "Valid material" });
                    canvas.Document.NewSolution(true, GH_SolutionMode.Silent);
                    Check(deconstruct, pass, "typed conversion");
                    Check(material, pass, "iteration validation");
                }

                // A path is published before writing. It must not downgrade a failed file operation.
                var model = canvas.Ifc("Model", 0, 0);
                canvas.Wire(project, model, "Project");
                var write = (GH_Component)canvas.Ifc("Write IFC", 0, 0);
                canvas.Wire(model, write, "Model");
                var blockedDirectory = Path.GetTempFileName();
                try
                {
                    Canvas.Set(write, "Directory", blockedDirectory);
                    Canvas.Set(write, "Write", true);
                    canvas.Document.NewSolution(true, GH_SolutionMode.Silent);
                    if (write.RuntimeMessageLevel != GH_RuntimeMessageLevel.Error
                        || write.RuntimeMessages(GH_RuntimeMessageLevel.Error).Count == 0)
                        throw new InvalidOperationException("Failed IFC write must retain its error despite its Path output.");
                }
                finally { File.Delete(blockedDirectory); }

                var clean = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ifc");
                var invalid = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ifc");
                try
                {
                    IfcHopper.Core.IO.IfcBackend.Writer.Write(
                        new IfcHopper.Core.Model.IfcHopperModel(new IfcHopper.Core.Model.Project()), clean);
                    var text = File.ReadAllText(clean);
                    File.WriteAllText(invalid, text.Insert(text.LastIndexOf("ENDSEC;", StringComparison.Ordinal),
                        "#900001=IFCSTRUCTURALMEMBER('0000000000000000000001',$,'Invalid',$,$,$,$);\n"));
                    var read = (GH_Component)canvas.Ifc("Read IFC", 0, 0);
                    for (int pass = 0; pass < 3; pass++)
                    {
                        Canvas.Set(read, "Path", pass < 2 ? invalid : clean);
                        canvas.Document.NewSolution(true, GH_SolutionMode.Silent);
                        var warnings = read.RuntimeMessages(GH_RuntimeMessageLevel.Warning);
                        if (!read.Params.Output[0].VolatileData.AllData(true).Any(v => v != null && v.IsValid)
                            || (pass < 2 ? !warnings.Any(w => w.Contains("IfcStructuralMember: 1")) : warnings.Count != 0)
                            || read.RuntimeMessages(GH_RuntimeMessageLevel.Error).Count != 0)
                            throw new InvalidOperationException($"Read IFC omission warning or recovery failed on pass {pass}.");
                    }
                }
                finally { File.Delete(clean); File.Delete(invalid); }
            }
            Console.WriteLine("Runtime message checks passed: valid/mixed/invalid/recovered input conversions and solve iterations.");
        }

        private static void Check(GH_Component component, int pass, string scenario)
        {
            bool output = component.Params.Output.Any(p => p.VolatileData.AllData(true).Any(item => item != null && item.IsValid));
            bool errors = component.RuntimeMessages(GH_RuntimeMessageLevel.Error).Count > 0;
            bool warnings = component.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count > 0;
            var expectedLevel = pass == 1 ? GH_RuntimeMessageLevel.Warning
                : pass == 2 ? GH_RuntimeMessageLevel.Error : GH_RuntimeMessageLevel.Blank;
            if (output != (pass != 2) || errors != (pass == 2) || warnings != (pass == 1)
                || component.RuntimeMessageLevel != expectedLevel)
                throw new InvalidOperationException($"{scenario}, pass {pass}: output={output}, errors={errors}, warnings={warnings}. " +
                    string.Join("; ", component.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
        }
    }
}
