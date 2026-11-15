# Building and contributing

Build and test (needs the .NET 8 SDK):

```sh
dotnet build IfcHopper.sln
powershell -File tools/FetchTestFixtures.ps1
dotnet test IfcHopper.sln
```

The plugin and SampleBuilder explicitly reference McNeel's Grasshopper SDK
`8.20.25147.11001-rc`, which pins RhinoCommon to the same version. This is the last
published 8.20 SDK package; NuGet marks it unlisted and labels it `-rc`, so keep the
explicit pin rather than choosing the latest listed package. Visual Studio restores
these references through NuGet; installing the full Rhino 8.20 application is not
required to compile. The runtime target stays `net8.0`.

Compiling against this SDK checks the 8.20 API baseline. It does not establish host
loading or platform compatibility: run the plugin/package in an actual Rhino 8.20
installation for that. `RHINO_SYSTEM_DIR` can point SampleBuilder at a separate
installation; current local host regressions use Rhino 8.37. For Rhino on Windows,
use `SetDotNetRuntime` and restart if the host has been switched to .NET Framework.

The build produces `IfcHopper.gha` in `IfcHopper/bin/<Configuration>/net8.0/`; add that folder to the Grasshopper library paths to load it. The first build automatically downloads the pinned buildingSMART templates in the [developer reference](DEVELOPMENT.md#reference-ifc-43-add2-specification) and verifies their SHA-256 checksum before extraction. No local reference files or manual template setup are required; subsequent builds reuse the ignored cache.

For offline builds, supply a copy of the pinned archive or its extracted XML folder (absolute paths recommended):

```sh
dotnet build IfcHopper.sln -p:PsdArchive="C:/reference/annex-a-psd.zip"
dotnet build IfcHopper.sln -p:PsdDir="C:/reference/annex-a-psd"
```

Use either option. Archives must match the pinned checksum; `PsdDir` embeds XML directly from the supplied folder, whose integrity/provenance is the caller's responsibility. Preserve upstream notices and do not substitute templates from a moving branch. A mirror serving the identical archive can be selected with `-p:PsdUrl=...`.

Contributions are licensed under the same licence as the project.

