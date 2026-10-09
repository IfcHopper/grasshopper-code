using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;

namespace IfcHopper.Components
{
    /// <summary>Tag and class-specific attribute inputs and outputs of element and type components (appended last).</summary>
    internal static class ComponentAttributes
    {
        /// <summary>Adds the Tag, Attribute Names and Attribute Values inputs (optional); call from RegisterInputParams.</summary>
        public static void RegisterInputs(GH_Component component, bool modify)
        {
            var unchanged = modify ? " Unconnected: unchanged." : "";
            component.Params.RegisterInputParam(Optional(new Param_String(), "Tag", "Tag", "Identifier, e.g. a mark or the id of the authoring tool (Tag)." +
                (modify ? " Connected but empty: removes the tag." : "") + unchanged, GH_ParamAccess.item));
            component.Params.RegisterInputParam(Optional(new Param_String(), "Attribute Names", "AN",
                "Attributes of the class beyond those of the other inputs, e.g. OverallHeight and OperationType of IfcDoor; an error lists those of the class.", GH_ParamAccess.list));
            component.Params.RegisterInputParam(Optional(new Param_GenericObject(), "Attribute Values", "AV",
                "One value per attribute name: text, numbers (lengths, areas and volumes in document units), booleans or enumeration values." +
                (modify ? " Empty items remove the attribute; others keep their value." : ""), GH_ParamAccess.list));
        }

        /// <summary>Adds the Tag, Attribute Names and Attribute Values outputs; call from RegisterOutputParams.</summary>
        public static void RegisterOutputs(GH_Component component)
        {
            component.Params.RegisterOutputParam(Optional(new Param_String(), "Tag", "Tag", "Identifier (Tag).", GH_ParamAccess.item));
            component.Params.RegisterOutputParam(Optional(new Param_String(), "Attribute Names", "AN", "Set attributes of the class beyond the other outputs, e.g. OverallHeight of IfcDoor.", GH_ParamAccess.list));
            component.Params.RegisterOutputParam(Optional(new Param_GenericObject(), "Attribute Values", "AV",
                "Value of each attribute; lengths, areas and volumes in document units, enumerations as text.", GH_ParamAccess.list));
        }

        private static IGH_Param Optional(IGH_Param param, string name, string nickName, string description, GH_ParamAccess access)
        {
            param.Name = name;
            param.NickName = nickName;
            param.Description = description;
            param.Access = access;
            param.Optional = true;
            return param;
        }

        /// <summary>
        /// Applies the inputs from <paramref name="index"/>: the tag, then the attributes merged by name (validated against
        /// <paramref name="ifcClass"/>). Returns false (with an error) for unknown names or values that do not convert.
        /// </summary>
        public static bool Apply(GH_Component component, IGH_DataAccess DA, int index, string ifcClass, Action<string> setTag, Dictionary<string, object> attributes)
        {
            string tag = null;
            if (DA.GetData(index, ref tag)) setTag(string.IsNullOrWhiteSpace(tag) ? null : tag.Trim());
            else if (component.Params.Input[index].SourceCount > 0) setTag(null);

            var names = new List<string>();
            var values = new List<IGH_Goo>();
            if (!DA.GetDataList(index + 1, names) || names.Count == 0) return true;
            DA.GetDataList(index + 2, values);
            if (values.Count != names.Count)
            {
                component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Give one value per attribute name ({names.Count} names, {values.Count} values).");
                return false;
            }

            var known = IfcBackend.Schema.GetAttributes(ifcClass);
            for (int i = 0; i < names.Count; i++)
            {
                var info = known.FirstOrDefault(a => a.Name.Equals(names[i]?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (info == null)
                {
                    component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, known.Count == 0
                        ? $"{ifcClass} has no attributes beyond the inputs of this component."
                        : $"'{names[i]}' is not an attribute of {ifcClass} ({string.Join(", ", known.Select(a => a.Name))}).");
                    return false;
                }
                try
                {
                    var value = info.Convert(Value(values[i]));
                    if (value == null) attributes.Remove(info.Name);
                    else attributes[info.Name] = value is double number ? DocumentUnits.ToSi(number, info.Dimension) : value;
                }
                catch (ArgumentException e)
                {
                    component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
                    return false;
                }
            }
            return true;
        }

        public static void SetOutputs(IGH_DataAccess DA, int index, string tag, IReadOnlyDictionary<string, object> attributes, string ifcClass)
        {
            var dimensions = IfcBackend.Schema.GetAttributes(ifcClass).ToDictionary(a => a.Name, a => a.Dimension);
            DA.SetData(index, tag);
            DA.SetDataList(index + 1, attributes.Keys);
            DA.SetDataList(index + 2, attributes.Select(a => Goo(a.Value, dimensions.TryGetValue(a.Key, out var dimension) ? dimension : 0)));
        }

        private static object Value(IGH_Goo goo)
        {
            switch (goo)
            {
                case null: return null;
                case GH_Boolean b: return b.Value;
                case GH_Integer i: return i.Value;
                case GH_Number n: return n.Value;
                case GH_String s: return string.IsNullOrWhiteSpace(s.Value) ? null : s.Value;
                default: return goo.ScriptVariable();
            }
        }

        private static IGH_Goo Goo(object value, int dimension)
        {
            switch (value)
            {
                case bool b: return new GH_Boolean(b);
                case long l when l >= int.MinValue && l <= int.MaxValue: return new GH_Integer((int)l);
                case long l: return new GH_Number(l);
                case double d: return new GH_Number(DocumentUnits.FromSi(d, dimension));
                default: return new GH_String(Property.ValueText(value));
            }
        }
    }
}
