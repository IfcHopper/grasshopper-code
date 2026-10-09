using System;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;
using IfcHopper.Types;
using Rhino.Geometry;

namespace IfcHopper.Components
{
    public class DeconstructPsetComponent : GH_Component
    {
        public DeconstructPsetComponent()
          : base("Deconstruct Pset", "DePset",
              "Deconstructs a property set (IfcPropertySet). Use Deconstruct Qto for quantity sets.",
              ComponentCategory.Tab, ComponentCategory.Object)
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new PropertySetParam(), "Pset", "PS", "IfcHopper property set.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Set name.", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "Set description.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("From Type", "FT", "True when the set is inherited from the object's type; such sets are not written.", GH_ParamAccess.item);
            pManager.AddTextParameter("Names", "PN", "Property names.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Values", "V",
                "Values; lengths, areas and volumes in document units. Bounded numbers are domains; enumerated and list values are joined with \"; \", " +
                "tables as \"defining: defined; …\"; reference and complex properties are text.",
                GH_ParamAccess.list);
            pManager.AddTextParameter("Types", "T", "IFC value type of each property, e.g. IfcLabel.", GH_ParamAccess.list);
            pManager.AddTextParameter("Kinds", "K", "Property kind: Single, Enumerated, List, Bounded, Table, Reference or Complex.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            PropertySetGoo goo = null;
            if (!DA.GetData(0, ref goo) || !goo.IsValid) return;

            if (!(goo.Value is PropertySet set))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{goo.Value.Name} is a quantity set; use Deconstruct Qto.");
                return;
            }

            DA.SetData(0, set.Name);
            DA.SetData(1, set.Description);
            DA.SetData(2, set.FromType);
            DA.SetDataList(3, set.Properties.Select(p => p.Name));
            DA.SetDataList(4, set.Properties.Select(Value));
            DA.SetDataList(5, set.Properties.Select(p => p.ValueType));
            DA.SetDataList(6, set.Properties.Select(p => p.Kind.ToString()));
        }

        private static IGH_Goo Value(Property property)
        {
            switch (property.WithNumbers(DocumentUnits.FromSi).Value)
            {
                case null: return null;
                case bool b: return new GH_Boolean(b);
                case long l when l >= int.MinValue && l <= int.MaxValue: return new GH_Integer((int)l);
                case long l: return new GH_Number(l);
                case double d: return new GH_Number(d);
                case BoundedValue bounded when bounded.Lower is double lower && bounded.Upper is double upper && bounded.SetPoint == null:
                    return new GH_Interval(new Interval(lower, upper));
                case object value: return new GH_String(Property.ValueText(value));
            }
        }

        protected override Bitmap Icon => Properties.Resources.DeconstructPset;

        public override Guid ComponentGuid => new Guid("5b9a3e72-c1d8-4f06-8a4b-7e2c9d1f6a38");
    }
}
