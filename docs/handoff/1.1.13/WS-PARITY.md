# WS-PARITY: rolling parity and the formal 1.x comparator (1.1.13)

Owner: Claude Code (Opus 5.5), designer. Board:
[1.1.13 board](../1.1.13.md), wave 3 row "Rolling v0.9.16 parity and the
formal 1.x comparator, including the NT51950 TP-work Diff NF plan correction".
Protocol: [handoff README](../README.md). Deliverable:
[design](DESIGN-rolling-parity.md). The 1.1.12 lane of the same name is the
[one-time v0.9.16 alignment](../1.1.12/WS-PARITY.md).

## Dispatch envelope (commander, 2026-09-26)

- **Outcome.** A design, not code, answering: (1) what rolling parity
  compares (previous release, v0.9.16 or both; which routes; complete output
  bytes or something else); (2) how the owner disposes of differences and
  whether an undisposed difference blocks a release; (3) the formal 1.x
  comparator's boundary with the ADR 0057 plan, canonical input authority and
  the R3 release workflow, and whether a new contract or ADR is needed;
  (4) what the NT51950 TP-work Diff NF plan correction is and how to fix it;
  (5) batches and risk (R2 or R3), records and reviews, files to change, and
  the order relative to the CI failure-evidence work and WS-GOV; (6) an
  explicit list of what is not done, including that the 27 routes keep their
  decision 12 disposition. Design documents in English; the report to the
  commander in Traditional Chinese.
- **Authority.** Documents only; local commits ending with the Claude
  co-author line; no push, pull request or GitHub write.
- **Branch and worktree.** `feature/1.1.13/rolling-parity` in
  `<worktrees>/parity`, base `cf4e42697`. Never merge the trunk.
- **Write lock.** New files under `docs/handoff/1.1.13/` (not governed). Not
  `scripts/`, `.github/workflows/`, contracts or ADRs: another agent is
  changing `scripts/verify.py` and `.github/workflows/ci.yml` (CI failure
  evidence), and WS-GOV will change `release.yml`.
- **Read first.** ADR 0057; the v0.9.16 plan contract and JSON and
  `scripts/v0916_parity_certification.py` (read-only); the 1.1.12
  WS-PARITY log; 1.1.12 board decisions 10 and 12 and WS-GOV decision 7b;
  owner decision 47; the
  WS-GOV drafts on `feature/1.1.13/ws-gov` (RO-1 and F-5: the rolling check is
  an extra gate, never a replacement for terminal certification); the Golden
  manifest's route-evidence kinds.
- **Acceptance.** Committed design with its path and commit, the key choices,
  and the owner questions in plain words with recommendations. Then stop; the
  commander arranges the independent design review.
- **Revision (commander, 2026-09-26).** Take in the review's F-1 to F-6 in new
  commits on the same branch, changing only this lane's files under
  `docs/handoff/1.1.13/`; also: R-5 builds on decision 47 option C; R-5
  staging covers a required comparison that failed, was cancelled or was
  skipped, and wrong source or run evidence; Q4 with Hiway and Display-OSD
  reads 39 scenarios over 37 routes; Q9 excludes the hidden General routes.
  The owner policy questions are written as pending; the commander is asking
  them. No push, no trunk merge. Report and stop.
- **P-1, first part (commander, 2026-09-26).** Draft the parts of P-1 that do
  not wait for P-0.5: the ADR draft, the rolling contracts and schemas, the
  39-scenario ledger with decision 12's 27-route debt set, the declaration
  and report schemas, and the design's 6.1 and 6.2 rows. First an R2
  capability-reuse record draft (`integrationBase`
  `84b084dd8fd218730ce75adb93b5dc2c456dd3c6`, design review to be filled),
  then the implementation content. Commit nothing: the commander arranges an
  independent design review, then admission, then implementation. Do not touch
  product code, profiles, Golden or `scripts/v0916_parity_certification.py`
  (its writer order: of WS-GOV R-1 and P-2, the batch admitted first writes,
  and the other integrates on the head after the first merges). The second
  v0.9.16 executor contract and the report-reader rules wait for the P-0.5
  evidence and get placeholders only. P-0.5 runs in another agent's test-area
  session; its results reach this log through the commander. No push, no trunk
  merge. Report the drafts, the record summary and the review focus, then
  stop.
