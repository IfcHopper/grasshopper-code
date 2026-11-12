# Release notes

## 1.0.0-beta.2 (unreleased)

- Fix [#6](https://github.com/IfcHopper/grasshopper-code/issues/6): Object and Modify Object preview only their outputs, so uncut input bodies and opening cutters no longer cover the cut result. Output preview visibility, selection colours and mesh-edge settings remain in effect. Hide previews of upstream geometry and Opening components to inspect the cuts.
- Sample 11 hides construction volumes, Opening components and the deconstructed opening geometry. Its builder checks output-only preview drawing, visibility and clipping bounds, and compares the wall's cut volume through Deconstruct Object and the object mesh cast.
- Address [#5](https://github.com/IfcHopper/grasshopper-code/issues/5): the same output-only preview change removes the default-colour input drawn over material-coloured Object and Modify Object results. Sample 08 now checks assigned and inherited material colours, explicit surface-colour precedence, material replacement without upstream mutation, and colour updates across repeated solves. Upstream components still have independent previews; hide those to inspect the result alone.

This fix keeps component GUIDs, input/output contracts and IFC storage unchanged. No definition migration is required. Beta releases may introduce deliberate breaking changes before 1.0.0 stable; any such changes will be listed here with migration instructions.

Validation for #6: solution build and all 194 Core tests passed (no skips); sample 11 was regenerated and its preview and cut-volume checks passed in headless Rhino 8.37.26307.15001 on Windows x64 with .NET 8. The maintainer also confirmed the fix manually in Rhino using the local beta.2 package. Builds used the installed .NET SDK 10.0.401 while targeting .NET 8; the .NET 8 SDK, minimum Rhino 8.20 and macOS were not available for verification. NuGet vulnerability checks reported network-access warnings.

Validation for #5: sample 08 was regenerated with all material and output-only preview checks passing on the same host. All 194 Core tests passed with no skips, and the maintainer confirmed the material preview in Rhino. No additional plugin rendering change was needed beyond #6; this follow-up adds regression coverage and makes the sample runner load the current plugin and its dependencies reliably. The minimum-host and platform limitations above still apply.
