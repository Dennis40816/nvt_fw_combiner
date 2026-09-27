# Rolling parity and the formal 1.x comparator: design

Status: **design draft, revised after the independent design review, with
the owner's decisions recorded**, 2026-09-26. Not implementation authority:
P-1's ADR and contracts carry that. Review: `codex/gpt-6-astra` over
`cf4e42697..bef3231ed`, ACCEPT-WITH-CHANGES (P1 F-1 and F-2; P2 F-3 to F-6);
how each finding was taken in is recorded in the [log](WS-PARITY.md). The
owner decided every question of section 11 on 2026-09-26 (board decisions 57
to 64); one approval is still to be asked: the 1.1.13 accepted gap for the 11
candidate routes without inputs. Board row: wave 3, "Rolling v0.9.16 parity
and the formal 1.x comparator, including the NT51950 TP-work Diff NF plan
correction" ([1.1.13 board](../1.1.13.md)). Written by Claude Code
(Opus 5.5) as designer; no script, workflow, contract, ADR, profile, Golden
or product file was changed.

Sources, read at this branch's base `cf4e42697` unless noted:
[ADR 0057](../../adr/0057-v0916-black-box-parity-certification.md), the plan
[contract](../../contracts/v0916-parity-certification-v1.md) and
[JSON](../../contracts/v0916-parity-certification-v1.json), its schema,
[`v0916_parity_certification.py`](../../../scripts/v0916_parity_certification.py),
the [baseline](../../contracts/v0916-baseline-executor-v1.json) and
[v1.0.0 candidate](../../contracts/v100-candidate-source-executor-v1.json)
executor contracts, the [workflow contract](../../contracts/v0916-parity-workflow-v1.json),
the [capability policy](../../contracts/canonical-capability-policy-v1.md)
(catalog 1.23.0), the [canonical Golden manifest](../../../testdata/golden/canonical/manifest.json),
[`CHANGELOG.md`](../../../CHANGELOG.md) 1.1.12, the 1.1.12
[WS-PARITY log](../1.1.12/WS-PARITY.md) and
[comparison table](../1.1.12/parity/v0916-local-comparison.md), the three
parity bugs cited in section 6, and [1.1.12 board](../1.1.12.md) decisions 10,
12 and 17. From other branches: decision 7b in
`feature/1.1.12/governance-reset:docs/handoff/1.1.12/WS-GOV.md` (`248726f1d`);
decisions 47, 48 and 57 to 64 in `feature/1.1.13/wave2:docs/handoff/1.1.12.md`
(`0cafaf768`, `affc576ea`, `4a106ec81`, `57a4c448d`); the WS-GOV drafts
`DESIGN-release-workflow-cleanup.md` (item 2, batch R-5, RO-1),
`ADR-DRAFT-governance-reset.md` and `WS-GOV.md` (review finding F-5) in
`feature/1.1.13/ws-gov` (`7700a7150`; R-5 row updated at `28bc715ba`).

## Summary

| Question | Answer (owner decisions cited; the rest is design) |
| --- | --- |
| 1. What is rolling parity | Every release candidate is compared with the previous stable release (decision 7b) on the same inputs, by complete primary-output bytes. The comparison starts with 39 scenarios over 37 routes: the 37 the 1.1.12 alignment ran plus the NT51950 Hiway and Display-OSD cases (decision 64). Coverage changes only through approved dispositions (decision 60), and the chain back to v0.9.16 holds only for scenarios and input revisions kept without a break. v0.9.16 is compared again only at milestones (decision 64). |
| 2. Differences | The owner, as firmware owner, approves every byte, acceptance and coverage change; each becomes a declaration entry with a CHANGELOG id (decisions 60, 64). The declaration and CHANGELOG are committed first; the report of record then comes from the exact release source under the CI clean-settings policy (decision 59). Anything undeclared, stale, invalid or undisposed blocks the release (decision 57). |
| 3. Boundary | ADR 0057 terminal certification is untouched (decision 47). A new comparator reuses the ADR 0057 Golden, input, custody, normalization, range and comparison owners; its versioned reader checks only report format and evidence. It never emits terminal evidence. A new ADR and contracts are needed; the release-workflow gate is WS-GOV R-5 (R3), which builds on decision 47 option C. 1.1.13 is gated procedurally (decision 61), with no automatic fallback (decision 58). |
| 4. NT51950 TP-work | Transcribe the difference decision 12 approved as one exact row in a separate 1.x amendment; the v1 plan stays unchanged. The cascade full-flash binding is recorded as not applicable to v0.9.16 (decision 62); the corrected v0.9.16 build recipe is approved and takes effect once P-0.5 has produced its evidence (decision 63). |
| 5. Batches | P-0 design, P-0.5 spike, P-1 ADR and contracts, P-2 comparator: R2 tooling and contract work. The approvals inside P-1 and P-3 are R3 (firmware owner; release owner for the executor authority and the release decision). R-5 (R3) belongs to WS-GOV. Section 12 lists what can start now. |
| 6. Not doing | Decision 12's fixed 27 routes stay not covered, and new gaps are not exempt (decision 60). No terminal certification and no Golden, profile, policy, plan or workflow change. Section 8. |

## 1. Facts that shape the design

F1. **Owner decisions.** 7b (2026-09-25): every release candidate is compared
with the previous stable release across all published routes, any difference
is declared in the release notes, and the cost is measured before
implementation; Golden stays the certified oracle; the check exists to catch
unintended changes on routes without Golden; 1.1.12 first aligns once with
v0.9.16 and 1.1.13 generalizes that into the rolling previous-release
baseline. Decision 10: the 1.1.12 alignment is non-certifying; the formal 1.x
comparator is 1.1.13 work. Decision 12: both NT5195x cascade Diff NF
differences are approved; the 27 routes without canonical input stay not
covered; the NT51950 cascade full-flash rejection is investigated and
corrected if needed. Decision 17: NT51950/51951 CtrlRAM Bases of any length.
Decision 47 (RO-1 option C): the terminal certification stays deferred to
2.0.0; the rolling parity is an extra check, not a replacement; of the 27
routes, 22 are contract-only, 4 synthetic-oracle and 1 has none (NT51950 AB
1024k, which has since left the policy). WS-GOV review F-5 (P1) carries the
same rule into the release design. Decisions 57 to 64 (2026-09-26) answer the
questions this design raised; section 11 maps them.

F2. **The v1 plan is historical authority.** It pins policy catalog 1.10.0
(`bf818a4c…`), the Golden snapshot at `1d1d1cfc` and the v1.0.0 candidate
executor (`dfaedec`); `compare` accepts only that candidate and stops with
`PARITY_FIXTURE_MISSING` before candidate admission (1.1.12 WS-PARITY, first
checkpoint). Against the current policy (catalog 1.23.0, `143c918a…`) every
one of the 62 plan routes still present has a new capability fingerprint, and
two left: NT51950 AB 512k became `nt51950-ab-merge-maps`; NT51950 AB 1024k is
gone, and the nearest current route is the candidate-publication
`nt51950-ab-cascade-maps`.

