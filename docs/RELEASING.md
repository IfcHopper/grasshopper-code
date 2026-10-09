# Releasing

How an IfcHopper release is made. What each release contains is in the README [Release plan](../README.md#release-plan); the rules for component inputs and outputs are in [Compatibility after 1.0](../README.md#compatibility-after-10).

## Versions

- Semantic versions: `1.0.0-beta.1`, `1.0.0-beta.2`, … then `1.0.0`, `1.1.0`, …
- Each release is a git tag `vX.Y.Z[-beta.N]` on `main` with a GitHub Release; betas are marked as pre-release.
- A fix for an older release while `main` has moved on gets a `release/X.Y` branch from its tag.

## Steps

1. **Version.** Set `<Version>` in `IfcHopper/IfcHopper.csproj`. Grasshopper shows it in full (`IfcHopperInfo.AssemblyVersion`).
2. **Check.** All must pass:

   ```sh
   dotnet build IfcHopper.sln -c Release
   dotnet test IfcHopper.sln
   dotnet run --project tools/SampleBuilder        # regenerates and solves the samples
   ```

   Commit the regenerated samples.
3. **Tag and push:** `git tag v1.0.0-beta.2`, `git push origin main v1.0.0-beta.2`.
4. **Package:** `powershell -File tools/Package/Package.ps1` builds in Release and writes `artifacts/IfcHopper-<version>.zip`: an `IfcHopper` folder with the plugin and its DLLs (no `.pdb`), `LICENSE.txt`, `INSTALL.txt` and `THIRD-PARTY-NOTICES.txt` (from `tools/Package/`). Update the notices when dependencies change.
5. **GitHub Release** for the tag: title `IfcHopper <version>`, release notes (highlights, install steps, breaking changes, known issues, how to report bugs), the zip attached, pre-release for betas.
6. **Docs** (below).

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
