using GH_IO.Serialization;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace IfcHopper.Types
{
    public class UnitsGoo : IfcHopperGoo<Units>
    {
        public UnitsGoo() { }

        public UnitsGoo(Units value) : base(value) { }

        public override string TypeName => "IFC Units";

        public override string TypeDescription => "IfcHopper project units (IfcUnitAssignment).";

        public override IGH_Goo Duplicate() => new UnitsGoo(Value);

        public override string ToString() =>
            Value == null ? "Null Units" : $"IfcUnitAssignment: {Value.Length.Symbol}, {Value.Area.Symbol}, {Value.Volume.Symbol}, {Value.Angle.Symbol}";

        public override bool Write(GH_IWriter writer)
        {
            if (Value == null) return true;
            writer.SetString("Length", Value.Length.Symbol);
            writer.SetString("Area", Value.Area.Symbol);
            writer.SetString("Volume", Value.Volume.Symbol);
            writer.SetString("Angle", Value.Angle.Symbol);
            return true;
        }

        public override bool Read(GH_IReader reader)
        {
            if (!reader.ItemExists("Length"))
            {
                Value = null;
                return true;
            }
            var units = new Units();
            units.Length = Units.Find(UnitKind.Length, reader.GetString("Length")) ?? units.Length;
            units.Area = Units.Find(UnitKind.Area, reader.GetString("Area")) ?? units.Area;
            units.Volume = Units.Find(UnitKind.Volume, reader.GetString("Volume")) ?? units.Volume;
            units.Angle = Units.Find(UnitKind.Angle, reader.GetString("Angle")) ?? units.Angle;
            Value = units;
            return true;
        }
    }
}