F3. **Active Golden coverage.** The manifest at `cf4e42697` (canonical tree
`3a8b162d`, the same as `v1.1.12`) binds a case to 33 of the 63 supported
routes (26 direct-golden, 7 approved-alias), each to the same case as in the
plan era. The four AB routes the 1.1.12 alignment compared (NT51919, NT51929,
NT51932 selector-free; NT51950 single) became contract-only at catalog 1.11.0
(v1.1.4, Dummy DP: earlier evidence "covers Normal inputs, not all new Dummy
selections"), but their Normal cases remain with expected outputs, as do
`nt51950-ab-hiway-d82t80` and the Display-OSD case
`nt51950-ab-osd-d03t02-20260924`. Every input of the 37 scenarios run in
1.1.12 is present in the active Golden with the same SHA-256.

F4. **The exact v1.1.12 release was never compared.** The 1.1.12 run used
`badc545b0` (`src` tree `3698fcec`); `v1.1.12` (`30b17e699`) has `src`
`a2fa6bba`. Eight later `src` commits (merges included) change input
classification (`c525698fa`, `38a360570`, `683f4c373`: nonstandard envelope
classification and warning) and UI code; profiles, external tools, Golden and
policy are identical. CHANGELOG 1.1.12 calls `badc545b0` "the 1.1.12 product
source".

F5. **The ADR 0057 report check does not fit 1.x reports (suspected).**
`_validate_raw_report` requires an empty `Issues` list and, for a candidate
report, exactly the 18 required members plus `MapId`. In the 1.1.12 run all 86
successful CtrlRAM workflow invocations, on both sides, reported one to three
issues, against none in the 52 Standard and AB invocations (issues carry
`info`, `warning` or `error`; the harness kept only counts), and current
reports add members such as `SourceEnvelope`, `AbMergeFormat` and
`InputDiagnostics` when present. Exit code 0 does not show that these issues
are harmless; P-0.5 reads their severities and codes. The terminal parser was
never exercised on these routes: the harness did not call it and the terminal
jobs run only for 2.0.0.

F6. **Executors.** The pinned v0.9.16 restore fails `NU1004`; the probe
(`--force-evaluate` restore, pinned build) reproduces the pinned CLI hash
`09321252…`, but its runtime closure, built from a `git archive` export,
differs by 7,168 bytes. 1.x sources restore with the 1.x recipe
(`--locked-mode --disable-parallel --runtime win-x64`) without lock changes
(1.1.11 and `badc545b0`). All three versions pin SDK 10.0.301 with
`latestPatch` (resolved 10.0.303 in both contracts).

F7. **The workflow contract freezes the release job set.**
`validate_protected_workflow_semantics` requires the jobs of `release.yml` to
equal `jobInventorySha256`, and `sync_derived.py --only v0916-workflow-contract`
refreshes only the digests of the `candidate`, `promote` and `published-smoke`
jobs and of the promotion steps. Any new release job, such as the WS-GOV
eligibility job or a comparison job, needs a reviewed extension of that
contract, which also moves the pinned workflow-contract digest in the v1 plan
and its schemas (`WORKFLOW_SYNC_INPUTS`).

F8. **The CLI reads per-user configuration** from the local application-data
folder (`NvtFwCombiner\toolchain-runtime.v1.json` and
`event-buffer-format.v1.json`; `CompositionHostServices`) through
`Environment.GetFolderPath`. On Windows that resolves the known folder through
the shell API rather than the `LOCALAPPDATA` variable, so an environment
override is not a reliable redirect, and the CLI has no option for it. The
toolchain file selects an external runtime; the Event Buffer file is loaded on
demand. Both executors of one run read the same files, but a run under another
profile may behave differently.

F9. **Cost.** 1.1.12, local and sequential: 659 s for 37 routes on both sides;
the candidate side 501 s (103 invocations, 4.9 s each on average), v0.9.16
141 s (95 invocations, 1.5 s). A 1.x-against-1.x run is about two candidate
sides (about 1,000 s sequential) plus two source builds.

F10. **1.1.13 still moves.** Since `v1.1.12` this branch changed only CLI and
UI `src` (wave 1). The NVT end-flag rule (`NVT-END-FLAG-1113-01`), TP SVN, the
Header backup CRC fix and the pre-built catalog will change fingerprints,
input admission and possibly bytes before the freeze; decision 48 already
records changed synthetic-oracle expectations.

## 2. Terms

- **Terminal certification**: the ADR 0057 64-route compare, attestation and
  finalize chain with a terminal `pass`; deferred to 2.0.0.
- **Rolling Golden**: ADR 0057's rule that the manifest and policy at the
  exact candidate commit are the one active Golden reference. It supplies the
  inputs below; it is not the comparison.
- **Previous-release comparison**: this design's per-release gate. The board
  and roadmap call the arrangement "rolling v0.9.16 parity": the 1.1.12
  alignment is the anchor, and each release is compared with its predecessor.
- **Chain claim**: the path back to v0.9.16 is the ordered list of declared
  differences. It holds only for a scenario and input revision compared at
  every release since the anchor; a retired scenario or replaced input ends
  its chain.
- **v0.9.16 1.x mode**: the same comparator against v0.9.16 through the
  historical v1 plan; never terminal.
- **Formal 1.x comparator**: the committed tool that runs both modes and
  replaces the 1.1.12 harness.
- **Scenario**: one comparison unit: workflow, IC, IC-count selection, map,
  CLI selection tokens, ordered input artifacts (case, artifact, slot, size,
  SHA-256), CtrlRAM base recipe and input revision. A route can have several
  scenarios.
- **Decision-12 debt set**: the 27 route ids the v1 plan lists in
  `canonicalInputAuthority.currentlyMissingRouteIds`. Decisions 12 and 47
  exempt exactly these ids from coverage.
- **Coverage disposition**: an approved change to what is compared: a retired
  scenario, an input revision that replaces a scenario's inputs, or an
  accepted gap (a universe route without a scenario outside the debt set).

## 3. Rolling comparison (question 1)

### 3.1 Baselines

- **Gate baseline: the previous stable release**, meaning the highest
  published `vX.Y.Z` release below the candidate `VERSION`. A tag without a
  complete published Release (a burned version under the WS-GOV recovery
  table) is not a baseline. The comparator checks that the named tag is an
  annotated stable tag below the candidate version whose peeled commit is an
  ancestor of the candidate; in the release workflow it also confirms through
  GitHub that the Release is published and that no higher published stable
  release lies in between. The declaration names the same tag.
- **v0.9.16 at milestones only** (decision 64): (a) once in 1.1.13 on the
  final candidate, because F4 means no exact release has yet been compared
  with v0.9.16; (b) at the 1.2.0 release approval, the end of the 1.x line;
  (c) before the owners decide RO-1 for 2.0.0. What such a run confirms is a
  comparison with exact approved differences, not that a release equals
  v0.9.16. A difference it finds is disposed of before that release: approved
  as a new exact correction row in the 1.x amendment, or recorded as a bug.

Why not both at every release: the chain already sees every byte change on
continuously compared scenarios. A second fixed baseline makes each intended
change be approved twice (in the release declaration and as an exact v0.9.16
correction row), and the correction rows break whenever an approved route
changes again; the Header backup CRC fix could, for example, touch the Header
CRC words both NT5195x correction rows cover. Execution cost was not the
reason (the v0.9.16 side adds about 2.5 minutes).

### 3.2 Coverage

- **Universe** (decision 64): every candidate-policy route with authoring
  `available` and publication `supported` or `candidate`; the hidden
  `internal` and `test-only` General routes are excluded. Today 74 routes: 63
  supported and 11 candidate. The universe is not the compared set.
- **Seed: 39 scenarios over 37 routes** (decision 64). First, the 37
  scenarios of the 1.1.12 alignment, rebound from plan route identities to
  current routes by IC, workflow, IC-count variant and map variant, with one
  declared rename (`nt51950-ab-merge-512k` to the single axis of
  `nt51950-ab-merge-maps`): 33 come from route evidence and 4 are AB
  Normal-mode case scenarios (F3). Second, the NT51950 Hiway and Display-OSD
  cases, as two more scenarios of the already covered NT51950 single route.
  The seed covers 37 of the 63 supported routes. The Display-OSD input is
  rejected by `v1.1.12` (decision 19 known issue), so the 1.1.13 NVT end-flag
  fix appears as one declared `baseline-rejects` line.
- **Decision-12 debt set**, fixed at 27 ids. Current mapping (P-1 copies the
  exact ids into the inventory):

  | IC | Workflow | Map variants | Current state |
  | --- | --- | --- | --- |
  | NT51917 | CtrlRAM | `nt51927-ctrlram-fw132-twochip` and `nt51927-ctrlram-fw140-threechip`, each `-full-flash` and `-tp-work-212k` | same ids, supported, contract-only |
  | NT51919 | CtrlRAM | `nt51929-ctrlram-fw1x-cascade-full-flash` | same id, supported, contract-only |
  | NT51927 | CtrlRAM | `nt51927-ctrlram-fw132-twochip` and `nt51927-ctrlram-fw140-threechip`, each `-full-flash` and `-tp-work-212k` | same ids, supported, contract-only |
  | NT51928 | CtrlRAM | `nt51928-ctrlram-fw141-single`, `nt51928-ctrlram-fw132-twochip` and `nt51928-ctrlram-fw140-threechip`, each `-full-flash` and `-tp-work-212k` | same ids, supported, contract-only |
  | NT51928 | Standard | `nt51928-dual-capacity-256k-512k` | same id, supported, contract-only |
  | NT51929 | CtrlRAM | `nt51929-ctrlram-fw1x-cascade-full-flash` | same id, supported, contract-only |
  | NT51932 | CtrlRAM | `nt51932-ctrlram-fw1x-single-full-flash` | same id, supported, contract-only |
  | NT51950 | CtrlRAM | `nt51950-ctrlram-fw200-single-tp-work` | same id, supported, contract-only |
  | NT51950 | Standard | `nt51950-standard-merge-512k`, `nt51950-standard-merge-1024k` | same ids, supported, synthetic-oracle |
  | NT51950 | AB | `nt51950-ab-merge-1024k` (2-plus-IC) | left the policy; not in the universe |
  | NT51951 | AB | `nt51951-ab-merge-1024k` | same id, supported, contract-only |
  | NT51951 | CtrlRAM | `nt51951-ctrlram-fw200-single-tp-work`, `nt51951-ctrlram-fw1x-cascade-tp-work` | same ids, supported, contract-only |
  | NT51951 | Standard | `nt51951-standard-merge-256k`, `nt51951-standard-merge-1024k` | same ids, supported, synthetic-oracle |

  The exemption follows these exact route ids; it passes to no successor,
  renamed route or new route.
- **New gaps**: universe routes that are neither covered nor in the debt set.
  Today these are the 11 candidate routes: the ten CtrlRAM Replace routes on
  AB maps (NT51919, NT51929 and NT51932 on `…-ab-merge-512k` for 1-IC and
  2-8-IC; NT51950 on `nt51950-ab-merge-512k` 1-IC and `…-1024k` 2-IC; NT51951
  on `nt51951-ab-merge-1024k` 1-IC and 2-IC) and `nt51950-ab-cascade-maps`,
  which is the nearest successor of the departed debt route but a different
  identity. They existed already in `v1.1.12` (same policy blob), and none has
  a canonical input. Under decision 60 each needs an accepted-gap disposition;
  the owner's 1.1.13 approval of these 11 is still to be asked, and the 1.1.13
  report of record cannot pass without it.
- **Coverage ledger**: the inventory holds the active scenarios, the retired
  scenarios (retiring version, reason, approval reference), the debt set and
  the accepted gaps (route id, approving version, approval reference). Every
  universe route is covered, or else in the debt set or an accepted gap;
  anything else blocks (decision 60).
- **Completeness**: every published route whose active route evidence names a
  case has an active scenario. A contract test checks it without execution, so
  a new direct or alias route joins the comparison instead of being missed.
- **Monotonicity**: every scenario active in the baseline tag's inventory is
  still active with the same input revision, or its change is a coverage
  disposition of this release. For 1.1.13, whose baseline has no inventory,
  the seed review checks that the 39 include all 37 of the 1.1.12 table.
- Every report lists, separately, the universe, the covered routes and
  scenarios, the debt set, the accepted gaps, and every coverage change since
  the baseline. A scenario never raises an evidence rank or changes a policy
  decision; the AB case scenarios do not make those routes Golden-verified.

### 3.3 Comparison method

- Both executors run the same workflow on byte-identical staged copies of the
  same inputs. The primary output is compared completely: size, every byte
  and SHA-256. Differing bytes are reported as half-open file-offset ranges
  `[start, endExclusive)`, with the byte count, the digest of the complete
  range list and the first 32 ranges.
- CtrlRAM full-flash scenarios build their base on each side with that side's
  own Standard Merge, as ADR 0057 requires; both base identities are compared
  and reported, so a Standard Merge change is visible where it enters.
- TP-work scenarios are compared directly, because both sides emit the same
  declared work-image capacity. The ADR 0057 transitive proof is needed only
  against v0.9.16.
- **Per-side execution safety**, checked for each side on its own: the report
  identity, ordered inputs and output agree with the capture; every mutation
  lies inside its own compiled operation and processor ranges (the shared ADR
  0057 normalizers, `validate_semantic_report_ranges` and projection checks);
  no `error` issue on a committed run. A failure makes the scenario `invalid`.
- The versioned report reader handles only format and evidence: which members
  a report version carries, issue severities and codes, unknown optional
  members (recorded by name, never used as authority) and issues without a
  severity in older shapes. Semantic normalization and range rules stay with
  the shared ADR 0057 owners, the terminal admission rules stay unchanged,
  and no second, looser firmware validation is created.
- Differences between versions in operations, issue codes, resolved map id or
  capability fingerprint are recorded beside the byte result as input to the
  attribution of 4.2; they never pass or fail a scenario on their own and are
  never normalized (ADR 0057 applies no cross-version operation equality).
- Output naming, delivery bundles and report bytes are outside the
  comparison; naming has its own contracts and tests.

### 3.4 Execution

- **Executors**: CLIs source-built by the comparator from exact Git
  authority, the baseline from its annotated tag and the candidate from the
  exact candidate commit, each in a fresh detached Git worktree (not a
  `git archive` export, so commit-bound SourceLink matches; F6). The recipe is
  the 1.x recipe of the v1.0.0 candidate contract: locked restore with
  `--disable-parallel`, Release build with `-m:1`, `ContinuousIntegrationBuild`
  and `PathMap`. Identity is recorded, not pinned in advance: tag object,
  commit and tree, the `src`, `profiles`, `external-tools` and
  `tools/crc-worker` tree ids, resolved SDK, lock-file hashes, CLI hash and
  runtime-closure digest. A restore that rewrites a lock file fails. From
  1.1.14 on, the rebuilt baseline identity is also compared with the
  candidate identity that the baseline release's own report recorded; a
  mismatch is reported, and becomes blocking once local and hosted builds are
  shown to match.
- **Inputs**: the active Golden at the candidate commit, materialized from Git
  objects and validated once with `canonical_golden_validation.py` through the
  existing ADR 0057 materializer. Each input is admitted by exact path, size
  and SHA-256 from the inventory. Every process gets its own read-only staged
  copy, hashed before and after; the repository Golden file is never a
  process input.
- **Environment** (decision 59): each CLI process gets fresh comparator-owned
  `TEMP` and `TMP` directories and a working directory inside its staging
  root. Release evidence uses the CI policy everywhere, locally included: the
  per-user configuration files of F8 must be absent before and after the run.
  A run with custom configuration is diagnostic only. If a setting ever has to
  stay, that needs its own owner approval and binds both the file bytes and
  the identity of the runtime it actually selected, not only the two JSON
  hashes. P-0.5 checks whether any scenario depends on these files.
- **Parallelism**: bounded workers (proposed default 3 per side), each
  scenario in its own staging root; the report is sorted by scenario id, and
  timings stay outside its digest-bound part.
- **Evidence**: firmware inputs and outputs stay in the run directory
  (`<test-area>` locally, runner temp in CI) and never enter Git or an
  uploaded artifact. The payload-free report (identities, sizes, hashes,
  ranges, exit codes, issue codes, outcomes, coverage counts) is the evidence.
  Its JCS digest covers its deterministic part, which binds the source commit
  and tree, the baseline tag and both executor identities, the comparator
  script digest, the digests of the contracts it applied, the inventory digest
  and the declaration digest.
- **Cost**: 7b asks for a measurement first. P-0.5 measures locally; the
  hosted-runner measurement runs in the WS-GOV staging repository (RO-9)
  during R-5. Options if it is too slow, in order: more workers; baseline and
  candidate as parallel jobs; and, only if the owner approves it in the ADR,
  build-only capture on the rolling path (the byte comparison stays, the
  preview cross-check goes).

## 4. Differences and their disposition (question 2)

### 4.1 Outcomes

| Observation | Outcome | Declaration entry needed | Undeclared, it blocks |
| --- | --- | --- | --- |
| Outputs equal | `equal` | none; an entry for it is stale | - |
| Both succeed, outputs differ | `different` | `byte-difference`: both output identities, byte count, complete range list, attribution | yes |
| Baseline rejects, candidate succeeds | `baseline-rejects` | baseline issue codes and stage, candidate output identity | yes |
| Baseline succeeds, candidate rejects | `candidate-rejects` | issue codes and stage; a route withdrawal, or a known non-blocking issue approved for this release (decision 57) | yes |
| Both reject | `both-reject` | issue codes and stage; a known non-blocking issue approved for this release (decision 57) | yes |
| Crash, timeout, tool or report failure; per-side safety, custody, identity or environment failure | `invalid` | cannot be declared | always |
| Scenario retired, input revision replaced, or a universe route outside the debt set without a scenario | coverage change | coverage disposition | yes |
| Debt-set route, or accepted gap from an earlier release | `not-covered` | none | no |

A rejection is a typed product rejection: a failed Preview or Build with its
issue codes. Anything else is `invalid`.

### 4.2 Attribution

Every `different` and every acceptance change needs two separate findings:

1. **Per-side safety** (3.3): each side's mutations stay inside its own
   approved operations. Automatic; a failure is `invalid`.
2. **Cross-version attribution**: the complete delta, range by range, is
   explained by an approved behavior change. The mechanisms to name are: the
   candidate writes different bytes; the candidate stopped writing and the
   range keeps the base or reference bytes (the Diff NF tail of 6.1); a change
   carried in by the Standard Merge precursor; derived fields such as CRC
   words recomputed over changed content. The union of both sides' write
   ranges is not sufficient evidence; the owner approves the exact cause and
   the exact ranges.

The agent proposes the attribution with its references (pull request,
CHANGELOG entry, owner decision or record); a difference it cannot attribute
is recorded as a bug, as in 1.1.12.

### 4.3 Flow and final evidence

1. **Rehearsal runs**, early and non-gating: on the trunk during development
   (for example as byte evidence for an R3 change) and on the frozen release
   branch.
2. **Disposition**: the owner, as firmware owner, approves or rejects each
   difference, rejection and coverage change; the board decision is the
   authority and the declaration cites it exactly (decision 64). A rejected
   one stays a bug; fix and rerun.
3. **Declaration first**: the declaration and its CHANGELOG lines are
   committed on the release branch before the final run (decision 59). Each
   declared item appears in the notes under the product change that caused
   it, with its id (decision 64).
4. **Final run on the final release source**: after the release pull request
   merges and before the release workflow is dispatched, the comparator runs
   on the exact `main` commit that will be released, under the CI
   clean-settings policy (decision 59). Its report is the report of record
   (bindings of 3.4). If it does not reproduce the declaration
   exactly, the release does not start; the fix follows the WS-GOV recovery
   path for "no tag yet".
5. **No implicit evidence transfer**: a report from the release-branch head
   counts for the merge commit only under a separate, exact and reviewed
   transfer contract; "no later product change" is not such a contract.
6. From R-5 on, the release workflow performs step 4 itself on the workflow
   SHA.

### 4.4 Declaration

One schema-validated file per release,
`docs/contracts/predecessor-comparison-declarations/<version>.json`: candidate
version, baseline tag and tag object, inventory digest, and entries. Each
entry carries a stable id (`RP-<version>-NN`), its kind (`byte-difference`,
`baseline-rejects`, `candidate-rejects`, `both-reject`, `scenario-retired`,
`input-revision`, `accepted-gap`), the scenario or route ids and
fingerprints, the expected outcomes (output size and SHA-256, or issue codes
and stage), byte count, range-list digest and ranges, the attribution, the
approval (board decision reference, which is authoritative, role
`firmware-owner`, date) and the CHANGELOG token. An empty entry list is valid
and is still committed, so a clean run is an explicit expectation. There is
no separate attestation file (decision 64).

### 4.5 What blocks a release

An undisposed difference blocks (decision 57). The gate fails when:

1. an observed outcome other than `equal` or `not-covered`, or a coverage
   change, has no matching declaration entry;
2. a declaration entry is not reproduced exactly (stale or changed);
3. any scenario is `invalid`;
4. a universe route is neither covered, in the debt set, nor an accepted gap;
5. completeness fails;
6. a declaration token is missing from the version's CHANGELOG section
   (decision 64);
7. the baseline is not the declared previous stable release;
8. the report of record does not come from the exact release source under
   the clean-settings policy, or does not bind the digests of 3.4 (decision
   59).

A newly rejected input ships only as a known non-blocking issue the owner
approves case by case for that release; a crash, timeout or tool or report
failure is never an approved rejection, and a declared issue never overrides
a P0 or P1 bug, a required Golden case or a protected check (decision 57).

### 4.6 Enforcement

Until WS-GOV R-5 lands, the gate is procedural (decision 61): the commander
runs the formal comparator as in 4.3, records the report of record with the
release evidence and in the board, and the owner disposes of every change as
firmware owner and approves the release as release owner with it. It is an
R3 approval now, not only once R-5 automates it. The procedural gate uses the
formal comparator only and never the old harness (decision 61): the 1.1.12
harness compares TP-work routes only against the baseline's full-output
prefix and has no declaration, severity or coverage gate. There is no
automatic fallback to it or to any other reduced comparison; if the formal
comparator is not ready when 1.1.13 is otherwise ready, the commander asks
the owner then, choosing between holding the release and a one-time reduced
check the owner bounds explicitly (decision 58). Running the comparison at
release-branch cut or weekly on the trunk is a later CI choice for WS-GOV
(G2, R-6).

## 5. Boundaries (question 3)

### 5.1 ADR 0057 terminal certification

Unchanged (decision 47): the v1 plan and its schemas, the workflow contract,
the three parity jobs, the `firmware-parity` environment and its secrets,
`compare`, `finalize` and the terminal parser. The formal comparator never
emits `v0916-parity-*` documents or a `pass`, never reads `candidateAuthority`,
`protectedBuild`, `authorityTransfer`, package admission or owner
attestations, and its reports state `certification: none`. The 2.0.0 promote
gate (WS-GOV R-1) does not accept them. This item edits no
`WORKFLOW_SYNC_INPUTS` file. Terminal-path findings (F5, the single-row
correction schema, the NT51950 cascade binding) go to the RO-1 list for the
2.0.0 rebinding and are not fixed here.

### 5.2 v0.9.16 1.x mode

A historical consumer of the v1 plan, as ADR 0057 permits: policy and inputs
read only at `canonicalInputAuthority.repositoryCommit`, the 37 bound routes,
the ADR 0057 proof kinds (exact output, the NT51951 approved correction, the
four runnable transitive TP routes), and the 27 unbound routes reported as
not covered instead of stopping with `PARITY_FIXTURE_MISSING`. What the
immutable plan cannot carry lives in a separate contract,
`docs/contracts/v0916-parity-1x-amendment-v1.json`, read only by this mode:
the NT51950 TP-work correction (6.1), the NT51950 cascade full-flash binding
(6.2) and the corrected baseline executor (6.3). The candidate is the exact
commit under test, built with the 1.x recipe. The result is `consistent` or
`inconsistent`, never `pass`. Deletion milestone: when the owners decide RO-1,
the amendment is folded into the rebound 2.0.0 plan (option A) or deleted with
the terminal chain (option B).

### 5.3 Canonical input authority

The rolling mode's only input authority is the active Golden at the candidate
commit. The comparator computes its descriptor from Git (commit, canonical
root tree, manifest blob, size and SHA-256), materializes it through the ADR
0057 materializer and validates it once with the current validator. The
inventory pins every scenario's artifacts; a caller cannot supply, substitute
or synthesize inputs. Outputs never become expected bytes, never update Golden
and never raise an evidence rank (ADR 0057: no self-rebaselining). The
rolling baseline is the previous release's executor, not stored bytes.

### 5.4 Release workflow (R3, WS-GOV batch R-5)

This design owns the comparator; WS-GOV owns its release integration. R-5
builds on decision 47, which already chose option C: the rolling gate is
added while the terminal gate stays, and R-5 does not wait for RO-1 to be
decided as A or B. R-5 needs:

- read-only Windows jobs that check out the workflow SHA with the baseline
  tag's history available, build both executors and run beside the candidate
  job;
- the committed declaration as input, and only the payload-free report as
  uploaded output;
- the report digest bound into the candidate manifest or a sibling evidence
  manifest;
- `promote` depending on the comparison, with a failed, cancelled or skipped
  comparison failing the run (the WS-GOV eligibility rule);
- the workflow contract's job inventory extended in the same change (F7,
  which also applies to R-1);
