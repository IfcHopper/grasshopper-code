# Compact component parameters

New creation, Modify and Deconstruct components with secondary metadata start compact. Click the arrow at the bottom
to show advanced parameters, then the arrow at the top to collapse unused ones. The context
menu's **Show advanced inputs** option (**Show advanced parameters** when outputs can collapse) performs the same action. The control uses
a small circular chevron centred two canvas units above the content-box bottom to
expand and on its top edge to collapse, matching the annotation control placement.
Input labels remain right-aligned and output labels left-aligned when toggling.

The circle has a resting radius of 1.5 canvas units and a maximum radius of 3.
It grows linearly as the cursor approaches from 9 units to 3 units from its centre;
outside that range it stays at the corresponding minimum or maximum. Screen
distances scale with canvas zoom. These values were checked against the actual
Rhino 8.37 annotation renderer at distances 0, 3, 4.5, 6, 7.5, 9 and 12.
The chevron has flat stroke ends and a sharp join. Hover refresh listens to the
canvas, so the same growth applies from every direction, including outside the
component rectangle. The handlers detach on component removal/replacement,
document changes and canvas disposal; leaving the canvas restores resting size.

## Individual plus/minus controls

At the normal Grasshopper parameter-control zoom level, eligible visible parameters
have a small minus button on the content-box edge at the centre of their row. A plus
button sits at each gap containing hidden parameters. Click it to reveal the next
hidden parameter at that gap in its original registered order; the tooltip names
that parameter. Repeated clicks reveal subsequent hidden parameters. Inputs use
the left content edge and outputs the right. Labels retain their native justification.
Opening even one unused parameter with plus also shows the top collapse arrow;
the bottom expand arrow remains available to show the others. The top arrow
collapses unused individually opened rows, and undo restores the previous choices.

These buttons use the native dimension components' black/white circular plus/minus
appearance, 1.5-to-3 canvas-unit proximity radius and zoom fading. They are custom
presentation controls; they do not insert or remove registered parameters. They are
interactive canvas controls and are omitted from export drawings.

Minus is available only for an eligible unused parameter. Connected parameters and
configured inputs cannot be hidden; their wires and values take precedence over an
individual hidden choice. Core task parameters remain visible and have no minus.
The same controls apply to secondary Deconstruct outputs.

Individual choices override the last overall expanded/compact setting and are saved
by parameter identity in a versioned GH_IO chunk. The overall arrow/menu actions
clear those choices and show all/collapse unused parameters. Undo/redo of either
action restores the entire prior layout choice without replacing parameters,
expiring computed data or writing files. Older definitions without individual choices
keep their existing overall setting; definitions predating compact mode still open expanded.

This visibility control is custom code using public GH SDK layout/rendering APIs.
Rhino's specific annotation button is private to `IOComponents.gha`, not a public
reusable SDK widget. The plugin does not call those private APIs. Both expanded
and compact input labels use Grasshopper's native label renderer.

The input groups are explicit, not inferred from optionality alone:

| Component inputs | Compact behavior |
| --- | --- |
| Name, Class, Type, Geometry, Colour, Material, Placement and spatial/object children | Stay visible |
| Optional Description, GlobalId, Tag, Attribute Names, Attribute Values, Classifications and Property Sets | Collapse when unused |
| Project's optional Contexts, Units and Georeference | Collapse when unused |
| Georeference's optional Description, Geodetic Datum and Vertical Datum | Collapse when unused |

This applies to Object, Opening, Element Type, Material, Project, Site, facilities,
facility parts, Storey, Space, Georeference and their Modify components. Components
without secondary metadata retain their standard layout. Modify Material uses the same input controls as the other Modify components.

Deconstruct components collapse unconnected secondary outputs: Description, GlobalId,
Tag, Attribute Names/Values, Classifications, Property Sets, Contexts, Units and Georeference;
model-header Author/Organization/Source; context Identifiers/Target Views; georeference
datums and Map Zone; property/quantity From Type, Method and Formulas. Main outputs
such as Name, Class, Type, Geometry, Colour, Material, Placement, children, property
Names/Values/Types/Kinds and unit values stay visible. Deconstruct Units has no
secondary metadata and keeps its standard layout. Wired outputs stay visible even
when they carry no data. Hidden output data is still computed normally.

Connected parameters remain visible, including connections carrying empty data. Inputs
with persistent values or configured flatten/graft, reverse or simplify settings
also remain visible. To set an advanced value, expand the component first. Clearing
that value makes its unused row eligible to collapse again.

Only canvas presentation changes. All parameters remain registered in their original
order, with their types, access modes, values and wires intact. Toggling the arrow
does not expire computation or trigger file writes. Undo/redo restores the layout
option without replacing parameters or expiring their computed outputs.

Expansion is saved per component through GH_IO. Definitions saved before this option
existed reopen fully expanded. Newly placed components start compact. The generated
teaching samples are explicitly expanded so all steps remain easy to discover.

## Verification

With the Debug plugin built and licensed Rhino available, run:

```powershell
dotnet run --project tools/SampleBuilder -- --check-inputs
```

The host checks exercise actual arrow clicks and menus, heights, input/output visibility,
parameter identities and access modes, input/output wires, persistent data, legacy missing-option
defaults, GH_IO roundtrips, repeated solves and undo/redo. They also check every
affected component family. Test definitions and offscreen captures go under ignored
`artifacts/input-checks/`. Native Grasshopper text/icon caches are not reliably
rendered by the headless capture, including on unchanged components; these images
are not a substitute for manual canvas verification.

The individual-control checks exercise plus/minus clicks, native row-edge/gap placement,
saved choices, undo/redo including overall-arrow resets, legacy missing-choice defaults,
connected/configured protection, identity preservation and real canvas hover from all directions.

Canvas-level mouse-event regressions also approach the button from all four cardinal
directions and diagonals, including points below the component bounds, and verify
mouse-leave reset and subscription cleanup. These exercise event delivery as well
as the radius calculation.

In Rhino, check the arrow, readable labels/icons, input grips, wire endpoints,
selection, zoom, moving the component, undo/redo and save/reopen. Test both a new
compact component and an older definition. Minimum Rhino 8.20 and macOS checks
remain required before claiming coverage on those hosts.
