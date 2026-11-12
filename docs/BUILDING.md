# Building and contributing

Build and test (needs the .NET 8 SDK):

```sh
dotnet build IfcHopper.sln
powershell -File tools/FetchTestFixtures.ps1
dotnet test IfcHopper.sln
```

The build produces `IfcHopper.gha` in `IfcHopper/bin/<Configuration>/net8.0/`; add that folder to the Grasshopper library paths to load it. The first build automatically downloads the pinned buildingSMART templates in the [developer reference](DEVELOPMENT.md#reference-ifc-43-add2-specification) and verifies their SHA-256 checksum before extraction. No local reference files or manual template setup are required; subsequent builds reuse the ignored cache.

For offline builds, supply a copy of the pinned archive or its extracted XML folder (absolute paths recommended):

```sh
dotnet build IfcHopper.sln -p:PsdArchive="C:/reference/annex-a-psd.zip"
dotnet build IfcHopper.sln -p:PsdDir="C:/reference/annex-a-psd"
```

Use either option. Archives must match the pinned checksum; `PsdDir` embeds XML directly from the supplied folder, whose integrity/provenance is the caller's responsibility. Preserve upstream notices and do not substitute templates from a moving branch. A mirror serving the identical archive can be selected with `-p:PsdUrl=...`.

Contributions are licensed under the same licence as the project.