- staging runs for a clean pass, an undeclared difference, a stale
  declaration, a required comparison that failed, was cancelled or was
  skipped, and report evidence from the wrong source or another run.

ADR 0033 and `docs/ci/release-package.md` are amended there. Once the
comparator is a release gate, it joins the release-owner script set, and later
changes to it are R3.

### 5.5 New ADR and contracts

Both are needed.

- **ADR** "Predecessor comparison for 1.x releases", Status Proposed. The
  commander assigns the number (0074 to 0077 are taken; WS-GOV holds a
  placeholder). It amends ADR 0057 by adding the non-terminal 1.x mode and its
  amendment contract, with the terminal chain unchanged, and defines the
  rolling gate, outcomes, attribution, coverage dispositions, declaration and
  approval rules. A new ADR rather than an ADR 0057 edit, because the owners
  will retain or retire ADR 0057 at 2.0.0 and the rolling gate outlives that
  choice.
- **Contracts**, each with schema and prose:
  - `predecessor-comparison-v1`: executor recipe, baseline selection,
    environment policy, report-reader rules, outcomes, failure codes, a closed
    list of version-specific CLI argument aliases (empty for 1.1.12 to
    1.1.13).
  - `predecessor-comparison-scenarios-v1`: the coverage ledger of 3.2, with
    declared CtrlRAM base kinds and artifact-to-slot bindings (base kind is
    never inferred from a map name).
  - `predecessor-comparison-report-v1` and
    `predecessor-comparison-declaration-v1` schemas; declarations under
    `predecessor-comparison-declarations/`.
  - `v0916-parity-1x-amendment-v1` and `v0916-baseline-executor-v2` for the
    1.x mode.
