using System;
using System.Collections.Generic;
using System.Linq;

namespace IfcHopper.Core.Model
{
    /// <summary>Model object whose IFC entity may have a PredefinedType attribute.</summary>
    public abstract class TypedModelObject : ModelObject
    {
        public const string UserDefined = "USERDEFINED";

        /// <summary>IFC predefined type (e.g. "DECK"), <see cref="UserDefined"/> or null when not set.</summary>
        public string PredefinedType { get; private set; }

        /// <summary>IfcObject.ObjectType, holds the custom type when <see cref="PredefinedType"/> is USERDEFINED.</summary>
        public string ObjectType { get; private set; }

        protected TypedModelObject(string name) : base(name) { }

        /// <summary>
        /// Predefined type values from the IFC4X3_ADD2 schema (USERDEFINED and NOTDEFINED excluded),
        /// or null when the IFC entity has no PredefinedType.
        /// </summary>
        public abstract IReadOnlyList<string> PredefinedTypes { get; }

        public bool HasPredefinedType => PredefinedTypes != null;

        /// <summary>
        /// Sets the predefined type. Values not in <see cref="PredefinedTypes"/> become USERDEFINED with the value as ObjectType.
        /// Returns false when the value was stored as user defined.
        /// </summary>
        public bool SetType(string type)
        {
            if (!HasPredefinedType) throw new InvalidOperationException($"{GetType().Name} '{Name}' has no predefined type.");

            PredefinedType = ObjectType = null;
            if (string.IsNullOrWhiteSpace(type)) return true;

            var match = PredefinedTypes.FirstOrDefault(t => t.Equals(type.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                PredefinedType = match;
                return true;
            }
            PredefinedType = UserDefined;
            ObjectType = type.Trim();
            return false;
        }
    }
}
