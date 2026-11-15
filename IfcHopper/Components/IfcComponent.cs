using System.Linq;
using Grasshopper.Kernel;

namespace IfcHopper.Components
{
    /// <summary>Shared solve diagnostics: partial results use warnings; total failures retain errors.</summary>
    public abstract class IfcComponent : GH_Component
    {
        /// <summary>Creates an IFC component with the supplied Grasshopper identity and category.</summary>
        protected IfcComponent(string name, string nickname, string description, string category, string subcategory)
            : base(name, nickname, description, category, subcategory) { }

        /// <summary>Whether this solve produced a usable result; side-effect components must also verify their operation succeeded.</summary>
        protected virtual bool HasSuccessfulResults =>
            Params.Output.Any(p => p.VolatileData.AllData(true).Any(item => item != null && item.IsValid));

        /// <summary>Classifies diagnostics after all iterations, without changing output data or solving again.</summary>
        protected override void AfterSolveInstance()
        {
            base.AfterSolveInstance();
            if (!HasSuccessfulResults) return;
            var errors = RuntimeMessages(GH_RuntimeMessageLevel.Error).ToArray();
            if (errors.Length == 0) return;
            var warnings = RuntimeMessages(GH_RuntimeMessageLevel.Warning).ToArray();
            var remarks = RuntimeMessages(GH_RuntimeMessageLevel.Remark).ToArray();
            // Include typed-input conversion failures, which Grasshopper records on parameters
            // rather than the component. Keep all diagnostic text and leave the data untouched (#20).
            ClearRuntimeMessages();
            foreach (var parameter in Params.Input.Concat(Params.Output)) parameter.ClearRuntimeMessages();
            foreach (var message in remarks) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, message);
            foreach (var message in warnings) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, message);
            foreach (var message in errors) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, message);
        }
    }
}