- The roadmap records the v0.9.16 milestone schedule (it owns the parity
  schedule).
- No change to the capability policy, the Golden manifest or any profile.

### 5.6 Reuse

Input for the capability-reuse gate of P-1 and P-2:

| Need | Existing owner | Disposition |
| --- | --- | --- |
| Golden from Git, validated | `materialize_and_validate_canonical_input_authority`, `canonical_golden_validation.py` | reuse with a computed authority descriptor |
| Route-evidence inputs, CtrlRAM bindings, CLI arguments | `resolve_canonical_route_input`, `_cli_arguments`, `_cli_selection_token` | reuse; public entry points for case scenarios are an additive extend-owner change (one writer, section 7) |
| Exact, approved-correction and transitive comparison | `compare_approved_semantic_correction_payloads`, `compare_transitive_payloads` | reuse; declared differences use the same exactness rule |
| Report semantics and ranges | the raw normalizers, `validate_semantic_report_ranges`, `validate_report_projection_against_compiled_authority` | reuse; only the format envelope (F5) differs for 1.x reports |
| Staging, custody, exclusive writes | `capture_workspace`, `hold_read_only_file_custody`, `write_json_exclusive_atomic` | reuse |
| Source executors | `verify_source_baseline_executor`, `verify_candidate_source_executor` | extend-owner: verify a recipe for a tag or commit and record the identity |
| CHANGELOG section | `render_release_notes.render_release_notes` | reuse, read-only |
| Orchestration, coverage ledger, declaration, gate | none; the 1.1.12 harness is handoff evidence | new `scripts/predecessor_comparison.py`; the harness stays as 1.1.12 history and is not maintained |

