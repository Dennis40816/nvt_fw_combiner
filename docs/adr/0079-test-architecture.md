# ADR 0079: Test architecture: selection groups, categories, class size and UI smoke scheduling

- Status: Accepted (owner, 2026-09-26, board decision 81). The owner accepted
  the complete text as reviewed at patch SHA-256
  `583740da471cc01a12d12ed8cec03b73cbe441fa6c75f76e679e167c0ee6159d` (design
  re-review: ACCEPT), with its measured targets filled in; board decisions 69 to
  76 had answered its design questions. As board decision 72 states, that
  acceptance replaced ADR 0027's 300-second clauses with the measured targets
  of item 9. The partition, selection and coverage rules take effect only
  through their own admitted batches and the T4b activation (see
  [Compatibility and migration](#compatibility-and-migration)). The ADR enters
  the repository through its admission `TEST-ARCH-ADR-1113-01`, in the batch
  after batch 2a.
- Decision source: `docs/handoff/1.1.12.md` on `feature/1.1.13/wave2`. Board
  decisions 69 to 76 were introduced by commit
  `dbc96c26873d00cc205c03bcc1c55443cb3d7e69` and board decision 81 by commit
  `557a9ee6ae1919c344c61426b049f9f0946d8eed`, which also contains the earlier
  ones. This ADR is integrated only after that board has merged into the trunk
  (batch 2a or the handoff commits after it), so the relative links to the
  [1.1.12 board](../handoff/1.1.12.md) below reach these decisions in the
  integrated tree.
- Date: 2026-09-26
- Owners: Repository owner (test, CI and release policy)
- Amends: [ADR 0027](0027-evidence-preserving-performance-remediation.md),
  section "2026-08-12 evidence-sharded .NET CI amendment", only as stated in
  decision item 9
- Relates to: [ADR 0080](0080-governance-reset.md) (governance reset, accepted
  2026-09-27 as a staged design: the authority path map and the CI tiers of its
  batches G1-A, G1-B and G2); the CI failure-evidence
  contract (`CI-FAILURE-EVIDENCE-1113-01`)
- Design review: `codex/gpt-6-astra` reviewed the WS-TEST design drafts on
  2026-09-26 at `87ac3d6c1` (ACCEPT-WITH-CHANGES, findings F-1 to F-6); all
  six were taken in at `20dbfcdc3`. The same reviewer reviewed this ADR text
  and its record on 2026-09-26 (patch SHA-256 `ac67dc2d…`, ACCEPT-WITH-CHANGES,
  findings F-1 to F-5 on the effective point, the pending entry texts, the
  pilot acceptance, the decision source and the WS-GOV prose wording), and
  re-reviewed the revision with its measured targets (patch SHA-256
  `583740da…`): ACCEPT, F-1 to F-5 closed, with one non-blocking suggestion on
  rounding, taken in.

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
[tests README](../../tests/README.md) keeps older measurements (3,962 methods
on 2026-09-06).

No test declares a category trait. Kinds of tests are implicit: the project a
test lives in, three UiSmoke collections (`UiAvaloniaRuntime`,
`UiProcessWideObservation` with parallelization disabled, `UiExternalGolden`)
and one Infrastructure collection (`ReadyProbeProcessSerialGroup`, disabled
parallelization, nine classes). There is no `xunit.runner.json` and no
assembly-level collection setting, so xUnit's default applies: each class is
one collection, classes run in parallel, and the tests of one class run one
after another.

### Cost in CI

The 60 most recent completed `ci.yml` runs before this decision (2026-09-15
to 2026-09-26, final attempts, successful jobs; read with `gh run view`; the
same window and method as the measured targets of item 9, p90 by nearest
rank):

| Job | n | Median s | p90 s | Largest step (median) |
| --- | ---: | ---: | ---: | --- |
| `dotnet / test (ui)` | 45 | 522 | 680 | evidence step 479 s (restore, Release build, discovery, one coverage run) |
| `dotnet / test (core)` | 46 | 380 | 403 | evidence step 338 s (six projects in order, each built then run) |
| `dotnet / test (bootstrap)` | 51 | 250 | 376 | evidence step 208 s |
| `dotnet / build` | 51 | 263 | 288 | build step 216 s |
| `python / repository policy (a-q)` | 55 | 294 | 370 | |
| `python / repository policy (r)` | 51 | 249 | 275 | |
| `python / repository policy (s-z)` | 56 | 227 | 249 | |
| `policy / polytail` | 50 | 116 | 138 | |
| `python-worker / verify` (after the script shards) | 50 | 69 | 79 | |
| `dotnet / build-test` (finalizer, after every .NET job) | 40 | 41 | 48 | |

The UI shard is the critical path, about 140 s ahead of `core` at the median.
Making UiSmoke alone faster therefore saves at most about 140 s of wall-clock
time before `core` becomes the critical path. The console log does not show
how the 479 s split into restore, build, discovery and execution; that is in
the shard log inside the uploaded artifact. A local UiSmoke run on 2026-09-26
took 5 min 58 s for 1,689 cases (Debug, no coverage; one run; machine load not
recorded).

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

An earlier plan named `ShellViewModelTests` the largest class (86 files,
24,746 lines). That count went by file name: commit `acc6ea039`
(2026-08-11, "test: parallelize isolated UI smoke groups") had already split
the class into group classes with their own class fixtures, and the files kept
the old prefix. At `cf4e42697`, 147 files under `tests/` (134 of them with
tests) declare no class matching their name prefix, including all 86
`ShellViewModelTests.*.cs` files.

A large class costs twice: it is one serial unit for xUnit, and its partial
files share private helpers, so its topics cannot be selected, timed or moved
separately.

### How tests are selected today

- Locally, the narrow-test table of
  [`development-execution-workflow.md`](../governance/development-execution-workflow.md)
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
- Tests read repository files at run time (item 5 lists the inventory):
  profiles, Golden and synthetic test data, contracts, external tools,
  documents and source text. Some data reaches tests through build outputs
  rather than a direct read: Infrastructure copies
  `docs/contracts/canonical-capability-policy-v1.json` and two
  `profiles/built-in` files to its output and embeds four contract schemas,
  VersionManagement.Infrastructure embeds seven, and Bootstrap's build imports
  `eng/profile-bundle-materializer` to copy `profiles/built-in` into its
  output. These flow to every project that references them. The default
  external-tool loader searches upward from the test output for
  `external-tools/`. Infrastructure.Tests also depends on two build-only
  project references (`ReferenceOutputAssembly="false"`: ReadyProbe and
  LauncherBootstrap), whose outputs it runs as child processes.
- `NvtFwCombiner.Desktop` and `NvtFwCombiner.Launcher` are referenced by no
  test project; only source-text checks and release smoke touch them.
- Two documents were within two lines of the 2,500-line ceiling that
  Architecture.Tests enforces over `docs/**/*.md` (a UI handoff document at
  2,499 lines and the roadmap at 2,498), so an ordinary edit can fail a .NET
  test project.

### Contracts that constrain selection

- **Golden.** The canonical manifest
  ([`manifest.json`](../../testdata/golden/canonical/manifest.json)) owns
  which tests execute certified cases: each case's
  `testDisposition.evidenceRefs` names `file#symbol` in Bootstrap.Tests or
  GoldenRegression.Tests. `verify.py --release-golden` runs those two projects
  completely and requires exactly one passed execution per case. Moving or
  renaming such a test changes an R3 contract.
- **Coverage.** The policy
  ([`coverage-baseline-v1.json`](../contracts/coverage-baseline-v1.json))
  requires no regression of overall line and branch coverage, the presence of
  every baseline module, and ratchets for changed modules, all computed from
  the union of the eight projects' reports. A run of some projects cannot
  produce that verdict.
- **Evidence.** The finalizer requires every shard's manifest, the exact
  eight-project inventory, and discovery reconciled with execution
  ([pull request CI](../ci/pull-request-ci.md)). ADR 0027 also asks for
  "unique test ownership": no verifier lane runs the same test owner twice, and
  artifacts carry logs, TRX and coverage only, never firmware payloads. The CI
  failure-evidence contract names every artifact by run attempt and has the
  finalizer verify each producer's newest attempt; until a real re-run has
  verified that, a failed run is followed by a new workflow run rather than
  "Re-run failed jobs".

### Stability

The core shard fails intermittently, including on a documentation-only pull
request ([bug](../handoff/bugs/BUG-20260925-core-shard-intermittent-failure.md)).
The diagnosis names H1 (a helper's start deadline, fixed test-only), H2 (an
exception from `RandomAccess.GetLength` in production code), H3 (a launcher
start that returns `false` with no cause), H4 (an empty discovery inventory)
and H5 (a re-run that picked a stale artifact). H4, H5 and the missing
failing-test names belong to the CI failure-evidence contract; H2 and H3 are
product issues waiting for that evidence.

### Related work

- The governance reset (ADR 0080, accepted as a staged design): its authority
  path map classifies every path as `firmware`, `release`, `governance`, `code`, `prose` or
  `unclassified`. Its draft first defined `prose` as text "that no test or
  script reads"; that wording is historical, and the draft now uses the joint
  definition of item 3 (WS-GOV head
  `878f4b1a7c353cdb0dde0b788d451a8a7293ecfa`). Its CI tiers say only `prose`
  paths skip product tests, an
  unclassifiable change runs everything, the structure lane always runs,
  document checks that live in Architecture.Tests move to the structure lane
  or stay mapped to the documents they read, and every required check comes
  from an aggregator that fails on a missing, failed, cancelled or unexpectedly
  skipped producer. Its batch G2 (R3) implements the CI tiers.
- The CI failure-evidence contract uploads failing projects' evidence, prints
  failing test names and introduces the per-attempt artifact model above.

## Decision drivers

1. Never select fewer tests than a change can affect. Over-selection costs
   minutes; under-selection is a defect. (The same rule as the authority map.)
2. What the selector cannot prove, it runs: an input it cannot resolve, a
   missing base object or an incomplete coverage contributor set means the
   full suite or full coverage.
3. One owner per fact: project files own compile dependencies, the canonical
   manifest owns Golden membership, the authority map owns authority classes,
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
   declared table of run-time inputs and the authority classes, with
   conservative fallbacks; categories only schedule.** Selected.

Declaring categories:

1. A trait on every class now. Drift and effort without a consumer.
2. One project per category. Strongest isolation, but changes the closed
   eight-project inventory (R3) and moves many files.
3. **Reuse what exists (project placement, collections, the Golden manifest)
   and add a class trait only where a runner filters by it.** Selected.

Size policy: extend the product-code hotspot growth block to test classes
(selected, board decision 75); an advisory report only (earlier advisory
reports did not stop growth); or a split deadline (forces splits into classes
that active work is editing).

## Decision

### 1. Test groups are the selection unit

The groups are exactly `scripts/verify.py`'s inventory: the eight .NET test
projects (`CI_DOTNET_SHARDS`), the repository-script tests
(`REPOSITORY_SCRIPT_TEST_SHARDS`, selected together) and the Python worker lane
until it is retired. A selected group runs completely. No path rule filters
tests inside a group; scheduling partitions follow item 9. Selecting single
repository-script modules (the runner already passes explicit module files) is
a later refinement, once each module's inputs are declared.

### 2. Execution categories and Golden membership

Board decision 76: a label is added only where a tool reads it, and Golden
membership stays with the canonical manifest. Each test class has exactly one
execution category:

| Category | Meaning | Declared by | How it runs |
| --- | --- | --- | --- |
| `unit` | In-memory behavior; clock, paths, process runner and randomness injected | project default: Domain, Application, ProfileContract | parallel |
| `integration` | Real adapters and files in a per-test temporary workspace, assembled composition graph | project default: Infrastructure, Bootstrap, GoldenRegression | parallel only with per-test isolation |
| `process` | Starts a child process, uses pipes, named OS objects or OS scheduling, or changes process-wide state such as environment variables | membership in a serialized collection (today `ReadyProbeProcessSerialGroup`, whose classes also change environment variables) | serialized; the rules of item 8 |
| `ui` | Avalonia headless session: controls, layout, bindings, rendering | project default: UiSmoke; control construction in `UiAvaloniaRuntime` | existing UI runtime rules (checked by Architecture.Tests) |
| `observation` | Process-wide state or timing observations | a collection with parallelization disabled (`UiProcessWideObservation`) | alone |
| `structure` | Reads repository source and project files; no product assemblies | project default: Architecture | parallel |
| `document` | Asserts the content of repository documents | one Architecture class after the first split | its generic checks move to the structure lane (item 3) |

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
  of item 10) are resolved through board decision 71 (document assertions,
  test by test with the owner) and option U2a, not by relabeling.
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
| 1 | Authority class `prose` (the joint definition below) | none; the structure lane runs | in force only after the generic document checks, including the line ceiling, run in the structure lane (until then `architecture`) |
| 2 | Authority class `unclassified` | full suite | an unclassifiable change runs everything |
| 3 | The runner and selector: `scripts/verify.py`, the selection map and selector, `.github/workflows/ci.yml`, `scripts/coverage_policy.py`, `scripts/coverage_configuration_policy.py`, `docs/contracts/coverage-baseline-v1.json` | full suite | a runner cannot vouch for its own selection |
| 4 | `profiles/**`, `testdata/**`, `docs/contracts/**`, `external-tools/**`, `refcode/**`, `.gitattributes` | full suite | read at build and run time by most groups, directly, through build outputs or through the upward tool search, often hash-pinned; firmware, contract and evidence inputs deserve complete evidence |
| 5 | Solution-wide build inputs: `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `global.json`, `NuGet.config`, `NvtFwCombiner.slnx`, `VERSION`, `.editorconfig` (code style is enforced in the build), `eng/**` | every .NET group and repository-scripts | every project build reads them |
| 6 | A file in a .NET project directory (`src/<P>/**`, `tests/<P>/**`, including its `packages.lock.json`), or a file a project includes from outside its directory | the groups the resolution rules below derive; plus `architecture`, which reads all source text; a project-file change also repository-scripts | evaluated `.csproj` graph; the script tests read project files |
| 7 | A `src/` project no test project references (today Desktop and Launcher) | full suite, reported as a coverage gap every time | no direct test owner; the fallback does not create tests |
| 8 | Any `src/**` path (together with row 6) | repository-scripts | size-policy, release-package and parity script tests read production sources |
| 9 | `scripts/**` (other than row 3), `tests/scripts/**` | repository-scripts and architecture | script tests; Architecture.Tests reads four scripts |
| 10 | `.github/**` (other than row 3), `.agents/**`, `.codex/**`, `.gitignore`, `tools/**` | repository-scripts; `tools/crc-worker/**` also the worker lane | read by the script tests only |
| 11 | Documents outside rows 1 to 10: non-prose files under `docs/**`, Markdown files elsewhere (for example the root `README.md`, `SPEC.md`, `CHANGELOG.md` and every `AGENTS.md`) and the root `LICENSE` | architecture (document checks, line ceiling) and repository-scripts; `docs/references/**` also bootstrap | run-time readers |
| 12 | Anything else | full suite | an unmatched path runs everything |

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

**Prose, joint definition with the authority map.** A `prose` file is not an
input to any product test or other semantic verification; only listed generic
document-structure checks may read it (Markdown links and anchors, the line
ceiling, encoding and file-name rules). Documents that topic tests still read
(for example the ADRs, UI and architecture documents asserted by
Architecture.Tests) are not prose and keep their row 11 mapping. Which
document assertions move into the structure lane, stay mapped to their
documents or are deleted is decided test by test with the owner (board
decision 71): a check that protects a fact stays, and only checks the owner
confirms freeze wording and nothing else are deleted. The governance reset's
draft has adopted this joint definition (WS-GOV head
`878f4b1a7c353cdb0dde0b788d451a8a7293ecfa`); its earlier wording ("no test or
script reads") is historical, because the structure lane reads every
document. Its batch G2 still has to implement the authority checker for this
definition and the list of document checks to migrate, together.

Independent of this table: the structure lane and the authority check run on
every pull request, and the full suite with Golden runs when a release branch
is cut, on the release pull request, on every push to `main`, weekly on the
trunk when it changed and on manual dispatch (the governance reset's CI
tiers).

The selection map is one data file next to the authority map (proposal:
`docs/governance/test-selection-map.json`, governance class). It holds rows 3,
4, 5 and 7 to 11 and each group's default category. Row 6 is never stored:
the selector derives it from the project files. The selector lives with the
lane inventory in `scripts/verify.py` (or a module it imports) and has two
consumers: a local report (`which groups does my diff need, and why`) that
replaces the narrow-test table, and the pull-request tier of the governance
reset's G2. Coverage can add groups to the selection (item 4).

### 4. Coverage on pull requests (board decision 69)

Board decision 69 takes the revised option B below. Two questions are answered
separately.

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

Until B is built and verified, today's full coverage rule stays unchanged.
Coverage only at full runs (option A, rejected by decision 69) is not a
fallback: lowering today's protection would need a new owner decision. A later
refinement may reuse the exact base's full-run reports for contributors whose
inputs are provably unchanged.

### 5. Declared run-time inputs

The map may over-select. The initial rows come from a read-only inventory of
what each test project reads at run time (2026-09-26). Every repository read
goes through `tests/NvtFwCombiner.TestSupport/RepositoryPaths.cs`, which
honors `NFC_TEST_REPOSITORY_ROOT` or searches upward for `NvtFwCombiner.slnx`.

| Test project | Reads at run time, beyond its project references | Covered by row |
| --- | --- | --- |
| Domain | nothing | 6 |
| Application | `profiles/built-in/ctrlram-postbuild-v2/catalog.json` through the Infrastructure output copy | 4 |
| ProfileContract | `profiles/built-in/**` | 4 |
| Infrastructure | `docs/contracts/*` (schemas, the capability policy), `.gitattributes`, `profiles/built-in/**`, `external-tools/legacy-combiner/**` (runs the real `Combiner.exe`), `testdata/golden/canonical/**`; built `ready-probe` and `launcher-bootstrap` outputs | 4, 6 |
| Bootstrap | `testdata/golden/canonical/**` (some at discovery time), `external-tools/**` (loaded eagerly by `BootstrapTestHost`), `profiles/built-in/**` and its materialized output copy, `docs/contracts/*.schema.json`, one hash-pinned file under `docs/references/`, `refcode/ab_code_combiner/**` (hash-pinned, Windows-gated) | 4, 11 |
| GoldenRegression | `testdata/golden/canonical/**` (theory cases from the manifest), `testdata/public-synthetic/**`, the materialized profiles; `external-tools/**` possibly, through the upward search | 4 |
| UiSmoke | `testdata/golden/canonical/**`, `external-tools/**` through `PresentationTestHost`, Presentation source and XAML as text, `VERSION`, `NvtFwCombiner.slnx` | 4, 5, 6 |
| Architecture | all `src/**` and `tests/**` source; eight topic files read `docs/**` or root Markdown; `profiles/built-in/**`; `testdata/**`; four scripts; `eng/**`; and, through its `dotnet msbuild` child, `Directory.Build.*`, `Directory.Packages.props`, `global.json` | 4, 5, 6, 9, 11 |
| Repository scripts | `.github/workflows/`, `.agents/`, `AGENTS.md` files, `docs/contracts/`, `docs/governance/`, `docs/ci/`, `docs/references/`, `profiles/`, `testdata/golden/`, `src/`, the test projects' `.csproj` files, `tools/crc-worker/` | 4, 6, 8 to 11 |

The table is a starting inventory, not a proof of completeness:

- A change that adds or changes a run-time read of a repository file updates
  the declared inputs in the same pull request, and the reviewer checks it.
- The selector's batch adds a best-effort structure check: it lists the
  repository-relative path literals that test sources pass to `RepositoryPaths`
  or the repository-root helpers and fails when one falls outside rows 3 to 11.
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

**Exit and activation** (board decision 70). Selection is enabled only when
all of these hold, and then only by the owner's explicit decision:

- the window, at least 20 distinct pull requests with complete full-run
  evidence over at least two weeks, has no `miss` and no open `unresolved`;
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
- Test aggregates of at least 2,000 nonblank lines join the product-code
  hotspot list and leave it below 1,500 (board decision 75: the product-code
  size rule applies to test classes too). A listed aggregate may not grow; new
  tests go into a feature-scoped class that keeps the isolation and collection
  contract of the tests it joins.
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
  before. Removing that serialization is a second change, allowed only when
  the static review of S9 shows no shared mutable state, no process-wide
  state, no indirect use of the UI dispatcher, global resources or background
  work through helpers or fixtures, and cleanup of all background work, backed
  by repeated parallel runs. Green parallel runs alone never prove safety;
  unclear evidence keeps the tests in their original collection. A split may
  add serialization without such evidence.
- **S5.** Removing serialization multiplies class fixtures, so their cost is
  measured; an expensive fixture is shared (an xUnit assembly or collection
  fixture) only if it is read-only and thread-safe, and otherwise the cost is
  accepted or the files stay together (the UiSmoke group fixtures are
  deliberately separate per group since `acc6ea039`).
- **S6.** Tests referenced by a contract (Golden `evidenceRefs`) or by a
  document link are not moved unless the same change updates the reference
  under that reference's authority.
- **S7.** Only classes outside every active write lock; each split is
  scheduled against the actual branch point, and a split pull request contains
  nothing else.
- **S8.** Every split records its equivalence evidence. The evidence items
  are: **E1** the discovered cases before and after, identical after removing
  class names, each mapped to its declared new class; **E2** identical
  outcomes, no new skip; **E3** a mechanical diff containing only renames,
  declaration lines, summary lines, `using` lines, helper moves under S3 and
  collection attributes and definitions; **E4** canary violations in a scratch
  copy that fail exactly the expected moved tests under their new names;
  **E5** before and after timing on a quiet machine; **E6** the pull request's
  CI with an unchanged case count; **E7** the static review of S9. A
  mechanical move records E1 to E7. A change that removes serialization
  records E1, E2, E5 and E7 and the repeated parallel runs of S4, with the run
  count set in its own plan.
- **S9.** Every split records a before and after table of class-level
  semantics: attributes (collection, traits, skips), base types, constructors,
  `Dispose` and `IAsyncLifetime`, fixtures and their scope, static and shared
  state with its owner and initialization count, background work and its
  cleanup, collection membership and any order dependence. Any difference
  other than the declared class names is explained.
- **S10.** A size limit is never a reason to remove serialization (board
  decision 75).

The first split is `RepositoryBoundaryTests` in Architecture.Tests (board
decision 74): a mechanical move into feature-scoped classes with one shared
collection, then a separate change that removes that serialization, with the
file list recomputed at the real branch point and files under active write
locks left in a residual class. It proves the split procedure; it is no
evidence that UI tests are safe to run in parallel.

**Pilot acceptance.** These numbers apply to this first split only; later
splits set their own in their plans.

- **E5 for the pilot:** three runs at the base B and three at the head H on the
  same quiet machine, comparing the median wall-clock, the per-class durations
  from the TRX files, and the MSBuild materialization batch's duration with its
  headroom to its two-minute budget.
- **T2a (the mechanical move):** E1 to E7 at one exact head, with scheduling
  unchanged (E7).
- **T2b (removing the serialization):** E1, E2, E5 and E7 at one exact head,
  five consecutive green runs of the project, and at least twofold headroom
  for the MSBuild batch (its duration at most 60 seconds).

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
  ([tests instructions](../../tests/AGENTS.md)). Whether a re-run's artifacts
  are valid verification evidence is decided by the CI failure-evidence
  contract, not here: its finalizer verifies each producer's newest attempt
  with attempt-named artifacts, and until a real re-run has verified that, a
  failed run is followed by a new workflow run, not by "Re-run failed jobs".
  This ADR keeps that pending gate. Shadow mode keeps every attempt.
- No automatic retry in CI or in the verifier.
- Timing assertions prefer counts and work units over wall-clock limits (ADR
  0027).
- **Hang detection (U1) is an R3 sub-item of the CI failure-evidence
  contract.** VSTest blame with dump type `none` names the hanging test and
  stops the test host without a memory dump. Its extra attachment changes the
  producer allowlist, the manifest and the finalizer, so it is delivered with
  them and with negative tests (a missing, extra or oversized attachment).
  Memory dumps are not uploaded: ADR 0027 allows logs, TRX and coverage only
  and forbids firmware payloads in artifacts, and a test-host dump can hold
  fixture bytes.

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

Board decision 72: this ADR replaces ADR 0027's 300-second acceptance clause
and its "rather than adding shards" stop rule with the measured targets below,
effective when the measured targets are filled in and the owner explicitly
accepts this ADR. That happened on 2026-09-26 (board decision 81); until then
both clauses stood, and past runs over 300 seconds had not voided them. That
effective point covers only these two clauses. The
partition rule above, and the selection and coverage on pull requests, take
effect separately, through their own admitted batches (T3, T4a) and the T4b
activation. A shard is added only after the UiSmoke measurement (U0) and
shared builds (U4).

**Measured targets.** They are reference values for later measurements, with
no obligation attached and no timing gate (ADR 0027 prefers counts and work
units over wall-clock gates). They were filled in before the owner's
acceptance, and the owner accepted these values with the text (board decision
81); U0 explains where the time goes and informs later targets.

| Measure | n | Median s | p90 s |
| --- | ---: | ---: | ---: |
| Whole run, creation to completion (successful first attempts) | 35 | 577 | 727 |
| `dotnet / test (ui)` | 45 | 522 | 680 |
| `dotnet / test (core)` | 46 | 380 | 403 |
| `dotnet / test (bootstrap)` | 51 | 250 | 376 |
| `dotnet / build-test` (finalizer) | 40 | 41 | 48 |

Source: queried on 2026-09-26 at 13:47 UTC, read-only (no artifact download,
no write), with
`gh run list --workflow ci.yml --status completed --limit 60 --json databaseId,attempt,conclusion,createdAt,updatedAt,event,headBranch`
and, for each listed run, `gh run view <databaseId> --json jobs,attempt,conclusion`,
which returns the jobs of the run's final attempt. The 60 completed runs range
from run `34922022009` (created 2026-09-15 02:39 UTC) to run `36225509388`
(created 2026-09-26 07:01 UTC), the same window as the context above. A job
counts when its final-attempt conclusion is `success`, with its duration from
`startedAt` to `completedAt`. The whole-run row counts the runs whose first
attempt succeeded, from `createdAt` to `updatedAt`. The median is the middle
value (the mean of the two middle values for an even count); p90 is the
nearest-rank 90th percentile. Displayed durations, here and in the context
table, are rounded to the nearest whole second using Python `round()`, which
rounds a half to the even second (the `core` median of 379.5 s shows as 380).

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
| U1 | Diagnostics: hang detection without memory dumps (item 8), failing test names in the log (CI failure-evidence contract), per-test timeouts on UI waits | Faster diagnosis; no speed change | R3 sub-item of the CI failure-evidence contract for the hang attachment | `verify.py` owner |
| U2 | In-project fixes guided by U0. U2a: split the four mixed classes in two changes (S4): first a mechanical move that keeps every file in `UiAvaloniaRuntime`, then a separate change that takes out only the files whose S9 review shows no direct or indirect UI, global-resource, background or process-wide use. U2b: replace fixed sleeps with waits on the observed event. U2c: where U2a multiplies Bootstrap group fixtures, measure the cost and apply S5 | U2a is the largest expected in-project gain (size known only after U0); U2b is for stability | R1 test-only; each new class adds a Bootstrap fixture; unclear files stay serialized | U0; write locks clear |
| U3 | Less coverage work on pull requests | Decided by board decision 69 (revised B): unchanged for pull requests that reach product code; none for those that reach no product module | a coverage policy and finalizer change (item 4) | G2 (R3) |
| U4 | Build once; test shards reuse the Release build | Removes the per-shard restore and build from every shard | artifact transfer; build-output custody (hash checks exist) | G2 (R3) |
| U5 | Partition UiSmoke over two runners (item 9) | Roughly halves the UI test-execution part | one more runner; partition evidence | U0, U4, G2 |
| U6 | Move tests that build no Avalonia control into a new, fully parallel project | Largest structural gain for UI | hundreds of moved tests; a ninth project changes the closed inventory (R3) | only if U2 to U5 are not enough |
| U7 | Skip UiSmoke by path | Helps prose, script and non-UI leaf changes only; UiSmoke references almost every `src/` project | none beyond item 3 | selector |

Decision: U0 and U1 first; U2a as the first UI split after U0, then U2b and
U2c per measured class; U3 and U4 with G2; U5 only if, after U4, the UI shard
is still the critical path by a margin the owner sets; U6 not planned. Because
`core` is only about 140 s behind, the next lever after UI is the serial
Infrastructure run inside `core`.

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
- Under decision 69, pull requests that reach product code keep full coverage,
  so their .NET run does not get shorter; only the rejected option A would
  have shortened it, at the cost of finding some coverage drops after merge.
  Golden cases run on a pull request only when a change can reach them; every
  full run and release still runs all of them.
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
- Splits collide with active work -> S7.
- The selector wrongly marks a producer not applicable -> the finalizer
  recomputes the selection itself (item 9).
- A later passing attempt hides a failure -> shadow mode keeps every attempt;
  the CI failure-evidence contract owns re-run evidence.

## Compatibility and migration

This ADR changes no script, workflow, test or product file. Apart from the
replacement of ADR 0027's 300-second clauses (item 9), each rule takes effect
through its own admitted batch:

| Batch | Content | Risk | Evidence and review | Depends on |
| --- | --- | --- | --- | --- |
| T1 | This ADR, the reciprocal "Amended by" line in ADR 0027 and a pointer in `tests/README.md` (`TEST-ARCH-ADR-1113-01`) | R2 | capability-reuse record, independent design and exact-head reviews, the owner's explicit acceptance of the complete text with the measured targets filled in (board decision 81) | board decisions 69 to 76 and 81; batch 2a merged |
| T2a | First split, mechanical move of `RepositoryBoundaryTests`, serialization unchanged | R1, tests only | the pilot acceptance of item 7 (E1 to E7) and an independent review; no capability-reuse record (the validator rejects a record whose only path is a test file) | board decision 74; write locks clear |
| T2b | First split, de-serialization: remove the shared collection, process batch in its own serialized collection | R1, tests only | the pilot acceptance of item 7 (E1, E2, E5 and E7; five consecutive green runs; at least twofold MSBuild headroom); separate review | T2a merged |
| T3 | Selection map, selector with its resolution rules, declared-input and structure checks, local report; the runbook's narrow-test table becomes a pointer | R2; any part that changes what a required check accepts is R3 | its own record or pull request fields, review | the CI failure-evidence contract; the authority map (G1-A) |
| T4a | Shadow mode in CI: selection computed and recorded on every pull request; full suite and required checks unchanged | R3 (workflow path) | inside the governance reset's G2: release-owner approval, negative tests | T3, G2 |
| T4b | Activation: "not applicable" producers, coverage per decision 69, document checks in the structure lane, the partition rule of item 9 in force | R3 | the exit criteria of item 6 (decision 70), the owner's explicit enablement | T4a evidence |
| T5 | U0 and U2; U1 as an R3 sub-item of the CI failure-evidence contract; Infrastructure with only process classes serialized, after measurement; further mechanical splits of listed classes; U5 if decided | R1 to R3 by item | per item | measurements; write locks |

Order: T2a and T2b do not depend on the other batches. T3 follows the CI
failure-evidence contract and the authority map; G2 carries T4a and then T4b;
T5 items follow their measurements. Version allocation (board decision 73):
1.1.13 carries T1, T2a, T2b and U0; T3 joins 1.1.13 only if the authority map
lands in 1.1.13; T4b has no version deadline and happens when its evidence is
complete. The roadmap owns the allocation text.

## Verification

- Splits: the evidence of rule S8 (E1 to E7 for a mechanical move; E1, E2, E5
  and E7 with repeated parallel runs for a change that removes
  serialization); the first split meets the pilot acceptance of item 7.
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

## Owner decisions

Decided on 2026-09-26, each as recommended by the independent design review
(board decisions 69 to 76 in the [1.1.12 board](../handoff/1.1.12.md); source
pinned in the status block: `feature/1.1.13/wave2`, commit
`dbc96c26873d00cc205c03bcc1c55443cb3d7e69`). These decisions answer the
design questions; the owner accepted the complete text separately, as board
decision 81 (commit `557a9ee6ae1919c344c61426b049f9f0946d8eed`):

| Question | Board decision | Where it applies |
| --- | --- | --- |
| Coverage on selected runs | 69: revised B; full coverage until B exists; A is not a fallback | item 4 |
| Trusting the test selection | 70: shadow mode with the exit conditions | item 6 |
| Document assertions | 71: decided test by test with the owner | item 3 |
| ADR 0027's 300-second rule | 72: replaced by this ADR, with measured targets, when the owner accepts it | item 9 |
| 1.1.13 commitment | 73: T1, T2a, T2b and U0; T3 only with the authority map; T4b without a version deadline | migration |
| Split pilot | 74: `RepositoryBoundaryTests`, with the S9 review, the list recomputed at the branch point, write locks respected | item 7 |
| Large test classes | 75: the product-code size rule applies to test classes | item 7 |
| Category labels | 76: only where a tool reads them; Golden stays with the manifest | item 2 |
| Acceptance of this text | 81: the complete text accepted as reviewed at patch `583740da…`, with the measured targets filled in | status, item 9 |

## Amendment 2026-09-27: test local-state isolation (board decisions 89 to 91)

- Status: Accepted (owner, 2026-09-27, board decisions 89 to 91 in the
  [1.1.12 board](../handoff/1.1.12.md); source `feature/1.1.13/wave2`, commit
  `2718022613595482f60cb4ecdcfa435d972c6124`).
- Change: `TEST-LOCAL-STATE-1113-01` (R2), fixing
  `BUG-20260926-tests-write-real-local-state`.
- Design review: `codex/gpt-6-astra`, 2026-09-27. The first review of proposal
  patch SHA-256 `8ebd6f73…` was ACCEPT-WITH-CHANGES (F-1 to F-3). The re-review
  of patch `d55a9ffc…` was ACCEPT-WITH-CHANGES: F-1 and F-3 closed; F-2 needed
  a bounded classification of the UiSmoke stall, which the change's evidence
  and this text now use. The reviewer accepted design admission on that basis;
  the F-2 wording itself was not re-reviewed. A third review accepted the
  r2-to-r3 delta after #459 (patch `f671f571…` on
  `e6e991af32d76d99ad156a7f86baa662947db8d9`: ACCEPT, no new findings).
- Integration: batch 2c, after batch 2b (ADR 0079 v5) has merged. This
  amendment is appended to v5 without changing it.

### Context

Test hosts composed the production graph without a local-state directory, so
UI and CLI tests read and wrote the developer's real
`%LOCALAPPDATA%\NvtFwCombiner` folder. Report history there was rewritten
repeatedly while other worktrees ran tests (2026-09-26), and parallel runs could
overwrite each other's state. Item 8 already requires a parallel test to own
its temporary workspace; this amendment applies that rule to local state.

### Decision

1. **One composed directory for four files.** `CompositionHostServices`
   (Bootstrap) owns the one resolver of the current user's local-state folder,
   `ResolveCurrentUserLocalStateDirectory`, and every host graph carries one
   `LocalStateDirectory`. Report history, shell preferences, toolchain runtime
   and Event Buffer format files are placed in that directory; Presentation
   receives the directory through `PresentationHostServices` and
   `DesktopApplication.Run` and only appends its own file names. With the
   switch below unset or false, the resolved paths are those of the previous
   release.
2. **Test-process guard.** Every project with `IsTestProject` declares the
   runtime switch `NvtFwCombiner.LocalState.CurrentUserFolderForbidden=true`
   in the root `Directory.Build.props`; no product runtime configuration sets
   it. In such a process the default resolver throws before any local-state IO
   of the four files. The guard does not cover explicitly injected paths,
   child product processes or version-manager state.
3. **Isolated test directories.** Test composition roots inject a directory
   from TestSupport `IsolatedLocalState`: each allocation is a new directory
   under a per-process root in the test temporary area, so two allocations never
   share files (a shared class fixture still shares its one host). A normal
   process exit attempts to delete the per-process root; a killed or hung
   process, or a failed delete, can leave it behind.
4. **CLI test contract (board decision 89).** CLI regressions enter through the
   CLI's internal overload and inject only their own local-state directory. The
   composition graph, the capability policy and the external-tool discovery
   stay the production defaults; a test may not substitute any of them. This
   amends the accepted CLI test contract of `VERIFY-111-SHADOW-ROOT-01`, whose
   sealed record stays unchanged as history. The public overload differs only by
   resolving the current user's folder.
5. **Composition API migration (board decision 90).** NFC is an application,
   not a library: the composition signatures migrate without keeping the old
   ones, and no compatibility is promised to code outside the repository. The
   migration covers `PresentationHostServices` (both constructors gain
   `localStateDirectory` after `localFiles`), `DesktopApplication.Run` (gains
   `localStateDirectory`), `CompositionHostServices` (new
   `Create(string localStateDirectory)`, `LocalStateDirectory` and
   `ResolveCurrentUserLocalStateDirectory`; the internal `Create` overloads take
   the directory), `CliApplication` (a new internal overload taking the
   directory resolver) and the removal of the public
   `ShellPreferenceFileStore.DefaultPreferencesPath`.
6. **Scope (board decision 91).** This amendment covers the four files above.
   Version-manager state keeps its own resolver, which honors `LOCALAPPDATA`,
   and its test isolation is a separate follow-up
   (`BUG-20260927-version-manager-state-test-isolation`). Until that follow-up
   is done, no claim is made that all local-state IO of a test process fails
   closed.

### Consequences

- A test that forgets to inject a directory fails with
  `InvalidOperationException` instead of touching the real folder; the guard
  found one such path (`Program.Main` with an unknown command) that source
  search had missed.
- Product behavior is unchanged unless a product's runtime configuration sets
  the switch, which would make the product refuse its local state.
- `DesktopApplication.Run`'s startup preference read and the process-entry
  composition branch of the CLI `Program.Main` have no behavioral test; the
  directory they use is pinned by architecture tests.

### Verification

Red tests at `9861d800c` with all local-state IO intercepted (the default root
did not refuse; a real shell addressed report history and preferences under
the real folder), green on the proposal; the Architecture, Bootstrap, UiSmoke
and GoldenRegression projects; the runtime configuration of every test project
carries the switch and no product's does. One of five UiSmoke runs of the
proposal stalled with no Avalonia test completing: a suspected Avalonia
headless-session startup stall whose root cause and relationship to this
change remain undetermined. No direct causal link was identified by static
review. Later passing runs do not close the failure; it stays open as
`BUG-20260927-uismoke-headless-session-stall` (item 8).

## Amendment 2026-09-27: local UiSmoke partition (board decisions 113 and 128)

- Status: Accepted (owner, 2026-09-27, board decisions 113 and 128 in the
  [1.1.12 board](../handoff/1.1.12.md); source `feature/1.1.13/wave2`, commits
  `e07b150a7fb085ca90081f04ce228a9ebf7ab142` (decision 113) and
  `51e7a09a330906f45683836c2b078e03ff24e11b` (decisions 128 to 130)).
- Change: `VERIFY-UISMOKE-PARTITION-1113-01` (R2), in the local verify-speed
  batch of board decision 129.
- Design review: `codex/gpt-6-astra`, three rounds on 2026-09-27. Rounds one
  and two were BLOCKED (the projection fallback, the substring filter, the
  binding of the filter to the real `FullyQualifiedName`, the ownership table
  and the writer inventory); round three APPROVED the design with one
  non-blocking P3 on the SDK wording, taken in below.
- This amendment is appended without changing the text above, except where it
  says so for items 9 and 10 and batch T5.

### Context

A full local run spent about 331 s in UiSmoke, one test process whose
Avalonia tests all run on one headless UI thread (U0). Three processes over
disjoint type lists took 155 s, with all 1,720 cases passing (experiment E2).
Item 9 already allows a verified exact partition; until now it waited for the
T4b activation.

### Decision

1. **Item 9 in force for the local verifier.** From this change, every local
   run of the complete .NET coverage inventory (`python scripts/verify.py
   --all`, including its callers such as `main-package.yml`, and the other
   local runs of that inventory) runs UiSmoke as a verified exact partition of
   three parts, ahead of T4b. T4b's "the partition rule of item 9 in force"
   then concerns the CI producers only; they still run each project
   unfiltered. Release-Golden subsets and the CI shard, finalizer and
   required-check paths are unchanged, as are the coverage policy and the
   Infrastructure settings and exclusivity.
2. **Declaration, identity and filter.** `scripts/verify.py` declares the
   partition (`DOTNET_TEST_PARTITIONS`): explicit type lists for parts 1 and 2;
   part 3 is every discovered type not listed, so new types land there. A
   listed type that is not discovered is reported as stale; a declared type
   that differs from a discovered type only by case fails. Grammar G applies to
   each real `FullyQualifiedName` F that VSTest lists with
   `--ListFullyQualifiedTests` (the value the filter evaluates): F is the
   declared namespace, one type segment and one method segment, each an ASCII
   identifier, with no sub-namespace, nested (`+`) or generic type; types and
   names are unique under ASCII casefold. The set of F must equal the set of
   method identities derived from the `--ListTests` display names, unfiltered
   and for each part, so no display name can carry a type outside G into a
   part. Each listed part filters on `FullyQualifiedName~NS.T.` terms and the
   last part negates every listed term; under G such a term selects exactly its
   type.
3. **One evidence path.** The local collector is the only finalizer and
   `require_exact_partition` the only checker. Before any part runs tests: the
   declared part set is present, each part's real filtered discovery equals the
   declaration applied to the unfiltered discovery (display counts with theory
   multiplicity, and FQN sets), and the parts are non-empty, pairwise disjoint
   and sum to the unfiltered discovery. After the parts run: each part's TRX
   equals its own filtered discovery with no failure or unapproved skip, each
   TRX case binds through its `testId` to exactly one definition whose test
   method is that case's identity in the part (a missing or ambiguous
   definition fails), and each part's test assembly hash equals the snapshot
   manifest's. Listings must be fresh: a listing file that exists before its
   discovery runs fails. A missing, empty or unparsable listing, a failed
   command and any failed check fail the coverage lane; there is no projection,
   partial acceptance or unpartitioned retry. `partition.json` records the
   declaration hash, the assembly hash, the per-part counts and the verdict.
4. **Trees and freshness.** Each part runs from its own complete, link-free,
   hash-verified copy of the Release output (Coverlet instruments modules in
   place), with its own results directory, TRX and coverage pair. The
   freshness gate checks every part copy once, after all batches and
   collectors.
5. **Execution.** The three parts run together, exclusive of the other
   projects, each with the existing 600-second project limit; a failing part
   does not stop the others, but the project fails and no coverage verdict
   passes. Cancellation and deadlines use the existing lane path.
6. **Caller-set overrides.** The partitioned lane fails before discovery when
   `NFC_VISUAL_OUTPUT_DIR`, `NFC_UI_REFERENCE_CAPTURE_DIR` or
   `NFC_REPORT_VISUAL_INPUT` is present, even empty or blank. The partition
   unit is the compiled type, so each fixed test-area evidence directory, which
   has exactly one writer type, is written by one part only; a repository
   script test checks this over the UiSmoke sources. Worktrees that share one
   test area still share those fixed files, as before this change.
7. **SDK and listing options.** The two FQN listing options are accepted by
   VSTest 18.6.0 but are not in its help. `global.json` pins 10.0.301 with
   `rollForward: latestPatch`, so a newer patch SDK can run (10.0.303 on the
   measuring host); the partition evidence records the SDK and VSTest versions
   of each run. Compatibility rests on the fail-closed checks of every run, not
   on an exact SDK pin; an SDK or adapter update re-checks the listing contract.
8. **Shards.** Item 9's "a shard is added only after the UiSmoke measurement
   (U0) and shared builds (U4)" governs CI runners, not local parts.
9. **Item 10 correction (board decision 113).** U0 showed that every Avalonia
   UiSmoke test already runs serially on one headless UI thread, so removing
   the UiSmoke serialization (U2a) gains about nothing; board decision 114
   closed U2a as a measurement result. The next levers are the product hotspot
   of board decision 112 and multi-process UiSmoke (U5), whose local form is
   this partition. This replaces item 10's "U2a as the first UI split after
   U0", and T5's "U5 if decided" reads "U5: local form decided (board decision
   128); CI with G2".

### Consequences

- The local UiSmoke phase measured about 155 s instead of 331 s (E2, without
  coverage); three instrumented test processes and three copies of the Release
  output (about 1.8 GB) raise CPU, memory and disk use during that phase.
- A rename of a listed type moves it to part 3 and reports the old name as
  stale; the balance drifts toward part 3 until the lists are updated.
- G2 may reuse the same declaration and checker for CI; the CI manifest then
  gains per-part rows. Until then CI stays unfiltered.

### Verification

`tests/scripts/test_verify_orchestration.py` covers grammar G and the FQN
binding (including a hidden sub-namespace type and a nested type behind legal
display names, and swapped display names caught by the per-case TRX binding),
the filter terms, every negative of the checker, the collector wiring
(concurrent exclusive parts with their own trees, 600 s each, a failing part
not stopping the others, one coverage verdict, freshness after all batches),
the rejected variables and the writer scan. The complete run and its
timings are recorded by the batch's acceptance runs (board decision 130).
