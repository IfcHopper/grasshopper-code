using System.Collections.Generic;
using System.IO;
using System.Linq;
using IfcHopper.Core.IO;
using IfcHopper.Core.Model;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace IfcHopper.Core.Backends.Xbim
{
    public class XbimIfcReader : IIfcReader
    {
        public IfcHeader ReadHeader(string path)
        {
            var model = XbimDocumentCache.Open(path);
            var header = model.Header;
            var fileName = header.FileName;
            return new IfcHeader
            {
                Description = header.FileDescription.Description.ToList(),
                ImplementationLevel = header.FileDescription.ImplementationLevel,
                Name = fileName.Name,
                TimeStamp = fileName.TimeStamp,
                Author = fileName.AuthorName.ToList(),
                Organization = fileName.Organization.ToList(),
                PreprocessorVersion = fileName.PreprocessorVersion,
                OriginatingSystem = fileName.OriginatingSystem,
                Authorization = fileName.AuthorizationName,
                Schemas = header.FileSchema.Schemas.ToList(),
                ProjectName = FindProject(model)?.Name,
            };
        }

        public IfcHopperModel Read(string path)
        {
            var model = XbimDocumentCache.Open(path);
            var project = FindProject(model) ?? throw new InvalidDataException("The IFC file has no IfcProject.");
            var fileName = model.Header.FileName;
            return new IfcHopperModel(new XbimModelLoader(model).LoadProject(project))
            {
                SourcePath = Path.GetFullPath(path),
                ReadWarnings = XbimCoordinateChecks.Warnings(model),
                Author = fileName.AuthorName.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)),
                Organization = fileName.Organization.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o)),
            };
        }

        public IReadOnlyList<string> GetAncestorIds(ModelObject obj)
        {
            if (!(obj?.Source is IIfcObjectDefinition entity)) return null;

            var ancestors = new List<string>();
            for (var parent = Parent(entity); parent != null; parent = Parent(parent))
                ancestors.Insert(0, parent.GlobalId);
            return ancestors;
        }

        /// <inheritdoc />
        public IReadOnlyList<string> GetTranslationLimitations(IfcHopperModel model)
        {
            if (!(model.Project.Source is IIfcProject project)) return System.Array.Empty<string>();
            var conversions = project.Model.Instances.OfType<IIfcMapConversion>().ToList();
            if (conversions.Count > 1 || conversions.Any(c => c.ExpressType.Name != "IfcMapConversion"))
                return new[] { "Whole-model translation supports one uniform IfcMapConversion; multiple or scaled map conversions are not supported." };
            var reached = new HashSet<IIfcObjectDefinition>();
            var pending = new Stack<IIfcObjectDefinition>();
            pending.Push(project);
            while (pending.Count > 0)
            {
                var item = pending.Pop();
                if (!reached.Add(item))
                    return new[] { $"'{item.Name}' occurs more than once in the source hierarchy. Whole-model translation requires a tree without cycles or repeated children." };
                foreach (var child in XbimModelLoader.MappedChildren(item)) pending.Push(child);
                if (item is IIfcElement element)
                    foreach (var relation in element.HasOpenings)
                        if (relation.RelatedOpeningElement != null) pending.Push(relation.RelatedOpeningElement);
            }
            var issues = new List<string>();
            var usedPlacements = new HashSet<IIfcObjectPlacement>();
            foreach (var product in project.Model.Instances.OfType<IIfcProduct>())
            {
                if (!reached.Contains(product))
                {
                    issues.Add($"{product.ExpressType.Name} '{product.Name}' is outside the supported project tree. Whole-model translation is not supported for this file.");
                    break;
                }
                var chain = new HashSet<IIfcObjectPlacement>();
                var placement = product.ObjectPlacement;
                if (placement == null)
                {
                    issues.Add($"{product.ExpressType.Name} '{product.Name}' has no placement. Whole-model translation requires local placements.");
                    break;
                }
                if (!usedPlacements.Add(placement))
                {
                    issues.Add($"'{product.Name}' shares its placement entity with another product. Whole-model translation of shared placements is not supported.");
                    break;
                }
                while (placement is IIfcLocalPlacement local && chain.Add(placement)) placement = local.PlacementRelTo;
                if (placement != null)
                {
                    issues.Add($"{product.ExpressType.Name} '{product.Name}' uses a non-local or cyclic placement chain. Whole-model translation requires local placements.");
                    break;
                }
            }
            return issues;
        }

        /// <summary>The aggregating object, or for elements the containing spatial structure.</summary>
        private static IIfcObjectDefinition Parent(IIfcObjectDefinition entity) =>
            entity.Decomposes.FirstOrDefault()?.RelatingObject ??
            (entity as IIfcElement)?.ContainedInStructure.FirstOrDefault()?.RelatingStructure;

        private static IIfcProject FindProject(IModel model) => model.Instances.FirstOrDefault<IIfcProject>();
    }
}
