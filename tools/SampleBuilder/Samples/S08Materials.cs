using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace SampleBuilder.Samples
{
    /// <summary>Single materials, a layer set and a constituent set, assigned to objects and a type.</summary>
    internal sealed class S08Materials : Sample
    {
        public override int Number => 8;
        public override string Title => "Materials";
        public override string Summary =>
            "Materials (IfcMaterial) have a category, a colour shown on geometry without its own, and property sets. Layer sets stack materials with thicknesses\n" +
            "(walls, slabs), constituent sets split an element into parts (frame, glazing). An object without a material shows its type's.";

        protected override void BuildBody(Canvas c)
        {
            var density = c.Ifc("Pset", 0, Top);
            Canvas.Set(density, "Name", "Pset_MaterialCommon");
            Canvas.Set(density, "Names", "MassDensity");
            Canvas.Set(density, "Values", new GH_Number(2400));
            var concrete = Material(c, Canvas.RightOf(density), Top, "Concrete C30/37", "concrete", Color.FromArgb(255, 185, 185, 180));
            c.Wire(density, concrete, "Property Sets");
            var brick = Material(c, Canvas.RightOf(density), Canvas.Below(concrete), "Brick", "brick", Color.FromArgb(255, 170, 85, 60));
            var wool = Material(c, Canvas.RightOf(density), Canvas.Below(brick), "Mineral wool", "insulation", Color.FromArgb(255, 235, 215, 120));
            var aluminium = Material(c, Canvas.RightOf(density), Canvas.Below(wool), "Aluminium", "aluminium", Color.FromArgb(255, 200, 200, 205));
            var glass = Material(c, Canvas.RightOf(density), Canvas.Below(aluminium), "Glass", "glass", Color.FromArgb(120, 150, 200, 230));
            var steel = Material(c, Canvas.RightOf(density), Canvas.Below(glass), "Steel S355", "steel", Color.FromArgb(255, 110, 115, 125));
            c.Group("1. Materials (concrete with a property set)", Canvas.Colours.Ifc, density, concrete, brick, wool, aluminium, glass, steel);

            var x = Canvas.RightOf(concrete, 80);
            var layers = c.Ifc("Layer Set", x, Top);
            Canvas.Set(layers, "Name", "Cavity wall 300");
            foreach (var material in new[] { brick, wool, concrete }) c.Wire(material, layers, "Materials");
            Canvas.Set(layers, "Thicknesses", 0.1, 0.08, 0.12);
            Canvas.Set(layers, "Layer Names", "Facing", "Insulation", "Core");
            var constituents = c.Ifc("Constituent Set", x, Canvas.Below(layers));
            Canvas.Set(constituents, "Name", "Window W1");
            c.Wire(aluminium, constituents, "Materials");
            c.Wire(glass, constituents, "Materials");
            Canvas.Set(constituents, "Constituent Names", "Frame", "Glazing");
            Canvas.Set(constituents, "Fractions", 0.15, 0.85);
            c.Group("2. A layer set and a constituent set", Canvas.Colours.Ifc, layers, constituents);

            var x2 = Canvas.RightOf(layers, 80);
            var wall = Element(c, x2, Top, new Point3d(3, 0.15, 1.5), new Vector3d(3, 0.15, 1.5), "IfcWall", "Cavity wall", layers);
            var slab = Element(c, x2, Canvas.Below(wall), new Point3d(3, 2.5, -0.1), new Vector3d(3, 2.5, 0.1), "IfcSlab", "Slab", concrete);
            var window = Element(c, x2, Canvas.Below(slab), new Point3d(1.5, 0.15, 1.5), new Vector3d(0.6, 0.05, 0.6), "IfcWindow", "Window", constituents);
            var beamType = c.Ifc("Element Type", x2, Canvas.Below(window));
            Canvas.Set(beamType, "Class", "IfcBeamType");
            Canvas.Set(beamType, "Name", "HEA 200");
            c.Wire(steel, beamType, "Material");
            var beam = Element(c, x2, Canvas.Below(beamType), new Point3d(3, 2.5, 3.1), new Vector3d(3, 0.1, 0.1), "IfcBeam", "Beam", null);
            c.Wire(beamType, beam, "Element Type");
            c.Group("3. Objects with a material; the beam shows the steel of its type", Canvas.Colours.Ifc, wall, slab, window, beamType, beam);

            var deconstruct = c.Ifc("Deconstruct Material", Canvas.RightOf(wall, 80), Top + 900);
            c.Wire(layers, deconstruct, "Material");
            var parts = Show(c, deconstruct, "Part Names", Canvas.RightOf(deconstruct, 30), Top + 900, 220, 70);
            var thicknesses = Show(c, deconstruct, "Thicknesses", Canvas.RightOf(deconstruct, 30), Canvas.Below(parts, 20), 220, 70);
            var deBeam = c.Ifc("Deconstruct Object", Canvas.RightOf(wall, 80), Canvas.Below(deconstruct, 60));
            c.Wire(beam, deBeam, "Object");
            var beamMaterial = Show(c, deBeam, "Material", Canvas.RightOf(deBeam, 30), deBeam.Attributes.Bounds.Y);
            c.Group("4. Deconstruct Material lists the layers; Deconstruct Object gives the type's material", Canvas.Colours.Read, deconstruct, parts, thicknesses, deBeam, beamMaterial);

            var project = House(c, Canvas.RightOf(wall, 80), Top, "Materials", wall, slab, window, beam);
            WriteFile(c, Canvas.RightOf(project, 80), Top, project, IfcName);
        }

        private static IGH_DocumentObject Material(Canvas c, float x, float y, string name, string category, Color colour)
        {
            var material = c.Ifc("Material", x, y);
            Canvas.Set(material, "Name", name);
            Canvas.Set(material, "Category", category);
            Canvas.Set(material, "Colour", colour);
            return material;
        }

        /// <summary>An object with a box as internalised geometry and a material.</summary>
        private static IGH_DocumentObject Element(Canvas c, float x, float y, Point3d centre, Vector3d half, string ifcClass, string name, IGH_DocumentObject material)
        {
            var obj = Object(c, x, y, null, ifcClass, name);
            Canvas.Set(obj, "Geometry", Brep.CreateFromBox(new BoundingBox(centre - half, centre + half)));
            if (material != null) c.Wire(material, obj, "Material");
            return obj;
        }
    }
}
