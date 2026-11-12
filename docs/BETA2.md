# Beta.2 preparation

Target: `1.0.0-beta.2`. Published as a GitHub prerelease on 2026-11-13 (Europe/Rome), followed by the docs deployment. Follow [RELEASING.md](RELEASING.md).

## Changes to include

- Issue #2: Placement input icon and persistent holder in `7 - Params`; stable parameter GUID and existing plane persistence retained. Fix commit: `96c18e3`; include through its PR.
- Contributor guide and public README link; private `Bible/` references excluded from Git.
- Record additional accepted fixes here as work progresses. Issue #1's preview-edge setting explanation is not a code fix.

## Validation recorded on 2026-10-13

- Development solution build passed; latest Placement build used separate `bin/Review/` output because Rhino/Visual Studio locked the normal output.
- Release solution build: 0 warnings, 0 errors. After replacing private-model checks with public specification fixtures: 194 passed, 0 skipped, 0 failed. Earlier counts (196 passed, 9 skipped) included private model tests and are superseded. Exact private bridge/building assertions were removed, not reproduced by public models; synthetic domain regressions remain.
- Automatic acquisition now uses buildingSMART/Standards-builds revision `41f3439ef11c77e7e159859a9e7c655b256f3a68`, with pinned ZIP SHA-256 and a revision-specific cache. All 760 XML files match the prior local snapshot exactly. No local reference folder is required; EXPRESS/XSD URLs are documented for optional reference use.
- Verified automatic download/extraction from an empty cache and a full Release solution build (0 warnings/errors); incorrect checksums stop before extraction. Offline archive/folder paths and actionable download errors remain supported. Snapshot status is development documentation, not verified frozen-release equivalence; provenance is recorded in README and package notices.

## Remaining release gates

- [x] Review remaining open issues and merge accepted fixes into `main`. Issue #15 is a follow-up enhancement for individual input controls, deferred beyond beta.2.
- [x] Set candidate version; prepare final release notes and known issues.
- [x] Final candidate Release build/tests using pinned public fixtures; no private models required.
- [x] Verify fresh-cache template acquisition and record pinned source/checksum/provenance. Recheck if templates change before release.
- [x] Regenerate/solve samples and review intentional artifact changes.
- [x] Regenerate component docs/icons, update manual pages/sample download and pass docs build.
- [x] Package exact final commit; inspect version, dependencies, resources and licence notices. Release source: `5858231`. Final Release archive passed host input regressions.
- [ ] Clean installation checks on supported platforms/minimum Rhino, including Placement toolbar/icon and persistence; record actual host coverage.
- [x] Tag and publish the authorized prerelease, verify its download, then deploy docs.

## Candidate checks on 2026-11-13

Release build, 195 tests (zero skips), all 15 sample solves, compact input host regressions, component docs generation and ZIP/Yak builds passed. SDK 10.0.401 targets .NET 8 on Windows x64 / Rhino 8.37.26307.15001. See [release notes](RELEASE_NOTES.md) for precise platform and clean-install limits. Docs production build passed with broken links configured to fail. Publication and live download verification completed.

The actual extracted Release ZIP also passed all 15 solves and compact-input regressions in headless Rhino. SampleBuilder accepts `IFCHOPPER_PLUGIN_DIR` to repeat this validation; the selected module identity is verified. Interactive clean-install/update/uninstall and minimum-host/macOS coverage remain disclosed limitations, not passed gates.

## Publication record

- [GitHub prerelease](https://github.com/IfcHopper/grasshopper-code/releases/tag/v1.0.0-beta.2), tag `v1.0.0-beta.2`, source commit `5858231`.
- ZIP SHA-256: `AAE57FAAFFC3034D16DC78D912D023ACCFB9A0067CD5628160EBB79CD05D57AC`. The published ZIP was downloaded and hash-verified before pushing docs.
- Yak SHA-256: `E6538A87F5D0F041930FC8BB9F4AA3AFCDAC16926633708D23425DD7815B119B`.
- Docs source `986cd99`; [deployment run](https://github.com/IfcHopper/grasshopper-documentation/actions/runs/43330423281) passed. Live installation and samples pages show beta.2; the live sample ZIP matches the validated local download.
- Documentation dependency audit: 66 vulnerabilities (9 low, 15 moderate, 24 high, 18 critical), recorded in the docs repository README. No dependency upgrades were included.
- The remaining unchecked clean-install/platform gate is an explicitly disclosed testing limit; it was not passed. See the prerelease notes for exact coverage.