- **P-1 revision (commander, 2026-09-26).** The P-1 draft review is
  ACCEPT-WITH-CHANGES and the drafts are not admissible yet. Still commit
  nothing; revise for F-1 to F-7 and the review's other points, splitting the
  work into an R2 record (ADR, contracts, schemas, ledger, contract tests) and
  an R3 record (the amendment and its evidence tests, finalized with an
  exact-head firmware-owner attestation). Write the P-0.5 results into this
  log. The second v0.9.16 executor contract and the report-reader rules get a
  separate record after P-0.5 and stay pending here. Report both record drafts
  and patches with their SHA-256, the test and counterexample counts and the
  points that still need the owner, then stop.

## Design review 2026-09-26

Reviewer `codex/gpt-6-astra`, drafter `claude-code`, fixed range
`cf4e42697..bef3231ed`; read-only (no build, test or verifier run). Verdict:
**ACCEPT-WITH-CHANGES**; the scoped Polytail design state was FAIL because P1
findings were open. The review text is held by the commander. It confirmed
the draft's F4 (source mismatch), F5 (parser gap and the 86/52 counts; the
issues' nature still open), F7 and F8, and accepted the direction: rolling
baseline, v0.9.16 at milestones, shared ADR 0057 owners, `certification:
none`.

| Finding | Severity | Taken in |
| --- | --- | --- |
| F-1 the 1.1.12 harness fallback cannot do the rolling comparison (its TP-work rows use the baseline's full-output prefix) | P1 | Fallback deleted (design 4.6, 8); a comparator that is not ready is owner question Q11 (hold, or a one-time reduced check the owner bounds); Q8 is not consent to a weaker comparison |
| F-2 coverage reductions were not declarable, and decision 12 was applied to every uncovered route | P1 | Coverage dispositions (retired scenario, replacing input revision, accepted gap) with firmware-owner approval, declaration entry and CHANGELOG id (design 3.2, 4.1, 4.4, 4.5, Q13); the fixed 27-id debt set with its current mapping; the 11 candidate routes named as new gaps that inherit nothing; negatives including a scenario deleted and relisted as not covered (design 7) |
| F-3 cross-version difference ranges treated as candidate write ranges | P2 | Per-side execution safety separated from cross-version attribution, naming stopped writes with preserved bytes, precursor propagation and derived CRC words; a union of write ranges is not evidence (design 3.3, 4.2) |
| F-4 local evidence can depend on user settings | P2 | Release evidence uses the CI clean-configuration policy; custom-setting runs are diagnostic; keeping a setting needs its own approval binding the setting and the selected runtime (design 3.4, Q12) |
| F-5 evidence committed after the run, then merged, changes the source identity | P2 | Declaration and CHANGELOG first, then the report of record from the exact final release source, binding source commit and tree, comparator, contracts, inventory and declaration digests; no implicit evidence transfer (design 3.4, 4.3, 4.5) |
| F-6 P-1 and P-3 risk labels understated the approvals | P2 | R2 work and R3 approvals in separate columns; decision 12 covers only the two recorded differences, not future values, Q5, Q6 or failure cases (design 6.1 to 6.3, 7) |

Also taken in: R-5 builds on decision 47 option C and its staging covers
failed, cancelled and skipped required comparisons and wrong source or run
evidence (design 5.4); the versioned reader checks format and evidence only
(design 3.3); the chain claim is limited to continuously kept scenarios and
input revisions (design 2); the v0.9.16 milestone result is a comparison with
exact approved differences (design 3.1); Q5 is limited to one canonical
binding; Q6 requires the complete lock diff and closure with their causes;
Q3 keeps the release decision with the release owner; Q7 covers coverage
dispositions; Q4 reads 39 scenarios over 37 routes; Q9 reports four separate
numbers. The review's six owner questions map to design Q10, Q11, Q12, Q5
with Q6, Q13, and Q2 with Q4 and Q9. The WS-GOV release design still lists
R-5 as depending on RO-1 approval; with decision 47 that dependency is met
(for the commander; outside this lane's lock).

## P-1 draft review 2026-09-26

Reviewer `codex/gpt-6-astra`, drafter `claude-code`; the uncommitted drafts on
`120201287` and the proposal patch `f1f990c3…`, read-only. Verdict:
**ACCEPT-WITH-CHANGES**, not admissible; the scoped Polytail state was FAIL
while P1 findings were open. It confirmed the plan binding without
`candidateAuthority` (P-2 must show the 1.x mode reads nothing from it), the
NT51950 rename to the single axis, the 39 scenarios over 37 routes, the exact
27-id debt set and the 11 gaps that inherit nothing, and found no private data
or firmware bytes. Revisions, all uncommitted:

| Finding | Severity | Taken in |
| --- | --- | --- |
| F-1 rolling and v0.9.16 rules mixed | P1 | The contract defines both modes separately (baseline, input authority, compared set, proof kinds, disposition, results); the v0.9.16 mode reads the plan's own canonical input authority and the 37 bound routes with the ADR 0057 proof kinds, and a difference or rejection is acceptable only through an exact plan or amendment row; the report schema is one of a rolling report and a v0.9.16 report with proof kind, transitive proof and disposition row per route |
| F-2 schemas admit contradictory reports and declarations | P1 | Conditional rules for formal and clear reports, every outcome and proof kind, side results and every entry kind; cross-document checks assigned to the comparator's single semantic validator; validated with the repository's Draft 2020-12 engine (JsonSchema.Net) in a new .NET test class with 7 consistent examples and 77 counterexamples, including the former "equal without candidate output" example; the Python subset checker is deleted |
| F-3 compiled-authority check missing | P1 | The contract names `validate_report_sequence`, `validate_report_projection_against_compiled_authority` and `validate_semantic_report_ranges`, with the typed Preview of the same executor and staged inputs as the authority producer; the reader may not relax them; a test shows a self-consistent widened report passing the range check and failing the authority check |
| F-4 one R2 record carries firmware authority | P1 | Two records: R2 `ROLLING-PARITY-CONTRACTS-1113-01` and R3 `V0916-1X-AMENDMENT-1113-01` with firmware-owner attestation at finalization; decision 64's citation form is stated to be the declaration's approval form only |
| F-5 report cannot hold exit codes, captures or unknown members | P2 | Per-process stage, exit code, timeout, stdout and stderr digests, unchanged inputs and captured report with reader version and unknown member names, all in the proposed schema; run, attempt and artifact binding assigned to the R-5 evidence envelope, with no claim yet to reject every wrong-run artifact |
| F-6 amendment identities not independently checked | P2 | A separate evidence test checks every bound identity against the recorded 1.1.12 evidence, the plan, the case manifests, the profile regions and the decision 12 record; the eight hand mutations are saved as tests with others (16 amendment mutations, 9 ledger mutations) |
| F-7 executor identity names differ | P2 | One name, `lockFileSetSha256`: the JCS SHA-256 of `{path, size, sha256}` of the Git-blob `src/*/packages.lock.json` files by path; a contract-to-report mapping table and a test that the recorded identity equals the report schema's executor identity |

Also taken in: ADR 0078's number is final; the certification statement names
only the contract, the amendment and reports; the four CRC words cite their
existing classifications and processor-only regions and are marked not
independently verified, while the Diff NF tail cites the alias fact scope and
the profile's Diff region; the full-flash candidate output and both rows'
candidate values are labelled historical observations; the report and
declaration schemas are marked proposed; and the missing Python Draft 2020-12
validator is recorded as a choice for P-2.

## P-0.5 results 2026-09-26

Diagnostic spike by another agent in the test area (design 12.1): a scratch
runner, not the formal comparator, and not release evidence (decision 59).
Summary at `<test-area>/parity-p05/evidence/p05-summary.json`, payload-free.

- **Executor v2 is feasible with no unexplained difference.** From a fresh
  detached worktree of `v0.9.16`, the restore with `--force-evaluate
  --runtime win-x64` rewrites 7 CLI-graph lock files: an empty
  `net10.0/win-x64` target, and first-party ranges `[0.9.2, )` to
  `[0.9.16, )` in three of them; no package id, version or content hash
  changes. NuGet writes them with CRLF and no final newline, so the 7 pinned
  post-restore SHA-256 values are Windows-specific. The default build's closure
  differs from v1 in exactly the 7 first-party DLLs, whose embedded PDBs record
  the compiler host runtime 10.0.12 (installed after v1 was pinned); forcing
  the host to 10.0.11 (`DOTNET_ROLL_FORWARD=Disable`,
  `-p:UseSharedCompilation=false`, `-nodeReuse:false`) reproduces the v1
  closure `18da1123…` bit for bit. A Git-archive build lacks the SourceLink
  record and reproduces the 1.1.12 probe closure. The owner fixed the pinning
  as decision 79; the 1.x identity comparison uses it too.
- **1.x builds.** `v1.1.12` and candidate `9b2a7369e` each built twice to
  identical closures with unchanged lock files; both have the same apphost
  hash with different closures, so the apphost never identifies an executor.
- **Issues.** Successful CtrlRAM runs report only `warning` issues
  (`input.address-space.truncated`, and on v0.9.16 also `DP_SIZE_WARNING` in
  the precursor); the only `error` is the decision-62 binding: v0.9.16's
  CtrlRAM Preview exits 1 with `profile.v2.compile.map-selection-invalid`.
  v0.9.16 reports carry no map id; 1.x reports carry `AbMergeFormat` and
  `SourceEnvelope` members outside the ADR 0057 set (for the reader record).
- **Amendment values.** v0.9.16 TP-work `cfae1591…`, `v1.1.12` and candidate
  `a239645d…` with the five recorded ranges; the full-flash precursor
  `ff9ad012…` on all three sides; `v1.1.12` and candidate full-flash output
  `1536d344…`.
- **Determinism and cost.** 303 invocations sequentially and with 3 workers
  gave identical exit codes, outputs, issues, report members and captures;
  767 s sequentially against 294 s with 3 workers for all three sides.
- **Environment.** `LOCALAPPDATA` or `APPDATA` alone does not redirect the
  per-user settings folder; `USERPROFILE` does on this machine only. The folder
  changed during one run because of other programs while the two settings
  files stayed absent. A malformed settings file makes 1.x exit without a
  report, with `AB_FORMAT_CONFIGURATION_INVALID` or
  `capability.readiness.runtime-dependency-blocked` only on stderr; custom
  settings leave outputs equal but change 50 of 104 normalized reports.
- **Path length.** With a 111-character temporary root the CLI layers reach
  260 characters and the legacy Combiner fails with
  `external-tool.process.failed` inside a written report, on all three sides
  (`BUG-20260926-legacy-combiner-long-path`).

Taken into the drafts: the compiler-host pinning and executor v2 stay pending
their own record; the settings check reads the two files and never redirects;
a refusal without a report is invalid; a 64-character temporary-root bound; a
typed rejection needs a written report with an `error` issue and no
`external-tool.process.failed`.

## Owner decisions 2026-09-26

The owner answered every question of the design on 2026-09-26. The board
text on `feature/1.1.13/wave2`, `docs/handoff/1.1.12.md` (`4a106ec81`,
`57a4c448d`), is the authority; design section 11 maps each question.

| Design question | Board decision | Answer |
| --- | --- | --- |
| Q1, Q10 undeclared differences and known issues | 57 | an undeclared difference blocks; a newly rejected input ships only as a case-by-case approved non-blocking issue; crashes, timeouts and tool or report failures are never approved rejections |
| Q11 late comparator | 58 | no automatic fallback; the commander asks the owner then: hold, or a one-time reduced check the owner bounds |
| Q12 release evidence | 59 | clean CI settings policy, exact release source, after the declaration and CHANGELOG are committed; custom-settings runs diagnostic only |
| Q13 coverage changes | 60 | declared like byte differences; decision 12's 27 stay; new gaps never inherit; the 1.1.13 approval of the 11 candidate routes is still to be asked |
| Q8 gating 1.1.13 | 61 | procedural, with the formal comparator only |
| Q5 NT51950 cascade full-flash binding | 62 | not applicable to v0.9.16 for this binding, revisited at 2.0.0 |
| Q6 v0.9.16 build recipe | 63 | executor v2 approved, effective once P-0.5 has produced its evidence |
| Q2, Q3, Q4, Q7, Q9 | 64 | as recommended: milestones only; firmware-owner approval with the board decision as authority; 39 scenarios over 37 routes; ids in the notes; supported plus candidate |

Also recorded by the commander on `feature/1.1.13/wave2`: the two findings as
`BUG-20260926-terminal-parity-rejects-ctrlram-issues` and
`BUG-20260926-changelog-1112-product-source` (`95bda40fe`), and the fixed
links of the trunk-merge bug file (`e17f7ccf5`). WS-GOV updated its R-5
dependency at `28bc715ba`.

## Checkpoints

### 2026-09-26 Design draft ready for review
State: local
Commits: the commit carrying this entry (design and this log)
Evidence: read-only inspection at `cf4e42697`, plus `feature/1.1.13/ws-gov`
`7700a7150`, `feature/1.1.13/wave2` `affc576ea` (decisions 47 and 48) and
`feature/1.1.12/governance-reset` `248726f1d` (decision 7b). Decisive facts,
each reproducible from committed files: the capability policy (catalog
1.23.0) has 85 routes, 63 `supported` and 11 `candidate`; the active manifest
binds a case to 33 supported routes and has the same case for each of them as
the plan era; all 62 plan routes still in the policy have new fingerprints;
every input of the 37 scenarios in
`docs/handoff/1.1.12/parity/v0916-local-comparison.json` exists in the active
Golden with the same SHA-256; in that file all 86 successful CtrlRAM workflow
invocations report issues and none of the 52 Standard and AB invocations do;
`git rev-parse <rev>:src` gives `3698fcec` for `badc545b0` and `a2fa6bba` for
`v1.1.12`, while `profiles`, `external-tools`, `testdata/golden/canonical` and
the policy blob are equal. No build, test or comparison was run; no firmware
byte was read beyond Git metadata and JSON.
Open: owner decisions Q1 to Q9 of the design (commander schedules the
interview); the independent design review (commander). Outside this lane's
write lock, for the commander: two findings for the bug ledger (design
section 10: the ADR 0057 terminal report check against 1.x reports,
suspected P2; the 1.1.12 notes naming `badc545b0` the product source, P3)
and one constraint for WS-GOV R-1 and R-5 (design F7: a new release job needs
a reviewed extension of the v0.9.16 workflow contract). Verification of this
commit is recorded in the next entry.
Next: structure check of this commit, then stop.

### 2026-09-26 Structure check: pre-existing broken links
State: local (verification blocked by a pre-existing failure)
Commits: `828745211` checked; the commit carrying this entry changes only
this log.
Evidence: `python scripts/verify.py --structure-only` with TEMP, TMP and
TMPDIR set to `<test-area>/temp`, at `828745211` -> `structure=FAIL`, 186 s;
`sync_derived` changed 0 files. The only errors are two broken links in
`docs/handoff/bugs/BUG-20260926-trunk-merge-flags-sealed-record.md`
(unchanged since `93a83e8a7` on the base line) to `../1.1.13/WS-GOV.md` and
`../1.1.13/ADR-DRAFT-governance-reset.md`. Both targets exist only on
`feature/1.1.13/ws-gov`; `git cat-file -e` fails for them at `cf4e42697`, at
`828745211` and at `feature/1.1.13/wave2` `affc576ea`, so the base line and
every branch built on it without the WS-GOV drafts fail the same way. The
validator's `validate_markdown_links` passes for both files of `828745211`;
the remaining output is the pre-existing code-size warnings.
Open: **commander**: record the pre-existing failure in the bug ledger
(outside this lane's write lock) and fix it before wave 2 or any branch on it
needs `policy / polytail`, for example by integrating the WS-GOV drafts first
or by turning the two links into code spans. This lane does not edit
`docs/handoff/bugs/`.
Next: stop; independent design review (commander).

### 2026-09-26 Revision after the independent design review
State: local
Commits: the commit carrying this entry (design and this log), on `bef3231ed`
Evidence: every correction of findings F-1 to F-6 and the commander's
additional points are in the design (table above). New facts, from the
committed plan, policy and manifest at `cf4e42697`: of the 27 debt-set ids,
26 are in the current policy with the same ids (22 contract-only, 4
synthetic-oracle) and 1 (`nt51950-ab-merge-1024k`, 2-plus-IC) has left it;
the 11 candidate routes (10 CtrlRAM Replace on AB maps and
`nt51950-ab-cascade-maps`) are universe routes outside the debt set without
a canonical input; 37 covered + 26 debt + 11 gaps = 74. The validator's
`validate_markdown_links` passes for both files. The structure check was not
rerun: only these two documents changed and the known pre-existing failure of
the previous entry is unchanged.
Open: owner decisions Q1 to Q13 of the design, all pending (the commander is
asking them); the bug-ledger findings and the WS-GOV R-5 dependency note of
design section 10 (commander, outside this lane's lock).
Next: stop.

### 2026-09-26 Owner decisions recorded
State: local
Commits: the commit carrying this entry (design and this log), on `288d9705d`
Evidence: board decisions 57 to 64 read with `git show
feature/1.1.13/wave2:docs/handoff/1.1.12.md` at `57a4c448d`. Design section 11
now lists every question as decided on 2026-09-26 with its decision number;
the summary and sections 3.1, 3.2, 3.4, 4.1, 4.3 to 4.6, 6.1 to 6.3 and 7
cite the decisions instead of pending questions; the seed is 39 scenarios
over 37 routes; section 10 records the commander's bug files and the WS-GOV
update; new section 12 lists the P-0.5 inputs, cost and risks and what can
start. New fact for P-0.5: on 2026-09-26 the machine's `NvtFwCombiner`
local-application-data folder holds neither per-user settings file the CLI
reads (only Desktop report history and a version-manager lock), and SDK
10.0.303 is installed. The validator's `validate_markdown_links` passes for
both files. The structure check was not rerun: only these two documents
changed, and this branch still carries the unfixed trunk-merge bug file
(fixed on wave 2 by `e17f7ccf5`) until it is rebased.
Open: the owner's 1.1.13 approval of the 11 candidate-route gaps (decision
60; commander to ask before P-3's final run); the dispatch of P-0.5 and of the
P-1 parts that do not wait for it (commander); the single-writer order on
`scripts/v0916_parity_certification.py` against WS-GOV R-1 (commander);
rebasing this branch onto the fixed base (commander).
Next: stop.

### 2026-09-26 P-1 first part drafted (uncommitted)
State: local (uncommitted drafts; nothing committed, by instruction)
Commits: none; the drafts are uncommitted changes on `120201287`
Evidence: drafts, all uncommitted: `docs/adr/0078-predecessor-comparison-for-1x-releases.md`
(number provisional) and the reciprocal `Amended by` line in ADR 0057;
`docs/contracts/predecessor-comparison-v1.{json,schema.json,md}`,
`predecessor-comparison-scenarios-v1.{json,schema.json}`,
`predecessor-comparison-declaration-v1.schema.json`,
`predecessor-comparison-report-v1.schema.json` and
`v0916-parity-1x-amendment-v1.{json,schema.json,md}`;
`tests/scripts/test_predecessor_comparison_contracts.py`. The R2 record draft
`ROLLING-PARITY-CONTRACTS-1113-01` and a proposal patch of the 13 paths are
kept outside the repository with the commander's other drafts. The ledger was
derived from the plan's pinned policy and manifest at `1d1d1cfc` and its
CtrlRAM bindings, rebound to the current policy, with artifact identities
from the active case manifests; its 37 seed scenarios match the 1.1.12 table
input by input (0 mismatches). `python -m pytest -p no:cacheprovider -q
tests/scripts/test_predecessor_comparison_contracts.py` with TEMP, TMP and
TMPDIR set to `<test-area>/temp` -> 17 passed; eight hand mutations (a widened
correction range, a changed input hash, a shortened debt set, a changed CLI
token, an AB case claiming route evidence, a changed plan digest, a successor
route in the debt set, a changed precursor hash) each fail the matching test;
`ruff check` passes; the validator's link and JSON checks pass for the new
files; `test_v0916_parity_artifacts.py -k "normative_parity_documents or
plan_pins"` -> 2 passed with the ADR 0057 header line. No structure check was
run, since the drafts are not staged.
Open: the independent design review of the record draft and the proposal
diff, and the ADR number (commander); the P-0.5 evidence for the second
v0.9.16 executor contract and the report-reader rules; the owner's 1.1.13
approval of the 11 candidate-route gaps; the review points in the report to
the commander.
Next: stop.

### 2026-09-26 P-1 drafts revised after the draft review (uncommitted)
State: local (uncommitted drafts; nothing committed, by instruction)
Commits: none; the drafts are uncommitted changes on `120201287`
Evidence: the drafts now form two records, kept outside the repository with
their proposal patches: R2 `ROLLING-PARITY-CONTRACTS-1113-01` (11 paths: ADR
0078, the ADR 0057 header item, the seven predecessor-comparison files, the
.NET schema test class and the Python contract tests; record draft SHA-256
`913bd0e8a5683acec2a7eb648c9a062ecdb04cb28910adaf9cfac0f1847113e3`, patch
`38435a2a7ce39b308f28d1c2fa93aae4fed205c22887cab73d9ea8abd1758c4a`) and R3
`V0916-1X-AMENDMENT-1113-01` (5 paths: the amendment JSON, schema and prose,
its evidence tests and two lines in the existing parity schema tests; record
draft `49e3aa9e38e6bcc369eba469992e1cfb92b441744eed8eae48f6d78fe3f88d0c`,
patch `c886eb429c5d6e7511c3eaed0bc56f9bee3d61717dcb7d14be510a09d1b70709`).
Both patches pass `git apply --check --cached` on `120201287`; this log is in
neither. With TEMP, TMP and TMPDIR set to `<test-area>/temp`: `dotnet test`
of the Infrastructure tests filtered to `PredecessorComparisonSchemaContractTests`
and `V0916ParitySchemaContractTests` -> 109 passed (90 new: 4 meta-schema, 2
committed documents, 7 consistent examples, 77 counterexamples; 19 parity
schema tests including the amendment); a one-off diagnostic run confirmed
that each counterexample fails at its intended rule. `python -m pytest -p
no:cacheprovider -q` of the two Python modules -> 21 passed (14 contract, 7
amendment), with 9 ledger, 16 amendment and 2 widened-report mutations
rejected; `test_v0916_parity_artifacts.py`, `test_v0916_parity_contracts.py`
and `test_v0916_parity_reports.py` -> 72 passed, 1 skipped; `ruff check`
passes; the validator's link and JSON checks pass; the repository's record
validator reports only the expected missing admitted design review for both
drafts. The first `dotnet test` restore rewrote nine `src/*/packages.lock.json`
files; they were restored from Git before any other step, and later runs used
`--no-restore`.
Open: the re-review of both record drafts and patches (commander); the owner's
1.1.13 approval of the 11 candidate-route gaps; the firmware owner's exact-row
confirmation for the R3 record at finalization; the executor record (executor
v2 and the compiler-host pinning of decision 79) and the reader record after
P-0.5; the P-2 choice of a runtime Draft 2020-12 validator; re-confirming
`integrationBase` at admission.
Next: stop.

### 2026-09-26 R2 revised after the P-1 re-review (uncommitted)
State: local (uncommitted drafts; nothing committed, by instruction)
Commits: none; the drafts are uncommitted changes on `120201287`
Evidence: the re-review (`codex/gpt-6-astra`) accepted the R3 amendment design
and left two P1 findings on R2. F-2: every process of a rejected side now
needs `timedOut` false and a written report, and a precursor comparison needs
both precursor identities. F-8: a declaration side binds its output and
precursor identities, and `differences` binds the output and the precursor
scopes separately, each with its own ranges, digest and attribution. New .NET
cases: 2 positive declarations (precursor only; output and precursor), 2
report scenarios (the same two cases), and 10 counterexamples, including a
legal rejection with an extra timed-out process and one with an extra
reportless process. With the new rules removed, exactly the 4 counterexamples
that target them are accepted, so each fails at its intended rule. A Python
check shows a declaration entry reproducing precursor-only and combined
differences, and rejecting 6 drifts on each (precursor hash or range in the
run or in the declaration, a precursor difference declared as an output
difference, an attribution gap). With TEMP, TMP and TMPDIR set to
`<test-area>/temp` and `--no-restore`: the filtered .NET schema tests -> 121
passed (102 predecessor: 4 meta-schema, 2 documents, 9 examples, 87
counterexamples; 19 parity schema tests); the two Python modules -> 22
passed; `ruff check` passes; no lock file changed. Records: R2
`ROLLING-PARITY-CONTRACTS-1113-01` design review still blocked, with the
re-review's R2 text (record draft
`ff44bc406fb4009821afdbe8263b925e299d977a5fd56660d7b5d3698f4c55ba`, patch
`07fc9b54740eee878cb7c7f0d68d09523dc7cd256ef8fcc61c233b68fc0edf90`); R3
`V0916-1X-AMENDMENT-1113-01` design review approved with the re-review's R3
text (record draft
`491c5df49574a2a552a6fdc75a6aac6bc3482d85f4d9f54b70a82a50c82ebc1b`, patch
unchanged at `c886eb429c5d6e7511c3eaed0bc56f9bee3d61717dcb7d14be510a09d1b70709`).
Both patches pass `git apply --check --cached`; the record validator reports
only R2's missing admitted design review.
Open: the R2 re-review (commander); joint admission of both records; the
owner's 1.1.13 approval of the 11 candidate-route gaps; the firmware owner's
exact-head attestation at R3 finalization.
Next: stop.

### 2026-09-27 R2 revised after the third review (uncommitted)
State: local (uncommitted drafts; nothing committed, by instruction)
Commits: none; the drafts are uncommitted changes on `120201287`
Evidence: the third review closed F-2 and F-8, kept the R3 acceptance, and
left F-9 and F-10 on R2. F-9: the output branch of a side result now also
rejects process-failure codes at every severity, which the v0.9.16 mode's
shared output side inherits; ordinary warnings stay accepted. F-10: a
declaration difference carries its complete range list without the 32-range
cap; the reproduction check compares it with the complete computed ranges
and checks separately that the report carries their first 32 with the full
count, byte count and digest. New .NET cases: 1 positive declaration and 1
report scenario with 33 output and 33 precursor ranges, and 3 counterexamples
(a `warning` and an `info` process failure on a rolling output side, and a
`warning` one on a v0.9.16 output side). With the two new rules reverted,
exactly those 3 counterexamples and the 33-range declaration change result.
The Python reproduction check now covers 3 runs (precursor only; output and
precursor; 33 ranges each) with 26 drifts, including a 33rd range changed in
the run or in the declaration, a declaration cut to the first 32 ranges and a
report whose first 32 ranges differ. With TEMP, TMP and TMPDIR set to
`<test-area>/temp` and `--no-restore`: the filtered .NET schema tests -> 125
passed (106 predecessor: 4 meta-schema, 2 documents, 10 examples, 90
counterexamples; 19 parity schema tests); the two Python modules -> 22
passed; `ruff check` passes; no lock file changed. R2 record draft
`ea506477de1a7143ff88310b72d43dd53e328c9fd9d05a2e91f9ccf9a09864e3` (design
review blocked, with the third review's text), patch
`a6d824be0d90b2cb2066bd21ccac5fc80fb330c89c1308c95a4685ad08f2b277`; R3
record draft and patch unchanged. Both patches pass `git apply --check
--cached`.
Open: the R2 re-review (commander); joint admission of both records; the
owner's 1.1.13 approval of the 11 candidate-route gaps; the firmware owner's
exact-head attestation at R3 finalization.
Next: stop.

### 2026-09-27 P-1 design accepted; admission on the batch 2a checkpoint
State: local commits on `feature/1.1.13/parity-contracts` (no push)
Commits: the commit carrying this entry, then the admission commit and the
two implementation commits
Evidence: the fourth review (`codex/gpt-6-astra`) accepted R2 with F-9 and
F-10 closed, kept the R3 acceptance, and cleared both records for joint
design admission. Batch 2a merged into `1.1.x` (`54974d5cc`); its evidence
checkpoint `38b85b15b861027972a9ca6945411f57220c4c47` is the `integrationBase`
of both records. The branch starts from `feature/1.1.13/wave2` `b7249de8f`;
this one commit carries the handoff documents of
`feature/1.1.13/rolling-parity` (`828745211` to `120201287`) and this log.
Between `120201287` and `b7249de8f` the policy, manifest and NT51950
profiles changed capability fingerprints, projection digests and an NT51950
metadata search range; the NVT end-flag work also replaced four
synthetic-oracle `expectedSha256` values (owner decision 48, attested) and
changed AB metadata declarations and map bindings. The plan, case and
artifact, region and historical output identities the P-1 documents bind did
not change (independent final review, 2026-09-27). The P-2 drafts stay uncommitted outside this branch.
Open: the fixed-head final review (commander); the owner's 1.1.13 approval
of the 11 candidate-route gaps; the R3 firmware-owner attestation at
finalization.
Next: the admission and implementation commits, then stop.