## 6. NT51950 plan corrections (question 4)

### 6.1 TP-work Diff NF correction

**What.** Route
`route-7-nt51950-15-ctrlram-replace-4-2-ic-36-nt51950-ctrlram-fw1x-cascade-tp-work`
(plan fingerprint `df822064…`) is an exact-output route. Its inputs come from
the alias case `nt51950-cascade2-geometry-nt51951-auto-prj-599-alias`
(resolving to `nt51951-fw200-cascade2-auto-prj-599-20260731`); the TP input
(225,280 bytes, `1397cfed…`) is the base. v0.9.16 writes the whole Diff
CtrlRAM record `[0x33200, 0x34600)`; 1.x writes `[0x33200, 0x33B10)` and
stops writing the Diff NF tail, which keeps the base bytes (0.10.1, #188). The
outputs differ in 2,816 bytes: `[0xA11C, 0xA120)`, `[0xA130, 0xA134)`,
`[0x2D428, 0x2D42C)`, `[0x2D43C, 0x2D440)` and `[0x33B10, 0x34600)`, which
are the tail and four Header and Header-copy CRC words, the same ranges and
count as the approved NT51951 row. Both outputs are 225,280 bytes: v0.9.16
`cfae15911aac4ef641eed9dc95acdb08bb69e988284b8753ad0528f3ed44e47e`, candidate
`a239645dd6e2527af34934e20ef7399ecb9d748aca32ce9ed4b829b94b8fc643`, the same
for the 1.1.11 and `badc545b0` candidates. The owner approved it (decision
12). The v1 plan approves only NT51951, and its schema and loader admit
exactly one correction row, so an ADR 0057 run would fail
`PARITY_EXACT_MISMATCH`
([bug](../bugs/BUG-20260925-v0916-plan-nt51950-tp-work-correction-missing.md)).

**How.**

1. P-1 adds one row to the 1.x amendment in the NT51951 row's shape: route id
   and v1 fingerprint; kind `owner-approved-diff-nf-preservation`; owner
   decision `owner-decision:2026-09-26:nt51950-cascade-tp-work-diff-nf-preservation-is-correct`
   (board decision 12); both output identities; the byte count; the five
   ranges in decimal (41244-41248, 41264-41268, 185384-185388,
   185404-185408, 211728-214528, each half-open); v0.9.16 and alias-case
   provenance; observation status `non-certifying-1.1.12-observation`; proof
   kind `exact-output-with-approved-semantic-correction`.
2. **Authority**: decision 12 covers this recorded difference with exactly
   these values and nothing more. The firmware owner confirms that the row
   transcribes it. Any later hash or range change and any failure case need
   approvals of their own; the 6.2 binding and the 6.3 executor rest on
   decisions 62 and 63, not on decision 12.
3. The 1.x mode checks it with `compare_approved_semantic_correction_payloads`:
   equal sizes, both hashes, the count and the complete range list, so every
   byte outside the ranges must still equal v0.9.16.
4. P-3 reproduces it on the final 1.1.13 candidate. If a 1.1.13 change alters
   this route's candidate bytes (the Header backup CRC fix could touch these
   CRC words), the row fails closed and the owner approves the new exact
   values; the row is never widened into a pattern.
