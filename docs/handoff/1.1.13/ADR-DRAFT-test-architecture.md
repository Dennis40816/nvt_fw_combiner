# ADR 00XX (draft): Test architecture: selection groups, categories, class size and UI smoke scheduling

- Status: **Proposed** — design draft for review; not implementation authority.
- Date: 2026-09-26
- Owners: repository owner (test, CI and release policy); drafted by Claude
  Code (Opus 5.5) for WS-TEST ([log](WS-TEST.md), [1.1.13 board](../1.1.13.md)).
- Number: placeholder. The commander assigns it at integration; 0074 to 0077
  are taken, and the WS-GOV governance draft also uses a placeholder.
- Risk: R2 (test and CI contract; `docs/adr/` is governed). Every part that
  changes `.github/workflows/` or what a required check accepts (including the
  CI evidence finalizer, whose verdict release admission relies on) is R3 and
  lands through WS-GOV G2, not through this ADR's own batches.
- Amends (on acceptance): [ADR 0027](../../adr/0027-evidence-preserving-performance-remediation.md),
  section "2026-08-12 evidence-sharded .NET CI amendment", only as stated in
  decision item 8.
- Depends on: the WS-GOV governance draft
  (`feature/1.1.13/ws-gov:docs/handoff/1.1.13/ADR-DRAFT-governance-reset.md`:
  authority path map, item 4; CI tiers, item 13 after review finding F-4) and
  the CI failure-evidence change (`CI-FAILURE-EVIDENCE-1113-01` on
  `feature/1.1.13/ci-evidence`, R3).
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
  `external-tools/`.
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
  is per production module and is computed from the union of all eight
  projects' reports, so a run of some projects cannot produce the coverage
  verdict.
- **Evidence.** The finalizer requires every shard's manifest, the exact
  eight-project inventory, and discovery reconciled with execution
  ([pull request CI](../../../docs/ci/pull-request-ci.md)). ADR 0027 also asks
  for "unique test ownership": no verifier lane runs the same test owner twice.

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
  as `firmware`, `release`, `governance`, `code`, `prose` or `unclassified`.
  After review finding F-4, item 13 says only `prose` paths skip product tests,
  an unclassifiable change runs everything, the structure lane always runs,
  document checks that live in Architecture.Tests move to the structure lane or
  stay mapped to the documents they read, and every required check comes from
  an aggregator that fails on a missing, failed, cancelled or unexpectedly
  skipped producer. G2 (R3) implements the CI tiers after the CI evidence
  change.
- CI failure evidence (R3): owns `ci.yml` and `scripts/verify.py` until it
  merges; it uploads failing projects' evidence and prints failing test names.

## Decision drivers

