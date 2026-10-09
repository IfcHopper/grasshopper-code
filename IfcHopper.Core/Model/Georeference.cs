namespace IfcHopper.Core.Model
{
    /// <summary>
    /// Map conversion of a project (IfcMapConversion to an IfcProjectedCRS): how model coordinates map to a projected CRS.
    /// Distances are in metres; the writer adjusts the IFC scale for the file length unit.
    /// </summary>
    public class Georeference
    {
        /// <summary>CRS name, e.g. "EPSG:5110".</summary>
        public string CrsName { get; set; }
        public string CrsDescription { get; set; }
        public string GeodeticDatum { get; set; }
        public string VerticalDatum { get; set; }
        public string MapProjection { get; set; }
        public string MapZone { get; set; }

        /// <summary>Map coordinates of the model origin, in metres.</summary>
        public double Eastings { get; set; }
        public double Northings { get; set; }
        public double OrthogonalHeight { get; set; }

        /// <summary>Direction of the model X axis in map coordinates (abscissa, ordinate).</summary>
        public double XAxisAbscissa { get; set; } = 1.0;
        public double XAxisOrdinate { get; set; }

        /// <summary>Map distance per model distance, both in metres (1 = no scaling).</summary>
        public double Scale { get; set; } = 1.0;

        public Georeference(string crsName = null)
        {
            CrsName = crsName;
        }
    }
}
