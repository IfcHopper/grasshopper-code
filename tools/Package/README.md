# Distribution packages

Build locally from the repository root on Windows with Rhino 8 installed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Package/Distribute.ps1
```

The execution policy override applies only to that process. The script builds Release,
uses `Package.ps1` for the manual-install ZIP, and runs Rhino's Yak CLI locally.
Use `-YakPath` for a different Rhino installation. Version comes from
`IfcHopper/IfcHopper.csproj`; `manifest.yml` is the maintained metadata source.
No login, upload, Git push or GitHub release modification occurs.

Outputs in ignored `artifacts/`:

- `IfcHopper-<version>.zip`: food4Rhino manual download (also suitable for GitHub Releases).
- `ifchopper-<version>-rh<major>_<service>-any.yak`: Windows/macOS Package Manager distribution.
- `yak-stage-<id>/IfcHopper/`: inspectable Yak input, including manifest and icon.

Both archives carry the plugin, dependency DLLs/runtime folders, installation
instructions, project licence and third-party notices. Host SDK assemblies and PDBs
are excluded. Generated packages stay local; commit only scripts, metadata and docs.
The icon is copied from the existing generated PNG; edit its SVG source and follow
[icon instructions](../../docs/ICONS.md) to change it.

Yak 0.15.5 reports assembly version `1.0.0.0` against package version
`1.0.0-beta.1`: the assembly's numeric version cannot contain a prerelease suffix.
The builder explicitly passes the project version, preserving the beta label in
the final manifest and filename. Yak may also append an inferred GUID keyword;
the manifest explicitly preserves the plugin's `GH_AssemblyInfo.Id` for discovery.

## Compatibility and release checks

The supported project target remains Rhino 8.20+, .NET 8, Windows/macOS. These local
packages are release candidates, not evidence of host compatibility. Yak infers its
minimum Rhino tag from the referenced SDK (currently 8.27); do not rename it to
8.20 without testing against the minimum host. The maintainer reports that the
release works on macOS on Apple Silicon. The Yak build therefore uses `any` so
Package Manager can offer the same managed payload on Windows and macOS. The
exact tested Rhino/macOS versions, Intel Mac coverage and clean installation of
the newly generated Yak archive have not been recorded. WindowsDesktop/System.Drawing
dependencies remain platform-sensitive; this report does not establish coverage
of every runtime path. The platform tag itself does not establish portability.

Before publishing, run `dotnet build IfcHopper.sln` and `dotnet test IfcHopper.sln`
with .NET 8. Inspect archive contents and notices; record source commit, any local
changes, version, SHA-256 hashes, SDK, OS/architecture and tested Rhino service release.
Compare with the GitHub release before reusing its version: a rebuild from a newer
commit is not necessarily the same binary. Never replace an already published
version with changed code; increment the project version for a new release.

Test the actual ZIP in a clean Grasshopper Libraries folder and the actual Yak by
dragging it into Rhino, with no other IfcHopper copy installed. Restart, check dependency
loading, icons, sample solves and messages, save/reopen, update and uninstall. Test
the minimum intended Rhino release and every advertised OS/architecture. Keep
unavailable checks explicit; a successful archive build is not a clean-install test.

## food4Rhino and Package Manager: choose one publishing route

McNeel confirms that food4Rhino can generate and publish a Yak package from a ZIP,
and can also link a package already published through Yak. Use one route for a
given name/version to avoid attempting to publish the same version twice.

### food4Rhino first

1. Log into food4Rhino with the intended maintainer account. Create an app with
   **Add App**, or edit the existing IfcHopper listing rather than creating a duplicate.
2. Upload the ZIP, set the exact beta version, authors, LGPL-3.0 licence, description,
   documentation/source links and icon (`IfcHopper/Resources/IfcHopper.png`). Add
   representative screenshots manually. Describe the IfcHopperShell 0.1 break and
   only the compatibility actually checked above. The ZIP includes its dependencies;
   do not upload only the GHA.
3. If using food4Rhino's Package Manager publishing facility, review the generated
   manifest, name `ifchopper`, prerelease version and Rhino/platform constraints
   before submitting. It generates its own Yak package from the ZIP; this is an
   alternative to pushing the local Yak. Exact controls may change; consult the
   [food4Rhino FAQ](https://www.food4rhino.com/en/faq).
4. Verify the published version in Rhino Package Manager with prereleases enabled,
   and test the food4Rhino install link. McNeel explains that after sending the ZIP
   to Yak, the download link opens Rhino Package Manager for the package. Keep a
   separate manual ZIP download only if desired and supported by the listing.

### Yak first (explicit local metadata and distribution tag)

Only after approval to publish, run these commands manually. Substitute the actual
filename printed by the build; no publishing command is part of either builder.

```powershell
$yak = 'C:\Program Files\Rhino 8\System\Yak.exe'
& $yak login
# Optional public test publication; the test server is cleared nightly:
& $yak push --source https://test.yak.rhino3d.com artifacts/<actual-filename>.yak
& $yak search --source https://test.yak.rhino3d.com --all --prerelease ifchopper
# Production publication, separately authorized:
& $yak push artifacts/<actual-filename>.yak
& $yak search --all --prerelease ifchopper
```

Login uses Rhino Accounts. Keep its stored credentials outside the repository.
Confirm package ownership before first publication; only owners can publish later
versions. Published versions cannot be overwritten/deleted. For a faulty version,
use Yak's `yank` to unlist it and publish a corrected, higher version; preserve the
remaining successful uploads if a multi-distribution release fails partially and
retry only missing distributions. Never rebuild different bytes under the same version.

Then use food4Rhino's facility to create/link an app from the existing Package Manager
package, using the owning account and matching name. Add screenshots, documentation
and release notes there. Do not also send the ZIP to Yak for the same version.

## Sources

- [Create a Grasshopper package](https://developer.rhino3d.com/guides/yak/creating-a-grasshopper-plugin-package/)
- [Push and verify packages](https://developer.rhino3d.com/guides/yak/pushing-a-package-to-the-server/)
- [Manifest reference](https://developer.rhino3d.com/guides/yak/the-package-manifest/)
- [McNeel explanation of food4Rhino/Yak integration](https://discourse.mcneel.com/t/linking-plugin-between-package-manager-and-food4rhino/196377)

The public FAQ could not be fetched during preparation (HTTP 405); the integration
description above is based on McNeel staff's linked explanation, not a verified
walkthrough of the current authenticated food4Rhino interface.
