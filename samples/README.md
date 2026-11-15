# IfcHopper samples

Grasshopper definitions, one per workflow. Open them in Grasshopper 1 in Rhino 8 or 9 with IfcHopper installed. Each one explains its steps in groups on the canvas. Geometry is built in Grasshopper and sizes are in metres. The Rhino document units are respected, but the samples are easiest to follow in a metre document.

| Sample | Shows |
| --- | --- |
| 01 First building | The minimum model: objects in a storey, a building, a site and a project, written to an IFC file. |
| 02 Read and explore | Read IFC Header, Read IFC, then the Deconstruct components down to the objects; preview and bake. |
| 03 Units, contexts and georeference | A millimetre file with subcontexts, true north and a map conversion, read back. |
| 04 Infrastructure | Road, bridge, railway and marine facility with typed facility parts (nested) and their elements. |
| 05 Spaces | Spaces in a storey and on the site, a partial space, furniture in a space, Pset_SpaceCommon. |
| 06 Geometry and colour | Brep, mesh and extrusion geometry, colours with transparency, an element assembly. |
| 07 Types and blocks | Three columns and two chairs placed from element types. Storey Elements is flattened to collect both branches into one IFC. Open `07 Types and blocks.3dm` first. |
| 08 Materials | Materials with a property set, a layer set and a constituent set; an object showing its type's material. |
| 09 Property and quantity sets | Standard and custom property sets, a quantity set, deconstructing them, replacing and deleting sets on Modify Object. |
| 10 Classification | Classification references on an object and its type, inheritance, and replacing or removing them by system. |
| 11 Openings | A wall with a door opening, a window opening and a recess, filled by a door and a window; the wall shows cut. |
| 12 Tags and attributes | Tags and class attributes of doors, windows and a window type. |
| 13 Edit in place | Read sample 01's file, find a wall and the storey, modify them, apply the edits and write: only the edits change. |
| 14 Schema export | One model written as IFC4X3_ADD2, IFC4 and IFC2X3, with the downgrade warnings. |
| 15 Find and summarise | Model Info (classes, counts, tree) and Find Objects by class, name regex (including exclusion), openings and property filters. |
| 16 Move model near origin | Translate a synthetic survey model by X/Y while adjusting its georeference to preserve projected map positions; inspect and write the result. |
| 17 Class colours | Native Custom Preview with stable colours by class, a partial wall override and `*` for all other classes. Writes no IFC. |

Samples that write a file write it to `ifc/` next to the definition. The Write toggle is saved off, so set it to true to write. The files in `ifc/` are the samples' own output, committed so that samples 02, 13 and 15 have a file to read. Relative paths in Read IFC, Read IFC Header and the Directory of Write IFC are taken from the folder of the saved definition.

## Regenerating

The definitions are generated; do not edit them by hand. Each one is built in code by `tools/SampleBuilder` (one class per sample in `tools/SampleBuilder/Samples`). The builder runs Rhino 8 headless (Rhino.Inside), loads IfcHopper from its build output, and solves every definition. It fails when a component reports an error, an unexpected warning or remark, or when objects overlap on the canvas. It then saves the `.gh` files, the `.3dm` of sample 07 and the IFC files.

Samples explicitly show advanced parameters for discoverability, including Deconstruct outputs. New components with secondary metadata placed from the toolbar start compact; use the bottom arrow to expand and the top arrow to collapse unused metadata. Connected parameters and configured inputs remain visible. See [compact parameters](../docs/ADVANCED_INPUTS.md) for the groups and compatibility behavior.

```sh
dotnet build IfcHopper/IfcHopper.csproj
dotnet run --project tools/SampleBuilder -f net8.0-windows              # all samples
dotnet run --project tools/SampleBuilder -f net8.0-windows -- 7 11      # samples 07 and 11
dotnet run --project tools/SampleBuilder -f net8.0-windows -- --describe     # inputs and outputs of the IfcHopper components
dotnet run --project tools/SampleBuilder -f net8.0-windows -- --docs ../grasshopper-documentation   # component pages of the docs site
dotnet run --project tools/SampleBuilder -f net8.0-windows -- --find Block   # Grasshopper components by name
dotnet run --project tools/SampleBuilder -f net8.0-windows -- --check-inputs # compact input layout, persistence and undo regressions
dotnet run --project tools/SampleBuilder -f net8.0-windows -- --check-large-coordinates # synthetic survey IFCs, definitions and precision checks
dotnet run --project tools/SampleBuilder -f net8.0-windows -- --check-custom-preview # native material override, plain geometry and bake-style regressions

dotnet run --project tools/SampleBuilder -f net8.0-windows -- --check-runtime-messages # partial-success warnings, total failures and repeated solves

dotnet run --project tools/SampleBuilder -f net8.0-windows -- --check-class-colours # colour/tree correspondence, overrides, fallback and repeated solves
```

Regenerate after changing component inputs or outputs. `IfcHopper.Tests/SampleDefinitionTests` reads the IFC files in `ifc/` back. The builder needs a licensed Rhino 8 in `C:\Program Files\Rhino 8`, or in `RHINO_SYSTEM_DIR`.

The builder verifies that the loaded IfcHopper module matches the current Debug build, including when Grasshopper loads assemblies into memory. If another installed copy is selected, it stops rather than validating the wrong plugin. When headless package discovery omits the development folder, it uses a signature-checked Grasshopper 8 loader fallback.

Sample 08 checks that Object and Modify Object draw only their outputs, preserves IFC style fallback/type inheritance/surface precedence, and tests native Custom Preview across repeated solves. Default shading uses standard Grasshopper colours; the slab branch explicitly wires Geometry and IFC concrete Material to Custom Preview, hiding upstream previews. Implicit/deconstructed meshes have no vertex colours. Stored IFC styles and element baking are unchanged. See [explicit colour preview](../docs/PREVIEW.md). The runner binds the current Debug plugin before installed packages and uses the plugin's existing drawing dependency to avoid host-version binding conflicts.

Sample 11 hides its construction volumes and opening previews so they do not cover the cut wall. Enable their Preview switches to inspect them. Building sample 11 also checks Object and Modify Object preview drawing, hidden/disabled visibility and clipping bounds, and the cut wall volume through Deconstruct Object and the object mesh cast. These checks require Rhino; the Core test suite does not exercise viewport drawing.

To validate an extracted Release ZIP, set `IFCHOPPER_PLUGIN_DIR` to the absolute path of its `IfcHopper` folder before running SampleBuilder. The default remains the current Debug build. The module identity check also applies to the supplied package.

### Rhino 9 host checks

Run dotnet run --project tools/SampleBuilder -f net10.0-windows -- --check-runtime-messages
and the other check switches above for GH1 inside Rhino 9. The default host and
Debug plugin folder follow the selected framework. Overrides RHINO_SYSTEM_DIR and
IFCHOPPER_PLUGIN_DIR must select a matching host/runtime/payload. For an extracted
package, point IFCHOPPER_PLUGIN_DIR at net10.0 (net8.0 for Rhino 8).
