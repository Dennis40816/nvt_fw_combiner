# ADR 00XX (draft): Test architecture: selection groups, categories, class size and UI smoke scheduling

- Status: **Proposed** — design draft; not implementation authority.
- Date: 2026-09-26 (revised the same day after the independent design review)
- Owners: repository owner (test, CI and release policy); drafted by Claude
  Code (Opus 5.5) for WS-TEST ([log](WS-TEST.md), [1.1.13 board](../1.1.13.md)).
- Review: independent design review by `codex/gpt-6-astra` at `87ac3d6c1`,
  ACCEPT-WITH-CHANGES (P1 F-1 to F-3, P2 F-4 to F-6). This revision takes in
  every finding ([log](WS-TEST.md#design-review-2026-09-26)).
- Number: placeholder. The commander assigns it at integration; 0074 to 0077
  are taken, and the WS-GOV governance draft also uses a placeholder.
- Risk: R2 (test and CI contract; `docs/adr/` is governed). Every part that
  changes `.github/workflows/` or what a required check accepts (including the
  CI evidence finalizer, whose verdict release admission relies on) is R3 and
  lands through WS-GOV G2, not through this ADR's own batches.
- Amends (on acceptance): [ADR 0027](../../adr/0027-evidence-preserving-performance-remediation.md),
  section "2026-08-12 evidence-sharded .NET CI amendment", only as stated in
  decision item 9.
- Depends on: the WS-GOV governance draft
  (`feature/1.1.13/ws-gov:docs/handoff/1.1.13/ADR-DRAFT-governance-reset.md`:
  authority path map, item 4; CI tiers, item 13) and the CI failure-evidence
  change (`CI-FAILURE-EVIDENCE-1113-01` on `feature/1.1.13/ci-evidence`, R3).
- Ports: the 1.1.12 WS-TEST plan
  (`feature/1.1.12/test-architecture:docs/handoff/1.1.12/WS-TEST.md`) and
  checklist item C-4 of the [1.1.12 board](../1.1.12.md).
- Pilot: [pilot split plan](PLAN-test-pilot-split.md).

## Context

### Inventory

Static counts at `cf4e42697` (no build or test run; "methods" are
`[Fact]`, `[Theory]`, `[AvaloniaFact]` and `[AvaloniaTheory]` declarations,
before theory expansion):

| Test project | Files | Lines | Methods | CI shard | Notes |
| --- | ---: | ---: | ---: | --- | --- |
| Domain | 65 | 13,598 | 391 | core | |
| Application | 144 | 41,398 | 972 | core | |
| ProfileContract | 55 | 15,975 | 375 | core | |
| Infrastructure | 129 | 32,787 | 813 | core | whole project serial (`INFRASTRUCTURE_VSTEST_SETTINGS`: collection parallelism off, one thread) plus a serialized process collection |
| Bootstrap | 190 | 49,634 | 935 | bootstrap | holds most canonical Golden cases |
| GoldenRegression | 5 | 604 | 6 | core | theories; Golden cases |
| Architecture | 80 | 12,247 | 241 | core | reads repository text; no product references |
| UiSmoke | 230 | 54,565 | 1,012 | ui | 1,689 executed cases in one local run |
| **.NET total** | 898 | 220,808 | 4,745 | | |

Repository-script tests: 37 modules in `tests/scripts`, split by file name into
three CI shards (`a-q`, `r`, `s-z`); the CRC worker has its own lane. The
[tests README](../../../tests/README.md) keeps older measurements (3,962
methods on 2026-09-06).

No test declares a category trait. Kinds of tests are implicit: the project a
test lives in, three UiSmoke collections (`UiAvaloniaRuntime`,
`UiProcessWideObservation` with parallelization disabled, `UiExternalGolden`)
and one Infrastructure collection (`ReadyProbeProcessSerialGroup`, disabled
parallelization, nine classes). There is no `xunit.runner.json` and no
assembly-level collection setting, so xUnit's default applies: each class is
one collection, classes run in parallel, and the tests of one class run one
after another.

### Cost in CI

The 60 most recent `ci.yml` runs (2026-09-15 to 2026-09-26, final attempts,
successful jobs; `gh run view`, read-only):

| Job | n | Median s | About p90 s | Largest step (median) |
| --- | ---: | ---: | ---: | --- |
| `dotnet / test (ui)` | 45 | 522 | 654 | evidence step 479 s (restore, Release build, discovery, one coverage run) |
| `dotnet / test (core)` | 46 | 380 | 403 | evidence step 338 s (six projects in order, each built then run) |
| `dotnet / test (bootstrap)` | 51 | 250 | 375 | evidence step 208 s |
| `dotnet / build` | 51 | 263 | 286 | build step 216 s |
| `python / repository policy (a-q)` | 55 | 294 | 363 | |
| `python / repository policy (r)` | 51 | 249 | 270 | |
| `python / repository policy (s-z)` | 56 | 227 | 248 | |
| `policy / polytail` | 50 | 116 | 138 | |
| `python-worker / verify` (after the script shards) | 50 | 69 | 79 | |
| `dotnet / build-test` (finalizer, after every .NET job) | 40 | 41 | 48 | |

The UI shard is the critical path, about 140 s ahead of `core` at the median.
Making UiSmoke alone faster therefore saves at most about 140 s of wall-clock
time before `core` becomes the critical path. The console log does not show
how the 479 s split into restore, build, discovery and execution; that is in
the shard log inside the uploaded artifact. A local UiSmoke run recorded by
the F08 workstream on 2026-09-26 took 5 min 58 s for 1,689 cases (Debug, no
coverage; one run; machine load not recorded).

### Oversized test classes

Test class aggregates (all partial files of one class) with at least 2,000
nonblank lines; a file counts toward the first class it declares:

| Class | Project | Files | Nonblank lines | Methods |
| --- | --- | ---: | ---: | ---: |
| `RepositoryBoundaryTests` | Architecture | 78 | 11,229 | 231 |
| `XamlControlStyleContractTests` | UiSmoke | 44 | 10,031 | 161 |
| `ShellNavigationSystemTests` | UiSmoke | 21 | 6,548 | 149 |
| `TrustedProfileBundleCatalogFactoryTests` | ProfileContract | 16 | 5,937 | 147 |
| `FirmwareInspectionSlotTests` | UiSmoke | 22 | 5,265 | 124 |
| `VersionManagementExperienceTests` | Application | 15 | 5,086 | 119 |
| `FirmwareFamilyResolutionNormalizerTests` | ProfileContract | 11 | 3,496 | 76 |
| `ManagedDistributionLauncherRuntimeTests` | Infrastructure | 6 | 3,019 | 69 |
| `ProfileBundleSchemaValidatorTests` | Infrastructure | 21 | 2,875 | 64 |
| `FirmwareInspectionSnapshotTests` | Bootstrap | 5 | 2,771 | 58 |
| `CompiledCompositionTests` | Domain | 12 | 2,618 | 50 |
| `CompositionRunServiceTests` | Application | 11 | 2,557 | 54 |
| `ManagedFirstInstallationExperienceTests` | Application | 3 | 2,444 | 57 |
| `FileSystemManagedVersionRepositoryTests` | Infrastructure | 9 | 2,412 | 58 |
| `MemoryLayoutProjectorTests` | Application | 9 | 2,258 | 35 |
| `AnonymousPipeManagedApplicationProcessTests` | Infrastructure | 5 | 2,101 | 54 |
| `ManagedSetupRecoveryExecutionTests` | Infrastructure | 5 | 2,050 | 39 |

The 1.1.12 plan named `ShellViewModelTests` the largest class (86 files,
24,746 lines). That count went by file name: commit `acc6ea039`
(2026-08-11, "test: parallelize isolated UI smoke groups") had already split
the class into group classes with their own class fixtures, and the files kept
the old prefix. Today 147 files under `tests/` (134 of them with tests) declare
no class matching their name prefix, including all 86
`ShellViewModelTests.*.cs` files.

A large class costs twice: it is one serial unit for xUnit, and its partial
files share private helpers, so its topics cannot be selected, timed or moved
separately.

### How tests are selected today

- Locally, the narrow-test table of
  [`development-execution-workflow.md`](../../governance/development-execution-workflow.md)
  names one project per layer and leaves dependents to judgment ("add broader
  tests only when the change crosses that boundary").
- In CI, every pull request runs all eight projects unfiltered with coverage,
  all script shards and the structure lane. ADR 0027's 2026-08-12 amendment
  makes this the contract: "each project is run unfiltered", "a disjoint and
  complete set of eight test projects", and the workflow contains "no project
  paths, filters, coverage merge logic, expected test counts". Its acceptance
  target (workflow to finalizer at most 300 seconds, "one miss stops the
  experiment rather than adding shards") is far from today: the 35 successful
  first attempts in the window above took a median of 577 s from run creation
  to completion.
- Tests read repository files at run time (a read-only inventory is in the
  [log](WS-TEST.md#declared-run-time-inputs-initial-table)): profiles, Golden
  and synthetic test data, contracts, external tools, documents and source
  text. Some data reaches tests through build outputs rather than a direct
  read: Infrastructure copies `docs/contracts/canonical-capability-policy-v1.json`
  and two `profiles/built-in` files to its output and embeds four contract
  schemas, VersionManagement.Infrastructure embeds seven, and Bootstrap's
  build imports `eng/profile-bundle-materializer` to copy `profiles/built-in`
  into its output. These flow to every project that references them. The
  default external-tool loader searches upward from the test output for
  `external-tools/`. Infrastructure.Tests also depends on two build-only
  project references (`ReferenceOutputAssembly="false"`: ReadyProbe and
  LauncherBootstrap), whose outputs it runs as child processes.
- `NvtFwCombiner.Desktop` and `NvtFwCombiner.Launcher` are referenced by no
  test project; only source-text checks and release smoke touch them.
- Two documents are within two lines of the 2,500-line ceiling that
  Architecture.Tests enforces over `docs/**/*.md` (a UI handoff document at
  2,499 lines and the roadmap at 2,498 on this branch), so an ordinary edit can
  fail a .NET test project.

### Contracts that constrain selection

- **Golden.** The canonical manifest
  ([`manifest.json`](../../../testdata/golden/canonical/manifest.json)) owns
  which tests execute certified cases: each case's
  `testDisposition.evidenceRefs` names `file#symbol` in Bootstrap.Tests or
  GoldenRegression.Tests. `verify.py --release-golden` runs those two projects
  completely and requires exactly one passed execution per case. Moving or
  renaming such a test changes an R3 contract.
- **Coverage.** The policy
  ([`coverage-baseline-v1.json`](../../../docs/contracts/coverage-baseline-v1.json))
  requires no regression of overall line and branch coverage, the presence of
  every baseline module, and ratchets for changed modules, all computed from
  the union of the eight projects' reports. A run of some projects cannot
  produce that verdict.
- **Evidence.** The finalizer requires every shard's manifest, the exact
  eight-project inventory, and discovery reconciled with execution
  ([pull request CI](../../../docs/ci/pull-request-ci.md)). ADR 0027 also asks
  for "unique test ownership": no verifier lane runs the same test owner twice,
  and artifacts carry logs, TRX and coverage only, never firmware payloads.
  The CI evidence change names every artifact by run attempt and has the
  finalizer verify each producer's newest attempt; until a real re-run has
  verified that, a failed run is followed by a new workflow run rather than
  "Re-run failed jobs".

### Stability

The core shard fails intermittently, including on a documentation-only pull
request ([bug](../bugs/BUG-20260925-core-shard-intermittent-failure.md)). The
board's diagnosis names H1 (a helper's start deadline, fixed test-only on the
CI evidence branch), H2 (an exception from `RandomAccess.GetLength` in
production code), H3 (a launcher start that returns `false` with no cause),
H4 (an empty discovery inventory) and H5 (a re-run that picked a stale
artifact). H4, H5 and the missing failing-test names belong to the CI evidence
change; H2 and H3 are product issues waiting for that evidence.

### Related work

- WS-GOV (governance ADR draft): the authority path map classifies every path
  as `firmware`, `release`, `governance`, `code`, `prose` or `unclassified`,
  with `prose` defined today as text "that no test or script reads". Item 13
  says only `prose` paths skip product tests, an unclassifiable change runs
  everything, the structure lane always runs, document checks that live in
  Architecture.Tests move to the structure lane or stay mapped to the documents
  they read, and every required check comes from an aggregator that fails on a
  missing, failed, cancelled or unexpectedly skipped producer. G2 (R3)
  implements the CI tiers after the CI evidence change.
- CI failure evidence (R3): owns `ci.yml` and `scripts/verify.py` until it
  merges; it uploads failing projects' evidence, prints failing test names and
  introduces the per-attempt artifact model above.

## Decision drivers

1. Never select fewer tests than a change can affect. Over-selection costs
   minutes; under-selection is a defect. (The same rule as WS-GOV's map.)
2. What the selector cannot prove, it runs: an input it cannot resolve, a
   missing base object or an incomplete coverage contributor set means the
   full suite or full coverage.
3. One owner per fact: project files own compile dependencies, the canonical
   manifest owns Golden membership, WS-GOV's map owns authority classes,
   `scripts/verify.py` owns the test inventory. Nothing is copied by hand.
4. A category decides how a test runs (isolation, scheduling, diagnostics),
   never whether it may be skipped at a full-suite boundary.
5. Test moves are mechanical and preserve behavior; removing serialization and
   changing what a test asserts are separate, reviewed changes.
6. Stability comes from isolation and deterministic budgets, not retries.
7. Each mechanism lands with its first consumer.

## Considered options

Selection model:

1. Keep the per-layer table and judgment. Free; relies on memory; unusable by
   CI.
2. A category trait on every test, and pull requests run only some categories
   (for example unit tests). Large labeling effort; skips integration, Golden
   and UI evidence for firmware-relevant changes. Rejected.
3. Per-test impact analysis from coverage data. Most precise, but needs
   per-test coverage collection and a trusted store; stale data under-selects.
   Rejected for now.
4. **Whole test groups selected from evaluated project dependencies, a short
   declared table of run-time inputs and WS-GOV's authority classes, with
   conservative fallbacks; categories only schedule.** Selected.

Declaring categories:

1. A trait on every class now. Drift and effort without a consumer.
2. One project per category. Strongest isolation, but changes the closed
   eight-project inventory (R3) and moves many files.
3. **Reuse what exists (project placement, collections, the Golden manifest)
   and add a class trait only where a runner filters by it.** Selected.

Size policy: extend WS-GOV's hotspot growth block to test classes (selected,
owner decision D-3); an advisory report only (the 1.1.12 WS-GOV evidence shows
advisory reports did not stop growth); or a split deadline (forces splits into
classes that active work is editing).

## Decision

### 1. Test groups are the selection unit

The groups are exactly `scripts/verify.py`'s inventory: the eight .NET test
projects (`CI_DOTNET_SHARDS`), the repository-script tests
(`REPOSITORY_SCRIPT_TEST_SHARDS`, selected together) and the Python worker lane
until WS-FLOW F11 retires it. A selected group runs completely. No path rule
filters tests inside a group; scheduling partitions follow item 9. Selecting
single repository-script modules (the runner already passes explicit module
files) is a later refinement, once each module's inputs are declared.

### 2. Execution categories and Golden membership

Each test class has exactly one execution category:

| Category | Meaning | Declared by | How it runs |
| --- | --- | --- | --- |
| `unit` | In-memory behavior; clock, paths, process runner and randomness injected | project default: Domain, Application, ProfileContract | parallel |
| `integration` | Real adapters and files in a per-test temporary workspace, assembled composition graph | project default: Infrastructure, Bootstrap, GoldenRegression | parallel only with per-test isolation |
| `process` | Starts a child process, uses pipes, named OS objects or OS scheduling, or changes process-wide state such as environment variables | membership in a serialized collection (today `ReadyProbeProcessSerialGroup`, whose classes also change environment variables) | serialized; the rules of item 8 |
| `ui` | Avalonia headless session: controls, layout, bindings, rendering | project default: UiSmoke; control construction in `UiAvaloniaRuntime` | existing UI runtime rules (checked by Architecture.Tests) |
| `observation` | Process-wide state or timing observations | a collection with parallelization disabled (`UiProcessWideObservation`) | alone |
| `structure` | Reads repository source and project files; no product assemblies | project default: Architecture | parallel |
| `document` | Asserts the content of repository documents | one Architecture class after the pilot | its generic checks move to the structure lane (item 3) |

**Golden is a membership, not an execution category.** The canonical manifest
names the tests that execute certified cases. Those tests keep their class's
execution category (today `integration`, in Bootstrap.Tests and
GoldenRegression.Tests), and a Golden test may share its class with non-Golden
tests. Golden membership decides only what must run at full runs and releases
(`--release-golden` and item 3's full-suite rows); it never changes scheduling.

Rules:

- A class has one execution category. A class whose tests need different
  categories is split, not labeled per method. Existing mixed classes
  (Architecture topic files that also read documents; the mixed UiSmoke classes
  of item 10) are resolved through D-4 and option U2a, not by relabeling.
- A `Category` trait is added at class level only when a runner must filter by
  it (first candidates: a separate process-test invocation with hang
  detection, and a UiSmoke partition). A structure check then rejects unknown
  values, method-level category traits and any `golden` trait.
- Repository-script tests and the worker keep their own groups; pytest markers
  may reuse these names when a consumer needs them.

### 3. Path selection

Each changed path of the two-point diff between the pull request base and head
(deletions count; a rename counts on both sides) collects the groups of every
row it matches. A match with a full-suite row selects the full suite; row 12
catches every path that no other row matches. The result is the union over all
paths, with the reason for each group.

| # | Changed path | Selected groups | Why |
| --- | --- | --- | --- |
| 1 | WS-GOV class `prose` (the joint definition below) | none; the structure lane runs | in force only after the generic document checks, including the line ceiling, run in the structure lane (until then `architecture`) |
| 2 | WS-GOV class `unclassified` | full suite | F-4 of the WS-GOV review |
| 3 | The runner and selector: `scripts/verify.py`, the selection map and selector, `.github/workflows/ci.yml`, `scripts/coverage_policy.py`, `scripts/coverage_configuration_policy.py`, `docs/contracts/coverage-baseline-v1.json` | full suite | a runner cannot vouch for its own selection |
| 4 | `profiles/**`, `testdata/**`, `docs/contracts/**`, `external-tools/**`, `refcode/**`, `.gitattributes` | full suite | read at build and run time by most groups, directly, through build outputs or through the upward tool search, often hash-pinned; firmware, contract and evidence inputs deserve complete evidence |
| 5 | Solution-wide build inputs: `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `global.json`, `NuGet.config`, `NvtFwCombiner.slnx`, `VERSION`, `.editorconfig` (code style is enforced in the build), `eng/**` | every .NET group and repository-scripts | every project build reads them |
| 6 | A file in a .NET project directory (`src/<P>/**`, `tests/<P>/**`, including its `packages.lock.json`), or a file a project includes from outside its directory | the groups the resolution rules below derive; plus `architecture`, which reads all source text; a project-file change also repository-scripts | evaluated `.csproj` graph; the script tests read project files |
| 7 | A `src/` project no test project references (today Desktop and Launcher) | full suite, reported as a coverage gap every time | no direct test owner; the fallback does not create tests |
| 8 | Any `src/**` path (together with row 6) | repository-scripts | size-policy, release-package and parity script tests read production sources |
| 9 | `scripts/**` (other than row 3), `tests/scripts/**` | repository-scripts and architecture | script tests; Architecture.Tests reads four scripts |
| 10 | `.github/**` (other than row 3), `.agents/**`, `.codex/**`, `.gitignore`, `tools/**` | repository-scripts; `tools/crc-worker/**` also the worker lane | read by the script tests only |
| 11 | Documents outside rows 1 to 10: non-prose files under `docs/**`, Markdown files elsewhere (for example the root `README.md`, `SPEC.md`, `CHANGELOG.md` and every `AGENTS.md`) and the root `LICENSE` | architecture (document checks, line ceiling) and repository-scripts; `docs/references/**` also bootstrap | run-time readers |
| 12 | Anything else | full suite | F-4 of the WS-GOV review |

**Resolution rules for row 6.** The selector evaluates the project files at the
base and at the head with MSBuild semantics (imports, conditions, properties,
wildcards) for the configuration CI builds, and takes the union of both graphs.

- Every `ProjectReference` counts, including build-only ones
  (`ReferenceOutputAssembly="false"`), because they supply run-time outputs:
  ReadyProbe and LauncherBootstrap for Infrastructure.Tests.
- A file that project Q includes from outside its directory (a linked
  `Compile`, `EmbeddedResource`, `None`, `Content` or `AvaloniaResource` item)
  counts as a file of Q: it selects every group whose test project references Q
  transitively, not only Q's own tests. Content that flows into referencing
  projects' outputs follows the same references.
- A deleted or renamed project is resolved in the graph where it exists, so
  its former consumers are selected; deleting a whole project therefore selects
  every group that referenced it at the base.
- A build target that copies repository files at build time is not an item and
  is invisible to evaluation. Each such target needs a declared row; today that
  is `eng/profile-bundle-materializer` with `profiles/built-in` (rows 4 and 5).
- **Unproven means full.** The selector selects the full suite, and reports
  why, when it cannot prove its answer: a project file, import or item that
  cannot be evaluated or resolved to concrete paths (a parse failure, an
  unsupported construct, a condition on an unknown property, a new custom
  target that reads outside its project without a declared row), or a missing
  base or head object (for example a shallow checkout or an unknown base).

**Prose, joint definition with WS-GOV.** A `prose` file is not an input to any
product test or other semantic verification; only listed generic
document-structure checks may read it (Markdown links and anchors, the line
ceiling, encoding and file-name rules). Documents that topic tests still read
(for example the ADRs, UI and architecture documents asserted by
Architecture.Tests) are not prose and keep their row 11 mapping. WS-GOV's
current wording ("no test or script reads") would stay false even after the
move, because the structure lane reads every document; G2 changes the wording,
its checker and the list of document checks to migrate together (commander
to-do in the log).

Independent of this table: the structure lane and the authority check run on
every pull request, and the full suite with Golden runs when a release branch
is cut, on the release pull request, on every push to `main`, weekly on the
trunk when it changed and on manual dispatch (WS-GOV item 13).

The selection map is one data file next to WS-GOV's authority map (proposal:
`docs/governance/test-selection-map.json`, governance class). It holds rows 3,
4, 5 and 7 to 11 and each group's default category. Row 6 is never stored:
the selector derives it from the project files. The selector lives with the
lane inventory in `scripts/verify.py` (or a module it imports) and has two
consumers: a local report (`which groups does my diff need, and why`) that
replaces the narrow-test table, and WS-GOV G2's pull-request tier. Coverage can
add groups to the selection (item 4).

### 4. Coverage on pull requests (D-1)

Two questions are answered separately.

1. **Which product modules must be checked:** every product module that any
   selected test group executes in-process. This covers a change to product
   code in any layer (a change in a higher layer can remove the only path that
   exercised lower-layer code), and a change to tests, fixtures, test data,
   linked support files or scheduling, which selects its group through item 3.
2. **Which reports each checked module needs:** its complete contributor set,
   every test project that loads the module in-process, that is, every project
   whose transitive references supply the module's assembly (a build-only
   reference runs the module out of process, where Coverlet does not collect).

A pull request's coverage verdict uses fresh reports from the complete
contributor set of every checked module, so coverage can add groups beyond
item 3's selection. If completeness cannot be proven, the pull request runs
full coverage. In this repository almost every selected .NET group executes
Domain, whose contributors are seven of the eight projects, so a pull request
that reaches product code or .NET tests in practice keeps today's full
coverage; the saving is for pull requests that reach no product module
(documents, scripts, tools, Architecture-only changes).

This is D-1 option B. Until it is built and verified, every pull request that
reaches a product module keeps today's full coverage; there is no automatic
fallback. Coverage only at full runs (D-1 option A) lowers today's protection
and is valid only with the owner's separate, explicit acceptance. A later
refinement may reuse the exact base's full-run reports for contributors whose
inputs are provably unchanged.

### 5. Declared run-time inputs

The initial rows come from a read-only inventory of what each test project
reads (recorded in the [log](WS-TEST.md)). The map may over-select.

- A change that adds or changes a run-time read of a repository file updates
  the declared inputs in the same pull request, and the reviewer checks it.
- T3 adds a best-effort structure check: it lists the repository-relative path
  literals that test sources pass to `RepositoryPaths` or the
  repository-root helpers and fails when one falls outside rows 3 to 11.
- A read that cannot be bounded (a computed path, an upward search, a directory
  enumeration) is declared with the widest prefix it can reach, or maps to the
  full suite.
- A document read by any test or semantic check is never `prose`. A check
  enforces this for the declared list; the first rule covers new reads, and
  shadow mode (item 6) is the backstop.

### 6. Consistency checks, shadow mode and activation

The selector's tests cover each row, and these negative cases: a deletion; a
rename across groups; deleting a whole project; a changed project reference,
including a build-only one (base and head graphs); an external include change
that must reach the including project's consumers; a project construct the
selector cannot evaluate; a missing base object; a new run-time read without a
declaration (the structure check fails); an unreferenced project; an unmatched
path; a prose-only change; an unclassified path; a change to the selector
itself. Structure checks require that every group in the map exists in
`verify.py`'s inventory and the reverse, that every `src/` project is
referenced by a test project or listed in row 7, and that no declared input is
`prose`.

**Shadow mode.** Before any pull request tier relies on the selection, CI
computes it on every pull request but still runs the full suite; required
checks do not change.

- Each observation is bound to the selector version, the map version (content
  hash), the base and head SHAs, the run id and the attempt, and records the
  selected groups with their reasons and every group's outcome in every
  attempt. A later passing attempt never erases an earlier failure.
- Each failure outside the selection gets one status: `miss` (caused by the
  change: the map is missing a row), `intermittent` (not caused by the change:
  a bug file), or `unresolved` (not attributable: no TRX, a cancelled or
  incomplete run, an unclear cause). An open `unresolved` blocks activation.
- The window counts only distinct pull requests whose full run has complete
  evidence (every producer present, none cancelled); all attempts are kept.
- A selector or map change that narrows any selection restarts the window; a
  change that only widens keeps the recorded observations, re-evaluated with
  the new version. An observation the new version cannot re-evaluate (for
  example because a base object is gone) no longer counts.

**Exit and activation.** Selection is enabled only when all of these hold,
and then only by the owner's explicit decision:

- the window (D-2; proposal: 20 distinct pull requests or two weeks, whichever
  is longer) has no `miss` and no open `unresolved`;
- a case matrix passes, each case with its expected selection checked on a
  synthetic diff and, where one exists, on a real pull request: cross-layer
  changes (Domain; a Presentation-only change); linked inputs (TestSupport
  files, the Application.Tests file linked into Bootstrap.Tests, an embedded
  contract schema); run-time inputs (profiles, test data, external tools, a
  document read by Architecture.Tests); deletions and renames (a file, a whole
  project, a rename across groups); prose-only and unclassified changes; a
  selector change; an unresolvable project construct.

After activation, a miss found in any full run returns pull requests to the
full suite until the corrected map passes the case matrix again and the owner
re-enables selection. Shadow mode sees a miss only when it coincides with a
real failure, so it adds to the inventory and the checks and does not replace
them.

### 7. Class size, naming and mechanical splits

- Size is measured per class aggregate: nonblank lines over all partial files
  and test methods.
- Test aggregates of at least 2,000 nonblank lines join the hotspot list of
  WS-GOV item 17 and leave it below 1,500 (owner decision D-3). A listed
  aggregate may not grow; new tests go into a feature-scoped class that keeps
  the isolation and collection contract of the tests it joins.
- A test file's name is `<Class>.cs` or `<Class>.<Topic>.cs` for the class it
  declares. Existing mismatches are fixed when their class is split or next
  touched, in a rename-only commit.

Split rules:

- **S1.** A split moves whole files; a test method is never edited, renamed or
  moved between files in the same change.
- **S2.** A moved file changes only its class declaration line, `using`
  directives, one summary comment per new class and the collection attribute
  that S4 requires.
- **S3.** Private helpers shared across the new classes move verbatim into one
  static support class (only accessibility changes), imported with
  `global using static`, so test bodies stay byte-identical.
- **S4.** A split is two separately reviewable changes. The mechanical move
  keeps execution semantics: the new classes stay in the original class's
  named collection, or, where the class had its own default collection, join
  one new shared collection, so their tests keep running one after another as
  before. Removing that
  serialization is a second change, allowed only when the static review of S9
  shows no shared mutable state, no process-wide state, no indirect use of the
  UI dispatcher, global resources or background work through helpers or
  fixtures, and cleanup of all background work, backed by repeated parallel
  runs. Green parallel runs alone never prove safety; unclear evidence keeps
  the tests in their original collection. A split may add serialization
  without such evidence.
- **S5.** Removing serialization multiplies class fixtures, so their cost is
  measured; an expensive fixture is shared (an xUnit assembly or collection
  fixture) only if it is read-only and thread-safe, and otherwise the cost is
  accepted or the files stay together (the UiSmoke group fixtures are
  deliberately separate per group since `acc6ea039`).
- **S6.** Tests referenced by a contract (Golden `evidenceRefs`) or by a
  document link are not moved unless the same change updates the reference
  under that reference's authority.
- **S7.** Only classes outside every active write lock; the commander
  schedules each split against the actual branch point, and a split pull
  request contains nothing else.
- **S8.** Every split records the equivalence evidence of the
  [pilot plan](PLAN-test-pilot-split.md): discovery mapping, outcomes,
  mechanical diff, canary, timing and the static review of S9.
- **S9.** Every split records a before and after table of class-level
  semantics: attributes (collection, traits, skips), base types, constructors,
  `Dispose` and `IAsyncLifetime`, fixtures and their scope, static and shared
  state with its owner and initialization count, background work and its
  cleanup, collection membership and any order dependence. Any difference
  other than the declared class names is explained.
- **S10.** A size limit is never a reason to remove serialization.

### 8. Stability rules

- Budgets are deterministic: a child's start budget is separate from its
  readiness budget (the H1 pattern), deadlines come from a monotonic clock and
  from observed events, and a fixed sleep is never synchronization.
- A launch or probe that can fail returns a typed result with its cause, not a
  bare `bool` (H3).
- A child process is always reaped; a failure message carries the child's exit
  code and the tail of its output.
- A parallel test owns a temporary workspace under the session scratch root,
  never writes shared repository paths and never mutates process-wide state
  outside a serialized collection.
- **Re-runs, two separate questions.** A passing re-run or a new run never
  proves that an intermittent failure is fixed: the failure stays a bug file
  until its cause is fixed or the change is reverted
  ([tests instructions](../../../tests/AGENTS.md)). Whether a re-run's
  artifacts are valid verification evidence is decided by the CI evidence
  contract, not here: its finalizer verifies each producer's newest attempt
  with attempt-named artifacts, and until a real re-run has verified that, a
  failed run is followed by a new workflow run, not by "Re-run failed jobs".
  This ADR keeps that pending gate. Shadow mode keeps every attempt.
- No automatic retry in CI or in the verifier.
- Timing assertions prefer counts and work units over wall-clock limits (ADR
  0027).
- **Hang detection (U1) is an R3 sub-item of the CI evidence contract.** VSTest
  blame with dump type `none` names the hanging test and stops the test host
  without a memory dump. Its extra attachment changes the producer allowlist,
  the manifest and the finalizer, so it is delivered with them and with
  negative tests (a missing, extra or oversized attachment). Memory dumps are
  not uploaded: ADR 0027 allows logs, TRX and coverage only and forbids
  firmware payloads in artifacts, and a test-host dump can hold fixture bytes.

### 9. Partitions and the ADR 0027 amendment

ADR 0027's "each project is run unfiltered" becomes: in a full run, each test
of each project runs exactly once, either in one unfiltered run or in a
verified exact partition. A partition is allowed only when it is declared in
`scripts/verify.py` (never in workflow YAML), each part records its filtered
discovery against the same assembly hash, the parts are disjoint and their
union equals the unfiltered discovery, and the finalizer checks all of it. At
the pull request tier a group that the selection excluded counts as "not
applicable" only when the finalizer recomputes the same selection for the exact
base and head (G2).

The 300-second acceptance clause and its "rather than adding shards" stop rule
remain in force until this ADR is accepted. Past runs over 300 seconds did not
void them. If the owner takes D-5 A, this ADR replaces both clauses from the
day it is accepted, and the duration targets that take their place are set
from the U0 measurement and recorded here; a shard is added only on U0 and U4
evidence.

### 10. UiSmoke

UiSmoke has 1,012 methods (673 `[Fact]`, 134 `[Theory]`, 90 `[AvaloniaFact]`,
115 `[AvaloniaTheory]`) in 85 classes and one headless Avalonia session with
Skia rendering (`UseHeadlessDrawing = false`). Since `acc6ea039` the former
shell class is a set of group classes, each with its own Bootstrap class
fixture (15 classes use one). Architecture.Tests pins the collection layout.

The largest serial unit is the `UiAvaloniaRuntime` collection. Its definition
says it "serializes Avalonia control construction while leaving pure
ViewModel tests parallel", but membership is per class: its 16 classes hold
502 methods, about half of UiSmoke, and run one after another. Four of them are
mixed (`XamlControlStyleContractTests`, `ShellNavigationSystemTests`,
`FirmwareInspectionSlotTests`, `VersionManagementSettingsTests`); by a
per-file estimate, about 300 of their methods sit in files that build no
control, yet they are serialized with the control tests. A file that builds no
control can still reach the UI dispatcher, global resources or background work
through helpers and fixtures, so the estimate is a candidate list, not
evidence. Separately, about ten classes use fixed sleeps of 30 to 500 ms as
synchronization (for example `MemoryCoveragePopupTests` with ten), which costs
little time but is a stability risk; the 10 to 30 s `WaitAsync` limits
elsewhere are upper bounds, not waits.

| ID | Option | Expected effect | Cost and risk | Needs |
| --- | --- | --- | --- | --- |
| U0 | Measure: per-class and per-test time from the TRX of a green CI UI shard (kept three days), the phase split from its shard log, and one local run on a quiet machine | Shows where the 479 s go; prerequisite for U2 to U5 | about an hour; none | an artifact download |
| U1 | Diagnostics: hang detection without memory dumps (item 8), failing test names in the log (CI evidence change), per-test timeouts on UI waits | Faster diagnosis; no speed change | R3 sub-item of the CI evidence contract for the hang attachment | CI evidence change merged; `verify.py` owner |
| U2 | In-project fixes guided by U0. U2a: split the four mixed classes in two changes (S4): first a mechanical move that keeps every file in `UiAvaloniaRuntime`, then a separate change that takes out only the files whose S9 review shows no direct or indirect UI, global-resource, background or process-wide use. U2b: replace fixed sleeps with waits on the observed event. U2c: where U2a multiplies Bootstrap group fixtures, measure the cost and apply S5 | U2a is the largest expected in-project gain (size known only after U0); U2b is for stability | R1 test-only; each new class adds a Bootstrap fixture; unclear files stay serialized; conflicts with F08 and the navigation work in the same classes | U0; write locks clear |
| U3 | Less coverage work on pull requests | Under D-1 A, no instrumentation on pull requests; under D-1 B, unchanged for pull requests that reach product code | A lowers protection (item 4); a coverage policy and finalizer change | G2 (R3); owner decision D-1 |
| U4 | Build once; test shards reuse the Release build | Removes the per-shard restore and build from every shard | artifact transfer; build-output custody (hash checks exist) | G2 (R3) |
| U5 | Partition UiSmoke over two runners (item 9) | Roughly halves the UI test-execution part | one more runner; partition evidence; amends ADR 0027 | U0, U4, G2; owner decision D-5 |
| U6 | Move tests that build no Avalonia control into a new, fully parallel project | Largest structural gain for UI | hundreds of moved tests; a ninth project changes the closed inventory (R3) | only if U2 to U5 are not enough |
| U7 | Skip UiSmoke by path | Helps prose, script and non-UI leaf changes only; UiSmoke references almost every `src/` project | none beyond item 3 | selector |

Decision: U0 and U1 first; U2a as the first UI split after U0 and after the
F08 and navigation work merges, then U2b and U2c per measured class; U3 and U4
decided with G2; U5 only if, after U4, the UI shard is still the critical path
by a margin the owner sets; U6 not planned. The Architecture pilot proves the
split procedure; it is no evidence that UI tests are safe to run in parallel.
Because `core` is only about 140 s behind, the next lever after UI is the
serial Infrastructure run inside `core` (batch T5).

## Consequences

### Positive

- A documentation-only or script-only pull request stops running .NET shards
  it cannot affect; the intermittent core failure seen on a documentation-only
  pull request cannot block such a change.
- Agents and CI choose tests from one derived, reviewable source instead of a
  per-layer table and memory.
- Splits become routine, reviewable and verifiable; smaller classes can be
  timed and selected by topic, and run in parallel where evidence allows.
- Timing-sensitive tests get one set of rules that addresses the diagnosed
  failure causes.

### Negative / trade-offs

- A path-selected pull request can miss a cross-group interaction until the
  next full run; shadow mode, its case matrix and weekly full runs bound that
  risk.
- With D-1 B, pull requests that reach product code keep full coverage, so
  their .NET run does not get shorter; only D-1 A would shorten it, at the cost
  of finding some coverage drops after merge. Golden cases run on a pull
  request only when a change can reach them; every full run and release still
  runs all of them.
- The map, the selector and the shadow-mode records are new governance-class
  code and data to maintain.
- Renaming classes changes test names in reports; historical references in
  frozen records keep old names.

### Risks and mitigations

- An incomplete declared-input table or an unresolvable project construct
  under-selects -> "unproven means full", conservative rows, the declaration
  rule and structure check, shadow mode, full runs at every release boundary.
- A split changes behavior through parallel execution -> S4 (mechanical move
  first, de-serialization separately), S9, canary checks and timing headroom
  for process tests.
- Splits collide with active work -> S7 and commander scheduling.
- The selector wrongly marks a producer not applicable -> the finalizer
  recomputes the selection itself (item 9).
- A later passing attempt hides a failure -> shadow mode keeps every attempt;
  the CI evidence contract owns re-run evidence.

## Compatibility and migration

| Batch | Content | Risk | Evidence and review | Depends on |
| --- | --- | --- | --- | --- |
| T0 | This draft, the pilot plan, the log | R0 (a non-binding draft) | independent design review of `87ac3d6c1` done (ACCEPT-WITH-CHANGES); this revision takes in its findings | - |
| T1 | The ADR into `docs/adr/` with its number; reciprocal "Amended by" line in ADR 0027; one pointer each in `tests/README.md` and `tests/AGENTS.md` | R2 | before WS-GOV G1-B: a capability-reuse record, exact-head review and owner acceptance; after G1-B: the pull request fields, review and owner approval | T0 review, owner decisions |
| T2a | Pilot mechanical move of `RepositoryBoundaryTests`, serialization unchanged | R1, tests only | pilot evidence E1 to E7 and independent review; no capability-reuse record (the validator rejects a record whose only path is a test file, as for the H1 fix) | owner decision D-6; mapping recomputed at the branch point; write locks clear |
| T2b | Pilot de-serialization: remove the shared collection, process batch in its own serialized collection | R1, tests only | S9 review, repeated parallel runs, timing; separate review | T2a merged |
| T3 | Selection map, selector with its resolution rules, declared-input and structure checks, local report; the runbook's narrow-test table becomes a pointer | R2; any part that changes what a required check accepts is R3 | record or pull request fields as in T1, review | CI evidence merged (`verify.py` has one writer); G1-A (authority map); the runbook edit goes through G1-B's writer |
| T4a | Shadow mode in CI: selection computed and recorded on every pull request; full suite and required checks unchanged | R3 (workflow path) | inside WS-GOV G2: release-owner approval, negative tests | T3, G2 |
| T4b | Activation: "not applicable" producers, coverage per D-1, document checks in the structure lane, ADR 0027 amendment in force | R3 | the exit criteria of item 6, the owner's explicit enablement | T4a evidence |
| T5 | U0 and U2; U1 as an R3 sub-item of the CI evidence contract; Infrastructure: only process classes serialized, after measurement; further mechanical splits of listed classes; U5 if decided | R1 to R3 by item | per item | measurements; write locks |

Order: the CI evidence change merges first. T0's review and the owner
decisions run in parallel with it, and T2a and T2b may land independently of
both (test files only). T1 lands before or with T3. G1-A precedes T3; T3
precedes G2, which carries T4a and then T4b. T5 items follow their
measurements. Nothing in WS-TEST edits `ci.yml` or `verify.py` before the CI
evidence change merges, and no WS-TEST branch merges the trunk (rebase only,
board working rules).

## Verification

- Pilot: the equivalence procedure E1 to E7 of the
  [pilot plan](PLAN-test-pilot-split.md), separately for T2a and T2b.
- Selector: the negative cases of item 6, on synthetic diffs and on the diffs
  of recent pull requests.
- Shadow mode: the recorded window and the case matrix of item 6, with no
  `miss` and no open `unresolved`, before the owner enables selection.
- Coverage: a pull request that changes only a test file, one that changes only
  a fixture, and one that changes a higher-layer product module each run the
  complete contributor set of every checked module (item 4).
- CI: a documentation-only pull request completes without .NET producers and
  the aggregator passes; a pull request that touches `profiles/` runs the full
  suite; a producer that is selected but missing fails the aggregator.
- UiSmoke: U0's table before and after each U2 change, on the same runner type.

## Open owner decisions

Listed in plain words, highest risk first, in the
[log](WS-TEST.md#owner-decisions-in-risk-order); all pending. D-1 whether some
coverage drops may surface only after merge (recommended: no, the revised B;
full coverage stays until B exists); D-2 when to trust the selector
(recommended: the shadow window with item 6's exit conditions); D-4 which
document assertions protect the specification; D-5 whether to replace ADR
0027's 300-second rule explicitly; D-7 what 1.1.13 commits to; D-6 the pilot
class; D-3 a growth block for large test classes; D-8 category labels only
where a tool reads them.
