using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace SampleBuilder.Samples
{
    /// <summary>Tags and class-specific attributes of doors, windows and a window type.</summary>
    internal sealed class S12TagsAndAttributes : Sample
    {
        public override int Number => 12;
        public override string Title => "Tags and attributes";
        public override string Summary =>
            "Tag is the mark of an element or type (e.g. D01). Attributes are the values IFC defines on a class beyond those of the other inputs,\n" +
            "e.g. OverallHeight and OperationType of IfcDoor: give names and values; a wrong name lists the attributes of the class.";

        protected override void BuildBody(Canvas c)
        {
            var door = Object(c, 0, Top, null, "IfcDoor", "Front door", "DOOR");
            Canvas.Set(door, "Geometry", Brep.CreateFromBox(new BoundingBox(0, 0, 0, 0.9, 0.06, 2.1)));
            Canvas.Set(door, "Tag", "D01");
            Canvas.Set(door, "Attribute Names", "OverallHeight", "OverallWidth", "OperationType");
            Canvas.Set(door, "Attribute Values", new GH_Number(2.1), new GH_Number(0.9), new GH_String("SINGLE_SWING_LEFT"));

            var windowType = c.Ifc("Element Type", Canvas.RightOf(door), Top);
            Canvas.Set(windowType, "Class", "IfcWindowType");
            Canvas.Set(windowType, "Name", "Window W1");
            Canvas.Set(windowType, "Type", "WINDOW");
            Canvas.Set(windowType, "Tag", "W1");
            Canvas.Set(windowType, "Attribute Names", "PartitioningType", "ParameterTakesPrecedence");
            Canvas.Set(windowType, "Attribute Values", new GH_String("SINGLE_PANEL"), new GH_Boolean(true));
            var window = Object(c, Canvas.RightOf(windowType), Top, null, "IfcWindow", "Window", "WINDOW");
            Canvas.Set(window, "Geometry", Brep.CreateFromBox(new BoundingBox(2, 0, 0.9, 3.2, 0.06, 2.1)));
            Canvas.Set(window, "Tag", "W1-01");
            Canvas.Set(window, "Attribute Names", "OverallHeight", "OverallWidth");
            Canvas.Set(window, "Attribute Values", new GH_Number(1.2), new GH_Number(1.2));
            c.Wire(windowType, window, "Element Type");
            c.Group("1. A door, a window type and a window with tags and attributes (lengths in document units)", Canvas.Colours.Ifc, door, windowType, window);

            var y = Canvas.Below(door, 60);
            var deDoor = c.Ifc("Deconstruct Object", 0, y);
            c.Wire(door, deDoor, "Object");
            var tag = Show(c, deDoor, "Tag", Canvas.RightOf(deDoor, 30), y);
            var names = Show(c, deDoor, "Attribute Names", Canvas.RightOf(deDoor, 30), Canvas.Below(tag, 20), 220, 80);
            var values = Show(c, deDoor, "Attribute Values", Canvas.RightOf(deDoor, 30), Canvas.Below(names, 20), 220, 80);
            var deType = c.Ifc("Deconstruct Element Type", Canvas.RightOf(names, 80), y);
            c.Wire(windowType, deType, "Element Type");
            var typeValues = Show(c, deType, "Attribute Values", Canvas.RightOf(deType, 30), y, 220, 60);
            c.Group("2. Deconstruct: tags and attributes come back by name", Canvas.Colours.Read, deDoor, tag, names, values, deType, typeValues);

            var project = House(c, Canvas.RightOf(window, 120), Top, "Tags and attributes", door, window);
            WriteFile(c, Canvas.RightOf(project, 80), Top, project, IfcName);
        }
    }
}
