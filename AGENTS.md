# Contributor and agent guide

Applies repository-wide to humans and coding agents. **Must** means required; **should** means the default unless a reason is documented. These are project policies informed by official sources, not universal McNeel requirements. Apply relevant rules without unrelated cleanup or implementing future capabilities.

## Orientation and architecture

Read [README.md](README.md) (Architecture, Data flow, Compatibility after 1.0), [sample instructions](samples/README.md), [icon conventions](docs/ICONS.md), affected code and tests. Discuss substantial architecture/API changes through the README contribution process; routine fixes can proceed.

| Location | Responsibility |
| --- | --- |
| `IfcHopper.Core/Model/`, `Geometry/`, `IO/` | Backend-neutral model/rules, geometry algorithms, IO/path contracts |
| `IfcHopper.Core/Backends/Xbim/` | All xBIM integration, behind IO contracts |
| `IfcHopper/Components/`, `Types/` | Thin GH input/validation/orchestration/output; goo and parameters |
| `IfcHopper.Tests/` | Core xUnit tests |
| `tools/SampleBuilder/`, `tools/IconRenderer/` | Generated examples; icon rendering |

- Must keep Core independent of RhinoCommon, Grasshopper and UI frameworks; Rhino conversion belongs in the plugin or a Rhino-dependent library.
- Must preserve **Rhino 8.20+, Windows/macOS, .NET 8 (`net8.0`)** unless explicitly changing support. Check APIs against the minimum service release; newer package references or installed SDKs prove nothing about host compatibility. Rhino 9 runtime advice does not retarget this project.
- Must verify affected macOS behavior before claiming portability, especially native/Windows dependencies and `System.Drawing.Common`; follow host-supported icon handling.
- Should use explicit dependency versions, justify additions and avoid unrelated upgrades. Document tested Rhino/service release, OS, runtime and architecture; compilation alone proves no host support.
- Must preserve unrelated edits/user files and change generated artifacts through their sources. Never invent SDK APIs, IFC rules, issue numbers, SHAs or test results; consult authoritative version-appropriate sources when uncertain.

## C# and comments

- Should favor cohesive methods, descriptive names, explicit ownership, typed contracts and surrounding style over reflection, `dynamic`, loose dictionaries or hidden globals unless integration requires them. Avoid unrelated reformatting.
- Must validate boundaries and handle null/invalid states explicitly; defaults must not conceal bad data. Should avoid new warnings; investigate, scope and explain necessary suppressions.
- Must dispose owned streams/stores/resources and unsubscribe at component/document lifetime end, without disposing borrowed objects or retaining closed documents through events/caches. Shared mutable state requires explicit lifetime, invalidation and concurrency rules.
- Must write clear English using consistent code/IFC/user terminology. Should explain reasons, invariants, assumptions and tradeoffs near the decision, not narrate obvious operations; link broader design notes instead of long inline essays.
- Must document new/materially changed public core APIs with `///` XML: purpose (`summary`), meaningful parameter/result contracts (`param`, `returns`), caller-visible exceptions (`exception`) and constraints (`remarks`). Avoid empty/redundant tags. Should use `see cref` and `inheritdoc` where accurate, documenting deviations.
- Must document relevant units, frames, null/empty semantics, mutation, ownership/lifetime, thread safety, failures and lazy-loading/IO side effects, including property access/enumeration.
- Must explain non-obvious mappings, tolerances and workarounds beside the code, citing specification/docs/issues; state affected versions and removal conditions for workarounds. Update comments with behavior, tests, tooltips and docs.
- Should reserve TODOs for concrete deferred work with reasons and available issue references; disclose correctness/data-loss defects. Must remove abandoned commented-out code; avoid author/date banners and generated filler. Never include secrets, personal/proprietary data or machine-specific paths in comments/examples.

## Grasshopper contracts and compatibility

