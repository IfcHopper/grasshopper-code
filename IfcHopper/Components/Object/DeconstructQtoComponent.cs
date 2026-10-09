using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class DeconstructQtoComponent : GH_Component
    {
        public DeconstructQtoComponent()
          : base("Deconstruct Qto", "DeQto",
              "Deconstructs a quantity set (IfcElementQuantity). Use Deconstruct Pset for property sets.",
              ComponentCategory.Tab, ComponentCategory.Object)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new PropertySetParam(), "Qto", "QS", "IfcHopper quantity set.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Set name.", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "Set description.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("From Type", "FT", "True when the set is inherited from the object's type; such sets are not written.", GH_ParamAccess.item);
            pManager.AddTextParameter("Method", "M", "Method of measurement, e.g. BaseQuantities.", GH_ParamAccess.item);
            pManager.AddTextParameter("Names", "QN", "Quantity names.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Values", "V", "Values. Lengths, areas and volumes in document units, weights in kg, times in s.", GH_ParamAccess.list);
            pManager.AddTextParameter("Kinds", "K", "Quantity kinds: Length, Area, Volume, Count, Weight, Time or Number.", GH_ParamAccess.list);
            pManager.AddTextParameter("Formulas", "F", "How each value was calculated; empty items when not given.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            PropertySetGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;
            if (!(goo.Value is QuantitySet set))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{goo.Value.Name} is a property set; use Deconstruct Pset.");
                return;
            }

            DA.SetData(0, set.Name);
            DA.SetData(1, set.Description);
            DA.SetData(2, set.FromType);
            DA.SetData(3, set.MethodOfMeasurement);
            DA.SetDataList(4, set.Quantities.Select(q => q.Name));
            DA.SetDataList(5, set.Quantities.Select(q => DocumentUnits.FromSi(q.Value, Quantity.Dimension(q.Kind))));
            DA.SetDataList(6, set.Quantities.Select(q => q.Kind.ToString()));
            DA.SetDataList(7, set.Quantities.Select(q => q.Formula));
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructQto;

        public override Guid ComponentGuid => new Guid("e47b2c95-0a3f-4d81-b6e2-9c5f1a8d3b70");
    }
}
