# Explicit colour preview

IfcHopper uses Grasshopper's standard preview and selection colours by default. IFC surface styles and assigned/inherited materials remain model data; they do not automatically tint the viewport. Object mesh casts, Deconstruct Object Geometry and Deconstruct Element Type Geometry have no vertex colours, so a supplied preview shader controls their appearance.

Use Grasshopper's native **Custom Preview** component:

- Connect an IFC object directly to Geometry, or use the Geometry output of Deconstruct Object/Deconstruct Element Type.
- Connect a colour, a native Grasshopper material, or an IFC Material to Material. An IFC material supplies its display colour and transparency; a material set uses its first styled part, matching Deconstruct Material's Colour output. An unstyled IFC material cannot supply a shader: choose a colour/material explicitly.
- To display an object's stored IFC style, connect Deconstruct Object's Geometry and Colour outputs to Custom Preview. Colour uses each mesh's surface style first, then the assigned/inherited material colour. These outputs retain their matching order and null colour entries; filter or supply explicit fallback colours for unstyled meshes.
- Hide the upstream object and deconstruct component previews when inspecting the custom result, so their standard shading does not overlap it.

Custom Preview's supplied material wins, including its transparency. This is why IFC colours are not baked into preview mesh vertices. IFC material/shader conversion creates a separate native Grasshopper material; it does not replace the material assigned to the source model.

Baking an IFC element still preserves its stored IFC colours, including mesh parts and type/block styling. IFC export is unchanged. Baking Custom Preview itself uses that component's native behavior and is distinct from baking the IFC element.

Sample **08 Materials** includes a colour-free slab Geometry plus IFC concrete Material wired to native Custom Preview. The source/deconstruct previews are hidden for that branch. No additional IfcHopper preview component is needed for this workflow. Class/attribute palettes and wireframe styling are possible future extensions, not part of #18.

## Compatibility and checks

Beta.3 deliberately changes the earlier beta behavior: automatic IFC-coloured previews and vertex colours on implicit/deconstructed meshes are removed. Existing component GUIDs, IO positions and types, IFC style data and bake colours are preserved. Definitions that relied on automatically coloured meshes should add explicit Custom Preview wiring.

```powershell
dotnet build IfcHopper/IfcHopper.csproj
dotnet run --project tools/SampleBuilder -- --check-custom-preview
dotnet run --project tools/SampleBuilder -- 6 7 8 11
```

The headless Rhino check instantiates native Custom Preview components and inspects their public display items: colour-free geometry, the requested shader colour/transparency, direct IFC material conversion and deconstructed Colour workflows. It covers unstyled objects, assigned/type-inherited materials, explicit surface colours, repeated solves, unchanged source styles and baked IFC mesh colours. It validates native preview inputs rather than captured viewport pixels. Interactive display modes, minimum Rhino 8.20 and macOS remain unverified.

Validated on 2026-11-15 with Windows x64, Rhino 8.37.26307.15001 and .NET 8.0.21. The solution build and all 220 Core tests passed without skips; samples 06, 07, 08 and 11 regenerated and solved, including the native Custom Preview and opening-volume regressions. NuGet vulnerability auditing still reported network warnings.
