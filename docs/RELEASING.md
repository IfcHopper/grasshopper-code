# Releasing

How an IfcHopper release is made. What each release contains is in the README [Release plan](../README.md#release-plan); the rules for component inputs and outputs are in [Compatibility after 1.0](../README.md#compatibility-after-10).

## Versions

- Semantic versions: `1.0.0-beta.1`, `1.0.0-beta.2`, … then `1.0.0`, `1.1.0`, …
- Each release is a git tag `vX.Y.Z[-beta.N]` on `main` with a GitHub Release; betas are marked as pre-release.
- A fix for an older release while `main` has moved on gets a `release/X.Y` branch from its tag.

## Steps

1. **Scope and version.** Merge accepted issue fixes into `main` through PRs. Keep individual fixes independent of the release version bump; set `<Version>` in `IfcHopper/IfcHopper.csproj` only when preparing the release candidate. Grasshopper shows it in full (`IfcHopperInfo.AssemblyVersion`). Track remaining work in [BETA2.md](BETA2.md).
2. **Check.** All must pass:

   ```sh
   dotnet build IfcHopper.sln -c Release
   powershell -File tools/FetchTestFixtures.ps1
   dotnet test IfcHopper.sln -c Release --no-build
   dotnet run --project tools/SampleBuilder        # regenerates and solves the samples
   ```

   Commit intentional regenerated sample changes. External fixtures come only from the pinned public buildingSMART specification-models repository; never require private manual-testing models. Record skipped tests; do not describe them as passed. For blocked template downloads, use the README's offline procedure and record source/version. Check a fresh build cache as well as an incremental build.
3. **Prepare docs and notes** using the workflow below, including changed parameter icons/visibility, samples downloads, highlights, installation, compatibility/migrations, known issues and bug reporting. Validate the docs build before release; publish in coordination with the release.
4. **Package and validate:** `powershell -File tools/Package/Package.ps1` builds in Release and writes `artifacts/IfcHopper-<version>.zip`: an `IfcHopper` folder with the plugin and its DLLs (no `.pdb`), `LICENSE.txt`, `INSTALL.txt` and `THIRD-PARTY-NOTICES.txt` (from `tools/Package/`). Update notices for dependency changes. Inspect archive contents and embedded version; exclude host assemblies and stale outputs. Test the ZIP in a clean installation on supported Windows/macOS and minimum Rhino release: loading, solving, preview, bake, save/reopen, undo, update/uninstall. Record actual coverage and outstanding limits. Close Rhino before rebuilding loaded DLLs.
5. **Freeze and tag only after validation.** Commit all intended release changes, merge into `main`, confirm a clean working tree, and build the final ZIP from that exact commit. If the commit changes, repeat affected checks and packaging. Then tag and push: `git tag v1.0.0-beta.2`, `git push origin main v1.0.0-beta.2`. Never move an already published tag to repair a release.
6. **Publish** a GitHub Release for that tag: title `IfcHopper <version>`, prepared notes and the validated ZIP attached; mark betas as pre-release. Publish coordinated docs. Tagging/publication require explicit release authorization, not merely successful local checks.

## Docs

The [docs site](https://github.com/IfcHopper/grasshopper-documentation) is a Docusaurus site, deployed by GitHub Actions on every push to its `main`. Clone it next to this repository (`../grasshopper-documentation`).

- `docs/` is the current IfcHopper version, served at the site root and labelled by `versions.current.label` in `docusaurus.config.js` (now `v1.0-beta`). Older versions are frozen in `versioned_docs/` (`0.1`, IfcHopperShell).
- `docs/components/` and `static/img/components/` are **generated** from the plugin; never edit them by hand:

  ```sh
  dotnet run --project tools/SampleBuilder -- --docs ../grasshopper-documentation
  ```

- The other pages (introduction, getting started, how it works, samples) are written by hand.
- The samples download is `static/files/ifchopper-samples-<version>.zip`: the `.gh`, `.3dm`, `README.md` and `ifc/` of `samples/`, in an `IfcHopper samples` folder. Replace it when the samples change and update the link on the samples page.
- Check with `npm ci` and `npm run build` (fails on broken links), preview with `npm start`, then push.

## Release 1.0

- Version `1.0.0`, a normal (not pre-release) GitHub Release.
- Docs: set `versions.current.label` to `v1.0` and update the beta wording (introduction notice, samples zip name).
- When 1.1 development starts, freeze 1.0 in the docs with `npm run docusaurus docs:version 1.0`.
- From 1.0 on, component inputs and outputs follow [Compatibility after 1.0](../README.md#compatibility-after-10).