5. The v1 plan is not edited: it is the immutable certification plan, and its
   files are workflow-sync inputs that WS-GOV's release changes rewrite. At
   RO-1 the row is folded into the rebound plan (A) or deleted (B).
6. The bug is resolved for 1.x when P-1 and P-3 land; its 2.0.0 part moves to
   the RO-1 list. The NT51951 twin TP-work route is one of the 27 and stays
   not covered.

### 6.2 NT51950 cascade full-flash binding

Route
`route-7-nt51950-15-ctrlram-replace-4-2-ic-39-nt51950-ctrlram-fw1x-cascade-full-flash`
([bug](../bugs/BUG-20260925-nt51950-cascade-ctrlram-plan-base.md)): its map
has declared 262,144 bytes since v0.9.16, while the plan builds the base with
`nt51950-standard-merge-512k` from the NT51951 AUTO_PRJ-599 inputs, giving the
524,288-byte base `ff9ad012…` on both sides. v0.9.16 rejects it at the CtrlRAM
Preview (`profile.v2.compile.map-selection-invalid`); `v1.1.11` also rejected
it; `badc545b0` accepts it as a Display-OSD envelope (decision 17) and writes
`1536d344…`, the NT51951 route's output. The two-sided run supports the bug's
hypothesis (a), a plan binding defect: the canonical inputs contain no base
v0.9.16 can run for this route (the alias keeps NT51950's separate `0x40000`
capacity). No 1.x product defect remains; the exact `v1.1.12` classification
changes after `badc545b0` (F4) are confirmed by the P-3 run.

Decided (decision 62; not covered by decision 12): the amendment records that
**this canonical binding** is not applicable to v0.9.16. It binds the
precursor identity `ff9ad012…`, the rejecting stage (CtrlRAM Preview) and
issue code, and the candidate output identity (`1536d344…` unless the 1.1.13
run shows another value, which the owner then confirms as firmware owner).
The report counts it apart from equal and different. It says nothing about
other NT51950 inputs or bindings. The rolling mode compares the scenario
normally from release to release, since both 1.x sides accept it. It is
revisited at 2.0.0: a genuine 256 KiB NT51950 cascade input, or this
disposition approved by both owners (RO-1 list). Not chosen: rebinding to the
256 KiB Standard Merge (no such input exists); moving the route to not
covered (loses a runnable check).

### 6.3 v0.9.16 build recipe

Approved by decision 63 (a new executor authority, not covered by decision
12), effective only once P-0.5 has produced the evidence below; until then no
formal v0.9.16 run is possible. P-0.5 builds v0.9.16 from a fresh detached
Git worktree with the probe restore. P-1 then writes
`v0916-baseline-executor-v2.json`: the same tag, commit, tree,
SDK and tool pins; restore with `--force-evaluate --runtime win-x64`; after
restore each rewritten lock file must equal a pinned SHA-256, and the complete
diff from the tag's lock file is pinned and explained (expected: only the
empty `net10.0/win-x64` target and first-party ranges from `0.9.2` to
`0.9.16`; no package id, version or content hash change); the pinned build
command; the managed files and the complete runtime closure re-measured from
the Git worktree and pinned with the cause of any difference from v1. The
same apphost CLI hash (`09321252…`) alone does not prove the same executor;
an unexplained closure difference blocks formal runs. v1 stays for the
terminal plan ([bug](../bugs/BUG-20260925-v0916-baseline-restore-nu1004.md)).

### 6.4 Not changed

Product code, profiles, Golden, the capability policy and the v1 plan.

## 7. Batches, risk and order (question 5)

| Batch | Content | Work and its risk | R3 approvals | Depends on |
| --- | --- | --- | --- | --- |
| P-0 | This design and the WS-PARITY log | R0 documents | decided (decisions 57 to 64) | - |
| P-0.5 | Spike in the test area, nothing committed but the log: v0.9.16 build from a Git worktree (6.3); `v1.1.12` tag build and identity; the severities and codes of the CtrlRAM issues (F5); CLI runs sequential and with 3 workers, with and without the per-user configuration files (F8); local cost | R0 diagnostic evidence | - | P-0 review |
| P-1 | ADR draft; contracts and schemas (5.5); coverage ledger seed and debt set; 1.x amendment (6.1, 6.2) and executor v2 (6.3); roadmap schedule line | R2: ADR, contract and schema text, the seed | firmware owner confirms the transcribed values: the 6.1 row (decision 12) and the 6.2 binding (decision 62); the 1.1.13 accepted gaps for the 11 candidate routes (still to be asked, decision 60). Executor v2: approved by decision 63, effective once P-0.5's evidence is pinned | parts of P-0.5 (section 12) |
| P-2 | `scripts/predecessor_comparison.py`; additive public entry points in `scripts/v0916_parity_certification.py` (terminal behavior unchanged); `tests/scripts/test_predecessor_comparison*.py` | R2: all of it; exact-head review, narrow tests and the unchanged `test_v0916_parity_*` | - | P-1 accepted; one writer on the parity script |
| P-3 | Rehearsal runs; attribution and difference table; `predecessor-comparison-declarations/1.1.13.json` and CHANGELOG lines committed; final runs on the final 1.1.13 release source (rolling against `v1.1.12`, and the v0.9.16 1.x mode); report of record with the release evidence and in the board; bug resolutions | R2 evidence work: executing the comparator, the declaration file | firmware owner: each difference and coverage change, including the 11 candidate-route gaps and the Display-OSD line. Release owner: the release, with the report of record (decision 61) | P-2 merged; 1.1.13 frozen; if the comparator is late, decision 58 |
| R-5 | Release-workflow integration (5.4) | R3 workflow change (WS-GOV) | release owner; staging | P-2, the ADR, WS-GOV R-1, the CI evidence change; decision 47 (C) already given |

