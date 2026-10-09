# Icons

Style reference: [aidannewsome/grasshopper-icons](https://github.com/aidannewsome/grasshopper-icons) (David Rutten's original Grasshopper icons and style guide, see its `SKILL.md`).

## Files and embedding

- Each icon has two files with the same name, in `IfcHopper/Resources/Icons/`:
  - `<Name>.svg` – vector master.
  - `<Name>.png` – 24×24 export of the SVG.
- Both are linked in `IfcHopper/Properties/Resources.resx` (System.Resources) and read through the generated `Properties.Resources` class.
- Components return the PNG from `Icon`, for example `protected override Bitmap Icon => Properties.Resources.ReadIfc;`.
- Name icons after the component or type they belong to. In the `.resx`, the PNG is `<Name>` and the SVG is `<Name>_svg`.
- Component icons are 24×24. The plugin icon `IfcHopper/Resources/IfcHopper.svg` is 64×64, kept from IfcHopperShell and not redrawn.
- Parameter icons (`<Name>Param`) follow Grasshopper parameters (see grasshopper-icons `parameters/`): a dark hexagon filling the 24×24 canvas (radial #454545 → black, black outline, white bevel highlights) with the matching component icon inside at 55% scale, recoloured to light grey `#b4b4b4` fills and white strokes. Rendered with `--no-shadow`.
- Deconstruct icons (`Deconstruct<Name>`) show the subject icon at 60% scale above three parts in the subject's colour, linked by pixel dashes.

## Rendering PNGs

PNGs are always generated from the SVGs with `tools/IconRenderer`, which also applies the drop shadow:

```sh
dotnet run --project tools/IconRenderer -- IfcHopper/Resources/Icons
dotnet run --project tools/IconRenderer -- --no-shadow --size 64 IfcHopper/Resources/IfcHopper.svg
```

`--preview` prints a text map of each rendered icon so it can be checked without opening image files.

To add an icon: draw `<Name>.svg`, render it, add both files to `Properties/Resources.resx` and add the two properties to `Properties/Resources.Designer.cs` (Visual Studio regenerates it when the `.resx` is saved).

## Style summary

- 24×24 canvas with about 2px empty border, so the artwork fills about 20×20.
- Vertices on pixel centres. Lines 1–2px, only true horizontals, verticals and 45° diagonals.
- No black outlines: use a darker shade of the fill colour, outline the silhouette only.
- Light always from the upper left, with subtle gradients on large areas (never flat).
- One IFC colour family per icon (see below) plus neutral greys; two families at most. Use warm blacks such as `#191919`, not pure RGB primaries.
- Drop shadow: offset +1px right and +1px down, 2px blur, black at 25–33% opacity.
- Reuse the standard glyphs: arrows (in the icon's IFC colour) with white end dots, white dots for control points, planes as perspective quads with an origin dot.
- Construct icons show parts converging downwards; deconstruct icons mirror this (whole above, parts below).
- Avoid text, photorealism, transparency and downscaled large images.
- Judge icons at their real 24px size, not zoomed in.

IfcHopper palette: the four IFC colours, one per family of icons. Each colour has a ramp (highlight, gradient light → base, darker shades, outline):

| Colour | Ramp | Used for |
| --- | --- | --- |
| Blue `#0E519C` | `#8DB8EA`, `#3D7FD0` → `#0E519C`, `#0B4483`, outline `#072D57` | Project, Model, Read/Write IFC, Apply Edits, Pset |
| Red `#DE2633` | `#F7A3A8`, `#F2626B` → `#DE2633`, `#C01E2A`, outline `#6E0A12` | Element/Object, Opening, Context, Georeference |
| Purple `#9A3B89` | `#E3A6D6`, `#C468B3` → `#9A3B89`, `#833274`, outline `#4D1C44` | Qto, Units, Classification |
| Teal `#2A9DAE` | `#A6E0E8`, `#5CC4D2` → `#2A9DAE`, `#238595`, outline `#124A52` | Site, Material, facilities, facility parts, Storey, Space |

Neutrals: greys `#E8E8E8`–`#454545`, white, `#191919` ink, `#F0D8A8` pencil wood. The Read/Write IFC body lines use all four colours. Parameter icons stay grey (see above). Grasshopper's own amber/green palette is no longer used.
