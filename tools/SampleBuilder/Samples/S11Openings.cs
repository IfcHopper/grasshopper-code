using System;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace SampleBuilder.Samples
{
    /// <summary>A wall with a door opening, a window opening and a recess, filled by a door and a window.</summary>
    internal sealed class S11Openings : Sample
    {
        public override int Number => 11;
        public override string Title => "Openings";
        public override string Summary =>
            "Openings (IfcOpeningElement) void the object they are connected to: the wall keeps its uncut body in the file and is shown cut.\n" +
            "Doors and windows fill an opening (Fills) and must also be in the storey. A recess cuts only part of the thickness.\n" +
            "Construction volumes and opening previews are hidden so they do not cover the cuts; enable their Preview to inspect them.";

        protected override void BuildBody(Canvas c)
        {
            var doorHole = Volume(c, 0, Top, new Point3d(1.5, 0.1, 1.05), new Vector3d(0.5, 0.2, 1.05));
            var windowHole = Volume(c, 0, Canvas.Below(doorHole), new Point3d(4.5, 0.1, 1.5), new Vector3d(0.75, 0.2, 0.6));
            var nicheBox = Volume(c, 0, Canvas.Below(windowHole), new Point3d(3, 0.17, 1.2), new Vector3d(0.3, 0.05, 0.3));
            foreach (var volume in new[] { doorHole, windowHole, nicheBox }) ((IGH_PreviewObject)volume).Hidden = true;
            c.Group("1. Opening volumes (larger than the wall thickness, except the niche)", Canvas.Colours.Geometry, doorHole, windowHole, nicheBox);

            var x = Canvas.RightOf(doorHole, 80);
            var door = Object(c, x, Top, null, "IfcDoor", "Door", "DOOR");
            Canvas.Set(door, "Geometry", Brep.CreateFromBox(new BoundingBox(1.05, 0.07, 0, 1.95, 0.13, 2.05)));
            var window = Object(c, Canvas.RightOf(door), Top, null, "IfcWindow", "Window", "WINDOW");
            Canvas.Set(window, "Geometry", Brep.CreateFromBox(new BoundingBox(3.8, 0.07, 0.95, 5.2, 0.13, 2.05)));
            c.Group("2. The door and the window", Canvas.Colours.Ifc, door, window);

            var y = Canvas.Below(door, 60);
            var doorOpening = Opening(c, x, y, doorHole, "Door opening", "OPENING", door);
            var windowOpening = Opening(c, Canvas.RightOf(doorOpening), y, windowHole, "Window opening", "OPENING", window);
            var niche = Opening(c, Canvas.RightOf(windowOpening), y, nicheBox, "Niche", "RECESS", null);
            c.Group("3. Openings, filled by the door and the window; the niche is a recess", Canvas.Colours.Ifc, doorOpening, windowOpening, niche);

            var wall = Object(c, Canvas.RightOf(niche, 80), y, null, "IfcWall", "Wall", "SOLIDWALL");
            Canvas.Set(wall, "Geometry", Brep.CreateFromBox(new BoundingBox(0, 0, 0, 6, 0.2, 3)));
            foreach (var opening in new[] { doorOpening, windowOpening, niche }) c.Wire(opening, wall, "Openings");
            var deconstruct = c.Ifc("Deconstruct Object", Canvas.RightOf(wall, 80), y);
            c.Wire(wall, deconstruct, "Object");
            ((IGH_PreviewObject)Canvas.Output(deconstruct, "Openings")).Hidden = true;
            c.BeforeSave.Add(() =>
            {
                PreviewChecks.OutputsOnly("Object");
                PreviewChecks.OutputsOnly("Modify Object");
                CheckWall(wall, deconstruct);
            });
            var deOpening = c.Ifc("Deconstruct Object", Canvas.RightOf(deconstruct, 50), y);
            ((IGH_PreviewObject)deOpening).Hidden = true;
            c.Wire(deconstruct, deOpening, "Object", "Openings");
            var names = Show(c, deOpening, "Name", Canvas.RightOf(deOpening, 30), y, 200, 70);
            var fills = Show(c, deOpening, "Fills", Canvas.RightOf(deOpening, 30), Canvas.Below(names, 20), 200, 70);
            c.Group("4. The wall with its openings: Geometry is cut, Openings and Fills deconstruct", Canvas.Colours.Read, wall, deconstruct, deOpening, names, fills);

            var project = House(c, Canvas.RightOf(window, 120), Top, "Openings", wall, door, window);
            WriteFile(c, Canvas.RightOf(project, 80), Top, project, IfcName);
        }

        private static IGH_DocumentObject Volume(Canvas c, float x, float y, Point3d centre, Vector3d half) => Box(c, x, y, centre, half);

        private static void CheckWall(IGH_DocumentObject wall, IGH_DocumentObject deconstruct)
        {
            // 6 x 0.2 x 3 wall, less door, window and the 0.08 m deep part of the recess inside the wall.
            const double expectedVolume = 3.6 - 0.42 - 0.36 - 0.0288;
            var geometry = Canvas.Output(deconstruct, "Geometry").VolatileData.AllData(true).OfType<GH_Mesh>().ToList();
            var volume = geometry.Sum(g => g.Value.Volume());
            Console.WriteLine($"  Opening check: deconstructed wall volume = {volume:R} m3 (expected {expectedVolume:R})");
            if (geometry.Count == 0 || Math.Abs(volume - expectedVolume) > 1e-6)
                throw new InvalidOperationException("The wall geometry must subtract the door, window and recess volumes.");

            GH_Mesh preview = null;
            var element = Canvas.Output(wall).VolatileData.AllData(true).Single();
            if (!element.CastTo(out preview) || preview?.Value == null)
                throw new InvalidOperationException("The wall preview/cast must use the same cut body as Deconstruct Object.");
            using (preview.Value)
                if (Math.Abs(preview.Value.Volume() - expectedVolume) > 1e-6)
                    throw new InvalidOperationException("The wall preview/cast must use the same cut body as Deconstruct Object.");
        }

        private static IGH_DocumentObject Opening(Canvas c, float x, float y, IGH_DocumentObject geometry, string name, string type, IGH_DocumentObject fill)
        {
            var opening = c.Ifc("Opening", x, y);
            Canvas.Set(opening, "Name", name);
            Canvas.Set(opening, "Type", type);
            c.Wire(geometry, opening, "Geometry");
            ((IGH_PreviewObject)opening).Hidden = true;
            if (fill != null) c.Wire(fill, opening, "Fills");
            return opening;
        }
    }
}