**Records.** If WS-GOV G1-B has not landed, P-1, P-2 and P-3 each need a
capability-reuse record admitted on the latest evidence checkpoint (after
#457: `84b084dd8` or newer), with the branch rebased, never merged, onto the
trunk before admission (board working rules). Those records declare R3 where
the table lists R3 approvals, with the owner attestations they require. P-1
and P-2 may share one record if they integrate as one batch. If G1-B has
landed, the pull-request fields, the exact-head review record and the
role-bound approvals of the governance ADR draft (item 7) replace records.

**Verification.**

- P-1: contract tests for ledger completeness against the active manifest,
  the debt set equal to the plan's 27 ids, seed equality with the 1.1.12 set
  by input identity, amendment rows equal to the 1.1.12 observations, and
  schema negatives.
- P-2, with the test-area environment: unit tests on synthetic payload-free
  byte fixtures (no Golden) for every outcome and gate rule, and negatives
  for:
  - an undeclared difference, a stale entry and a changed range list;
  - a scenario deleted and its route listed as not covered without an
    approved disposition in this version's declaration;
  - an input revision that replaces coverage without a disposition, and a new
    universe route without a scenario or accepted gap;
  - a successor or renamed route claiming the debt-set exemption;
  - a crash, timeout or report failure presented as a declared rejection;
  - a difference justified only by the union of both sides' write ranges;
    and, as positive cases, a stopped write and a precursor-carried change
    that pass only with their exact approved attribution;
  - a wrong baseline, a missing CHANGELOG token, a report from a source other
    than the final release source, and a report whose declaration, inventory
    or contract digest differs;
  - an `error` issue, a report that disagrees with the output, a mutation
    outside its own operation, a per-user configuration file present in a
    formal run, and a rewritten lock file;
  - a v0.9.16 correction mismatch, and a not-covered route counted as
    compared.

  The ADR 0057 tests pass unchanged. The new tests fall into the existing
  `test_[a-q]*.py` shard, so `verify.py` is not touched.
- P-3: the final runs are the acceptance evidence. The v0.9.16 1.x run must
  reproduce the 1.1.12 table except where an approved 1.1.13 change explains
  a difference.

**Order with other lanes.**

- The CI failure-evidence change owns `scripts/verify.py`,
  `.github/workflows/ci.yml` and `tests/scripts/test_verify_orchestration.py`;
  this item touches none of them.
- WS-GOV owns `.github/workflows/`, the release scripts,
  `docs/ci/release-package.md`, `scripts/validate_repository.py` and the
  `WORKFLOW_SYNC_INPUTS` files. Its R-1 also removes `validate-package-source`
  from `scripts/v0916_parity_certification.py`, so that file needs one writer
  at a time: the commander orders R-1 and P-2, and the second integrates on the
  head the first produced. F7 applies to R-1 too.
- The roadmap line in P-1 needs the roadmap's current writer (the
  roadmap-tidy lane) to be free.
- The other 1.1.13 R3 items should merge before P-3's final run so the release
  comparison covers them; each may use the comparator earlier as non-gating
  byte evidence once P-2 lands.

## 8. Not in scope (question 6)

- Terminal 64-route certification, any ADR 0057 terminal evidence, and any
  change to the terminal chain, its jobs, environment or secrets (decision 47).
- **Decision 12's fixed 27 routes stay not covered** (decisions 12 and 47): no
  new canonical inputs, no synthetic or representative substitution, no
  relabeling as Verified; they are listed in every report. The exemption does
  not extend to any other route. The active Golden holds unbound input-only or
  alias cases for some of the 27 (for example the NT51927 two- and three-chip
  self cases); using them would need a new owner decision and is not proposed.
- Golden, profiles, the capability policy and product code; evidence ranks and
  publication values.
- The v1 plan, its schemas, the workflow contract and every
  `WORKFLOW_SYNC_INPUTS` file; `scripts/verify.py`; `.github/workflows/`;
  `scripts/validate_repository.py`.
- The release-workflow integration (WS-GOV R-5).
- Cross-version equality of operations, issue codes, map ids or fingerprints
  as a gate of its own; output naming.
- Baselines other than the previous stable release and v0.9.16.
- Package certification (the release workflow keeps it) and GUI automation.
- Fixing the terminal report parser (F5); it goes to the 2.0.0 rebinding.
- Any fallback to the 1.1.12 harness.

## 9. Risks

- **Build identity drift** (SDK `latestPatch`, machine differences): identity
  and resolved SDK are recorded per run; the hosted measurement in R-5 shows
  whether local and hosted builds match.
- **CLI concurrency and per-user configuration**: per-process temporary
  directories and the clean-configuration rule, checked in P-0.5 before
  parallel runs are enabled.
- **Report shape drift across 1.x**: the versioned reader checks format only;
  semantics stay with the shared owners, and an `error` severity fails.
- **A second route list to maintain**: the completeness test ties the ledger
  to route evidence, and every coverage change needs a disposition.
- **A wrong change absorbed into the chain**: every difference needs a
  range-by-range attribution and a firmware-owner approval; the v0.9.16
  milestone runs re-anchor the chain.
- **Hosted cost**: measured before R-5, with the options of 3.4.
- **A baseline that no longer builds** (packages or SDK unavailable, as NU1004
  showed for v0.9.16): the run is `invalid` and the owner decides; locked
  restores and recorded identities make the cause visible.
- **Late product changes**: the final run is on the final release source, so
  any later product change produces a new report of record.
- **Public repository**: payload-free evidence only, no firmware bytes in logs,
  no paths in reports.

## 10. Findings and coordination notes

Outside this lane's write lock; recorded by the commander on
`feature/1.1.13/wave2` unless noted:

1. `BUG-20260926-terminal-parity-rejects-ctrlram-issues` (suspected, P2;
   `95bda40fe`): the ADR 0057 terminal report check rejects successful 1.x
   and v0.9.16 CtrlRAM reports (non-empty `Issues`) and 1.x reports with
   optional members (F5). The review confirmed the parser gap and the 86/52
   counts; P-0.5 reads the actual severities and codes.
2. `BUG-20260926-changelog-1112-product-source` (open, P3; `95bda40fe`):
   CHANGELOG 1.1.12 calls the comparison candidate `badc545b0` "the 1.1.12
   product source", but `v1.1.12`'s `src` differs by eight commits, including
   input classification (F4). The 1.1.12 evidence stays as history; the
   1.1.13 notes state its actual scope, and the new comparison is never
   presented as retroactive verification of `v1.1.12`.
3. The broken links in `BUG-20260926-trunk-merge-flags-sealed-record.md` are
   fixed on `feature/1.1.13/wave2` by `e17f7ccf5`. This branch is based on
   `cf4e42697` and fails the structure check for that reason until the
   commander rebases it (never by merging the trunk).
4. F7 constrains WS-GOV R-1 and R-5. The WS-GOV release design now builds
   R-5 on decision 47 (`feature/1.1.13/ws-gov` at `28bc715ba`).

## 11. Owner decisions

The owner decided every question on 2026-09-26. The authority is the board
text on `feature/1.1.13/wave2`, `docs/handoff/1.1.12.md`, decisions 57 to 64
(`4a106ec81`, `57a4c448d`); this section only maps it. The question numbers
are those of the earlier drafts. One approval is still to be asked: the
1.1.13 accepted gap for the 11 candidate routes without inputs (Q13).

**Q1. Does an undeclared difference from the previous release stop the
release?** Decided 2026-09-26, decision 57: yes. A difference the version's
declaration does not name (changed bytes, a newly rejected input, reduced
coverage) blocks the release. Applied in 4.5.

**Q10. May a newly rejected input ship as a known issue?** Decided
2026-09-26, decision 57: only as a known non-blocking issue the owner
approves case by case for that release and the declaration lists; a crash,
timeout or tool or report failure is never an approved rejection, and a
declared issue never overrides a P0 or P1 bug, a required Golden case or a
protected check. Applied in 4.1 and 4.5.

**Q11. What if the formal comparator is late for 1.1.13?** Decided
2026-09-26, decision 58: there is no automatic fallback to the 1.1.12 harness
or any other reduced comparison. If the formal comparator is not ready when
1.1.13 is otherwise ready, the commander asks the owner then, choosing
between holding the release and a one-time reduced check the owner bounds
explicitly. Applied in 4.6.

**Q12. Where does release evidence come from?** Decided 2026-09-26, decision
59: the report of record comes from a run under the same clean settings
policy as CI, against the exact release source, after the declaration and
CHANGELOG are committed; runs with custom user settings are diagnostic only.
Applied in 3.4, 4.3 and 4.5.

