# Large coordinates

Read IFC warns when a stored point or a resolved local placement has an absolute coordinate of at least 100,000 metres. The check uses the file's length unit. Large Eastings/Northings in a project's map conversion alone do not trigger it: local geometry with a georeference is the preferred arrangement.

The screening reads stored Cartesian points, 2D/3D point lists and local placement chains. It does not facet geometry or load the IfcHopper object tree. It is conservative: unused representation coordinates can trigger a warning, and it is not an exhaustive bound on evaluated geometry (for example, mapped geometry, swept extents and linear placements). The threshold is a diagnostic heuristic, not a guarantee of accuracy below it.

IfcHopper automatically keeps double-precision mesh vertices when converting to Rhino and back. Mesh cleanup and normal calculation run temporarily near the mesh's first vertex, then return to world coordinates. This prevents nearby survey vertices from being merged through Rhino's single-precision mesh mirror. It does not relocate the model, change placements, alter the CRS or rewrite the source file. The warning explains the precision protection and the remaining limits.

Viewport display, other Grasshopper components and mesh booleans can still be inaccurate far from the origin. Use Move Model to bring supported models near the origin, or re-export with a local engineering origin and a map conversion/georeference in the authoring application. Moving only the preview or subtracting survey coordinates without preserving the coordinate transformation can misplace exported data. IfcHopper does not infer a CRS or silently rebase a file.

## Move Model

Find **Move Model** in **IfcHopper > 6 - Utilities**. Connect `Read IFC > Move Model > Find Objects` to inspect the translated elements, or connect the output to Write IFC to save explicitly. The inputs are Model, X, Y and Keep Map Location. X and Y are signed offsets in Rhino document units; they default to zero. Keep Map Location defaults to true. The component outputs a copy and leaves upstream object values and the source file unchanged. Repeated solves apply the offset to the input model once; chaining two Move Model components adds their offsets.

Three coordinate concepts matter:

| Concept | Meaning | Effect of Move Model |
| --- | --- | --- |
| World placement | An object's local origin and axes expressed in the model's engineering coordinate system; Core stores lengths in metres. | Add X/Y to the origin. Keep axes, rotation and Z. Shift body geometry by the same amount. |
| Parent-relative placement | How IFC stores an object's coordinate system relative to another placement. | Preserve the object's final world position. When parent and child move together, their relative offset normally stays the same. The writer can re-express an independent source placement relative to its spatial parent; the placement graph need not remain byte-identical. |
| Georeference/map conversion | Converts engineering coordinates to a projected map CRS using scale, rotation and an offset. It is metadata, not a second geometry translation in the Rhino preview. | Compensate its offset when Keep Map Location is true; leave it unchanged when false. |

For example, take an object at model XY `(500000, 5000000)` with an existing map conversion whose offset is `(0, 0)`, rotation is zero and scale is one. Enter X=`-500000`, Y=`-5000000` in a metre document:

| Result | Keep Map Location = true | Keep Map Location = false |
| --- | --- | --- |
| New model XY and placement origin | `(0, 0)` | `(0, 0)` |
| New map-conversion offset | `(500000, 5000000)` | `(0, 0)` |
| Object's projected map XY | `(500000, 5000000)` — preserved | `(0, 0)` — geographically moved |

For a rotated/scaled CRS, compensation uses `new offset = old offset - scale × rotation × (X,Y)`, rather than subtracting X/Y directly from Eastings/Northings. This follows the [IFC map conversion order](https://standards.buildingsmart.org/IFC/RELEASE/IFC4_3/HTML/lexical/IfcMapConversion.htm). CRS identity, rotation, scale, orthogonal height, object Z and storey elevations do not change. Without a georeference, the component translates the model and reports that it cannot preserve a projected map location; it does not invent an EPSG code.

Spatial objects, element parts, openings and their filling elements move together. Type definitions stay in type-local coordinates; instance transforms move with their occurrences. Object GlobalIds and source links are preserved. The operation traverses/copies the object hierarchy but leaves body geometry lazy until used. In-place writing retains supported original representations, including mapped instances, instead of requiring a schema rebuild.

Imported files are rejected if some products lie outside the supported project tree, placements are absent/non-local/cyclic/shared, the hierarchy repeats children, or the file uses multiple or non-uniform map conversions. This avoids silently translating only part of an IFC. Export/restructure these cases in the authoring application first. IFC2X3 georeferencing uses the existing `ePSet_MapConversion`/`ePSet_ProjectedCRS` convention; replacement sets receive new property-set identities while product identities remain unchanged.

The large-coordinate screening remains conservative after translation: in-place exports can retain unused old coordinates or large representation-local coordinates whose placement cancels them. A warning on reread is not an evaluated bound of the translated model.

Sample **16 Move model near origin** includes a synthetic source IFC, a Move Model component with compensation enabled, and an explicit Write toggle saved off.

## Reproduce and verify

From the repository root, with licensed Rhino 8 installed:

```powershell
dotnet build IfcHopper/IfcHopper.csproj
dotnet run --project tools/SampleBuilder -- --check-large-coordinates
```

The generator creates synthetic IFC files and wired Grasshopper definitions in ignored `artifacts/large-coordinate-checks/`. They contain a 4 m long, 20 mm thick, 3 m high wall at easting 500,000 m and northing 5,000,000 m. Cases cover nested survey placements with small local vertices, survey coordinates stored directly in vertices, IFC2X3/IFC4/IFC4X3_ADD2, and metre/millimetre files and Rhino documents. Open a generated `.gh` in a matching-unit Rhino document to inspect the wall and the Read IFC warning.

Before the fix, the first metre case produced a zero-thickness open mesh with two faces. The host check requires the original world northing, a closed six-face wall, thickness within 1e-8 m of 0.02 m, a valid element mesh cast, preservation through Rhino-to-Core conversion, Move Model translation to the origin in document units, and repeated solves. Core tests generate their own files and also check coordinate warnings, nested placement sums, map-conversion false positives and in-place read/write preservation. All 220 Core tests passed, including translation of parts/openings/fills/type instances and compensation with rotated/scaled map conversions in IFC2X3, IFC4 and IFC4X3.

Validation on 2026-11-15: the twelve host cases passed on Windows x64, Rhino 8.37.26307.15001, .NET 8.0.21. Minimum Rhino 8.20, macOS and interactive viewport/bake behavior remain unverified. This is not validation of a published beta.3 package.
