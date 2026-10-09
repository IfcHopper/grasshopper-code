using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;
using IfcHopper.Types;

namespace IfcHopper.Components
{
    public class PsetComponent : GH_Component
    {
        public PsetComponent()
          : base("Pset", "Pset",
              "Creates a property set (IfcPropertySet), e.g. Pset_WallCommon. Connect it to the Property Sets input of an object or spatial component.",
              ComponentCategory.Tab, ComponentCategory.Object)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Set name, e.g. Pset_WallCommon. Standard IFC sets (Annex A) give the value types of their properties.", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "Set description.", GH_ParamAccess.item);
            pManager.AddTextParameter("Names", "PN", "Property names. Empty: a set without properties, which deletes the set with its name on Modify components.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Values", "V",
                "One value per name: booleans, integers, numbers or text; a domain for a bounded value; a Pset for a complex property. Text gives " +
                "list (\"a; b\"), bounded (\"lower .. upper\") and table (\"defining: defined; …\") values. Lengths, areas and volumes in document units. " +
                "Empty items: no value.", GH_ParamAccess.list);
            pManager.AddTextParameter("Types", "T",
                "IFC value types, e.g. IfcLabel, IfcBoolean, IfcLengthMeasure: one for all or one per name. Empty: the type of the standard set, " +
                "else IfcBoolean, IfcInteger, IfcReal or IfcLabel from the value.", GH_ParamAccess.list);
            pManager.AddTextParameter("Kinds", "K",
                "Property kinds: Single, Enumerated, List, Bounded, Table or Complex; one for all or one per name. Empty: from the value (domain: " +
                "Bounded, Pset: Complex), else the kind of the standard set, else Single.", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new PropertySetParam(), "Pset", "PS", "IfcHopper property set.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = null, description = null;
            var names = new List<string>();
            var values = new List<IGH_Goo>();
            var types = new List<string>();
            var kindNames = new List<string>();
            if (!DA.GetData(0, ref name)) return;
            DA.GetData(1, ref description);
            DA.GetDataList(2, names);
            DA.GetDataList(3, values);
            DA.GetDataList(4, types);
            DA.GetDataList(5, kindNames);

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
            if (types.Count > 1 && types.Count != names.Count || kindNames.Count > 1 && kindNames.Count != names.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Give one type and kind for all names or one per name ({names.Count} names, {types.Count} types, {kindNames.Count} kinds).");
                return;
            }
            var kinds = new List<PropertyKind?>();
            foreach (var kindName in kindNames)
            {
                if (string.IsNullOrWhiteSpace(kindName)) kinds.Add(null);
                else if (Enum.TryParse(kindName.Trim(), true, out PropertyKind kind) && kind != PropertyKind.Reference) kinds.Add(kind);
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"'{kindName}' is not a property kind (Single, Enumerated, List, Bounded, Table, Complex).");
                    return;
                }
            }

            var set = new PropertySet(name.Trim()) { Description = description };
            for (int i = 0; i < names.Count; i++)
            {
                try
                {
                    var type = types.Count == 0 ? null : types[types.Count == 1 ? 0 : i];
                    var kind = kinds.Count == 0 ? null : kinds[kinds.Count == 1 ? 0 : i];
                    var property = set.Add(names[i], Value(values[i]), type, kind);
                    // Nested properties come from Pset components and are in SI units already.
                    if (property.Kind != PropertyKind.Complex) set.Properties[set.Properties.Count - 1] = property.WithNumbers(DocumentUnits.ToSi);
                }
                catch (ArgumentException e)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
                    return;
                }
            }
            if (names.Count == 0) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Without properties this set deletes the set {set.Name} on Modify components.");
            else foreach (var warning in PropertyTemplates.Check(set)) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, warning);
            DA.SetData(0, new PropertySetGoo(set));
        }

        /// <summary>CLR value of a Grasshopper value; text for other kinds of data.</summary>
        private static object Value(IGH_Goo goo)
        {
            switch (goo)
            {
                case null: return null;
                case GH_Boolean b: return b.Value;
                case GH_Integer i: return i.Value;
                case GH_Number n: return n.Value;
                case GH_String s: return s.Value;
                case GH_Interval interval: return new BoundedValue(interval.Value.Min, interval.Value.Max);
                case PropertySetGoo set when set.Value is PropertySet properties: return properties;
                default: return goo.ScriptVariable() is var value && (value is bool || value is int || value is double || value is string) ? value : goo.ToString();
            }
        }

        protected override Bitmap Icon => Properties.Resources.Pset;

        public override Guid ComponentGuid => new Guid("a3c5e71f-2d84-4b96-9f0a-1e7b6c3d8a25");
    }
}
