using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.Model;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace IfcHopper.Core.Backends.Xbim
{
    /// <summary>
    /// Reads the property and quantity sets of objects (IfcRelDefinesByProperties) and of their types (HasPropertySets).
    /// Each IFC set is converted once, so objects sharing it in the file share it in the model.
    /// </summary>
    internal class XbimPropertySets
    {
        private readonly double _toMetres;
        private readonly Units _units;
        private readonly Dictionary<(IPersistEntity, bool), PropertySetDefinition> _converted = new Dictionary<(IPersistEntity, bool), PropertySetDefinition>();

        /// <param name="toMetres">Factor from the file length unit to metres.</param>
        /// <param name="units">File units, for areas and volumes.</param>
        public XbimPropertySets(double toMetres, Units units)
        {
            _toMetres = toMetres;
            _units = units ?? new Units();
        }

        /// <summary>The object's own sets, then the sets of its type that it has no set of the same name for. Types have only their own sets.</summary>
        public IEnumerable<PropertySetDefinition> Read(IIfcObjectDefinition entity)
        {
            if (entity is IIfcTypeObject type) return type.HasPropertySets.Select(d => Convert(d, false)).Where(s => s != null).ToList();

            var own = Definitions(entity).Select(d => Convert(d, false)).Where(s => s != null).ToList();
            var typed = (entity as IIfcObject)?.IsTypedBy.SelectMany(r => r.RelatingType?.HasPropertySets ?? Enumerable.Empty<IIfcPropertySetDefinition>())
                .Select(d => Convert(d, true)).Where(s => s != null && own.All(o => o.Name != s.Name)) ?? Enumerable.Empty<PropertySetDefinition>();
            return own.Concat(typed).ToList();
        }

        /// <summary>Property sets of a material or material set (IfcMaterialProperties); none in IFC2X3, whose material properties are typed.</summary>
        public IEnumerable<PropertySet> ReadMaterial(IIfcMaterialDefinition material) =>
            material.HasProperties.Where(p => !string.IsNullOrEmpty(p.Name) || p.Properties.Any()).Select(p =>
            {
                var set = new PropertySet(p.Name) { Description = p.Description, Source = p };
                set.Properties.AddRange(p.Properties.Select(Property));
                return set;
            }).ToList();

        /// <summary>Property set definitions related to an object or a context (project).</summary>
        internal static IEnumerable<IIfcPropertySetDefinition> Definitions(IIfcObjectDefinition entity) =>
            Relations(entity).SelectMany(r => Definitions(r.RelatingPropertyDefinition));

        internal static IEnumerable<IIfcRelDefinesByProperties> Relations(IIfcObjectDefinition entity)
        {
            switch (entity)
            {
                case IIfcObject obj: return obj.IsDefinedBy;
                case IIfcContext context: return context.IsDefinedBy;
                default: return Enumerable.Empty<IIfcRelDefinesByProperties>();
            }
        }

        internal static IEnumerable<IIfcPropertySetDefinition> Definitions(IIfcPropertySetDefinitionSelect select)
        {
            switch (select)
            {
                case IIfcPropertySetDefinition definition: return new[] { definition };
                // IfcPropertySetDefinitionSet is a schema-specific value type holding a list.
                case IExpressValueType set when set.Value is IEnumerable items: return items.OfType<IIfcPropertySetDefinition>();
                default: return Enumerable.Empty<IIfcPropertySetDefinition>();
            }
        }

        /// <summary>The set, or null for other definitions (e.g. predefined property sets such as door lining properties).</summary>
        private PropertySetDefinition Convert(IIfcPropertySetDefinition definition, bool fromType)
        {
            if (_converted.TryGetValue((definition, fromType), out var converted)) return converted;

            PropertySetDefinition set;
            switch (definition)
            {
                case IIfcPropertySet properties:
                    var propertySet = new PropertySet(properties.Name);
                    propertySet.Properties.AddRange(properties.HasProperties.Select(Property));
                    set = propertySet;
                    break;
                case IIfcElementQuantity quantities:
                    var quantitySet = new QuantitySet(quantities.Name) { MethodOfMeasurement = quantities.MethodOfMeasurement };
                    quantitySet.Quantities.AddRange(quantities.Quantities.Select(Quantity).Where(q => q != null));
                    set = quantitySet;
                    break;
                default:
                    return _converted[(definition, fromType)] = null;
            }
            set.Description = definition.Description;
            set.Source = definition;
            set.FromType = fromType;
            return _converted[(definition, fromType)] = set;
        }

        private Property Property(IIfcProperty entity)
        {
            var property = new Property { Name = entity.Name, Description = entity.Description };
            switch (entity)
            {
                case IIfcPropertySingleValue single:
                    property.Kind = PropertyKind.Single;
                    (property.ValueType, property.Value) = Value(single.NominalValue);
                    break;
                case IIfcPropertyEnumeratedValue enumerated:
                    property.Kind = PropertyKind.Enumerated;
                    SetList(property, enumerated.EnumerationValues);
                    break;
                case IIfcPropertyListValue list:
                    property.Kind = PropertyKind.List;
                    SetList(property, list.ListValues);
                    break;
                case IIfcPropertyBoundedValue bounded:
                    property.Kind = PropertyKind.Bounded;
                    var lower = Value(bounded.LowerBoundValue);
                    var upper = Value(bounded.UpperBoundValue);
                    var setPoint = Value(bounded.SetPointValue);
                    property.ValueType = upper.Type ?? lower.Type ?? setPoint.Type;
                    property.Value = new BoundedValue(lower.Value, upper.Value, setPoint.Value);
                    break;
                case IIfcPropertyTableValue table:
                    property.Kind = PropertyKind.Table;
                    var defining = table.DefiningValues.Select(Value).ToList();
                    var defined = table.DefinedValues.Select(Value).ToList();
                    property.ValueType = defined.Select(v => v.Type).FirstOrDefault(t => t != null);
                    property.Value = new TableValue(defining.Select(v => v.Value), defined.Select(v => v.Value), defining.Select(v => v.Type).FirstOrDefault(t => t != null));
                    break;
                case IIfcPropertyReferenceValue reference:
                    property.Kind = PropertyKind.Reference;
                    property.Value = (reference.PropertyReference as IPersistEntity)?.ExpressType.Name ?? (string)reference.UsageName;
                    break;
                case IIfcComplexProperty complex:
                    property.Kind = PropertyKind.Complex;
                    property.UsageName = complex.UsageName;
                    property.Value = complex.HasProperties.Select(Property).ToList();
                    break;
                default:
                    property.Kind = PropertyKind.Complex;
                    property.Value = entity.ExpressType.Name;
                    break;
            }
            return property;
        }

        private void SetList(Property property, IEnumerable<IIfcValue> values)
        {
            var read = values.Select(Value).ToList();
            property.ValueType = read.Select(v => v.Type).FirstOrDefault(t => t != null);
            property.Value = read.Select(v => v.Value).ToList();
        }

        private static string Text(object value) => Model.Property.ValueText(value);

        /// <summary>IFC type name and CLR value of a value; lengths, areas and volumes converted to metres, m² and m³.</summary>
        internal (string Type, object Value) Value(IIfcValue value) => Value(value as IExpressValueType);

        /// <summary>IFC type name and CLR value of any value type, also those that are no IfcValue (e.g. IFC4X3 positive lengths).</summary>
        internal (string Type, object Value) Value(IExpressValueType express)
        {
            if (express == null) return (null, null);
            var type = express.GetType().Name;
            var raw = express.Value;
            switch (raw)
            {
                case double number:
                    var dimension = Units.MeasureDimension(type);
                    return (type, dimension == 0 ? number : number * ToSi(dimension));
                case int integer:
                    return (type, (long)integer);
                case string _:
                case bool _:
                case long _:
                case null:
                    return (type, raw);
                default:
                    return (type, Text(raw));
            }
        }

        private double ToSi(int dimension) => dimension == 1 ? _toMetres : _units.ToSi(dimension);

        private Quantity Quantity(IIfcPhysicalQuantity entity)
        {
            QuantityKind kind;
            double value;
            string formula = null;
            switch (entity)
            {
                case IIfcQuantityLength length:
                    (kind, value, formula) = (QuantityKind.Length, Number(length.LengthValue) * ToSi(1), length.Formula);
                    break;
                case IIfcQuantityArea area:
                    (kind, value, formula) = (QuantityKind.Area, Number(area.AreaValue) * ToSi(2), area.Formula);
                    break;
                case IIfcQuantityVolume volume:
                    (kind, value, formula) = (QuantityKind.Volume, Number(volume.VolumeValue) * ToSi(3), volume.Formula);
                    break;
                case IIfcQuantityCount count:
                    (kind, value, formula) = (QuantityKind.Count, Number(count.CountValue), count.Formula);
                    break;
                case IIfcQuantityWeight weight:
                    (kind, value, formula) = (QuantityKind.Weight, Number(weight.WeightValue), weight.Formula);
                    break;
                case IIfcQuantityTime time:
                    (kind, value, formula) = (QuantityKind.Time, Number(time.TimeValue), time.Formula);
                    break;
                case global::Xbim.Ifc4x3.QuantityResource.IfcQuantityNumber number:
                    (kind, value, formula) = (QuantityKind.Number, Number(number.NumberValue), number.Formula);
                    break;
                default:
                    return null;
            }
            return new Quantity { Name = entity.Name, Description = entity.Description, Kind = kind, Value = value, Formula = formula };
        }

        private static double Number(IExpressValueType value) => System.Convert.ToDouble(value.Value);
    }
}
