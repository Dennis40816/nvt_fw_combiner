# WS-PERF: startup performance

Owner: Codex. Commander: Claude Code. Board: [1.1.12 board](../1.1.12.md),
checklist C-1. Protocol: [handoff README](../README.md).

## Dispatch envelope 2026-09-25

**Outcome.** Every launch, cold and warm, measured from process launch: the
first main window is visibly presented within **500 ms**, and all startup
loading (catalog validation, required page readiness, deferred startup views)
completes within **2,000 ms**. Warm baseline on the window-handle proxy:
1.333 s to window, then 4.846 s to catalog applied, 6.518 s to warm-up
finished.

1. Measure first: one warm-up plus five scored warm launches, and separate
   controlled cold launches, on the same machine, settings, build flavor and
   arguments; raw stage timings; visible-presentation evidence (a window
   handle alone does not prove presentation). Commit the measuring script so
   anyone can reproduce it.
2. Break down the 4.846 s post-window interval. Hypotheses to test, not
   causes: catalog and profile loading, validation and compilation, repeated
   work, UI materialization; also, from code mapping, catalog publication
   compiling every static route at startup (`CanonicalCapabilityCatalogSource.Load`).
3. Optimize through the existing startup and catalog owners. Background work
   still counts toward 2,000 ms.
4. Check that the first Merge and Replace navigation does not inherit the
   wait.
5. Report gains, memory and allocation trade-offs and residual delays. A
   missed target names its blocker; the limit is never relaxed.

Non-goals: CtrlRAM cold first-open and F14/F15 (1.2.8); broad profile
reference convergence (1.2.1); weakening validation, catalog completeness,
honest loading and error feedback, page readiness, independent page instances
or bounded lifetime.

**Authority.** R1, or R2 if an optimization changes catalog or compilation
semantics (for example lazy route compilation): then pause for architecture
review. Local commits on this branch; no push or pull request (owner). Final
acceptance (actual package on the owner machine) is the owner's.

**Model.** `gpt-6-sol` at xhigh; escalate to `gpt-6-astra` for a catalog or
compilation redesign. Headless runs pass `--sandbox workspace-write`.

**Branch and worktree.** `feature/1.1.12/startup-performance`, worktree
`<worktrees>/startup-performance`, rebased onto
the `1.1.x` trunk at the base refresh.

**Write lock.** This log; new files under `docs/handoff/bugs/`;
`tools/NvtFwCombiner.PerformanceProbe/`; startup and catalog code in
`src/NvtFwCombiner.Desktop/`, `src/NvtFwCombiner.Bootstrap/`,
`src/NvtFwCombiner.Application/Capabilities/`,
`src/NvtFwCombiner.Infrastructure/Composition/` catalog and registry loading,
`src/NvtFwCombiner.Infrastructure/Bundles/`, and Presentation startup files
(`MainWindow*`, shell construction and catalog application); their tests.
List the exact files in your first checkpoint. WS-IO owns picker,
persistence and Report files in the same projects: overlap stops that part.

**Read first.** Root `AGENTS.md`; the roadmap "Owner-approved 1.1.12 startup
optimization" section and the audit handoff "Startup optimization assigned to
1.1.12" section (on the trunk since `v1.1.11`); board C-1.

**Acceptance.** Items 1-5 above with committed measurement tooling, a stage
breakdown table in this log, before and after numbers on comparable launches,
and passing affected tests. Final C-1 acceptance happens at freeze on the
owner machine (checklist D-2).

**Stop and ask.** When a target looks unreachable without changing validation
or catalog semantics; when a change would touch profiles, contracts or
firmware bytes; when owner-machine measurement is needed.

**Bugs.** Record every bug you find, in or out of scope, as a new file under
`docs/handoff/bugs/` per the bug ledger in `docs/handoff/README.md`. Cite bug
IDs here; fix only what is in scope.

**Start.** Right after the base refresh (checklist A-7).

## Amendment 2026-09-25: release by 2026-09-28 (board decision 9)

**Owner change.** Claude Code (Opus 5.5) now owns this workstream as Startup
A: everything after `main-window.opened` (catalog loading, validation,
compilation and application; startup warm-up of views). Startup B, process
launch to `main-window.opened`, moves to WS-WINDOW (Codex) on
`feature/1.1.12/startup-window`.

**Baseline** (board, "Release plan to 2026-09-28"): `main-window.opened` to
`startup-warmup.catalog-state.applied` 5,005 ms with 770 MB allocated; warm-up
completed 6,498 ms after managed entry.

