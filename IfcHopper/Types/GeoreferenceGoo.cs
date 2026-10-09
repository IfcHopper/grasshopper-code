using GH_IO.Serialization;
using Grasshopper.Kernel.Types;
using IfcHopper.Core.Model;

namespace IfcHopper.Types
{
    public class GeoreferenceGoo : IfcHopperGoo<Georeference>
    {
        public GeoreferenceGoo() { }

        public GeoreferenceGoo(Georeference value) : base(value) { }

        public override string TypeName => "IFC Georeference";

        public override string TypeDescription => "IfcHopper georeference (IfcMapConversion to an IfcProjectedCRS).";

        public override IGH_Goo Duplicate() => new GeoreferenceGoo(Value);

        public override string ToString() =>
            Value == null ? "Null Georeference" : $"IfcMapConversion: {Value.CrsName} E {Value.Eastings:0.###} N {Value.Northings:0.###}";

        public override bool Write(GH_IWriter writer)
        {
            if (Value == null) return true;
            SetString(writer, "CrsName", Value.CrsName);
            SetString(writer, "CrsDescription", Value.CrsDescription);
            SetString(writer, "GeodeticDatum", Value.GeodeticDatum);
            SetString(writer, "VerticalDatum", Value.VerticalDatum);
            SetString(writer, "MapProjection", Value.MapProjection);
            SetString(writer, "MapZone", Value.MapZone);
            writer.SetDouble("Eastings", Value.Eastings);
            writer.SetDouble("Northings", Value.Northings);
            writer.SetDouble("OrthogonalHeight", Value.OrthogonalHeight);
            writer.SetDouble("XAxisAbscissa", Value.XAxisAbscissa);
            writer.SetDouble("XAxisOrdinate", Value.XAxisOrdinate);
            writer.SetDouble("Scale", Value.Scale);
            return true;
        }

        public override bool Read(GH_IReader reader)
        {
            if (!reader.ItemExists("Eastings"))
            {
                Value = null;
                return true;
            }
            Value = new Georeference(GetString(reader, "CrsName"))
            {
                CrsDescription = GetString(reader, "CrsDescription"),
                GeodeticDatum = GetString(reader, "GeodeticDatum"),
                VerticalDatum = GetString(reader, "VerticalDatum"),
                MapProjection = GetString(reader, "MapProjection"),
                MapZone = GetString(reader, "MapZone"),
                Eastings = reader.GetDouble("Eastings"),
                Northings = reader.GetDouble("Northings"),
                OrthogonalHeight = reader.GetDouble("OrthogonalHeight"),
                XAxisAbscissa = reader.GetDouble("XAxisAbscissa"),
                XAxisOrdinate = reader.GetDouble("XAxisOrdinate"),
                Scale = reader.GetDouble("Scale"),
            };
            return true;
        }

        private static void SetString(GH_IWriter writer, string name, string value)
        {
            if (value != null) writer.SetString(name, value);
        }

        private static string GetString(GH_IReader reader, string name) => reader.ItemExists(name) ? reader.GetString(name) : null;
    }
}