- Must assign unique stable component/parameter GUIDs, never generated at runtime or repurposed. Provide clear names/nicknames, categories, descriptions and readable icons following toolbar/icon conventions.
- Must describe IO meaning, units, defaults, optionality and limits in tooltips. Prefer typed parameters; deliberately match `GH_ParamAccess.item/list/tree` to `IGH_DataAccess` calls.
- Must preserve meaningful paths, ordering, correspondence and null entries; document intentional flattening/grafting/simplification or other restructuring. Distinguish unconnected optional inputs, connected empties and explicit clearing: modify components preserve unconnected values and apply documented connected-list replacement.
- Must check required retrieval and handle empty lists/branches, null goo, invalid casts and repeated iterations without stale results or unhandled exceptions. Should produce deterministic results for inputs/context; avoid computation in constructors, icon getters and preview callbacks.
- Must require explicit user triggers for file writes, baking, network/destructive actions and document repeated-solve behavior; ordinary recomputation must have no unintended side effects.
- Must not mutate upstream values: goo shares core references. Edited copies preserve required identity/source links; `Duplicate()` may share only values whose contract prevents downstream mutation affecting upstream data. Goo validity, descriptions, display strings and casts must be predictable; unsupported casts fail without data loss.
- Must define internalisation/persistence support explicitly and expose limitations. Persist portable meaningful state, never handles/pointers/caches. Save options through GH_IO `Read`/`Write`, calling appropriate base methods, defaulting missing older fields and versioning changed formats. Record undo, expire changed computation and restore options/visible state on reopen.
- Must treat saved definitions as public contracts. After 1.0, append IO only; never rename, reorder, remove or change existing types. Assess access mode, defaults, units, matching and output meaning too.
- Must give incompatible replacements new GUIDs, retain old components with `GH_Exposure.hidden`, and supply `IGH_UpgradeObject` migrations per README. Should preserve beta definitions; deliberate beta breaks require documentation and regenerated samples.
- Must test migrations on older definitions, including wires, persistent data and options; document breaks, minimum-version changes and migration steps in release notes.

## Geometry, execution and IFC

- Must convert Core metre lengths only at the Rhino boundary, using squared/cubed scales for area/volume. Distinguish world/local/parent/georeferenced frames; apply placements once and test relevant nested transforms/large coordinates.
- Must use the owning document/context for units and appropriate documented tolerances, define no-document behavior, and avoid arbitrary active documents, unexplained constants or exact floating-point comparisons.
- Must validate geometry and boolean/meshing/conversion results, reporting failure/degradation. Duplicate borrowed mutable geometry before edits; define ownership/disposal/cache rules. Should bound meshing/preview cost and cache expensive results with correct invalidation.
- Must honor preview visibility/selection and clipping bounds without model mutation. Bake only on explicit action into the intended document, with attributes, undo and a documented repeated-bake policy.
- Must give actionable runtime messages identifying affected inputs/objects and remedies: errors for failures, warnings for partial/degraded output, remarks for useful status. Never swallow failures or report success; catch expected exceptions at handling boundaries and retain diagnostics without private data. Should avoid exceptions for normal branching.
- Should profile representative large models, repeated solves and memory before parallelism/complex caching. Must keep `IGH_DataAccess`, UI and document modification out of workers; parallelize only known thread-safe APIs/data.
- Must use the supported task-capable lifecycle: collect inputs on solve thread, compute independently, publish through solve, handle cancellation/exceptions/synchronous fallback, reject obsolete results and avoid blocking UI waits.
- Must bound caches, invalidate for file/context changes, release backend resources and preserve lazy loading instead of eagerly materializing unrelated IFC data.
- Must follow buildingSMART schema semantics and existing spatial/containment/aggregation/type/relationship rules, not backend convenience alone. Preserve GlobalIds/source links, unmodified/unmodeled data in supported in-place edits, and distinguish unloaded collections from explicit empties.
- Must distinguish in-place editing from rebuild/schema conversion and disclose unsupported changes, downgrade behavior and loss. Test affected IFC2X3/IFC4/IFC4X3_ADD2 paths, preserving unrelated entities during edits.
- Must follow existing path rules, handle absent/unreadable files and release handles; process working directory need not equal definition directory. Should use transactions/temporary replacement to protect destinations; handle source overwrite and partial failure explicitly.
- Must treat imported files as untrusted: no embedded execution, unsafe deserialization or uncontrolled extraction. No telemetry/model uploads without explicit consent.