**Write lock (Startup A).** This log; new bug files;
`src/NvtFwCombiner.Application/Capabilities/`; catalog and registry loading in
`src/NvtFwCombiner.Infrastructure/Composition/` and
`src/NvtFwCombiner.Infrastructure/Bundles/`; catalog application and startup
warm-up code in `src/NvtFwCombiner.Presentation.Avalonia/` (files listed in
the first checkpoint); `scripts/measure-startup.ps1`; their tests.

**Due.** Pull request by 2026-09-27 12:00 +08:00, reviewed by a fresh Codex
thread.

**Common to every lane (decision 9).** Base `feature/1.1.12/handoff`; pull
requests target the `1.1.12` integration branch; the commander pushes and
opens them, the worker never pushes. The current rules still apply in full:
complete the capability-reuse gate your change requires
(`docs/governance/development-execution-workflow.md`), run the affected tests,
and have `python scripts/verify.py --structure-only` pass on your final commit
before you report `verified`. Builds must not leave modified
`packages.lock.json` files; restore them if a build rewrites them. Record every
bug in the bug ledger. The live board is `git show 1.1.x:docs/handoff/1.1.12.md`
(section "Release plan to 2026-09-28").

## Checkpoints

### 2026-09-25 Startup A unit 1 admission: deduplicate bundle schema validation
State: planned
Commits: none yet; source base `d4902f5ee` (product source equal to `v1.1.11`, `1c37bd718`).
Authorization: owner decisions 9 and 10 (board); this envelope and its amendment. Bounded local
R1 path (`docs/governance/development-execution-workflow.md`, ADR 0070): an existing capability's
implementation correction; no contract, schema, profile, ADR or byte change.
Semantic owner: `ProfileBundleSchemaValidator` (Infrastructure, Bundles) owns bundle-schema
meta-validation and build; `ProfileBundleLoader` stays its only bundle caller. No second path.
Exact paths: `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleSchemaValidator.cs`; a new
partial test file under `tests/NvtFwCombiner.Infrastructure.Tests/Bundles/`.
Change: a bounded, process-wide cache of schemas that passed meta-validation and build, keyed by
schema id, entry content SHA-256 and maximum JSON depth. Failures are never cached; every schema
entry is still read, hashed and matched; every document is still validated.
Evidence: read-only hot-path analysis found 35 schema meta-validations and builds for 10 distinct
contents across the 19 bundles loaded between `main-window.opened` and
`startup-warmup.catalog-state.applied` (5,005 ms, 770 MB in the baseline).
Acceptance: existing validator tests unchanged and passing; new tests for equal `$id` with
different content, an invalid schema rejected on every attempt, and the depth limit still enforced
on a cache hit; measured time and allocation reduction on the package-equivalent build.
Narrow tests: Infrastructure `Bundles/ProfileBundleSchemaValidatorTests*`, `ProfileBundleLoaderTests`,
`TrustedProfileBundleDocumentProjectionTests`; architecture `RepositoryBoundaryTests` profile-schema
trust and infrastructure convergence; Bootstrap `CanonicalCapabilityCatalogMigrationTests*`.
Residual gates: scoped Polytail; capability-reuse record, cross-runtime review (Codex) and Golden
regression before integration; `verify.py --structure-only`.
Next: implement unit 1 with its tests.

### 2026-09-25 Startup A unit 1 verified locally: schema validation dedup
State: local
Commits: this commit (unit 1 code, tests and this entry) on `0f4495edd`.
Evidence: package-equivalent publish (the exact `scripts/package.ps1` app flags: self-contained
win-x64, single file with compression, ReadyToRun composite, untrimmed) and a no-compression
variant; `scripts/measure-startup.ps1 -RequirePreloadLifecycle`, one warm-up and five scored runs;
raw data in the test area under `evidence/v1112-pkg-baseline-d4902f5ee/` and
`evidence/v1112-pkg-unit1-unit1/`. Other agents built on the machine during some runs; allocation
figures are unaffected by that.

| Median | Before, package | Unit 1, package | Unit 1, no compression |
| --- | ---: | ---: | ---: |
| Process launch to first window handle | 765 ms | 762 ms | 494 ms |
| `main-window.opened` to `catalog-state.applied` | 3,080 ms | 1,590 ms | 1,505 ms |
| Managed entry to warm-up completed | 3,639 ms | 2,128 ms | 2,001 ms |
| Allocated by warm-up completed | 783 MB | 371 MB | 371 MB |

