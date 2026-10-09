using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Components;

namespace IfcHopper.Types
{
    /// <summary>Georeferences can be internalised: they are small and self-contained.</summary>
    public class GeoreferenceParam : GH_PersistentParam<GeoreferenceGoo>
    {
        public GeoreferenceParam()
          : base("Georeference", "Geo", "IfcHopper georeference.", ComponentCategory.Tab, ComponentCategory.Params)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        // Georeferences are defined with the Georeference component; values can be internalised from wired data.
        protected override GH_GetterResult Prompt_Singular(ref GeoreferenceGoo value) => GH_GetterResult.cancel;

        protected override GH_GetterResult Prompt_Plural(ref List<GeoreferenceGoo> values) => GH_GetterResult.cancel;

        protected override Bitmap Icon => Properties.Resources.GeoreferenceParam;

        public override Guid ComponentGuid => new Guid("c5d8f2a1-7e43-4b96-8a0f-2d6b9e1c4f37");
    }
}