## Verification and documentation

Use .NET 8 from repository root:

```sh
dotnet build IfcHopper.sln
dotnet test IfcHopper.sln
```

First core build downloads buildingSMART templates; restore needs package sources. Distinguish environment/network failures from code failures.

- Must add/update meaningful regression tests for domain changes/bugs, covering relevant invalid/empty data, units, transforms and schemas rather than mirroring implementation. External fixtures must come only from `buildingSMART/IFC4.x-specification-models`, pinned and hash-verified via `tools/FetchTestFixtures.ps1`; never use private manual-testing models. Report skips; green does not prove skipped coverage.
- Must update SampleBuilder sources for component-contract/example changes and regenerate affected samples, never manually editing generated `.gh`, `.3dm` or IFC files:

```sh
dotnet build IfcHopper/IfcHopper.csproj
dotnet run --project tools/SampleBuilder -- 7 11
```

Numbers select examples; omit for all. Builder requires licensed Rhino 8 at the documented location or `RHINO_SYSTEM_DIR`; see [sample workflow](samples/README.md).

- Must check affected host behavior: load/solve/messages, item/list/tree matching, repeated solves, preview/bake, save/reopen, undo and cancellation as applicable. Verify platform-sensitive behavior on affected systems/minimum Rhino; state unavailable checks precisely.
- Must align README requirements, component help, user docs and small reproducible examples with behavior, expected results and units. Should document clean-checkout builds and host/licence requirements, minimizing private services/tools and local paths.
- Should supply issue reproduction versions (Rhino/service release, OS, runtime, plugin), steps and minimal shareable files. Remove personal/proprietary content from fixtures/logs.
- Documentation-only edits need links/paths/diff checks, not application tests. Must report final changes, reasons, actual checks/skips and limitations; fix introduced failures and disclose unrelated ones with evidence, never disable checks to appear green.

## GitHub collaboration and CI

Follow existing templates/branch rules. These conventions authorize no external messages, pushes, merges or publication beyond requested scope.

- Should scope substantial changes in issues/discussions with problem, expected behavior, compatibility and acceptance criteria; small fixes need no bookkeeping issue. Inspect branch/worktree, preserve work and use focused descriptive branches (e.g. `fix/placement-units`).
- Should make cohesive commits with short imperative subjects and useful motivation/compatibility/validation bodies. Avoid unrelated formatting/upgrades. Must inspect diffs, excluding secrets, local config, build output and unrelated work; commit intentional regenerated samples with sources.
- Must not discard work, rewrite shared history, force-push or bypass protections without explicit authorization. Do not invent signing/DCO/prefix requirements. Merge only as authorized after required checks/reviews; maintainers choose strategy, and branches are deleted only when permitted/unneeded.
- Must describe the final problem/behavior in PR titles/bodies, linking applicable issues and listing actual tests, host/platform coverage, limitations and migrations. Closing keywords require full issue resolution. Should use clearly incomplete drafts for authorized early feedback.
- Must review applicable architecture, ownership, units, trees, persistence, compatibility, platforms, docs and samples. Should give actionable review comments with reasoning, distinguish requirements/suggestions, and resolve threads after fixes or recorded agreement.

No `.github/` workflows existed when guidance was added; the following governs future automation, not an assertion of configured CI/protection.

