# Class Colours

**Class Colours** is in **IfcHopper > Utilities**. It outputs display colours for native Grasshopper **Custom Preview**. It does not change IFC materials, surface styles, geometry, placements, default preview, export or element bake colours.

| Input | Meaning |
| --- | --- |
| Objects | Tree of IFC objects, including spatial objects and element types. Output paths and item positions match this tree. |
| Classes (optional) | Exact IFC class names to override, ignoring case and surrounding whitespace. Use `*` for all other classes. |
| Colours (optional) | Colours paired by index with Classes, including transparency. |

With no overrides, each class receives an opaque pseudo-random colour derived from its name. The same class keeps the same colour across recomputations, reordering and reopening; changing the set of classes does not reshuffle the palette. Automatic colours can coincide and are not a semantic or accessibility palette.

Precedence is **exact class override → `*` fallback → automatic colour**, regardless of override order. Overrides do not expand subclasses: IfcWall and IfcWallStandardCase are different keys.

For example, use these paired lists:

| Classes | Colours |
| --- | --- |
| IfcWall | Red |
| IfcSlab | Blue |
| * | Grey |

Walls become red, slabs blue, and all other classes grey. Omit `*` to keep automatic colours for classes without an exact override. Only the literal `*` is special; regex and partial class-name wildcards are not accepted here.

Both override lists must have the same length. Blank/unknown class names, duplicate names (including differently cased duplicates), duplicate `*` entries and empty colours produce actionable errors and no palette output. Null or unsupported Objects produce empty colour entries and warnings naming their path/index; subsequent objects keep their original positions. Empty branches are retained.

## Custom Preview wiring

Connect the same object tree to **Class Colours → Objects** and native **Custom Preview → Geometry**, then connect **Class Colours → Colours** to **Custom Preview → Material**. Hide upstream previews to avoid overlapping standard shading.

The output is one colour per object, not per mesh. When using Deconstruct Object Geometry instead of direct object conversion, graft the objects before feeding both components so each object has its own branch; native Custom Preview can then reuse its class colour for that object's meshes. Preserve null positions and tree structure when filtering either side.

Sample **17 Class colours** demonstrates two walls and a slab with automatic colours, a partial translucent wall override, and a `*` grey fallback. Only one Custom Preview is visible at a time; toggle the other preview branches to compare. The sample writes no IFC file.

## Verification

```powershell
dotnet build IfcHopper.sln
dotnet test IfcHopper.sln
dotnet run --project tools/SampleBuilder -- --check-class-colours
dotnet run --project tools/SampleBuilder -- 17
```

The Rhino checks cover paths, empty branches, null/unsupported positions, repeated solves, object reordering, partial overrides, wildcard fallback precedence, transparency, spatial/type objects, invalid override messages and unchanged source data. The sample checks native Custom Preview display items and shader data, not captured viewport pixels. Minimum Rhino 8.20, macOS and interactive display modes remain unverified.
