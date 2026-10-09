using System;
using System.Collections.Generic;

namespace IfcHopper.Core.Model
{
    public class Project : ModelObject
    {
        public ChildView<Site> Sites => new ChildView<Site>(this);

        /// <summary>Facilities directly under the project, without a site.</summary>
        public ChildView<Facility> Facilities => new ChildView<Facility>(this);

        private LazyList<RepresentationContext> _contexts = new LazyList<RepresentationContext>();
        /// <summary>Representation contexts. When empty, a default 3D "Model" context is written.</summary>
        public List<RepresentationContext> Contexts => _contexts.Value;
        internal bool ContextsLoaded => _contexts.IsLoaded;

        /// <summary>Fills <see cref="Contexts"/> on first access (models read from a file).</summary>
        internal void LoadContexts(Func<IEnumerable<RepresentationContext>> loader) => _contexts.SetLoader(loader);

        /// <summary>Map conversion of the model context; null when not georeferenced.</summary>
        public Georeference Georeference { get; set; }

        /// <summary>Units written to the file. Null means SI defaults (m, m², m³, rad).</summary>
        public Units Units { get; set; }

        public const string DefaultName = "Hopper Project";

        public Project(string name = DefaultName) : base(name) { }

        protected override void CopyChildren()
        {
            _contexts = LazyList<RepresentationContext>.CopyOf(_contexts);
        }
    }
}
