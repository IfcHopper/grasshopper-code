using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Components;

namespace IfcHopper.Types
{
    /// <summary>Contexts can be internalised: they are small and self-contained.</summary>
    public class ContextParam : GH_PersistentParam<ContextGoo>
    {
        public ContextParam()
          : base("Context", "Cx", "IfcHopper representation context.", ComponentCategory.Tab, ComponentCategory.Params)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        // Contexts are defined with the Context component; values can be internalised from wired data.
        protected override GH_GetterResult Prompt_Singular(ref ContextGoo value) => GH_GetterResult.cancel;

        protected override GH_GetterResult Prompt_Plural(ref List<ContextGoo> values) => GH_GetterResult.cancel;

        protected override Bitmap Icon => Properties.Resources.ContextParam;

        public override Guid ComponentGuid => new Guid("6a3f9c2d-8e14-4b75-a0d6-e5b2c7f1d483");
    }
}