1. Never select fewer tests than a change can affect. Over-selection costs
   minutes; under-selection is a defect. (The same rule as WS-GOV's map.)
2. One owner per fact: project files own compile dependencies, the canonical
   manifest owns Golden membership, WS-GOV's map owns authority classes,
   `scripts/verify.py` owns the test inventory. Nothing is copied by hand.
3. A category decides how a test runs (isolation, scheduling, diagnostics),
   never whether it may be skipped at a full-suite boundary.
4. Test moves are mechanical and preserve behavior; a change to what a test
   asserts is a separate, reviewed change.
5. Stability comes from isolation and deterministic budgets, not retries.
6. Each mechanism lands with its first consumer.

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
4. **Whole test groups selected from derived project dependencies, a short
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
filters tests inside a group; scheduling partitions follow item 8. Selecting
single repository-script modules (the runner already passes explicit module
files) is a later refinement, once each module's inputs are declared.

### 2. Categories

| Category | Meaning | Declared by | How it runs |
| --- | --- | --- | --- |
| `unit` | In-memory behavior; clock, paths, process runner and randomness injected | project default: Domain, Application, ProfileContract | parallel |
| `integration` | Real adapters and files in a per-test temporary workspace, assembled composition graph | project default: Infrastructure, Bootstrap, GoldenRegression | parallel only with per-test isolation |
| `process` | Starts a child process, uses pipes, named OS objects or OS scheduling, or changes process-wide state such as environment variables | membership in a serialized collection (today `ReadyProbeProcessSerialGroup`, whose classes also change environment variables) | serialized; the rules of item 7 |
| `ui` | Avalonia headless session: controls, layout, bindings, rendering | project default: UiSmoke; control construction in `UiAvaloniaRuntime` | existing UI runtime rules (checked by Architecture.Tests) |
| `observation` | Process-wide state or timing observations | a collection with parallelization disabled (`UiProcessWideObservation`) | alone |
| `golden` | Executes a certified Golden case | the canonical manifest; never labeled by hand | complete at every full run and release |
| `structure` | Reads repository source and project files; no product assemblies | project default: Architecture | parallel |
| `document` | Asserts the content of repository documents | one Architecture class after the pilot | moves to the structure lane (WS-GOV F-4) |

Rules:

- A class has one category. A class whose tests need different categories is
  split, not labeled per method. Existing mixed classes (Architecture topic
  files that also read documents; the mixed UiSmoke classes of item 9) are
  resolved through D-4 and option U2a, not by relabeling.
- A `Category` trait is added at class level only when a runner must filter by
  it (first candidates: a separate process-test invocation with hang dumps,
  and a UiSmoke partition). A structure check then rejects unknown values,
  method-level category traits and any `golden` trait.
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
| 1 | WS-GOV class `prose` | none; the structure lane runs | F-4; in force only after the document checks, including the line ceiling, run in the structure lane (until then `architecture`) |
| 2 | WS-GOV class `unclassified` | full suite | F-4 |
| 3 | The runner and selector: `scripts/verify.py`, the selection map and selector, `.github/workflows/ci.yml`, `scripts/coverage_policy.py`, `scripts/coverage_configuration_policy.py`, `docs/contracts/coverage-baseline-v1.json` | full suite | a runner cannot vouch for its own selection |
| 4 | `profiles/**`, `testdata/**`, `docs/contracts/**`, `external-tools/**`, `refcode/**`, `.gitattributes` | full suite | read at build and run time by most groups, directly, through build outputs or through the upward tool search, often hash-pinned; firmware, contract and evidence inputs deserve complete evidence |
| 5 | Solution-wide build inputs: `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `global.json`, `NuGet.config`, `NvtFwCombiner.slnx`, `VERSION`, `.editorconfig` (code style is enforced in the build), `eng/**` | every .NET group and repository-scripts | every project build reads them |
| 6 | A file in a .NET project directory (`src/<P>/**`, `tests/<P>/**`, including its `packages.lock.json`), or a file a project includes from outside its directory (linked `Compile`, `EmbeddedResource`, `None`, `AvaloniaResource`) | every group whose test project references `<P>` transitively or includes the file, from the base and head project graphs; plus `architecture`, which reads all source text; a `.csproj` change also repository-scripts | derived from `.csproj` files; the script tests read project files |
| 7 | A `src/` project no test project references (today Desktop and Launcher) | full suite, reported as a gap | no direct test owner |
| 8 | Any `src/**` path (together with row 6) | repository-scripts | size-policy, release-package and parity script tests read production sources |
| 9 | `scripts/**` (other than row 3), `tests/scripts/**` | repository-scripts and architecture | script tests; Architecture.Tests reads four scripts |
| 10 | `.github/**` (other than row 3), `.agents/**`, `.codex/**`, `.gitignore`, `tools/**` | repository-scripts; `tools/crc-worker/**` also the worker lane | read by the script tests only |
| 11 | Documents outside rows 1 to 10: non-prose files under `docs/**`, Markdown files elsewhere (for example the root `README.md`, `SPEC.md`, `CHANGELOG.md` and every `AGENTS.md`) and the root `LICENSE` | architecture (document checks, line ceiling) and repository-scripts; `docs/references/**` also bootstrap | run-time readers |
| 12 | Anything else | full suite | F-4 |

Independent of this table: the structure lane and the authority check run on
every pull request, and the full suite with Golden runs when a release branch
is cut, on the release pull request, on every push to `main`, weekly on the
trunk when it changed and on manual dispatch (WS-GOV item 13).

The selection map is one data file next to WS-GOV's authority map (proposal:
`docs/governance/test-selection-map.json`, governance class). It holds rows 3,
4, 5 and 7 to 11 and each group's default category. Row 6 is never stored:
the selector derives it from the project files. The selector lives with the lane
inventory in `scripts/verify.py` (or a module it imports) and has two
consumers: a local report (`which groups does my diff need, and why`) that
replaces the narrow-test table, and WS-GOV G2's pull-request tier.

### 4. Declared run-time inputs

The initial rows come from a read-only inventory of what each test project
reads (recorded in the [log](WS-TEST.md)). The map may over-select. A document
that any test or script reads can never be `prose` in WS-GOV's map; a check
enforces that.

### 5. Consistency checks and shadow mode

The selector's own tests cover: each row; a deletion; a rename across groups;
a changed project reference (base and head graphs); an unreferenced project;
an unmatched path; a prose-only change; a change to the selector itself.
Structure checks require that every group in the map exists in `verify.py`'s
inventory and the reverse, that every `src/` project is referenced by a test
project or listed in row 7, and that no declared input is `prose`.

Before any pull request tier relies on the selection (G2), the selector runs
in **shadow mode**: CI still runs the full suite, and the aggregator records
for every failing group whether the selection included it. Each failure
outside the selection is classified: caused by the change (a map miss) or
intermittent (a bug file). Selection is enabled only after the owner-set
window (proposal: 20 pull requests or two weeks, whichever is longer) shows no
miss; a miss adds a map row and restarts the window. Shadow mode sees a miss
only when it coincides with a real failure, so it adds to the inventory and the
checks and does not replace them.

### 6. Class size, naming and mechanical splits

- Size is measured per class aggregate: nonblank lines over all partial files
  and test methods.
- Test aggregates of at least 2,000 nonblank lines join the hotspot list of
  WS-GOV item 17 and leave it below 1,500 (owner decision D-3). A listed
  aggregate may not grow; new tests go into a feature-scoped class.
- A test file's name is `<Class>.cs` or `<Class>.<Topic>.cs` for the class it
  declares. Existing mismatches are fixed when their class is split or next
  touched, in a rename-only commit.
- **Split rules.** S1: a split moves whole files; a test method is never
  edited, renamed or moved between files in the same change. S2: a moved
  file changes only its class declaration line, `using` directives, one
  summary comment per new class and a collection attribute that rule S4
  requires. S3: private helpers shared across the new classes move verbatim
  into one static support class (only accessibility changes), imported with
  `global using static`, so test bodies stay byte-identical. S4:
  serialization is preserved: a class that relied on serial execution or on
  shared state joins an explicit collection that keeps it serial; a split may
  add serialization but never removes it without evidence. S5: a split
  multiplies class fixtures, so their cost is measured; an expensive fixture is
  shared (an xUnit assembly or collection fixture) only if it is read-only and
  thread-safe, and otherwise the cost is accepted or the files stay together
  (the UiSmoke group fixtures are deliberately separate per group since
  `acc6ea039`). S6: tests referenced by a contract (Golden
  `evidenceRefs`) or by a document link are not moved unless the same change
  updates the reference under that reference's authority. S7: only classes
  outside every active write lock; the commander schedules each split, and a
  split pull request contains nothing else. S8: every split records the
  equivalence evidence of the [pilot plan](PLAN-test-pilot-split.md)
  (discovery mapping, outcomes, mechanical diff, canary, timing).

### 7. Stability rules

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
- No automatic retry in CI or in the verifier. An intermittent failure becomes
  a bug file with the failing test name and its artifact, and is fixed or
  reverted ([tests instructions](../../../tests/AGENTS.md) already say a retry is
  not a fix). A CI re-run remains a manual diagnostic, not evidence.
- Timing assertions prefer counts and work units over wall-clock limits (ADR
  0027).
- Proposed to the `verify.py` owner after the CI evidence change: hang dumps
  (`--blame-hang-timeout`) for the `process` and `ui` groups, uploaded only on
  failure.

### 8. Partitions and the ADR 0027 amendment

ADR 0027's "each project is run unfiltered" becomes: in a full run, each test
of each project runs exactly once, either in one unfiltered run or in a
verified exact partition. A partition is allowed only when it is declared in
`scripts/verify.py` (never in workflow YAML), each part records its filtered
discovery against the same assembly hash, the parts are disjoint and their
union equals the unfiltered discovery, and the finalizer checks all of it. At
the pull request tier a group that the selection excluded counts as "not
applicable" only when the finalizer recomputes the same selection for the exact
base and head (G2). The 300-second acceptance and "rather than adding shards"
clauses are a 2026-08 experiment's stop rule; whether they still bind is owner
decision D-5.

### 9. UiSmoke

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
control, yet they are serialized with the control tests. Separately, about ten
classes use fixed sleeps of 30 to 500 ms as synchronization (for example
`MemoryCoveragePopupTests` with ten), which costs little time but is a
stability risk; the 10 to 30 s `WaitAsync` limits elsewhere are upper bounds,
not waits.

| ID | Option | Expected effect | Cost and risk | Needs |
| --- | --- | --- | --- | --- |
| U0 | Measure: per-class and per-test time from the TRX of a green CI UI shard (kept three days), the phase split from its shard log, and one local run on a quiet machine | Shows where the 479 s go; prerequisite for U2 to U5 | about an hour; none | an artifact download |
| U1 | Diagnostics: hang dumps on failure, failing test names in the log (CI evidence change), per-test timeouts on UI waits | Faster diagnosis; no speed change | small; dumps uploaded only on failure | `verify.py` owner |
| U2 | In-project fixes guided by U0. U2a: split the four mixed classes by file so that files building no control leave `UiAvaloniaRuntime` (rules S1 to S5; about 300 methods leave the serial chain). U2b: replace fixed sleeps with waits on the observed event. U2c: where U2a multiplies Bootstrap group fixtures, measure the cost and apply rule S5 | U2a is the largest expected in-project gain (size known only after U0); U2b is for stability | R1 test-only. U2a removes serialization, so rule S4 needs evidence per moved file: it builds no control, and repeated parallel runs stay green. Each new class adds a Bootstrap fixture. Conflicts with F08 and the navigation work in the same classes | U0; write locks clear |
| U3 | Less coverage work on pull requests: none (D-1 A) or instrumenting only the changed modules (D-1 B) | Removes all or most instrumentation overhead from pull request runs (size unknown until U0) | with A, a coverage drop surfaces at the next full run; a coverage policy and finalizer change either way | G2 (R3); owner decision D-1 |
| U4 | Build once; test shards reuse the Release build | Removes the per-shard restore and build from every shard | artifact transfer; build-output custody (hash checks exist) | G2 (R3) |
| U5 | Partition UiSmoke over two runners (item 8) | Roughly halves the UI test-execution part | one more runner; partition evidence; amends ADR 0027 | U0, U4, G2; owner decision D-5 |
| U6 | Move tests that build no Avalonia control into a new, fully parallel project | Largest structural gain for UI | hundreds of moved tests; a ninth project changes the closed inventory (R3) | only if U2 to U5 are not enough |
| U7 | Skip UiSmoke by path | Helps prose, script and non-UI leaf changes only; UiSmoke references almost every `src/` project | none beyond item 3 | selector |

Decision: U0 and U1 first; U2a as the first UI split after U0 and after the
F08 and navigation work merges, then U2b and U2c per measured class; U3 and U4
decided with G2; U5 only if, after U4, the UI shard is still the critical path
by a margin the owner sets; U6 not planned. Because `core` is only about 140 s
behind, the next lever after UI is the serial Infrastructure run inside `core`
(item 7 and batch T5).

## Consequences

### Positive

- A documentation-only or script-only pull request stops running .NET shards
  it cannot affect; the intermittent core failure seen on a documentation-only
  pull request cannot block such a change.
- Agents and CI choose tests from one derived, reviewable source instead of a
  per-layer table and memory.
- Splits become routine, reviewable and verifiable; smaller classes run in
  parallel and can be timed and selected by topic.
- Timing-sensitive tests get one set of rules that addresses the diagnosed
  failure causes.

### Negative / trade-offs

- A path-selected pull request can miss a cross-group interaction until the
  next full run; shadow mode and weekly full runs bound that risk.
- The whole-repository coverage totals move to full runs (with D-1 B the
  changed modules are still checked on each pull request; with D-1 A nothing
  is), so some coverage drops fail later than today. Golden cases run on a pull
  request only when a change can reach them; every full run and release still
  runs all of them.
- The map and the selector are new governance-class code to maintain.
- Renaming classes changes test names in reports; historical references in
  frozen records keep old names.

### Risks and mitigations

- An incomplete declared-input table under-selects -> conservative rows,
  structure checks, shadow mode, full runs at every release boundary.
- A split changes behavior through parallel execution -> rule S4, canary
  checks, and the pilot's timing headroom check for process tests.
- Splits collide with active work -> rule S7 and commander scheduling.
- The selector wrongly marks a producer not applicable -> the finalizer
  recomputes the selection itself (item 8).

## Compatibility and migration

| Batch | Content | Risk | Evidence and review | Depends on |
| --- | --- | --- | --- | --- |
| T0 | This draft, the pilot plan, the log | R0 | independent design review (another runtime, or a fresh session) because the ADR is R2 | - |
| T1 | The ADR into `docs/adr/` with its number; reciprocal "Amended by" line in ADR 0027; one pointer each in `tests/README.md` and `tests/AGENTS.md` | R2 | before WS-GOV G1-B: a capability-reuse record, exact-head review and owner acceptance; after G1-B: the pull request fields, review and owner approval | T0 review, owner decisions |
| T2 | Pilot split of `RepositoryBoundaryTests` | R1, tests only | equivalence evidence and independent review; no capability-reuse record (the validator rejects a record whose only path is a test file, as for the H1 fix) | owner decision D-6; nvt-marker's two Architecture files merged or excluded |
| T3 | Selection map, selector, structure checks, local report; the runbook's narrow-test table becomes a pointer | R2 | record or pull request fields as in T1, review | CI evidence merged (`verify.py` has one writer); G1-A (authority map); the runbook edit goes through G1-B's writer |
| T4 | Pull request tier in CI: shadow mode, then selection; "not applicable" producers; coverage per D-1; document checks into the structure lane; ADR 0027 amendment in force | R3 | inside WS-GOV G2: release-owner approval, negative tests, shadow-mode evidence | T3, G2 |
| T5 | U0 and U2; Infrastructure: only process classes serialized, after measurement; further mechanical splits of listed classes; U5 if decided | R1 to R3 by item | per item | measurements; write locks |

Order: the CI evidence change merges first. T0's review and the owner
decisions run in parallel with it, and T2 may land independently of both
(test files only). T1 lands before or with T3. G1-A precedes T3; T3 precedes
G2, which carries T4. T5 items follow their measurements. Nothing in WS-TEST
edits `ci.yml` or `verify.py` before the CI evidence change merges, and no
WS-TEST branch merges the trunk (rebase only, board working rules).

## Verification

- Pilot: the equivalence procedure of the [pilot plan](PLAN-test-pilot-split.md).
- Selector: the negative tests of item 5, run on synthetic diffs and on the
  diffs of recent pull requests.
- Shadow mode: the recorded window with zero misses before selection is
  enabled.
- CI: a documentation-only pull request completes without .NET producers and
  the aggregator passes; a pull request that touches `profiles/` runs the full
  suite; a producer that is selected but missing fails the aggregator.
- UiSmoke: U0's table before and after each U2 change, on the same runner type.

## Open owner decisions

Listed in plain words, highest risk first, in the
[log](WS-TEST.md#owner-decisions-in-risk-order): D-1 how pull requests check
coverage (recommended: the changed modules on every pull request, totals on
full runs); D-2 the shadow-mode window; D-4 what happens to document-wording
assertions; D-5 whether ADR 0027's 300-second rule still binds; D-3 a growth
block for large test classes; D-8 category labels only where a tool reads
them; D-6 the pilot class; D-7 what lands in 1.1.13.
