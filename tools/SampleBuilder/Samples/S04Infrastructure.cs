using Grasshopper.Kernel;
using Rhino.Geometry;

namespace SampleBuilder.Samples
{
    /// <summary>IFC 4.3 facilities (road, bridge, railway, marine facility) with their parts and elements.</summary>
    internal sealed class S04Infrastructure : Sample
    {
        public override int Number => 4;
        public override string Title => "Infrastructure";
        public override string Summary =>
            "IFC 4.3 facilities on one site: a road, a bridge, a railway and a marine facility, divided into facility parts (type and usage) holding their elements.\n" +
            "Parts nest: the bridge deck holds its girders, the substructure holds a pier part.";

        private float _y = Top;

        protected override void BuildBody(Canvas c)
        {
            var road = Facility(c, "Road", "Main road", null,
                Part(c, "Road Part", "Carriageway", "CARRIAGEWAY", "LONGITUDINAL", Element(c, "IfcPavement", "Asphalt pavement", new Point3d(0, -20, -0.15), new Vector3d(20, 3.5, 0.15))));
            var deck = Part(c, "Bridge Part", "Deck", "DECK", "LONGITUDINAL", Element(c, "IfcBeam", "Girder", new Point3d(0, 0, 5), new Vector3d(15, 0.4, 0.6)));
            var pier = Part(c, "Bridge Part", "Pier 1", "PIER", "VERTICAL", Element(c, "IfcColumn", "Pier column", new Point3d(0, 0, 2), new Vector3d(0.6, 0.6, 2.2)));
            var substructure = Part(c, "Bridge Part", "Substructure", "SUBSTRUCTURE", "VERTICAL", null, pier);
            var bridge = Facility(c, "Bridge", "Creek bridge", "GIRDER", deck, substructure);
            var railway = Facility(c, "Railway", "Freight line", null,
                Part(c, "Railway Part", "Track", "PLAINTRACK", "LONGITUDINAL", Element(c, "IfcRail", "Rail", new Point3d(0, 20, 0.1), new Vector3d(20, 0.04, 0.08))));
            var marine = Facility(c, "Marine Facility", "Quay", null, Element(c, "IfcKerb", "Quay edge", new Point3d(0, 35, 0.5), new Vector3d(20, 0.3, 0.5)));

            var site = c.Ifc("Site", 1500, Top + 300);
            Canvas.Set(site, "Name", "Harbour corridor");
            foreach (var facility in new[] { road, bridge, railway, marine }) c.Wire(facility, site, "Facilities");
            var project = c.Ifc("Project", Canvas.RightOf(site), Top + 300);
            Canvas.Set(project, "Name", "Infrastructure");
            c.Wire(site, project, "Sites");
            c.Group("Site and project", Canvas.Colours.Ifc, site, project);
            WriteFile(c, Canvas.RightOf(project, 80), Top + 300, project, IfcName);
        }

        /// <summary>A box and an object in the next row; returns the object.</summary>
        private IGH_DocumentObject Element(Canvas c, string ifcClass, string name, Point3d centre, Vector3d half)
        {
            var box = Box(c, 0, _y, centre, half);
            var obj = Object(c, Canvas.RightOf(box), _y, box, ifcClass, name);
            c.Group(null, Canvas.Colours.Geometry, box, obj);
            _y = Canvas.Below(obj);
            return obj;
        }

        /// <summary>A facility part right of its contents; <paramref name="element"/> goes to Elements, <paramref name="nested"/> to Parts.</summary>
        private static IGH_DocumentObject Part(Canvas c, string component, string name, string type, string usage, IGH_DocumentObject element, IGH_DocumentObject nested = null)
        {
            var anchor = element ?? nested;
            var part = c.Ifc(component, Canvas.RightOf(anchor, 80), anchor.Attributes.Bounds.Y);
            Canvas.Set(part, "Name", name);
            Canvas.Set(part, "Type", type);
            Canvas.Set(part, "Usage", usage);
            if (element != null) c.Wire(element, part, "Elements");
            if (nested != null) c.Wire(nested, part, "Parts");
            return part;
        }

        /// <summary>A facility right of its first child; children that are parts go to Parts, objects to Elements.</summary>
        private static IGH_DocumentObject Facility(Canvas c, string component, string name, string type, params IGH_DocumentObject[] children)
        {
            var x = 1100f;
            var facility = c.Ifc(component, x, children[0].Attributes.Bounds.Y);
            Canvas.Set(facility, "Name", name);
            if (type != null) Canvas.Set(facility, "Type", type);
            foreach (var child in children) c.Wire(child, facility, child.Name == "Object" ? "Elements" : "Parts");
            c.Group(null, Canvas.Colours.Ifc, facility);
            return facility;
        }
    }
}