- Should use `.github/workflows/`, descriptive job/step names, locally reproducible commands and checked-in scripts for complex logic. Validate PRs/relevant pushes with supported SDK/build/core tests; separate and label licensed Rhino coverage.
- Must keep ordinary fork core checks free of private credentials/licensed Rhino; host checks need documented controlled runners/licensing and visible skips.
- Must explicitly minimize token `permissions` (normally `contents: read` for validation), limit writes to necessary jobs and keep secrets out of ordinary PR jobs/logs.
- Must treat fork code, event text and artifacts as untrusted. Pass event values through environment variables/structured arguments with correct shell quoting, never direct script interpolation. Never execute untrusted PR code under privileged `pull_request_target`/`workflow_run`, on persistent privileged self-hosted runners, or in release jobs.
- Must pin external actions/reusable workflows to verified full SHAs with readable version comments; review sources, permissions and updates. Should use reviewed Dependabot/equivalent update proposals.
- Should explicitly select shells for platform-specific steps and supported runners/SDKs, avoid cross-platform path assumptions and use matrices for meaningful platform coverage.
- Should set timeouts and cancel superseded PR runs; do not interrupt publication without recovery policy. Must cache only disposable build/restore data keyed by platform/dependencies, never secrets/models; untrusted caches/artifacts cannot become trusted release inputs.
- Should retain useful failure reports/artifacts for bounded periods. Must identify release artifacts by source commit/version/platform and include licences.
- Must check workflow YAML and use a checker such as `actionlint` when available; inspect actual affected runs when authorized/accessible. Parsing alone proves no trigger/permission/runner correctness. Required checks must still report with filters/conditions; never mask required failures with unconditional success or `continue-on-error`.
- Must restrict publishing to intentional triggers/authorized maintainers, separate from contributor validation. Prefer short-lived credentials where supported and document retries/partial-publication recovery.

## Licensing and delivery

- Must preserve LGPL-3.0 project notices/contribution terms and third-party licences; check redistribution rights for code, packages, fonts, icons and datasets.
- Must not ship private copies of host-provided RhinoCommon/Grasshopper assemblies. Include required redistributable dependencies/resources deliberately with notices.
- Should use Yak when packaging is requested; maintain consistent plugin identity/assembly/package versions and accurate Rhino/platform tags. Must test the actual package in a clean install: dependency loading, icons, examples, update/uninstall. Development output is insufficient evidence.
- Before handoff, review applicable contracts/migrations, ownership, units/trees/errors, persistence/undo/preview/bake/cancellation, tests/workflows, comments/docs/samples/licences/release notes and diff cleanliness. State unresolved limits; publish only within requested scope.

## Official references

Consulted **2026-10-13**. Prefer target-version SDK/source and authoritative documentation over snippets. Recheck runtime/packaging when changing support, and Actions docs when changing triggers/security boundaries.

- McNeel: [Grasshopper guides](https://developer.rhino3d.com/guides/grasshopper/), [RhinoCommon API](https://developer.rhino3d.com/api/rhinocommon/), [runtime migration](https://developer.rhino3d.com/guides/rhinocommon/moving-to-dotnet-core/).
- Components: [identity/parameters](https://developer.rhino3d.com/guides/grasshopper/simple-component/), [options/persistence/undo](https://developer.rhino3d.com/guides/grasshopper/custom-component-options/), [task lifecycle and data-access thread safety](https://developer.rhino3d.com/guides/grasshopper/programming-task-capable-component/). Older examples require target-SDK checks.
- Formats/distribution: [IFC specification](https://standards.buildingsmart.org/IFC/DEV/IFC4_3/HTML/content/introduction.html), [Yak structure](https://developer.rhino3d.com/en/guides/yak/the-anatomy-of-a-package/), [package restore](https://developer.rhino3d.com/en/guides/yak/package-restore-in-grasshopper/).
- Microsoft: [XML comments](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/documentation-comments), [exceptions](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions).
- GitHub: [secure workflow use](https://docs.github.com/en/actions/reference/security/secure-use).
