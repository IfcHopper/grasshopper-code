using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace IfcHopper.Types
{
    /// <summary>
    /// Placement input value: a world plane in document units. Its own type, not a plane, so later placements
    /// (e.g. linear placements along an alignment) can use the same Placement inputs without changing them.
    /// </summary>
    public class PlacementGoo : GH_Goo<Plane>, IGH_PreviewData
    {
        public PlacementGoo() { Value = Plane.Unset; }

        public PlacementGoo(Plane value) : base(value) { }

        public override bool IsValid => Value.IsValid;

        public override string TypeName => "IFC Placement";

        public override string TypeDescription => "IfcHopper placement: a world plane in document units.";

        public override IGH_Goo Duplicate() => new PlacementGoo(Value);

        public override string ToString() => IsValid ? $"Placement {new GH_Plane(Value)}" : "Null Placement";

        public override bool CastFrom(object source)
        {
            if (source is PlacementGoo goo)
            {
                Value = goo.Value;
                return true;
            }
            var plane = Plane.Unset;
            if (!GH_Convert.ToPlane(source, ref plane, GH_Conversion.Both)) return false;
            Value = plane;
            return true;
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(GH_Plane)))
            {
                target = (Q)(object)new GH_Plane(Value);
                return true;
            }
            if (typeof(Q) == typeof(Plane))
            {
                target = (Q)(object)Value;
                return true;
            }
            return false;
        }

        // Stored like a plane, so values internalised in the former plane inputs still load.
        public override bool Write(GH_IWriter writer) => new GH_Plane(Value).Write(writer);

        public override bool Read(GH_IReader reader)
        {
            var plane = new GH_Plane();
            if (!plane.Read(reader)) return false;
            Value = plane.Value;
            return true;
        }

        public BoundingBox ClippingBox => new GH_Plane(Value).ClippingBox;

        public void DrawViewportWires(GH_PreviewWireArgs args) => new GH_Plane(Value).DrawViewportWires(args);

        public void DrawViewportMeshes(GH_PreviewMeshArgs args) { }
    }
}