Evidence: a sampled thread-time trace of the package build (`package.nettrace`, same folder) put
81% of catalog loading in `ProfileBundleSchemaValidator.ValidateEntries`, 46% in `ParseSchema`.
Evidence: tests at this state: Infrastructure `Bundles` 312 passed (3 new); Architecture profile
schema trust, infrastructure convergence and catalog 9 passed; Bootstrap
`CanonicalCapabilityCatalogMigrationTests` and `CatalogLoadScopedCtrlRamTests` 45 passed.
Open: the previous local baseline (1,235 ms to window, 6,498 ms to warm-up) was a plain
`dotnet build` without ReadyToRun and overstates the shipped package; decisions use the
package-equivalent figures. Turning off single-file compression is a packaging change (owner
decision pending on the board).
Next: GC configuration experiment; unit 2 (single parse per bundle document).

### 2026-09-25 GC experiment; the compression option conflicts with an owner decision
State: local
Commits: this entry.
Evidence: GC variants by environment on the unit 1 no-compression build (one warm-up, five scored
runs each; test area `evidence/v1112-gc-nocompress-unit1/`), medians of process to window and
managed entry to warm-up completed: default 484 / 2,026 ms; `GCgen0size` 64 MB 476 / 2,202 ms;
non-concurrent 531 / 2,291 ms; server GC 608 / 2,596 ms. Default GC stays; no GC setting is adopted.
Evidence: `tests/README.md` ("Home single-file compression diagnosis" and "Home size-constrained
follow-up", 2026-09-11) records that the owner rejected the uncompressed single-file EXE (173 MB),
kept the 80,000,000-byte EXE ceiling, asked for alternatives within 100,000,000 bytes, and accepted
compressed composite ReadyToRun; a compressed non-composite ReadyToRun probe exited with
`0xC0000602`. The commander's earlier proposal to turn compression off conflicts with that decision
and is withdrawn unless the owner revisits it. New facts for the owner: the uncompressed EXE adds
0.4 MB to a Deflate ZIP entry (71.5 to 71.9 MB), and unit 1 does not change the pre-window time.
Open: first window stays about 760 ms in the package shape; the remaining gap is single-file
decompression before managed entry. Owner decision needed on the options on the board.
Next: unit 2 and unit 3 (R1) for the loading target; a composite-exclusion probe only if the owner
approves it.

### 2026-09-25 Startup A units 2 and 3 admission (local R1), delegated to Claude sub-agents
State: planned
Commits: none yet; source base is this commit. Authorization: decisions 9 to 11; bounded local R1
path; same semantic owners, no second path, no contract, schema, profile or byte change.
Unit 2 (single parse per bundle document), branch `feature/1.1.12/startup-parse-once`, worktree
`<worktrees>/perf-u2`. Exact paths: `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleLoader.cs`,
`ProfileBundleSchemaValidator.cs`, `TrustedProfileBundleDocumentProjection.cs`,
`ProfileBundleFileSnapshot.cs`, `ProfileBundleEntrySnapshotCollection.cs` (same folder), and tests under
`tests/NvtFwCombiner.Infrastructure.Tests/Bundles/`. Change: each manifest and family/profile entry is
parsed strictly once per load; the same duplicate-key, depth, schema and DTO checks run in the same
order; `ValidateEntries` still precedes `new TrustedProfileBundle(` and `ValidateAndClone(`.
Unit 3 (duplicate compilation removal), branch `feature/1.1.12/startup-compile-dedup`, worktree
`<worktrees>/perf-u3`. Exact paths: `src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2RegistrationRegistry.cs`,
`CtrlRamV2RouteRegistry.cs`, `CanonicalCapabilityDisclosureInventory.cs` (same folder), and tests under
`tests/NvtFwCombiner.Bootstrap.Tests/`. Change: identical compilations within one process-static
initialization (CtrlRAM report metadata plans per standard registration and map) and identical
disclosure compilations within one load are reused; CtrlRAM definitions stay per load
(`CatalogLoadScopedCtrlRamTests`) and no compiled object is shared across catalog loads.
Acceptance for both: the existing affected tests unchanged and passing, new tests proving identical
results, and measured gains on the package-equivalent build (commander measures).
Residual gates: capability-reuse record (R1, independent final review by Codex), Golden regression,
`verify.py --structure-only`, scoped Polytail.

### 2026-09-25 ADR 0075 independent design review and revision
State: planned (R2 design; not implemented)
Commits: draft `e2f5d80b7`; revision in this commit.
Evidence: Codex (`gpt-6-sol`, high, read-only sandbox) reviewed the draft: ACCEPT-WITH-CHANGES.
P1 before implementation: GR-1 (equivalence claim too strong: a preload can cache a transient
failure earlier), GR-2 (concurrent evaluation of shared `JsonSchema` instances unproven; JsonSchema.Net
8.0.5 documents thread safety only for its registries), GR-3 (deadlock argument must be a checked
invariant; recommended provider-first layering), GR-5 (memory gate needs per-launch peaks and GC heap,
not medians). P2: GR-4 (pin the preload set by test; the proposed predicate selects 19 of 24 today),
GR-6 (precise cancellation and failure rules), GR-7 (wording, reciprocal ADR 0049 link).
Revision: provider layer first and serially, then a bounded worker layer; tested precondition that
non-provider bundles reference only providers; equivalence limited to fixed re-readable inputs;
per-launch memory gate; a concurrency stress test as an adoption condition; PublicationOnly rejected
for 1.1.12.
Open: adoption waits for units 2 and 3 measurements (decision 11). If adopted, a fresh design
review of this revision precedes the R2 record's design-active admission.
Note: GR-2 also touches unit 1 (schemas now shared across bundles). Today the only caller is each
bundle's lazy load, which the catalog drives serially on one worker thread; the concurrency stress
test will be added before integration either way.

### 2026-09-25 Preload gate measurements (package shape, per launch)
State: local (preload implemented at `412c7b4b6`; adoption pending owner decision)
Evidence: `412c7b4b6` passed Bootstrap catalog 626, Architecture 268, Golden regression 14 and the new
preload tests 5. Package-shape publishes, one warm-up and five scored launches each, plus three
`dotnet-counters` runs for GC heap (test area `evidence/v1112-pkg-*`):

| Build | Process to loading done | Peak private | Peak working set |
| --- | ---: | ---: | ---: |
| `v1.1.11` source | 3,885-4,179 ms | 328.7-331.1 MB | 334.2-335.7 MB |
| Units 1 to 3 | 2,291-2,344 ms | 324.5-326.8 MB | 321.8-325.2 MB |
| Preload, 4 workers | 1,973-2,077 ms | 331.0-334.9 MB | 330.5-337.3 MB |
| Preload, 2 workers (experiment, reverted) | 2,077-2,166 ms | 331.7-334.0 MB | 330.2-334.0 MB |

GC heap after warm-up with preload: 36-38 MB (budget 50 MB). The catalog interval fell from 1,487 ms
(units 1 to 3) to 1,184 ms (preload, 4 workers). Preload raises peak private bytes by about 7 MB over
units 1 to 3 regardless of the worker bound, which is 2-4 MB above the `v1.1.11` maximum; 2 of 5
launches met 2,000 ms. Neither variant meets every gate of ADR 0075 decision 8; the owner decides.
Open: JsonSchema.Net 8.0.5 source audit (ADR 0075 decision 4) still running.

### 2026-09-25 Race-freedom evidence for the preload
State: local
Commits: this commit (build lock, architecture guard, ADR 0075 decision 4) on `c63c95a98`.
Evidence: source audit of JsonSchema.Net 8.0.5 (commit `3520d7ac43e5c6c9b91abeac5af992eb82ffbf63`,
identified from the NuGet nuspec, the csproj version and the loaded assembly's informational version):
concurrent evaluation of one fully resolved schema with shared options, concurrent Draft 2020-12
meta-validation, and concurrent builds with private registries are safe under three conditions: no
production write to the library's global registries, schemas fully resolved (local references only),
and a known `$schema`. Enforcement added: meta-validation and build under one lock; the architecture
test `ProductionCodeNeverWritesJsonSchemaGlobalState`. Observed evidence: the snapshot digest pinned
before units 3 and the preload matched in 11 fresh processes, each running the cold parallel preload;
the schema concurrency stress test passed 15 repeated runs; Architecture 269, Infrastructure bundles 328
and Bootstrap preload, digest and catalog 54 passed.
Follow-up (1.1.13): the audit recommends the local-reference rule also for
`EmbeddedVersionManagementSchema.Load`; not needed for the preload, which evaluates bundle schemas only.
