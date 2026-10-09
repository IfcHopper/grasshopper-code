using System.Collections.Generic;
using IfcHopper.Core.Model;

namespace IfcHopper.Core.IO
{
    /// <summary>Schema knowledge provided by the active backend (IFC4X3_ADD2 for xBIM).</summary>
    public interface ISchemaInfo
    {
        /// <summary>Instantiable IfcElement subclasses, e.g. "IfcWall".</summary>
        IReadOnlyList<string> ElementClasses { get; }

        /// <summary>The schema spelling of any entity, abstract ones included (case-insensitive), e.g. "IfcBuildingElement"; null when there is none.</summary>
        string FindClass(string name);

        /// <summary>
        /// True when <paramref name="ifcClass"/> is <paramref name="baseClass"/> or one of its subtypes (case-insensitive), e.g. IfcWall of
        /// IfcBuiltElement. Classes the schema lacks (e.g. IFC2X3 ones) match only their own name.
        /// </summary>
        bool IsClassOf(string ifcClass, string baseClass);

        /// <summary>The schema spelling of an element class (case-insensitive), or null when it is not an instantiable element class.</summary>
        string FindElementClass(string name);

        /// <summary>The schema spelling of an element type class (case-insensitive), e.g. "IfcWallType", or null when it is none.</summary>
        string FindTypeClass(string name);

        /// <summary>Type class of an element class (IfcWall: IfcWallType), or null when the class has no type class of its own.</summary>
        string TypeClassOf(string elementClass);

        /// <summary>Element class of a type class (IfcWallType: IfcWall; IFC2X3 IfcDoorStyle: IfcDoor), or null when there is none.</summary>
        string ElementClassOf(string typeClass);

        /// <summary>PredefinedType values of an element or type class (USERDEFINED and NOTDEFINED excluded), or null when it has no PredefinedType.</summary>
        IReadOnlyList<string> GetPredefinedTypes(string elementClass);

        /// <summary>
        /// Attributes of an element or type class beyond those IfcHopper models (names, Tag, PredefinedType, ObjectType, ElementType, placement,
        /// representation), with simple values; empty for unknown classes. E.g. IfcDoor: OverallHeight, OverallWidth, OperationType.
        /// </summary>
        IReadOnlyList<AttributeInfo> GetAttributes(string ifcClass);

        /// <summary>The schema spelling of a simple value type (case-insensitive), e.g. "IfcLabel" or "IfcLengthMeasure", or null when it is none.</summary>
        string FindValueType(string name);

        /// <summary>CLR form of a value type, or null when it is not a simple value type.</summary>
        ValueKind? GetValueKind(string valueType);
    }
}
