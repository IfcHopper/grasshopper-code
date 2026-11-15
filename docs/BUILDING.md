# Building and contributing

Build and test (needs the .NET 10 SDK and .NET 8/10 runtimes for tests):

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
required to compile. The plugin targets `net8.0;net10.0`. Core stays `net8.0`; its tests execute under both runtimes.

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


Rhino 9 development uses IfcHopper/bin/<Configuration>/net10.0/ and the Rhino 9
launch profile. Select the corresponding target framework before debugging.
SampleBuilder requires -f net8.0-windows for Rhino 8 or -f net10.0-windows
for Rhino 9. Keep both framework folders together in distribution packages.
See [packaging](../tools/Package/README.md).

### VS Code launch profiles

Open either the repository folder (grasshopper-code) or its IfcHopper project
folder in VS Code. Both include matching launch/tasks configurations. In Run and
Debug, choose "Rhino 9 - GH1" and press F5. This builds Debug/net10.0 and passes
that exact directory to RHINO_PACKAGE_DIRS before starting Rhino 9 with GH1.
"Rhino 8 - GH1" builds and loads Debug/net8.0 instead. Restart an already running
Rhino to pick up changed environment variables. Avoid other installed copies of
IfcHopper while debugging.

The previous project-folder VS Code configuration only launched Rhino 8 and
pointed at bin/Debug, rather than an explicit framework output. The Visual Studio
launchSettings profiles do not replace VS Code's separate launch.json profiles.

### Visual Studio launch profiles

Use "Rhino 9 - netcore" with the net10.0 target selected (or Rhino 8 with net8.0).
Each profile now explicitly sets RHINO_PACKAGE_DIRS to its framework folder under
bin/<Configuration>. It does not use OutputPath, which can follow the selected
framework or an outer multi-target build. Stop debugging and restart Rhino after
changing profiles; reload the project if Visual Studio retains the old settings.
