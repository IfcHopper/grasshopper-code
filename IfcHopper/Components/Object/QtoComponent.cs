using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class QtoComponent : GH_Component
    {
        public QtoComponent()
          : base("Qto", "Qto",
              "Creates a quantity set (IfcElementQuantity), e.g. Qto_WallBaseQuantities. Connect it to the Property Sets input of an object or spatial component.",
              ComponentCategory.Tab, ComponentCategory.Object)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Set name, e.g. Qto_WallBaseQuantities. Standard IFC sets (Annex A) give the kinds of their quantities.", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "Set description.", GH_ParamAccess.item);
            pManager.AddTextParameter("Names", "QN", "Quantity names. Empty: a set without quantities, which deletes the set with its name on Modify components.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Values", "V", "One value per name. Lengths, areas and volumes in document units, weights in kg, times in s.", GH_ParamAccess.list);
            pManager.AddTextParameter("Kinds", "K",
                $"Quantity kinds ({string.Join(", ", Enum.GetNames(typeof(QuantityKind)))}): one for all or one per name. Empty: the kinds of the standard set.", GH_ParamAccess.list);
            pManager.AddTextParameter("Method", "M", "Method of measurement, e.g. BaseQuantities.", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new PropertySetParam(), "Qto", "QS", "IfcHopper quantity set.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = null, description = null, method = null;
            var names = new List<string>();
            var values = new List<double>();
            var kinds = new List<string>();
            if (!DA.GetData(0, ref name)) return;
            DA.GetData(1, ref description);
            DA.GetDataList(2, names);
            DA.GetDataList(3, values);
            DA.GetDataList(4, kinds);
            DA.GetData(5, ref method);

            if (string.IsNullOrWhiteSpace(name))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name cannot be empty.");
                return;
            }
            if (values.Count != names.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Give one value per name ({names.Count} names, {values.Count} values).");
                return;
            }
            if (kinds.Count > 1 && kinds.Count != names.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Give one kind for all names or one per name ({names.Count} names, {kinds.Count} kinds).");
                return;
            }

            var set = new QuantitySet(name.Trim()) { Description = description, MethodOfMeasurement = string.IsNullOrWhiteSpace(method) ? null : method.Trim() };
            for (int i = 0; i < names.Count; i++)
            {
                QuantityKind? kind = null;
                var kindText = kinds.Count == 0 ? null : kinds[kinds.Count == 1 ? 0 : i];
                if (!string.IsNullOrWhiteSpace(kindText))
                {
                    if (!Enum.TryParse(kindText.Trim(), true, out QuantityKind parsed) || !Enum.IsDefined(typeof(QuantityKind), parsed))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Unknown kind '{kindText}'. Use one of: {string.Join(", ", Enum.GetNames(typeof(QuantityKind)))}.");
                        return;
                    }
                    kind = parsed;
                }
                try
                {
                    var quantity = set.Add(names[i], values[i], kind);
                    quantity.Value = DocumentUnits.ToSi(quantity.Value, Quantity.Dimension(quantity.Kind));
                }
                catch (ArgumentException e)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
                    return;
                }
            }
            if (names.Count == 0) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Without quantities this set deletes the set {set.Name} on Modify components.");
            else foreach (var warning in PropertyTemplates.Check(set)) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, warning);
            DA.SetData(0, new PropertySetGoo(set));
        }

        protected override Bitmap Icon => Properties.Resources.Qto;

        public override Guid ComponentGuid => new Guid("c8e2f4a9-6b13-4d70-a5e8-3f9d1b7c2e64");
    }
}