**Q13. How are coverage changes approved?** Decided 2026-09-26, decision 60:
retiring a scenario, replacing a scenario's inputs, and a published route
without comparable inputs are declared like a byte difference (owner approval
as firmware owner, a declaration entry and a CHANGELOG id each time); an
undeclared reduction fails; decision 12's fixed 27 routes stay as they are,
and a new gap never inherits their exemption. **Still to be asked**: the
1.1.13 approval of the 11 candidate routes without inputs (3.2), including
`nt51950-ab-cascade-maps`. Applied in 3.2, 4.1 and 4.4.

**Q8. How is 1.1.13 gated?** Decided 2026-09-26, decision 61: procedurally,
since R-5 will not be ready. The commander runs the formal comparator under
decision 59; the owner disposes of every change as firmware owner and
approves the release as release owner with the report of record; the old
harness is never permitted. Applied in 4.6.

**Q5. NT51950 2-IC cascade full flash against v0.9.16.** Decided 2026-09-26,
decision 62: this canonical binding is recorded as not applicable to
v0.9.16, binding the precursor, the rejecting stage and issue code and the
candidate output, counted apart from equal and different; it says nothing
about other NT51950 inputs or bindings and is revisited at 2.0.0; the route
stays compared from release to release. Applied in 6.2.

**Q6. The v0.9.16 build recipe.** Decided 2026-09-26, decision 63: executor
v2 is approved. It re-resolves the missing Windows section, pins the complete
lock-file diff and the runtime closure with the cause of every difference,
and blocks formal runs while any difference is unexplained. It takes effect
only once P-0.5 has produced that evidence; v1 stays for the terminal plan.
Applied in 6.3.

**Q2. How often is v0.9.16 compared?** Decided 2026-09-26, decision 64: only
at milestones: the 1.1.13 final candidate, the 1.2.0 release approval, and
before RO-1 is decided. Applied in 3.1.

**Q3. Who approves, and where is it written down?** Decided 2026-09-26,
decision 64: the owner approves each item as firmware owner; the board
decision is the authority and the declaration cites it exactly; the release
decision stays with the owner as release owner; there is no separate
attestation file. Applied in 4.3 and 4.4.

**Q4. AB inputs.** Decided 2026-09-26, decision 64: the four AB Normal
scenarios stay, and the NT51950 Hiway and Display-OSD cases are added: 39
scenarios over 37 routes; the Display-OSD case needs one declared line.
Applied in 3.2.

**Q7. Release notes.** Decided 2026-09-26, decision 64: each declared
difference and coverage change appears in the release notes under the
product change that caused it, with its id. Applied in 4.3 and 4.5.

**Q9. Scope.** Decided 2026-09-26, decision 64: supported plus candidate
routes, hidden General routes excluded. Applied in 3.2.

## 12. Next steps

### 12.1 P-0.5 spike

Purpose: the evidence decision 63 needs, and the facts P-1 needs (F5, F6,
F8, cost). R0 diagnostic work in the test area: nothing it produces is
release evidence (decision 59), and it commits only its log entry.

**Inputs.**

- Git objects, all in the shared object store: tag `v0.9.16` (object
  `578b2614`, peeled `462590e8`, tree `dc46c9aa`); tag `v1.1.12` (object
  `ec3104f0e`, peeled `30b17e699`); one current candidate commit (the latest
  trunk or wave 2 head); the v1 plan with its snapshot at `1d1d1cfc`; the
  active Golden at the candidate commit.
- Toolchain: .NET SDK 10.0.303, which is installed and is what `global.json`
  (10.0.301, `latestPatch`) resolves to; NuGet access for the v0.9.16
  `--force-evaluate` restore unless the local package cache already holds
  every package (the restore log shows which).
- Test area: `NFC_TEST_AREA_ROOT` with TEMP, TMP and TMPDIR set to its
  `temp` child; about 3 GB for the worktrees, build outputs, materialized
  Golden and outputs (77 GB free on 2026-09-26).
- Per-user settings: on 2026-09-26 the machine's `NvtFwCombiner`
  local-application-data folder holds neither `toolchain-runtime.v1.json` nor
  `event-buffer-format.v1.json` (only Desktop report history and a
  version-manager lock, which the CLI does not read), so clean runs need no
  change to the owner's settings. The spike re-checks before every run. A
  with-settings comparison runs only if the owner provides such a file; the
  spike never edits the owner's settings.
- A quiet machine window: builds and runs are heavy jobs and must not overlap
  another lane's timing measurement, such as the pre-built catalog's Step 0.
- The CLIs may be driven by the 1.1.12 harness or a scratch script in the
  test area, as measurement tools only; decisions 58 and 61 keep them out of
  any release evidence.

**Steps and cost** (estimates from the 1.1.12 timings and build logs):

1. v0.9.16 from a fresh detached Git worktree: `--force-evaluate` restore,
   pinned build, lock-file diff, managed files and runtime closure against v1
   (about 5 minutes of machine time plus analysis).
2. `v1.1.12` and the candidate with the 1.x recipe, each built twice from
   separate worktrees to check reproducibility (about 5 minutes).
3. The severities and codes of the CtrlRAM report issues (F5), from the
   reports the runs of step 4 keep.
4. Timing on the plan's 37 scenarios: one sequential pass of both 1.x sides
   (about 17 minutes, F9), one pass with 3 workers per side, and the v0.9.16
   side (about 2.5 minutes): about 30 to 40 minutes in all.
5. The results in the log, without firmware bytes or paths.

In total about an hour of machine time, one heavy job at a time, and a few
hours of agent time.

**Risks.**

- The closure difference stays unexplained: under decision 63 executor v2
  does not take effect, and the 1.1.13 v0.9.16 milestone run cannot be
  formal until it is explained. The rolling comparison is not affected.
- NuGet is unavailable or a package no longer resolves: the v0.9.16 restore
  fails; the log records it and the owner decides.
- An `error` severity among the CtrlRAM issues: recorded as a bug; P-1's
  reader rules wait for its disposition.
- Parallel CLI runs interfere (shared temporary state or file locks): the
  default stays sequential and the cost options of 3.4 change.
- Two local builds of one source differ: identity checks stay reported, not
  blocking, until the cause is known.
- Timings on a shared machine are indicative only; the hosted measurement
  stays with R-5.

### 12.2 What can start

| Batch | When | Needs |
| --- | --- | --- |
| P-0.5 | now | a quiet machine window; nothing from the owner |
| P-1, the parts that do not wait for P-0.5 | now, in parallel with P-0.5 | ADR draft, rolling contracts and schemas, the ledger seed (39 scenarios) and debt set, the declaration and report schemas, the 6.1 row and the 6.2 binding; admission on the latest evidence checkpoint, or under G1-B once it lands |
| P-1, the rest | after P-0.5 | executor v2 (decision 63) and the report-reader rules (F5) |
| Owner approval | before P-3's final run | the 1.1.13 accepted gap for the 11 candidate routes (decision 60) |
| P-2 | after P-1 is accepted | the commander's order of the single writer on `scripts/v0916_parity_certification.py` relative to WS-GOV R-1 |
| P-3 | after P-2 merges and 1.1.13 freezes | if the comparator is late, decision 58 |
| R-5 | WS-GOV, after P-2, the ADR, R-1 and the CI evidence change | - |

Model tiers under board decision 56: P-0.5, P-1 and P-2 are Opus 5.5 work
(executor-authority evidence, architecture and contracts, cross-module
implementation), reviewed by `gpt-6-astra`; review-driven text revisions may
use Sonnet 5.
