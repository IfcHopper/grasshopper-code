using System;
using System.Collections.Generic;

namespace IfcHopper.Core.Model
{
    /// <summary>
    /// Opening or recess in an element (IfcOpeningElement, or another IfcFeatureElementSubtraction), related to the element it voids by
    /// IfcRelVoidsElement. Its geometry is the volume subtracted from the host, whose own body stays uncut, as IFC stores it.
    /// Doors and windows fill it through IfcRelFillsElement.
    /// </summary>
    public class Opening : Element
    {
        public const string DefaultOpeningClass = "IfcOpeningElement";
        public const string DefaultOpeningName = "Hopper Opening";

        private LazyList<Element> _fills = new LazyList<Element>();

        /// <summary>
        /// Elements filling the opening (IfcRelFillsElement), e.g. a door or window. Read from a file they are separate objects with the
        /// same GlobalId as the element in the spatial structure.
        /// </summary>
        public List<Element> Fills => _fills.Value;
        internal bool FillsLoaded => _fills.IsLoaded;

        /// <summary>Fills <see cref="Fills"/> on first access (models read from a file).</summary>
        internal void LoadFills(Func<IEnumerable<Element>> loader) => _fills.SetLoader(loader);

        public Opening(string name = DefaultOpeningName, string ifcClass = DefaultOpeningClass) : base(name, ifcClass)
        {
        }

        protected override void CopyChildren()
        {
            base.CopyChildren();
            _fills = LazyList<Element>.CopyOf(_fills);
        }
    }
}
