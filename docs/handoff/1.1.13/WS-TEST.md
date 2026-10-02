# WS-TEST: test architecture (1.1.13)

Owner: Claude Code (Opus 5.5), design drafter. Board:
[1.1.13 board](../1.1.13.md). Protocol: [handoff README](../README.md).
Deliverables: the [test architecture ADR draft](ADR-DRAFT-test-architecture.md)
(ADR 0079) and the [pilot split plan](PLAN-test-pilot-split.md), both revised
after the [independent design review](#design-review-2026-09-26). The owner
decided D-1 to D-8 on 2026-09-26 (board decisions 69 to 76,
[owner decisions](#owner-decisions-in-risk-order)); the 1.1.13 work they
schedule is planned under [1.1.13 batch plans](#1113-batch-plans-board-decision-73).
The commander's to-dos are listed under
[coordination](#to-dos-for-the-commander).

## Dispatch envelope (commander, 2026-09-26)

- **Outcome.** A test architecture ADR draft, number `00XX` (the board
  allocated 0079 later the same day), and one pilot
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
| Outcome 1: ADR with feature-scoped classes, shared builders in `TestSupport`, one category trait per test (Unit, Integration, UiHeadless, Golden, Governance), naming and size guidance | ADR items 2 and 7. Changed: no trait on every test; execution categories reuse projects and collections, and a trait is added only where a runner filters (D-8, board decision 76). Golden is a membership owned by the canonical manifest, not an execution category. "Governance" becomes `structure` and `document`. |
| Outcome 2: path-to-category map feeding tiered CI and the `ui` shard split | ADR items 3 to 6 (selection, coverage, declared inputs, shadow mode), 9 (partitions) and 10 (UiSmoke). The unit of selection is the test group (project), not the category. |
| Outcome 3: pilot split of `ShellViewModelTests` with timing and identical counts | Pilot changed to `RepositoryBoundaryTests` ([plan](PLAN-test-pilot-split.md)). The 1.1.12 measurement (86 files, 24,746 lines) counted files by name prefix; commit `acc6ea039` had already split that class into twelve group classes in 2026-08. |
| Outcome 4: review of architecture tests that assert document prose | Board decision 71 (D-4) and the pilot's `RepositoryDocumentTests`; the move into the structure lane belongs to WS-GOV G2 (F-4). |
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
   577 s. The clause stands until ADR 0079 is accepted, which replaces it
   (board decision 72); past overruns do not void it.
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
  skip `architecture` (item 3), coverage per decision 69 (item 4), and the shadow-mode
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
   The commander forwards it with WS-GOV's next revision.
2. **WS-GOV G2:** replace the `prose` wording ("no test or script reads") with
   the joint definition of ADR item 3, and change the authority checker and the
   list of document checks to migrate in the same batch (review F-4). The
   commander forwards it with WS-GOV's next revision.
3. **WS-GOV G1-A or G1-B, optional:** a pull request field "adds or changes a
   run-time read of a repository file; declared in the selection map" (ADR
   item 5).
4. **CI evidence owner:** after `CI-FAILURE-EVIDENCE-1113-01` merges, plan U1's
   hang detection (VSTest blame, dump type `none`) as an R3 sub-item of that
   contract, with allowlist, manifest, finalizer and negative tests; no memory
   dumps in artifacts (ADR 0027) (review F-6).
5. **Bug ledger:** decide whether any of the four discrepancies above becomes
   a bug file; the UI handoff document at 2,499 lines is the most urgent.
6. **Owner interview:** done; D-1 to D-8 were decided on 2026-09-26 (board
   decisions 69 to 76).
7. **U0 go:** pending. P-0.5 has finished, but the machine is not quiet yet;
   the commander reports the window, which U0 does not share with the ADR 0077
   Step 0 measurement.
8. **Pilot scheduling:** decided. T2a starts from the batch 2a head after its
   freeze; `Roadmap.cs` stays in the residual class; `CliStructure.cs` and
   `BootstrapCliConvergence.cs` are rechecked at the branch point
   ([pilot plan](PLAN-test-pilot-split.md#files-to-avoid)).
9. **T1 design review and owner acceptance:** the commander has sent the
   design review of the ADR text and the record to `codex/gpt-6-astra`; after
   it passes, the owner accepts the text explicitly before the implementation
   commit sets Accepted.

## 1.1.13 batch plans (board decision 73)

Board decision 73 puts T1, T2a, T2b and U0 into 1.1.13; T3 joins only if WS-GOV
G1-A lands in 1.1.13, and T4b has no version deadline. The commander decided
the open points of these plans on 2026-09-26 (recorded in the checkpoint of that
date). None of the batches has started; T1 exists only as an uncommitted draft.

### T1: ADR 0079 into `docs/adr/` (R2 admission)

- **Outcome.** `docs/adr/0079-test-architecture.md`, carrying this workstream's
  reviewed design and decisions 69 to 76 without the handoff-only material;
  the reciprocal "Amended by" entry in ADR 0027's header; one pointer paragraph
  in `tests/README.md`. `tests/AGENTS.md` is not part of T1 (commander). No
  executable, CI, test or product file changes: the rules take effect through
  T2 to T5.
- **Status and effective point** (commander). The ADR stays **Proposed**:
  decisions 69 to 76 answered its design questions and are not the owner's
  acceptance of the text. After the design review the owner accepts the text
  explicitly (as decision 46 did for ADR 0077); only then does the
  implementation commit set Accepted. It takes effect when
  `TEST-ARCH-ADR-1113-01` is integrated after that acceptance.
- **Batch and base** (commander). T1 is in batch 2b, after batch 2a; its
  `integrationBase` is the latest sealed evidence checkpoint once batch 2a has
  merged (the draft carries `84b084dd8` provisionally). Branch
  `feature/1.1.13/test-architecture-adr` from the trunk at that checkpoint,
  rebased, never merged, if the checkpoint moves again.
- **Mode** (commander). A capability-reuse record under the current rules,
  because WS-GOV G1-B has not landed.
- **Record draft**, `TEST-ARCH-ADR-1113-01` (schema 2), kept outside the
  repository until the admission is staged, so that the validator never sees
  an unstaged record:

| Field | Draft value |
| --- | --- |
| `taskId` | `TEST-ARCH-ADR-1113-01` |
| `capability` | Accept ADR 0079 (test architecture): test groups, execution categories and Golden membership, the path-selection, coverage and shadow-mode rules for the later selector, class-size, split and stability rules, and the amendment of ADR 0027's 2026-08-12 section (verified exact partitions; the 300-second stop rule replaced by measured targets); decision text, the ADR 0027 header line and one README pointer only |
| `integrationBase` | the latest sealed evidence checkpoint after batch 2a merges |
| `risk`, `kind` | `R2`, `governance` |
| `state` | `design-active`; `final-complete` in the evidence commit that is the reviewed head's only child |
| `mutablePaths` | `docs/adr/0027-evidence-preserving-performance-remediation.md`, `docs/adr/0079-test-architecture.md`, `tests/README.md` (non-governed auxiliary evidence) |
| `implementationOwner` | `claude-code` |
| `searchEvidence` | six entries: ADR 0027's 2026-08-12 amendment is the only owner of the CI test partition; no ADR or contract owns test categories, path selection, coverage on selected runs, class size or split rules; `scripts/verify.py` owns the test inventory; the canonical manifest owns Golden membership; the coverage policy owns the coverage verdict; board decisions 69 to 76 and the ADR number allocation |
| `semanticOwner` | ADR 0079 owns test selection, categories, split and stability rules; ADR 0027 keeps the CI sharding contract as amended; `scripts/verify.py` keeps the inventory; the canonical manifest keeps Golden membership; the coverage policy keeps the verdict; the authority map (ADR 0080) keeps authority classes; `tests/AGENTS.md` keeps per-test instructions |
| `terminalContract` | ADR 0079 Proposed during the design review and set to Accepted in the implementation commit only after the owner's explicit acceptance; in force from the integration of this admission after that acceptance; measured targets filled at the final review with no placeholder left; the reciprocal ADR 0027 line; the README pointer; no other file |
| `disposition` | `extend-owner` |
| `designReview` | `blocked` until the design review of the ADR text and the record (arranged by the commander, `codex/gpt-6-astra`) |
| `implementationHead`, `reviewedHead`, `pathStateDigest` | null until finalization |
| `finalReview` | `pending`; then an independent reviewer at the exact head |

- **Measured targets** (commander). Taken at the T1 final review from the 60
  most recent `ci.yml` runs (final attempts, successful jobs), median and
  about p90 for the whole run and the `ui`, `core`, `bootstrap` and finalizer
  jobs. Reference values only, with no obligation attached. The draft states the
  method and leaves the cells as placeholders.
- **Review focus at the exact head.**
  1. Fidelity: the ADR equals the reviewed design plus decisions 69 to 76,
     each cited by number, with no new rule.
  2. The ADR 0027 amendment changes only the stated clauses (unfiltered
     projects become verified exact partitions; the 300-second rule is
     replaced when ADR 0079 takes effect), the reciprocal line follows the ADR
     lifecycle rules, and nothing says the old rule had lapsed.
  3. The measured targets are filled, dated and sourced, and carry no
     obligation.
  4. No over-claim: every part that needs T3, T4 or G2 is marked decided but
     not yet in force; no firmware, release, approval or required-check
     authority changes.
  5. Consistency with the ADR 0080 draft: the joint prose definition, the
     authority classes and G2's role; whichever ADR is accepted second cites
     the first.
  6. Self-contained: links resolve from `docs/adr/`, with no link to handoff
     drafts.
  7. The record: exact paths, the latest checkpoint as base, R2 governance, an
     independent reviewer.
- **Steps.** Design review (arranged, ADR text and record) -> owner's explicit
  acceptance of the text -> branch from the trunk after batch 2a -> refresh the
  CI window read-only for the measured targets -> admission commit (record
  staged as `design-active`, re-pinned base) -> implementation commit (ADR 0079
  with status Accepted, the ADR 0027 line, the README pointer) -> checks ->
  exact-head review -> evidence commit (`final-complete`) -> batch 2b.
- **Checks.** With the test-area setup: `python scripts/verify.py
  --structure-only` (governed documents, the record, links) and
  Architecture.Tests (the board's rule for document edits; the line ceiling
  and the roadmap checks).
- **Draft now** (uncommitted, not staged): the three repository changes in
  `<worktrees>/wstest`, and, outside the repository in the folder `wstest-t1`
  of the agent's scratch area, `TEST-ARCH-ADR-1113-01.record-draft.json` (SHA-256
  `c424f26e260b9b09f2c225757868cc8a3f93c9b88a0ee952d6fb66e9448c1378`) and
  `TEST-ARCH-ADR-1113-01.proposal.patch` (SHA-256
  `ac67dc2d970021069bb8e14a2ed9e105799527ffdcbd2d6b716845358b6bb3d7`; three
  files, 776 insertions, 1 deletion; applies cleanly to `a19300b62`). The first
  version, `proposal.v1.patch` (SHA-256
  `f8ca5dfce6e04b910fff8896ed6e427e742d3ece49d6763d7d3bec870da541d5`), differs
  only in the status paragraph and the decision 72 effective sentence, which
  said Accepted before the commander's correction.

### T2a: mechanical split pilot (R1)

The [pilot split plan](PLAN-test-pilot-split.md) is the executable plan. In
short:

- **Base** (commander). T2a starts after batch 2a is frozen, from the batch
  head, so that it needs no rebase later.
- **List.** Nine target classes, one static support class and a residual
  `RepositoryBoundaryTests`, covering all 78 files once; recomputed for the
  trunk head `9b2a7369e`, whose Architecture test files equal `cf4e42697`, and
  recomputed again at the batch 2a head.
- **Files to avoid.** `WorkbenchStructure.cs` (nvt-marker; the C-7 worktree),
  `JsonSchemaConcurrency.cs` (VersionManagement JSON),
  `PresentationStructure.cs` and `DesktopHostConvergence.cs` (roadmap links)
  and `Roadmap.cs` (commander: kept in the residual class while the old
  `feature/1.1.x/roadmap-and-agent-workflow` branch is unconfirmed) stay in the
  residual class, and the two files that nvt-marker and VersionManagement JSON
  add join it when they merge. F08's UiSmoke files, the C-7 proposal's other
  test files and the CLI test files are outside Architecture.Tests, so the
  pilot does not overlap them; `CliStructure.cs` and
  `BootstrapCliConvergence.cs` are rechecked at the branch point.
- **Evidence.** E1 discovery mapping, E2 outcomes, E3 mechanical diff, E4
  canary, E5 timing (no change expected for T2a), E6 CI, E7 class, fixture and
  shared-state review.
- **Mode.** R1, tests only, no capability-reuse record; an admission entry in
  this log, an exact-head review by the other runtime, its own pull request.
  T2b (removing the serialization) follows as a separate pull request after
  T2a merges.
- **Suggested staffing.** A clear-spec R1 task for one implementer, reviewed by
  the other runtime; the commander decides.

### U0: UiSmoke measurement (waits for the commander's go)

- **Trigger** (commander). Rolling parity P-0.5 has finished, but other agents
  still build and test; U0 starts only when the commander reports a quiet
  machine. The ADR 0077 Step 0 measurement waits for the same kind of window,
  and the two never run at the same time.
- **Quiet machine.** Before each run, list the build, test and verifier
  processes of other agents (none may run) and record the list with the
  timing.
- **Runs**, at the trunk head with the test-area setup:
  1. A Release build of `tests/NvtFwCombiner.UiSmoke.Tests` with build servers
     disabled.
  2. Three runs without coverage (`dotnet vstest` on the UiSmoke assembly with
     a TRX logger and a results directory under
     `<test-area>/evidence/wstest-u0/<sha>`), with wall-clock time.
  3. One run with the CI coverage settings (the repository-pinned Coverlet
     adapter, `XPlat Code Coverage`, format `json,cobertura`) to size the
     coverage overhead.
  4. Optional, with the owner's approval of the download: the TRX and shard
     log of a green CI `ui` shard (kept three days), for CI-hardware class
     times and the restore, build, discovery and execution split.
- **Analysis.** Per class: tests, summed duration and collection; per
  collection: summed duration (the `UiAvaloniaRuntime` sum bounds wall-clock
  from below); the top 20 tests; the share of the U2a candidate files;
  run-to-run spread; the coverage overhead.
- **Record.** Machine (logical processors, memory), SDK, commit, the
  quiet-machine evidence and the tables in this log; raw files stay outside
  Git, cited by hash.
- **Cost and risk.** About 45 minutes of machine time; R0 (a measurement and
  a log entry).
- **Feeds.** U2a's file choice, U3 and U5, and later duration targets for ADR
  0079. The T1 targets come from the CI window, so T1 does not wait for U0.

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

### 2026-09-26 Owner decisions recorded; ADR number; 1.1.13 batch plans
State: local
Commits: the commit carrying this entry, on `20dbfcdc3` (the ADR draft, the
pilot plan and this log)
Evidence: board decisions 69 to 76 (`docs/handoff/1.1.12.md` on
`feature/1.1.13/wave2` at `dbc96c268`) are recorded below with their numbers,
the options not taken marked rejected; the ADR draft carries its allocated
number 0079 and cites the decisions where they apply; the pilot plan is
recomputed for the trunk head `9b2a7369e` and lists the files to avoid from
the committed branches and worktrees seen on 2026-09-26; the 1.1.13 batch
plans for T1, T2a and U0 are above. Nothing was implemented. Checks, with the
test-area setup and no other build or test process running: the
Architecture.Tests line ceiling test (`--filter
FullyQualifiedName~RepositoryTextFilesStayBelowEmergencyCeiling`) -> 1 passed
with the updated documents (ADR 745 lines, plan 281, log 553); a local link,
anchor, table, line-ending and private-string check and `git diff --check`
passed.
Open: commander to-dos 1 to 5, 7 and 8.
Next: stop; U0 waits for the commander's go.

### 2026-09-26 T1 draft prepared; commander decisions recorded
State: local (the T1 draft is uncommitted)
Commits: `9b6e9632f` (the handoff ADR draft marked as moved to ADR 0079); the
commit carrying this entry (this log and the pilot plan). The T1 draft
(`docs/adr/0079-test-architecture.md`, the ADR 0027 header line and the
`tests/README.md` pointer) stays uncommitted and unstaged in
`<worktrees>/wstest`, as the commander directed.
Evidence: the commander decided on 2026-09-26: T1 through a capability-reuse
record under the current rules, without `tests/AGENTS.md`, in batch 2b with the
checkpoint after batch 2a as base; the ADR stays Proposed until the owner
accepts the text after the design review, and takes effect when
`TEST-ARCH-ADR-1113-01` is integrated after that acceptance; measured targets
from 60 runs (final attempts, successful jobs, median and about p90), reference
only; the design review of the ADR text and the record sent to
`codex/gpt-6-astra`; T2a from the batch 2a head after its freeze, `Roadmap.cs`
in the residual class, the CLI structure files rechecked at the branch point;
U0 after the commander's go and never together with the ADR 0077 Step 0
measurement; WS-GOV to-dos 1 and 2 forwarded with WS-GOV's next revision.
T1 draft checks: the patch (SHA-256 `ac67dc2d…`, see the T1 plan) applies
cleanly to `a19300b62` and touches only the three paths; a local link and
anchor check of the three files passed (43 links); the files stay below the
line ceiling (ADR 0079 767 lines, `tests/README.md` 1,629, ADR 0027 390); the
record draft parses with allowed values. Not run: `verify.py --structure-only`
(it fails by design while governed paths change without a staged record) and
Architecture.Tests (other agents were running UiSmoke and Bootstrap tests);
both run in the T1 admission.
Open: the T1 design review result (commander); the owner's acceptance of the
ADR text; the U0 go.
Next: stop until the review result.

### 2026-10-02 T2a implemented, reviewed and merged
State: pull request #519 (`feature/1.2.11/t2a-boundary-split`, head `c169d87b9`, base `092641516`) merged into the
trunk as `1ab3319c0`, delivered early under board decision 256 on the 1.2.x board; the item (R33-01) keeps its
place in `1.2.11`.
Admission, recorded after the commit instead of before it: authority board decisions 73 and 74 and ADR 0079 rules
S1 to S10; paths `tests/NvtFwCombiner.Architecture.Tests/**` only; narrow test Architecture.Tests; implemented by
Codex `gpt-6.1-sol`, built and run by the commander, reviewed by Claude Opus 5.5 at the exact head (accept, no P0 or
P1).
Evidence at `c169d87b9` against `092641516`:
- E1: 277 discovered cases on both sides; identities identical once the class name is removed.
- E2: 277 passed, 0 failed, 0 skipped on both sides.
- E3: 75 changed paths, all in the test project; only the line kinds the plan allows.
- E4: three canaries, each exactly one failure in the expected moved test.
- E5: three interleaved runs per side on a machine that was also running other builds: wall-clock median 15.9 s
  before and 16.3 s after; sum of test durations 13.3 s before and 14.1 s after. No speed change, as expected.
- E7: one serial collection attribute per class; no `DisableParallelization`, fixture or lifetime change.
Classes after the split (files, cases): Application 19, 33; BootstrapCli 3, 25; CanonicalCatalog 15, 45;
HostInfrastructure 6, 28; PackageTrust 2, 25; Presentation 11, 34; Profile 6, 23; RepositoryDocument 1, 1;
Retirement 7, 19; the remainder 10, 34; `ProjectDependencyTests` untouched, 10. The 82 files are these 80 plus the
two support files.
Differences from the plan, all accepted by the review:
- the mapping was recomputed at the branch point: 82 files, not the 78 measured for the plan. The remainder holds
  10 files, not six: `LocalStateIsolation` and `PrebuiltProfileCatalog` were added later and, by the plan's rule
  for late files, stay in the remainder;
- one commit instead of the two the plan names (support class, then moves); E1 to E7 are defined from the base to
  the head, so the evidence is unaffected;
- `AssertStartupStageOrder` moved to the support class although the plan does not list it: two classes use it
  (rule S3);
- a few separator blank lines stayed where helpers were removed; they go when those files next change;
- `docs/governance/v0.10.5-preload-baseline-and-ticket-ledger.md` and about 20 closed change records still name
  old file paths. A moved test pins that document's text, so it cannot change in T2a; the document owner
  decides later.
E6: the pull request's CI passed on `c169d87b9` (the `core` shard and the other required checks) and it merged
without a second attempt.
Open: nothing for T2a.
Next: T2b (de-serialization) and R33-03 (name cleanup) can be scheduled.

## Owner decisions in risk order

Decided on 2026-09-26, each as the independent review recommended (board
decisions 69 to 76, `docs/handoff/1.1.12.md` on `feature/1.1.13/wave2` at
`dbc96c268`, which holds the authoritative text). The questions stay below in
plain words, in the review's risk order, with the options not taken marked
rejected.

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
- **A. Yes** (rejected). Coverage only on full runs; product pull requests
  would get faster, but a coverage drop would be found after merge. Not a
  fallback if B is late.
- **C. No change at all** (rejected). Every pull request runs all eight
  projects with coverage, even documentation-only ones.

**Decided 2026-09-26 (board decision 69): B, revised.** When completeness
cannot be shown, the pull request runs full coverage; today's rule stays
until B exists; A is not a fallback. Applied in ADR item 4.

### 2. D-2: When can CI trust the new test selection?

Before trusting it, CI runs everything and only records what the selection
would have skipped (shadow mode).

- **A, with exit conditions.** At least 20 distinct pull requests with
  complete full-run evidence over at least two weeks; no miss; no open
  `unresolved` failure; every run attempt kept; a narrowing change restarts
  the count; the risky-case checklist passes; then the owner switches it on
  explicitly, and a later miss returns CI to the full suite.
- **B. No shadow mode** (rejected).
- **C. A whole release cycle** of shadow mode (rejected).

**Decided 2026-09-26 (board decision 70): A with the exit conditions.**
Applied in ADR item 6.

### 3. D-4: Which document checks really protect the specification?

Some Architecture tests assert exact sentences or figures in documents (for
example the AB progress figures in the supported IC matrix, sentences in ADRs,
sections of the roadmap). Any wording change breaks them.

- **A. Decide test by test with the owner.** Keep every check that protects a
  fact, as a structural check or mapped to its documents; delete only checks
  the owner confirms freeze wording and nothing else.
- **B. Move all of them unchanged** into the structure lane (rejected).
- **C. Leave them in Architecture.Tests**, mapped to the documents they read
  (rejected).

**Decided 2026-09-26 (board decision 71): A.** Applied in ADR item 3; the
test-by-test list is prepared when WS-GOV G2 moves the document checks.

### 4. D-5: Should ADR 0027's 300-second stop rule be formally replaced?

In August 2026 the decision to split .NET CI over several runners said it
would stop, rather than add shards, if a run took longer than 300 seconds. The
median successful run now takes about 9.6 minutes (577 s). That does not by
itself cancel the rule.

- **A. Replace it explicitly** in this ADR, effective when the ADR is
  accepted, with measured targets. Adding a shard still needs the UiSmoke
  measurement (U0) and shared builds (U4) first. Until acceptance the rule
  stands.
- **B. Keep it** (rejected). No extra shards; UiSmoke could only get faster
  inside the project.

**Decided 2026-09-26 (board decision 72): A.** Applied in ADR item 9; T1
records the measured targets.

### 5. D-7: How far does 1.1.13 commit?

- **A. In 1.1.13:** the ADR (T1), the pilot (T2a and T2b) and the UiSmoke
  measurement (U0). The selector (T3) joins only if WS-GOV G1-A lands in
  1.1.13. Switching CI to the selection (T4b) has no version deadline.
- **B. Everything in 1.1.14** (rejected).

**Decided 2026-09-26 (board decision 73): A.** Planned under
[1.1.13 batch plans](#1113-batch-plans-board-decision-73); the roadmap owns the
allocation text.

### 6. D-6: Where is the split procedure proven first?

- **A. `RepositoryBoundaryTests`** (Architecture.Tests), with the class,
  fixture and shared-state review, the file list recomputed at the real branch
  point, and active write locks respected.
- **B. A UI class** such as `XamlControlStyleContractTests` (rejected as the
  first pilot; the UI split U2a remains the planned second split).
- **C. No pilot** before the UiSmoke measurement (rejected).

**Decided 2026-09-26 (board decision 74): A.** Applied in the
[pilot plan](PLAN-test-pilot-split.md).

### 7. D-3: Should large test classes be blocked from growing?

- **A. Same rule for test classes.** New tests go into a new feature-scoped
  class, which keeps the isolation and collection rules of the tests it joins;
  serialization is never removed just to get under the limit.
- **B. Advisory report only** (rejected).
- **C. No size rule for tests** (rejected).

**Decided 2026-09-26 (board decision 75): A.** Applied in ADR item 7.

### 8. D-8: Label every test class with a category now?

- **A. Label only where a tool reads the label** (first: running process tests
  separately with hang detection, and splitting UiSmoke); Golden membership
  stays decided by the canonical manifest and is not an execution category.
- **B. Label every class now** (rejected).

**Decided 2026-09-26 (board decision 76): A.** Applied in ADR item 2.
