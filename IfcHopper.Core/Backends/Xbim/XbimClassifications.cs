using System.Collections.Generic;
using System.Linq;
using IfcHopper.Core.Model;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace IfcHopper.Core.Backends.Xbim
{
    /// <summary>
    /// Reads the classification references of objects and types (IfcRelAssociatesClassification). Each IFC reference is converted once,
    /// so objects sharing it in the file share it in the model.
    /// </summary>
    internal class XbimClassifications
    {
        private readonly Dictionary<(IPersistEntity, bool), ClassificationReference> _converted = new Dictionary<(IPersistEntity, bool), ClassificationReference>();

        /// <summary>The object's own references, then those of its type in systems it has no reference in. Types have only their own.</summary>
        public IEnumerable<ClassificationReference> Read(IIfcObjectDefinition entity)
        {
            var own = Associated(entity, false).ToList();
            if (!(entity is IIfcObject obj)) return own;
            var typed = obj.IsTypedBy.Where(r => r.RelatingType != null).SelectMany(r => Associated(r.RelatingType, true))
                .Where(t => !own.Any(o => o.SameSystem(t)));
            return own.Concat(typed).ToList();
        }

        private IEnumerable<ClassificationReference> Associated(IIfcObjectDefinition entity, bool fromType) =>
            entity.HasAssociations.OfType<IIfcRelAssociatesClassification>().Select(r => Convert(r.RelatingClassification, fromType)).Where(c => c != null);

        /// <summary>The reference, or null for selects IfcHopper does not map (e.g. IFC2X3 classification notations).</summary>
        private ClassificationReference Convert(IIfcClassificationSelect select, bool fromType)
        {
            if (!(select is IPersistEntity entity)) return null;
            if (_converted.TryGetValue((entity, fromType), out var converted)) return converted;

            var reference = Values(select);
            if (reference != null)
            {
                reference.Source = entity;
                reference.FromType = fromType;
            }
            return _converted[(entity, fromType)] = reference;
        }

        /// <summary>A new reference with the values of a classification reference or classification (without source); null for other selects.</summary>
        internal static ClassificationReference Values(IIfcClassificationSelect select)
        {
            switch (select)
            {
                case IIfcClassificationReference item:
                    var top = System(item);
                    return new ClassificationReference(top?.Name, item.Identification, item.Name, top?.Edition, item.Location);
                case IIfcClassification classification:
                    return new ClassificationReference(classification.Name, null, null, classification.Edition, classification.Location);
                default:
                    return null;
            }
        }

        /// <summary>The classification at the top of a reference's parent references; null when there is none.</summary>
        internal static IIfcClassification System(IIfcClassificationReference reference)
        {
            var visited = new HashSet<IIfcClassificationReference>();
            IIfcClassificationReferenceSelect parent = reference.ReferencedSource;
            while (parent is IIfcClassificationReference item && visited.Add(item)) parent = item.ReferencedSource;
            return parent as IIfcClassification;
        }
    }
}
