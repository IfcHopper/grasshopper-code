using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Components;

namespace IfcHopper.Types
{
    /// <summary>Units can be internalised: they are small and self-contained.</summary>
    public class UnitsParam : GH_PersistentParam<UnitsGoo>
    {
        public UnitsParam()
          : base("Units", "U", "IfcHopper project units.", ComponentCategory.Tab, ComponentCategory.Params)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        // Units are defined with the Units component; values can be internalised from wired data.
        protected override GH_GetterResult Prompt_Singular(ref UnitsGoo value) => GH_GetterResult.cancel;

        protected override GH_GetterResult Prompt_Plural(ref List<UnitsGoo> values) => GH_GetterResult.cancel;

        protected override Bitmap Icon => Properties.Resources.UnitsParam;

        public override Guid ComponentGuid => new Guid("3d7b1e9a-c42f-4a86-b5d0-8e6f2a9c4b17");
    }
}
