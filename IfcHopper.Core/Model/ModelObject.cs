using System;
using System.Collections.Generic;

namespace IfcHopper.Core.Model
{
    /// <summary>
    /// Base class for every object of the IfcHopper model.
    /// </summary>
    public abstract class ModelObject
    {
        /// <summary>IFC GlobalId (22 chars). Generated on write when empty.</summary>
        public string GlobalId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// World placement in metres. Null means the parent placement. Storeys are written at their elevation in the building;
        /// their placement is set on read only. Not used for projects.
        /// </summary>
        public Placement Placement { get; set; }

        /// <summary>Backend entity this object was read from; null for objects created in Grasshopper.</summary>
        public object Source { get; internal set; }

        private LazyList<ModelObject> _children = new LazyList<ModelObject>();

        /// <summary>
        /// Child objects: spatial objects and the elements they contain, or the parts of an element.
        /// Allowed children are given by <see cref="SpatialRules"/>; typed views (e.g. Site.Facilities) filter this list.
        /// </summary>
        public List<ModelObject> Children => _children.Value;
        internal bool ChildrenLoaded => _children.IsLoaded;

        /// <summary>Fills <see cref="Children"/> on first access (models read from a file).</summary>
        internal void LoadChildren(Func<IEnumerable<ModelObject>> loader) => _children.SetLoader(loader);

        private LazyList<PropertySetDefinition> _propertySets = new LazyList<PropertySetDefinition>();

        /// <summary>
        /// Property and quantity sets (IfcRelDefinesByProperties). Sets of the object's type are included with
        /// <see cref="PropertySetDefinition.FromType"/>, unless the object has its own set of the same name.
        /// </summary>
        public List<PropertySetDefinition> PropertySets => _propertySets.Value;
        internal bool PropertySetsLoaded => _propertySets.IsLoaded;

        /// <summary>Fills <see cref="PropertySets"/> on first access (models read from a file).</summary>
        internal void LoadPropertySets(Func<IEnumerable<PropertySetDefinition>> loader) => _propertySets.SetLoader(loader);

        /// <summary>
        /// Puts each set in place of the set with the same name (also one from the type), or adds it.
        /// A set without properties removes the set with its name.
        /// </summary>
        public void MergePropertySets(IEnumerable<PropertySetDefinition> sets)
        {
            foreach (var set in sets)
            {
                var index = PropertySets.FindIndex(s => s.Name == set.Name);
                if (index >= 0) PropertySets.RemoveAt(index);
                if (set.Count > 0) PropertySets.Insert(index >= 0 ? index : PropertySets.Count, set);
            }
        }

        private LazyList<ClassificationReference> _classifications = new LazyList<ClassificationReference>();

        /// <summary>
        /// Classification references (IfcRelAssociatesClassification). References of the object's type are included with
        /// <see cref="ClassificationReference.FromType"/>, unless the object has its own reference in the same system.
        /// </summary>
        public List<ClassificationReference> Classifications => _classifications.Value;
        internal bool ClassificationsLoaded => _classifications.IsLoaded;

        /// <summary>Fills <see cref="Classifications"/> on first access (models read from a file).</summary>
        internal void LoadClassifications(Func<IEnumerable<ClassificationReference>> loader) => _classifications.SetLoader(loader);

        /// <summary>
        /// Puts each reference in place of the references in the same system (also those from the type), or adds it.
        /// A reference without a code removes the references of its system.
        /// </summary>
        public void MergeClassifications(IEnumerable<ClassificationReference> references)
        {
            foreach (var reference in references)
            {
                var index = Classifications.FindIndex(c => c.SameSystem(reference));
                Classifications.RemoveAll(c => c.SameSystem(reference));
                if (reference.Code != null) Classifications.Insert(index >= 0 ? index : Classifications.Count, reference);
            }
        }

        protected ModelObject(string name = null)
        {
            Name = name;
        }

        /// <summary>
        /// A copy with the same GlobalId, source and values; child lists are copied on first access.
        /// Components edit copies, never the objects they receive.
        /// </summary>
        public ModelObject Copy()
        {
            var copy = (ModelObject)MemberwiseClone();
            copy._children = LazyList<ModelObject>.CopyOf(_children);
            copy._propertySets = LazyList<PropertySetDefinition>.CopyOf(_propertySets);
            copy._classifications = LazyList<ClassificationReference>.CopyOf(_classifications);
            copy.CopyChildren();
            return copy;
        }

        /// <summary>Replaces other lists shared by MemberwiseClone with lazy copies.</summary>
        protected virtual void CopyChildren() { }
    }
}
