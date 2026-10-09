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

        /// <summary>The aggregating object, or for elements the containing spatial structure.</summary>
        private static IIfcObjectDefinition Parent(IIfcObjectDefinition entity) =>
            entity.Decomposes.FirstOrDefault()?.RelatingObject ??
            (entity as IIfcElement)?.ContainedInStructure.FirstOrDefault()?.RelatingStructure;

        private static IIfcProject FindProject(IModel model) => model.Instances.FirstOrDefault<IIfcProject>();
    }
}
