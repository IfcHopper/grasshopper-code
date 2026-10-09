using System;
using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Common;
using Xbim.Common.Metadata;
using Xbim.Ifc4x3;
using Xbim.Ifc4x3.Kernel;
using Xbim.Ifc4x3.MeasureResource;
using Xbim.Ifc4x3.ProductExtension;

namespace IfcHopper.Core.Backends.Xbim
{
    /// <summary>Element classes, their predefined types (also of type classes) and the simple value types, read from the xBIM IFC4X3_ADD2 metadata.</summary>
    public class XbimSchemaInfo : ISchemaInfo
    {
        private readonly Dictionary<string, Type> _elements;
        private readonly Dictionary<string, Type> _types;
        private readonly Dictionary<string, string[]> _predefinedTypes = new Dictionary<string, string[]>();
        private readonly Dictionary<string, (Type Type, ValueKind Kind)> _valueTypes;
        private readonly ExpressMetaData _metadata;
        private readonly Dictionary<string, IReadOnlyList<AttributeInfo>> _attributes = new Dictionary<string, IReadOnlyList<AttributeInfo>>(StringComparer.OrdinalIgnoreCase);

        public XbimSchemaInfo()
        {
            var metadata = _metadata = ExpressMetaData.GetMetadata(new EntityFactoryIfc4x3Add2());
            _elements = metadata.Types()
                .Where(t => !t.Type.IsAbstract && typeof(IfcElement).IsAssignableFrom(t.Type))
                .ToDictionary(t => t.Name, t => t.Type, StringComparer.OrdinalIgnoreCase);
            ElementClasses = _elements.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();
            _types = metadata.Types()
                .Where(t => !t.Type.IsAbstract && typeof(IfcTypeProduct).IsAssignableFrom(t.Type))
                .ToDictionary(t => t.Name, t => t.Type, StringComparer.OrdinalIgnoreCase);

            _valueTypes = typeof(IfcLabel).Assembly.GetTypes()
                .Where(t => t.IsValueType && typeof(IfcValue).IsAssignableFrom(t) && typeof(IExpressValueType).IsAssignableFrom(t))
                .Select(t => (Type: t, Kind: KindOf(((IExpressValueType)Activator.CreateInstance(t)).UnderlyingSystemType)))
                .Where(t => t.Kind != null)
                .ToDictionary(t => t.Type.Name, t => (t.Type, t.Kind.Value), StringComparer.OrdinalIgnoreCase);
        }

        private static ValueKind? KindOf(Type type)
        {
            if (type == typeof(string)) return ValueKind.Text;
            if (type == typeof(bool)) return ValueKind.Boolean;
            if (type == typeof(bool?)) return ValueKind.Logical;
            if (type == typeof(long)) return ValueKind.Integer;
            if (type == typeof(double)) return ValueKind.Real;
            return null;
        }

        /// <summary>Classes whose attributes IfcHopper models itself, or which hold no class-specific data.</summary>
        private static readonly HashSet<string> ModelledClasses = new HashSet<string>
        {
            "IfcRoot", "IfcObjectDefinition", "IfcObject", "IfcProduct", "IfcElement", "IfcTypeObject", "IfcTypeProduct", "IfcElementType",
        };

        public IReadOnlyList<AttributeInfo> GetAttributes(string ifcClass)
        {
            var name = ifcClass?.Trim();
            if (string.IsNullOrEmpty(name)) return Array.Empty<AttributeInfo>();
            lock (_attributes)
            {
                if (_attributes.TryGetValue(name, out var attributes)) return attributes;
                var type = _metadata.ExpressType(name.ToUpperInvariant());
                return _attributes[name] = type == null ? Array.Empty<AttributeInfo>() : type.Properties.OrderBy(p => p.Key).Select(p => p.Value)
                    .Where(p => !ModelledClasses.Contains(p.PropertyInfo.DeclaringType.Name) && p.Name != "PredefinedType")
                    .Select(Attribute).Where(a => a != null).ToList();
            }
        }

        /// <summary>The attribute when its value is a simple value type or an enumeration; null otherwise (entities, lists).</summary>
        private AttributeInfo Attribute(ExpressMetaProperty property)
        {
            var optional = property.EntityAttribute.State == EntityAttributeState.Optional;
            var type = Nullable.GetUnderlyingType(property.PropertyInfo.PropertyType) ?? property.PropertyInfo.PropertyType;
            if (type.IsEnum) return new AttributeInfo(property.Name, type.Name, null, Enum.GetNames(type), optional);
            return _valueTypes.TryGetValue(type.Name, out var value) && value.Type == type ? new AttributeInfo(property.Name, type.Name, value.Kind, null, optional) : null;
        }

        public string FindValueType(string name) =>
            name != null && _valueTypes.TryGetValue(name.Trim(), out var type) ? type.Type.Name : null;

        public ValueKind? GetValueKind(string valueType) =>
            valueType != null && _valueTypes.TryGetValue(valueType, out var type) ? type.Kind : (ValueKind?)null;

        /// <summary>The IFC4X3 struct of a simple value type, or null.</summary>
        internal Type ValueType(string valueType) =>
            valueType != null && _valueTypes.TryGetValue(valueType, out var type) ? type.Type : null;

        public IReadOnlyList<string> ElementClasses { get; }

        public string FindClass(string name) => string.IsNullOrWhiteSpace(name) ? null : _metadata.ExpressType(name.Trim().ToUpperInvariant())?.Name;

        public bool IsClassOf(string ifcClass, string baseClass)
        {
            if (string.IsNullOrWhiteSpace(ifcClass) || string.IsNullOrWhiteSpace(baseClass)) return false;
            if (string.Equals(ifcClass.Trim(), baseClass.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            var type = _metadata.ExpressType(ifcClass.Trim().ToUpperInvariant());
            var baseType = _metadata.ExpressType(baseClass.Trim().ToUpperInvariant());
            return type != null && baseType != null && baseType.Type.IsAssignableFrom(type.Type);
        }

        public string FindElementClass(string name) =>
            name != null && _elements.TryGetValue(name.Trim(), out var type) ? _elements.Keys.First(k => _elements[k] == type) : null;

        public string FindTypeClass(string name) =>
            name != null && _types.TryGetValue(name.Trim(), out var type) ? type.Name : null;

        public string TypeClassOf(string elementClass)
        {
            var name = FindElementClass(elementClass);
            return name == null ? null : FindTypeClass(name + "Type");
        }

        public string ElementClassOf(string typeClass)
        {
            var name = typeClass?.Trim();
            if (name == null) return null;
            foreach (var suffix in new[] { "Type", "Style" })
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && FindElementClass(name.Substring(0, name.Length - suffix.Length)) is string element)
                    return element;
            return null;
        }

        public IReadOnlyList<string> GetPredefinedTypes(string elementClass)
        {
            var name = FindElementClass(elementClass);
            if (name == null && elementClass != null && _types.TryGetValue(elementClass.Trim(), out var typeClass)) name = typeClass.Name;
            if (name == null) return null;

            lock (_predefinedTypes)
            {
                if (!_predefinedTypes.TryGetValue(name, out var values))
                {
                    var property = (_elements.TryGetValue(name, out var type) ? type : _types[name]).GetProperty("PredefinedType");
                    var enumType = property == null ? null : Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    values = enumType != null && enumType.IsEnum
                        ? Enum.GetNames(enumType).Where(v => v != "USERDEFINED" && v != "NOTDEFINED").ToArray()
                        : null;
                    _predefinedTypes[name] = values;
                }
                return values;
            }
        }
    }
}
