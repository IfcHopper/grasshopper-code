# Compact component inputs

New creation and Modify components start compact. Click the arrow at the bottom
to show advanced inputs, then the arrow at the top to collapse unused ones. The context
menu's **Show advanced inputs** option performs the same action. The control uses
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
without these advanced inputs retain their standard layout. Outputs are unchanged.

Connected inputs remain visible, including connections carrying empty data. Inputs
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

The host checks exercise actual arrow clicks and menus, heights, visibility,
parameter identities and access modes, wires, persistent data, legacy missing-option
defaults, GH_IO roundtrips, repeated solves and undo/redo. They also check every
affected component family. Test definitions and offscreen captures go under ignored
`artifacts/input-checks/`. Native Grasshopper text/icon caches are not reliably
rendered by the headless capture, including on unchanged components; these images
are not a substitute for manual canvas verification.

Canvas-level mouse-event regressions also approach the button from all four cardinal
directions and diagonals, including points below the component bounds, and verify
mouse-leave reset and subscription cleanup. These exercise event delivery as well
as the radius calculation.

In Rhino, check the arrow, readable labels/icons, input grips, wire endpoints,
selection, zoom, moving the component, undo/redo and save/reopen. Test both a new
compact component and an older definition. Minimum Rhino 8.20 and macOS checks
remain required before claiming coverage on those hosts.
