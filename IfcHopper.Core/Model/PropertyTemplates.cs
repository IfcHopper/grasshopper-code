using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace IfcHopper.Core.Model
{
    /// <summary>
    /// Standard property and quantity set templates of IFC 4.3 (Annex A, downloaded from buildingSMART by the build), embedded in the library and read on first use.
    /// </summary>
    public static class PropertyTemplates
    {
        private const string ResourcePrefix = "IfcHopper.Core.Psd.";

        private static readonly Lazy<Dictionary<string, PropertySetTemplate>> _templates = new Lazy<Dictionary<string, PropertySetTemplate>>(Load);

        /// <summary>Names of all standard sets, e.g. "Pset_WallCommon" and "Qto_WallBaseQuantities".</summary>
        public static IEnumerable<string> Names => _templates.Value.Keys;

        /// <summary>The template of a standard set (case-sensitive name), or null for custom sets.</summary>
        public static PropertySetTemplate Find(string setName) =>
            setName != null && _templates.Value.TryGetValue(setName.Trim(), out var template) ? template : null;

        /// <summary>
        /// Differences between a set and its template: properties not in the template, values not in the template enumeration and
        /// value types other than the template's. Empty for custom sets.
        /// </summary>
        public static IEnumerable<string> Check(PropertySetDefinition set)
        {
            var template = Find(set.Name);
            if (template == null) yield break;
            if (template.IsQuantitySet != set is QuantitySet)
            {
                yield return $"{set.Name} is a standard {(template.IsQuantitySet ? "quantity" : "property")} set.";
                yield break;
            }
            switch (set)
            {
                case PropertySet properties:
                    foreach (var property in properties.Properties)
                    {
                        var expected = template.Find(property.Name);
                        if (expected == null)
                            yield return $"'{property.Name}' is not a property of {set.Name}.";
                        else if (expected.Kind == PropertyKind.Enumerated && property.Value is IEnumerable<object> values)
                        {
                            foreach (var value in values.OfType<string>().Where(v => !expected.EnumItems.Contains(v)))
                                yield return $"'{value}' is not a value of {set.Name}.{property.Name} ({string.Join(", ", expected.EnumItems)}).";
                        }
                        else if (expected.Kind == PropertyKind.Single && expected.ValueType != null && property.ValueType != null && property.ValueType != expected.ValueType)
                            yield return $"{set.Name}.{property.Name} is {expected.ValueType}, not {property.ValueType}.";
                    }
                    break;
                case QuantitySet quantities:
                    foreach (var quantity in quantities.Quantities)
                    {
                        var expected = template.Find(quantity.Name);
                        if (expected == null) yield return $"'{quantity.Name}' is not a quantity of {set.Name}.";
                        else if (expected.QuantityKind != quantity.Kind) yield return $"{set.Name}.{quantity.Name} is a {expected.QuantityKind} quantity, not {quantity.Kind}.";
                    }
                    break;
            }
        }

        private static Dictionary<string, PropertySetTemplate> Load()
        {
            var assembly = typeof(PropertyTemplates).Assembly;
            return assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                .Select(n => Parse(assembly, n)).Where(t => t != null).ToDictionary(t => t.Name);
        }

        private static PropertySetTemplate Parse(Assembly assembly, string resource)
        {
            XElement root;
            using (var stream = assembly.GetManifestResourceStream(resource)) root = XDocument.Load(stream).Root;
            var name = (string)root.Element("Name");
            if (name == null) return null;

            var isQuantitySet = root.Name.LocalName == "QtoSetDef";
            var properties = isQuantitySet
                ? root.Element("QtoDefs")?.Elements("QtoDef").Select(ParseQuantity)
                : root.Element("PropertyDefs")?.Elements("PropertyDef").Select(ParseProperty);
            return new PropertySetTemplate(name, (string)root.Element("Definition"), isQuantitySet,
                root.Element("ApplicableClasses")?.Elements("ClassName").Select(c => c.Value.Trim()).ToList() ?? new List<string>(),
                properties?.ToList() ?? new List<PropertyTemplate>());
        }

        private static PropertyTemplate ParseProperty(XElement element)
        {
            var type = element.Element("PropertyType")?.Elements().FirstOrDefault();
            PropertyKind kind;
            switch (type?.Name.LocalName)
            {
                case "TypePropertyEnumeratedValue": kind = PropertyKind.Enumerated; break;
                case "TypePropertyBoundedValue": kind = PropertyKind.Bounded; break;
                case "TypePropertyListValue": kind = PropertyKind.List; break;
                case "TypePropertyTableValue": kind = PropertyKind.Table; break;
                case "TypePropertyReferenceValue": kind = PropertyKind.Reference; break;
                case "TypeComplexProperty": kind = PropertyKind.Complex; break;
                default: kind = PropertyKind.Single; break;
            }
            return new PropertyTemplate((string)element.Element("Name"), (string)element.Element("Definition"), kind,
                (string)type?.Descendants("DataType").FirstOrDefault()?.Attribute("type"),
                type?.Descendants("EnumItem").Select(i => i.Value.Trim()).ToList() ?? new List<string>(), null);
        }

        private static PropertyTemplate ParseQuantity(XElement element)
        {
            var qtoType = ((string)element.Element("QtoType"))?.Trim();
            QuantityKind? kind = null;
            if (qtoType != null && qtoType.StartsWith("Q_") && Enum.TryParse(qtoType.Substring(2), true, out QuantityKind parsed)) kind = parsed;
            return new PropertyTemplate((string)element.Element("Name"), (string)element.Element("Definition"), PropertyKind.Single, null, new List<string>(), kind);
        }
    }

    /// <summary>A standard property or quantity set (Annex A).</summary>
    public sealed class PropertySetTemplate
    {
        public string Name { get; }
        public string Definition { get; }
        public bool IsQuantitySet { get; }

        /// <summary>IFC classes the set applies to, e.g. IfcWall and IfcWallType.</summary>
        public IReadOnlyList<string> ApplicableClasses { get; }

        public IReadOnlyList<PropertyTemplate> Properties { get; }

        internal PropertySetTemplate(string name, string definition, bool isQuantitySet, IReadOnlyList<string> applicableClasses, IReadOnlyList<PropertyTemplate> properties)
        {
            Name = name;
            Definition = definition;
            IsQuantitySet = isQuantitySet;
            ApplicableClasses = applicableClasses;
            Properties = properties;
        }

        public PropertyTemplate Find(string name) => Properties.FirstOrDefault(p => p.Name == name);

        public override string ToString() => Name;
    }

    /// <summary>A property or quantity of a standard set.</summary>
    public sealed class PropertyTemplate
    {
        public string Name { get; }
        public string Definition { get; }

        /// <summary>Property kind; <see cref="PropertyKind.Single"/> for quantities.</summary>
        public PropertyKind Kind { get; }

        /// <summary>IFC value type, e.g. "IfcBoolean" (the defining value type for tables); null for enumerations, references and quantities.</summary>
        public string ValueType { get; }

        /// <summary>Allowed values of an enumerated property; empty otherwise.</summary>
        public IReadOnlyList<string> EnumItems { get; }

        /// <summary>Kind of a quantity; null for properties.</summary>
        public QuantityKind? QuantityKind { get; }

        internal PropertyTemplate(string name, string definition, PropertyKind kind, string valueType, IReadOnlyList<string> enumItems, QuantityKind? quantityKind)
        {
            Name = name;
            Definition = definition;
            Kind = kind;
            ValueType = valueType;
            EnumItems = enumItems;
            QuantityKind = quantityKind;
        }

        public override string ToString() => Name;
    }
}
