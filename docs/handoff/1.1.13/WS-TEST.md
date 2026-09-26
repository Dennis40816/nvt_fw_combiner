# WS-TEST: test architecture (1.1.13)

Owner: Claude Code (Opus 5.5), design drafter. Board:
[1.1.13 board](../1.1.13.md). Protocol: [handoff README](../README.md).
Deliverables: the [test architecture ADR draft](ADR-DRAFT-test-architecture.md)
and the [pilot split plan](PLAN-test-pilot-split.md), both revised after the
[independent design review](#design-review-2026-09-26). The commander's to-dos
are listed under [coordination](#to-dos-for-the-commander); the owner
decisions are at the end, in risk order and still pending
([owner decisions](#owner-decisions-in-risk-order)).

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
| Outcome 1: ADR with feature-scoped classes, shared builders in `TestSupport`, one category trait per test (Unit, Integration, UiHeadless, Golden, Governance), naming and size guidance | ADR items 2 and 7. Changed: no trait on every test; execution categories reuse projects and collections, and a trait is added only where a runner filters (D-8). Golden is a membership owned by the canonical manifest, not an execution category. "Governance" becomes `structure` and `document`. |
| Outcome 2: path-to-category map feeding tiered CI and the `ui` shard split | ADR items 3 to 6 (selection, coverage, declared inputs, shadow mode), 9 (partitions) and 10 (UiSmoke). The unit of selection is the test group (project), not the category. |
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

This table is a starting inventory, not a proof of completeness: ADR item 5
makes every new or changed run-time read update the declaration in the same
pull request (unbounded reads at their widest prefix, or the full suite), and
item 6's shadow mode and case matrix test it before activation.

No .NET test reads `.github/`, `.agents/`, `tools/` or `third-party/`; the
`AGENTS.md` files under `src/` and `tests/` are read only by the line ceiling.
Compile-time links that a project-reference graph alone misses,
all handled by row 6: `tests/NvtFwCombiner.Application.Tests/CompiledCompositionTestFactory.cs`
into Bootstrap.Tests; `tests/Shared/*.cs` into Bootstrap.Tests and
ProfileContract.Tests; six TestSupport files linked (not referenced) into
Architecture, Domain, ProfileContract, Application, Bootstrap and
Infrastructure.

Parallelism facts behind ADR items 2, 8 and 10: `ReadyProbeProcessSerialGroup`
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
   577 s. The clause stands until an accepted ADR replaces it; past overruns
   do not void it (D-5).
2. 147 files under `tests/` (134 of them with tests) declare no class matching
   their name prefix, including all 86 `ShellViewModelTests.*.cs` files. No
   rule is broken today; ADR item 7 adds one.
3. `NvtFwCombiner.Desktop` and `NvtFwCombiner.Launcher` are referenced by no
   test project. The selection runs the full suite for them (row 7) and reports
   the coverage gap each time; the fallback creates no tests. Whether they are
   intentionally outside unit coverage is for their owner.
4. The 1.1.12 WS-TEST measurement of `ShellViewModelTests` was wrong (see the
   port table); corrected here.

## Design review 2026-09-26

Reviewer `codex/gpt-6-astra`, implementation owner `claude-code`, range
`cf4e42697` to `87ac3d6c1`, compared with WS-GOV at `28bc715ba` and the CI
evidence change at `935eac2bc`; read-only (no build or test). Verdict:
**ACCEPT-WITH-CHANGES**, 0 P0, 3 P1, 3 P2. The scoped Polytail verdict was
FAIL because P1 findings were open, not because the approach was rejected. The
reviewer accepted the main choices: whole-group selection with categories that
only schedule, the union of base and head graphs, full runs for sensitive
inputs, `RepositoryBoundaryTests` as the first pilot (it proves the procedure,
not UI parallel safety), the batch order, and U0 before U5 with U6 deferred.

| Finding | Severity | Taken in |
| --- | --- | --- |
| F-1 no conservative fallback when dependencies cannot be fully resolved | P1 | ADR driver 2; item 3 resolution rules (graphs evaluated at base and head, build-only references kept, an external include reaches the including project's consumers, deleted projects resolved at the base, build-time copies need declared rows, "unproven means full" for unresolvable constructs and missing base or head objects); item 5 (a new or changed run-time read updates the declaration in the same pull request, a best-effort structure check, unbounded reads declared at their widest prefix or mapped to the full suite); item 6 negative cases, including deleting a whole project |
| F-2 D-1 B does not keep today's coverage protection | P1 | ADR item 4 (modules to check and required reports defined separately; test, fixture, data and scheduling changes covered; complete contributor sets; full coverage when completeness is unproven; full coverage stays until B exists; A only by the owner's explicit acceptance); D-1 rewritten |
| F-3 shadow-mode exit criteria insufficient | P1 | ADR item 6 (`unresolved` blocks activation; only distinct pull requests with complete full-run evidence count; all attempts kept; observations bound to selector and map versions, base, head, run and attempt; a narrowing change restarts the window; case matrix; explicit owner enablement; a later miss returns pull requests to the full suite); T4 split into T4a (shadow) and T4b (activation); D-2 rewritten |
| F-4 WS-GOV's prose definition stays false after the move | P2 | ADR item 3 joint definition (not an input to product tests or other semantic verification; listed generic document-structure checks allowed; documents read by topic tests keep their mapping); the WS-GOV wording, checker and migration list, and WS-GOV's citation of the ADR 0027 amendment, are commander to-dos 1 and 2 |
| F-5 split evidence does not cover execution semantics; U2a release evidence too weak | P2 | ADR rules S4 (mechanical move and de-serialization as separate changes), S9 (class, fixture and shared-state table) and S10; item 10 U2a; pilot T2a and T2b with E7 |
| F-6 re-run and hang-dump evidence not aligned with the CI evidence contract | P2 | ADR item 8 (a re-run never proves a fix; re-run artifacts follow the CI evidence contract and keep its pending real re-run gate; hang detection without memory dumps as an R3 sub-item with allowlist, manifest, finalizer and negative tests); pilot E6; commander to-do 4 |

Risk grading as the review states it: T0 R0; T1 and T3 R2; T2 (T2a and T2b)
R1; T4 (T4a and T4b) R3; any part of T3 that changes what a required check
accepts is R3. T2 may proceed independently of G1-A, T3 waits for the CI
evidence change and G1-A, and T4 goes with G2.

## Conflicts and coordination

- **CI failure evidence (R3, `CI-FAILURE-EVIDENCE-1113-01`).** It owns
  `.github/workflows/ci.yml` and `scripts/verify.py` until it merges; its
  implementation commit is `935eac2bc` on `feature/1.1.13/ci-evidence`.
  WS-TEST edits neither file before then. T3 and T4 build on its evidence
  model: attempt-named artifacts, the finalizer verifying each producer's
  newest attempt (pending verification by a real re-run; until then a failed
  run is followed by a new workflow run), failing projects' evidence and
  failing test names. WS-TEST keeps that pending gate and adds no re-run rule
  of its own. U1's hang detection changes its producer allowlist and
  finalizer, so it is an R3 sub-item of that contract. The U0 measurement uses
  the TRX files it keeps.
- **WS-GOV G1-A.** The selector reads the authority map's `prose` and
  `unclassified` classes and never redefines them. For documents, the map
  decides cost: a document outside every class runs the full suite, so the map
  should classify all of `docs/**` explicitly, and a document that a test reads
  must not be `prose` (checked by T3 for the declared list).
- **WS-GOV G1-B.** One writer for `development-execution-workflow.md` (the
  narrow-test table becomes a pointer to the selector) and, with WS-AI, for
  `AGENTS.md` files including `tests/AGENTS.md`.
- **WS-GOV G2.** Carries T4a and T4b. It needs from this ADR: the ADR 0027
  amendment (item 9), the joint prose definition and the move of the generic
  document checks, including the 2,500-line ceiling, before prose-only changes
  skip `architecture` (item 3), coverage per D-1 (item 4), and the shadow-mode
  records and exit criteria (item 6).
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

### To-dos for the commander

These need a writer outside this workstream's lock (`docs/handoff/1.1.13/`);
no WS-GOV or CI evidence file was changed here.

1. **WS-GOV governance draft:** cite this ADR's amendment of ADR 0027's
   2026-08-12 section (item 9) in its "Amends" list and in G2 (review F-4).
2. **WS-GOV G2:** replace the `prose` wording ("no test or script reads") with
   the joint definition of ADR item 3, and change the authority checker and the
   list of document checks to migrate in the same batch (review F-4).
3. **WS-GOV G1-A or G1-B, optional:** a pull request field "adds or changes a
   run-time read of a repository file; declared in the selection map" (ADR
   item 5).
4. **CI evidence owner:** after `CI-FAILURE-EVIDENCE-1113-01` merges, plan U1's
   hang detection (VSTest blame, dump type `none`) as an R3 sub-item of that
   contract, with allowlist, manifest, finalizer and negative tests; no memory
   dumps in artifacts (ADR 0027) (review F-6).
5. **Bug ledger:** decide whether any of the four discrepancies above becomes
   a bug file; the UI handoff document at 2,499 lines is the most urgent.
6. **Owner interview:** D-1 to D-8 below, all pending.

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

### 2026-09-26 Revision after the independent design review
State: local
Commits: the commit carrying this entry, on `87ac3d6c1` (the ADR draft, the
pilot plan and this log)
Evidence: every finding F-1 to F-6 is taken in (see the review table); the
owner decisions are reordered and rewritten to the review's risk order and
recommendations, and remain pending; no WS-GOV or CI evidence file changed.
With TEMP, TMP and TMPDIR set to `<test-area>/temp`, no other test run active:
`dotnet test tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj --filter FullyQualifiedName~RepositoryTextFilesStayBelowEmergencyCeiling`
-> 1 passed with the revised documents in the working tree (ADR 715 lines,
plan 253, log 409). A local link, anchor, table and private-string check of the
three documents and `git diff --check` passed.
Open: commander to-dos 1 to 6; owner decisions D-1 to D-8 (the commander
asks).
Next: stop.

## Owner decisions in risk order

All pending; the commander asks the owner. One question at a time, highest
risk first, in the order and with the recommendations of the independent
review. Each item gives the question in plain words, the options with their
consequences, and a recommendation.

### 1. D-1: May some coverage drops show up only after a merge?

Today every pull request measures coverage over all eight test projects and
fails if it drops below the stored baseline. Once pull requests run only the
tests their changes can affect, that whole-repository number cannot be computed
on them. Coverage can drop without touching the module that loses it: by
removing tests, changing a fixture, or changing code in a higher layer.

- **B (revised). No.** Each pull request checks every product module its
  selected tests run, using every test project that runs that module. In this
  repository that means a pull request touching product code or .NET tests
  still runs the complete .NET suite with coverage; only pull requests that
  touch just documents, scripts or tools skip it. Until B is built, today's
  rule stays exactly as it is.
- **A. Yes.** Coverage only on full runs (release-branch cut, release pull
  request, push to `main`, weekly, manual). Product pull requests get faster,
  but a coverage drop is found after merge and fixed in a follow-up. This
  lowers today's protection: it needs your separate, explicit acceptance and
  is not a fallback if B is late.
- **C. No change at all.** Every pull request runs all eight projects with
  coverage, even documentation-only ones.

Recommendation: B; today's full coverage until B exists.

### 2. D-2: When can CI trust the new test selection?

Before trusting it, CI runs everything and only records what the selection
would have skipped (shadow mode).

- **A, with exit conditions.** At least 20 different pull requests with
  complete full-run evidence, over at least two weeks; no miss; no open
  unexplained failure (an `unresolved` one blocks); every run attempt kept; a
  change that narrows the selection restarts the count; a checklist of risky
  cases passes (cross-layer changes, linked files, run-time inputs, deletions
  and renames, prose and unclassified paths). Then you switch it on
  explicitly. If a miss appears later, CI goes back to running everything until
  the map is fixed.
- **B. No shadow mode**; rely on the weekly and release full runs.
- **C. A whole release cycle** of shadow mode.

Recommendation: A with these conditions. Without enough cases, or with an
unexplained failure left open, shadow mode continues.

### 3. D-4: Which document checks really protect the specification?

Some Architecture tests assert exact sentences or figures in documents (for
example the AB progress figures in the supported IC matrix, sentences in ADRs,
sections of the roadmap). Any wording change breaks them.

- **A. Decide test by test with you.** Keep every check that protects a fact,
  as a structural check in the structure lane or mapped to its documents;
  delete only the ones you confirm freeze wording and nothing else. A check is
  not deletable just because it compares a sentence.
- **B. Move all of them unchanged** into the structure lane.
- **C. Leave them in Architecture.Tests**, mapped to the documents they read.

Recommendation: A, approved item by item.

### 4. D-5: Should ADR 0027's 300-second stop rule be formally replaced?

In August 2026 the decision to split .NET CI over several runners said it
would stop, rather than add shards, if a run took longer than 300 seconds. The
median successful run now takes about 9.6 minutes (577 s). That does not by
itself cancel the rule.

- **A. Replace it explicitly** in this ADR, effective when the ADR is
  accepted, with measured targets. Adding a shard still needs the UiSmoke
  measurement (U0) and shared builds (U4) first. Until acceptance the rule
  stands.
- **B. Keep it.** No extra shards; UiSmoke can only get faster inside the
  project.

Recommendation: A.

### 5. D-7: How far does 1.1.13 commit?

The roadmap owns allocation; this is the proposal.

- **A. In 1.1.13:** the ADR (T1), the pilot (T2a and T2b) and the UiSmoke
  measurement (U0). The selector (T3) joins only if WS-GOV G1-A lands in
  1.1.13. Switching CI to the selection (T4b) has no version deadline: it
  happens when its evidence is complete.
- **B. Everything in 1.1.14.**

Recommendation: A.

### 6. D-6: Where is the split procedure proven first?

- **A. `RepositoryBoundaryTests`** (Architecture.Tests), with the class,
  fixture and shared-state review, the file list recomputed at the real branch
  point, and active write locks respected ([plan](PLAN-test-pilot-split.md)).
- **B. A UI class** such as `XamlControlStyleContractTests`: the UI split
  (U2a) may save more time, but F08 and the navigation work edit these
  classes, removing their serialization needs stronger evidence, and the
  UiSmoke measurement should come first. Better as the second split.
- **C. No pilot** before the UiSmoke measurement.

Recommendation: A.

### 7. D-3: Should large test classes be blocked from growing?

For product code, WS-GOV proposes that a class of at least 2,000 nonblank
lines may not grow without your approval, and leaves the list below 1,500. The
same could apply to test classes (17 would be listed today).

- **A. Same rule for test classes.** New tests go into a new, feature-scoped
  class, so testing is never blocked. A new class keeps the isolation and
  collection rules of the tests it joins: serialization is never removed just
  to get under the limit.
- **B. Advisory report only.** Earlier advisory reports did not stop growth.
- **C. No size rule for tests.**

Recommendation: A.

### 8. D-8: Label every test class with a category now?

The 1.1.12 plan proposed one category label on every test.

- **A. Label only where a tool reads the label** (first: running process tests
  separately with hang detection, and splitting UiSmoke). Golden membership
  stays decided by the canonical manifest; a Golden test keeps the execution
  category of its class, so Golden is a membership, not a category.
- **B. Label every class now.** About 900 files touched, with no tool reading
  most labels.

Recommendation: A.
