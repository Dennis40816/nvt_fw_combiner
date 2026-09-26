# WS-TEST: test architecture (1.1.13)

Owner: Claude Code (Opus 5.5), design drafter. Board:
[1.1.13 board](../1.1.13.md). Protocol: [handoff README](../README.md).
Deliverables: the [test architecture ADR draft](ADR-DRAFT-test-architecture.md)
and the [pilot split plan](PLAN-test-pilot-split.md). The owner decisions are
at the end, in risk order ([owner decisions](#owner-decisions-in-risk-order)).

## Dispatch envelope (commander, 2026-09-26)

- **Outcome.** A test architecture ADR draft, number `00XX`, and one pilot
  split plan, answering: (1) test categories and which changed paths run which
  tests, consistent with WS-GOV's authority path map and review finding F-4
  (full verification when a change cannot be classified); (2) split rules for
  oversized test classes and one pilot with its reason, expected effect and
  proof of equivalence; (3) options to make UiSmoke faster or more stable, with
  their costs; (4) risk levels, records, reviews, batches and the integration
  order with the CI failure-evidence change and WS-GOV G2. Owner decisions in
  plain words with recommendations. Non-goals: any change to tests, code,
  scripts, workflows or profiles; the board; other worktrees.
- **Authority.** Documents only (R0 for this workstream); local commits; no
  push, pull request or GitHub write; never merge the trunk.
- **Branch and worktree.** `feature/1.1.13/test-architecture` in
  `<worktrees>/wstest`, at `cf4e42697`.
- **Write lock.** New files under `docs/handoff/1.1.13/`: this log, the ADR
  draft and the pilot plan.
- **Read first.** The 1.1.12 WS-TEST plan
  (`feature/1.1.12/test-architecture:docs/handoff/1.1.12/WS-TEST.md`), the
  WS-TEST parts of the 1.1.12 WS-GOV log
  (`feature/1.1.12/governance-reset:docs/handoff/1.1.12/WS-GOV.md`), the 1.1.13
  WS-GOV drafts (`feature/1.1.13/ws-gov`), `tests/README.md`, the lanes and
  shards of `scripts/verify.py`, the test shards of `.github/workflows/ci.yml`,
  the core-shard bug and the board's diagnosis.
- **Acceptance.** Committed drafts; owner decisions with recommendations;
  conflicts with other work named. Then stop.

## Ported from 1.1.12

| 1.1.12 item | 1.1.13 disposition |
| --- | --- |
| Outcome 1: ADR with feature-scoped classes, shared builders in `TestSupport`, one category trait per test (Unit, Integration, UiHeadless, Golden, Governance), naming and size guidance | ADR items 2 and 6. Changed: no trait on every test; categories reuse projects, collections and the Golden manifest, and a trait is added only where a runner filters (D-8). Golden membership stays owned by the canonical manifest. "Governance" becomes `structure` and `document`. |
| Outcome 2: path-to-category map feeding tiered CI and the `ui` shard split | ADR items 3 to 5 (selection), 8 (partitions) and 9 (UiSmoke). The unit of selection is the test group (project), not the category. |
| Outcome 3: pilot split of `ShellViewModelTests` with timing and identical counts | Pilot changed to `RepositoryBoundaryTests` ([plan](PLAN-test-pilot-split.md)). The 1.1.12 measurement (86 files, 24,746 lines) counted files by name prefix; commit `acc6ea039` had already split that class into twelve group classes in 2026-08. |
| Outcome 4: review of architecture tests that assert document prose | D-4 and the pilot's `RepositoryDocumentTests`; the move into the structure lane belongs to WS-GOV G2 (F-4). |
| Authority: R1 test changes plus an R2 ADR | Unchanged; the CI adoption (T4) is R3 inside G2. |
| Model: Codex `gpt-6-luna` max for the mechanical migration | Kept for the later mechanical splits (T5), after the pilot has proven the procedure. |
| Write lock: `TestSupport`, the pilot files, bug files | This workstream writes documents only; the pilot gets its own envelope. |
| Start: design during Phase B, pilot after Phase B | Replaced: the pilot does not depend on WS-GOV G1; the selector (T3) needs G1-A; the CI adoption (T4) is part of G2. |

## Findings (read-only, 2026-09-26)

The ADR's context section holds the measurements: static counts at
`cf4e42697`, CI job durations from the 60 most recent `ci.yml` runs (read with
`gh run view`), the oversized classes, and the contracts that constrain
selection. Additional findings:

- **Where CI time goes.** Wall-clock is roughly the UI shard (522 s median)
  plus the finalizer (41 s); the median successful first attempt took 577 s
  from creation to completion. `core` (380 s) is next, then the script chain
  (`a-q` 294 s plus the worker lane 69 s). UiSmoke work alone can win at most
  about 140 s before `core` becomes the critical path.
- **Documents at the line ceiling.** `docs/ui/v1.1.x-custom-options-layout-handoff.md`
  has 2,499 lines, one below the Architecture.Tests ceiling; the next edit that
  adds two lines fails the `core` shard. The roadmap has 2,498 lines here;
  roadmap-tidy brings it to 1,062.
- **Infrastructure runs fully serial.** `verify.py` passes
  `xUnit.ParallelizeTestCollections=false` and `xUnit.MaxParallelThreads=1` to
  the whole project (added in `f789ff4be`, "release: close v1.0.7 CI admission
  blockers"), on top of the serialized `ReadyProbeProcessSerialGroup`. The
  reason is not recorded next to the setting; recover it before changing it
  (T5).
- **Architecture.Tests starts a build.** `PackageTrustMaterializationBatch`
  runs `dotnet msbuild` on the Bootstrap project's
  `MaterializeBuiltInProfileBundles` target for 23 cases, once per test
  process, with a two-minute budget: a `process` test inside the `structure`
  project, and a run-time dependency on `profiles/built-in` and the Bootstrap
  build targets.
- **Documents read by Architecture.Tests.** Besides the ceiling and the roadmap
  checks, topic files read documents as part of code checks: LauncherBootstrap
  (`docs/ci/release-package.md`, launcher contracts), PackageTrustIndex (trust
  index schema), PostbuildStructure (two `docs/architecture/` matrices),
  PresentationStructure (`docs/adr/0006`, `docs/ui/viewmodel-boundaries.md`),
  ProfileStructure (`docs/adr/0035`, an AB contract document, family
  contracts), Roadmap (`README.md`, `SPEC.md`, `CHANGELOG.md`, `VERSION`,
  `docs/adr/0032`, roadmap documents) and StartupDiagnostics (`CHANGELOG.md`,
  `docs/adr/0021`, `docs/adr/0049`, a governance baseline document).
- **Python script tests read widely**: `.github/workflows/`, `docs/contracts/`,
  `docs/governance/`, `docs/ci/`, `profiles/`, `testdata/golden/`, `src/`
  (size policy, release packaging, parity artifacts) and the `.csproj` files
  of the test projects (verifier inventory, source ownership).

### Declared run-time inputs (initial table)

A read-only inventory of what each .NET test project reads at run time
(Explore subagent, model inherited from this session, read-only, 2026-09-26;
the decisive items were rechecked by hand). Every repository read goes through
`tests/NvtFwCombiner.TestSupport/RepositoryPaths.cs`, which honors
`NFC_TEST_REPOSITORY_ROOT` or searches upward for `NvtFwCombiner.slnx`. The
last column names the selection row of ADR item 3 that covers the input.

| Test project | Reads at run time, beyond its project references | Covered by row |
| --- | --- | --- |
| Domain | nothing | 6 |
| Application | `profiles/built-in/ctrlram-postbuild-v2/catalog.json` through the Infrastructure output copy | 4 |
| ProfileContract | `profiles/built-in/**` | 4 |
| Infrastructure | `docs/contracts/*` (schemas, the capability policy), `.gitattributes`, `profiles/built-in/**`, `external-tools/legacy-combiner/**` (runs the real `Combiner.exe`), `testdata/golden/canonical/**`; built `ready-probe` and `launcher-bootstrap` outputs | 4, 6 |
| Bootstrap | `testdata/golden/canonical/**` (about 73 files, some at discovery time), `external-tools/**` (loaded eagerly by `BootstrapTestHost`), `profiles/built-in/**` and its materialized output copy, `docs/contracts/*.schema.json`, `docs/references/ic-flashmap/postbuild/` (one hash-pinned file), `refcode/ab_code_combiner/**` (hash-pinned, Windows-gated) | 4, 11 |
| GoldenRegression | `testdata/golden/canonical/**` (theory cases from the manifest), `testdata/public-synthetic/**`, the materialized profiles; `external-tools/**` through the upward search is possible, not confirmed | 4 |
| UiSmoke | `testdata/golden/canonical/**` (59 files including helpers), `external-tools/**` through `PresentationTestHost`, Presentation source and XAML as text, `VERSION`, `NvtFwCombiner.slnx` | 4, 5, 6 |
| Architecture | all `src/**` and `tests/**` source; eight topic files read `docs/**` or root Markdown; `profiles/built-in/**`; `testdata/**` (RetiredIc); `scripts/package.ps1`, `scripts/smoke-release.ps1`, `scripts/measure-startup.ps1`, `scripts/code_size_policy.py`; `eng/**`; and, through its `dotnet msbuild` child, `Directory.Build.*`, `Directory.Packages.props`, `global.json` | 4, 5, 6, 9, 11 |
| Repository scripts | `.github/workflows/`, `.agents/`, `AGENTS.md` files, `docs/contracts/`, `docs/governance/`, `docs/ci/`, `docs/references/`, `profiles/`, `testdata/golden/`, `src/`, the test projects' `.csproj` files, `tools/crc-worker/` (by module; from `import` lines and path literals) | 4, 6, 8 to 11 |

No .NET test reads `.github/`, `.agents/`, `tools/` or `third-party/`; the
`AGENTS.md` files under `src/` and `tests/` are read only by the line ceiling.
Compile-time links that a project-reference graph alone misses,
all handled by row 6: `tests/NvtFwCombiner.Application.Tests/CompiledCompositionTestFactory.cs`
into Bootstrap.Tests; `tests/Shared/*.cs` into Bootstrap.Tests and
ProfileContract.Tests; six TestSupport files linked (not referenced) into
Architecture, Domain, ProfileContract, Application, Bootstrap and
Infrastructure.

Parallelism facts behind ADR items 2, 7 and 9: `ReadyProbeProcessSerialGroup`
holds nine Infrastructure classes (197 methods) that start processes and change
environment variables; `UiProcessWideObservation` holds five UiSmoke classes
(29 methods); `UiAvaloniaRuntime` holds sixteen UiSmoke classes (502 methods);
`UiExternalGolden` one class (7 methods); nothing else declares a collection.
In Architecture.Tests the only mutable state is the materialization batch's
once-per-process guard and its lazy result, both inside the two package-trust
files, which the pilot keeps in one class.

### Discrepancies found (not filed as bugs)

This workstream's write lock is `docs/handoff/1.1.13/`, so no bug file was
created. The commander decides whether any of these becomes one.

1. ADR 0027's 2026-08-12 amendment accepts sharded .NET CI only if workflow
   to finalizer takes at most 300 s ("one miss stops the experiment rather than
   adding shards"); the median successful run in the measured window took
   577 s. Decision D-5 resolves whether the clause still binds.
2. 147 files under `tests/` (134 of them with tests) declare no class matching
   their name prefix, including all 86 `ShellViewModelTests.*.cs` files. No
   rule is broken today; ADR item 6 adds one.
3. `NvtFwCombiner.Desktop` and `NvtFwCombiner.Launcher` are referenced by no
   test project. The selection runs the full suite for them (row 7); whether
   they are intentionally outside unit coverage is for their owner.
4. The 1.1.12 WS-TEST measurement of `ShellViewModelTests` was wrong (see the
   port table); corrected here.

## Conflicts and coordination

- **CI failure evidence (R3, `CI-FAILURE-EVIDENCE-1113-01`).** It owns
  `.github/workflows/ci.yml` and `scripts/verify.py` until it merges; its
  implementation commit is `935eac2bc` on `feature/1.1.13/ci-evidence`.
  WS-TEST edits neither file before then. T3 and T4 build on its evidence
  model: per-attempt artifacts, failing projects' evidence and failing test
  names. The U0 measurement uses the TRX files it keeps.
- **WS-GOV G1-A.** The selector reads the authority map's `prose` and
  `unclassified` classes and never redefines them. For documents, the map
  decides cost: a document outside every class runs the full suite (F-4), so
  the map should classify all of `docs/**` explicitly, and a document that a
  test reads must not be `prose` (checked by T3).
- **WS-GOV G1-B.** One writer for `development-execution-workflow.md` (the
  narrow-test table becomes a pointer to the selector) and, with WS-AI, for
  `AGENTS.md` files including `tests/AGENTS.md`.
- **WS-GOV G2.** Carries T4. Three additions for its batch: (1) ADR 0027's
  2026-08-12 amendment ("each project is run unfiltered", "a disjoint and
  complete set of eight test projects") conflicts with path-selected pull
  request runs; this ADR's item 8 amends it, and the WS-GOV draft, which does
  not list ADR 0027 today, should cite that amendment. (2) WS-GOV defines
  `prose` as text no test or script reads, but the 2,500-line ceiling in
  Architecture.Tests counts the lines of every `docs/**/*.md`, including
  `docs/handoff/`; the definition becomes true only when G2 moves the ceiling
  and the other document checks into the structure lane, so that move must
  come before prose-only changes skip `architecture`. (3) Coverage on
  selected runs is decided together with D-1.
- **nvt-marker.** Edits `RepositoryBoundaryTests.WorkbenchStructure.cs` and
  adds `RepositoryBoundaryTests.NvtEndFlagCallers.cs`; both stay in the pilot's
  residual class.
- **roadmap-tidy.** The roadmap links two `RepositoryBoundaryTests` files; they
  stay in the residual class, and the link update goes through the roadmap's
  owner.
- **F08 and the navigation focus item.** Edit UiSmoke files; no UI split and
  no U2 change before they merge.
- **Pre-built catalog (ADR 0077) and VersionManagement JSON.** May edit the
  ProfileContract and Application aggregates; those splits wait.

## Checkpoints

### 2026-09-26 Design drafts ready for review
State: local
Commits: the commit carrying this entry (this log, the ADR draft, the pilot
plan)
Evidence: read-only inspection at `cf4e42697`; CI durations from
`gh run view` over 60 runs; static counts by script (outside Git). With TEMP,
TMP and TMPDIR set to `<test-area>/temp`:
`dotnet test tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj --filter FullyQualifiedName~RepositoryTextFilesStayBelowEmergencyCeiling`
-> 1 passed, with these documents in the working tree (the board's rule that
documentation edits run Architecture.Tests; the ceiling is the only
Architecture check that reads new handoff files, and the full project was not
run to keep its MSBuild batch off a busy machine). A local link, anchor and
table check of the three documents passed. No UiSmoke or other product test
ran: other workstreams were running tests, including timing-sensitive
Infrastructure tests, so the U0 measurement is left to its batch.
Open: independent design review (commander); owner decisions D-1 to D-8.
Next: stop.

## Owner decisions in risk order

One question at a time, highest risk first. Each item gives the question in
plain words, the options with their consequences, and a recommendation.

### 1. D-1: How should pull requests check test coverage?

Today every pull request measures coverage over all eight test projects and
fails if it drops below the stored baseline. Once pull requests run only the
test projects their changes can affect, the whole-repository number cannot be
computed on them.

- **A. Coverage only on full runs** (release-branch cut, release pull request,
  push to `main`, weekly, manual). Pull requests get faster; a coverage drop is
  found at the next full run and fixed in a follow-up.
- **B. Coverage of the changed code on every pull request; totals on full
  runs.** For each product module a pull request changes, the tests that can
  run that module's code in-process are exactly the ones the selection runs, so
  its coverage can be checked exactly. More work in G2 (the coverage policy
  learns a per-module mode).
- **C. Keep today's rule.** Every pull request that touches product code runs
  all eight projects; only documentation-only and script-only pull requests
  get faster.

Recommendation: B; A if G2 cannot build B in time. Decided together with
WS-GOV G2.

### 2. D-2: How long should the new test selection run in shadow mode?

Before CI trusts the selection, CI can still run everything and only record
what the selection would have skipped. A failure in a skipped group caused by
the change means the map is missing a row.

- **A. 20 pull requests or two weeks, whichever is longer**, with zero misses;
  a miss adds a row and restarts the count. Each failure outside the selection
  is classified: caused by the change (a miss) or intermittent (a bug file).
- **B. No shadow mode**; rely on the weekly and release full runs.
- **C. A whole release cycle.**

Recommendation: A. Shadow mode only sees misses that coincide with a real
failure, so it adds to, and does not replace, the inventory and the checks.

### 3. D-4: What happens to Architecture tests that check document wording?

Some Architecture tests assert exact sentences or percentages in documents
(for example the AB progress figures in the supported IC matrix, sentences in
ADRs, sections of the roadmap). Any wording change breaks them, and they force
.NET tests to run for document edits.

- **A. Move into the structure lane only what protects a fact** (version
  order, required sections, links, the line ceiling) as structural checks, and
  delete assertions that only freeze wording. You approve the list, test by
  test, in G2.
- **B. Move all of them unchanged** into the structure lane.
- **C. Leave them in Architecture.Tests**, mapped to the documents they read;
  then every document edit keeps running Architecture.Tests, and prose-only pull
  requests cannot skip it.

Recommendation: A.

### 4. D-5: Does ADR 0027's "300 seconds, no extra shards" rule still bind?

The August 2026 decision to split .NET CI over several runners said it would
stop, rather than add shards, if a run took longer than 300 seconds. The
median successful run now takes about 9.6 minutes (577 s).

- **A. Treat it as an expired experiment rule.** Allow a verified split of one
  test project over two runners later, only after measurement and after
  test shards reuse one build (G2).
- **B. Keep it.** No extra shards; UiSmoke can only get faster inside the
  project.

Recommendation: A, with the split itself decided later on measured numbers.

### 5. D-3: Should oversized test classes be blocked from growing?

For product code, WS-GOV proposes: a class of at least 2,000 nonblank lines
may not grow without your approval, and it leaves the list below 1,500. The
same could apply to test classes (17 are on such a list today).

- **A. Same rule for test classes.** New tests go into a new, feature-scoped
  class by default, so testing is never blocked; growing a listed class needs
  your approval in the pull request.
- **B. Advisory report only.** Earlier advisory reports did not stop growth.
- **C. No size rule for tests.**

Recommendation: A.

### 6. D-8: Label every test class with a category now?

The 1.1.12 plan proposed one category label on every test.

- **A. Label only where a tool uses the label** (first: running process tests
  separately with hang dumps, and splitting UiSmoke); everything else is
  described by its project, its collection or the Golden manifest.
- **B. Label every class now.** About 900 files touched, with no tool reading
  most labels.

Recommendation: A.

### 7. D-6: Which class is the pilot?

- **A. `RepositoryBoundaryTests`** (Architecture.Tests): the largest, safest to
  prove the method on, cheap to time, and it separates the document checks G2
  needs ([plan](PLAN-test-pilot-split.md)).
- **B. A UI class** such as `XamlControlStyleContractTests`: closer to the slow
  UI shard, and the most promising UI split (moving files that build no
  control out of the serialized UI collection, U2a) could save more time. But
  F08 and the navigation work edit these classes, that split removes
  serialization and so needs extra evidence, and the UiSmoke measurement
  should come first. Better as the second split, with the proven procedure.
- **C. No pilot** before the UiSmoke measurement.

Recommendation: A.

### 8. D-7: What lands in 1.1.13?

The roadmap owns allocation; this is the proposal.

- **A. In 1.1.13:** the ADR (T1), the pilot (T2) and the UiSmoke measurement
  (U0). The selector (T3) joins 1.1.13 only if WS-GOV G1-A lands in 1.1.13.
  The CI adoption (T4) goes with G2.
- **B. Everything in 1.1.14.**

Recommendation: A.
