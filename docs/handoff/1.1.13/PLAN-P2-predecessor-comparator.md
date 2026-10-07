# P-2: formal predecessor comparator, design summary and plan

Status: draft plan, uncommitted, on `feature/1.1.13/parity-p2` from `1.1.x`
`e6e991af3`. Lane: [WS-PARITY](WS-PARITY.md). Specification: the P-1
contracts ([contract](../../contracts/predecessor-comparison-v1.md),
[amendment](../../contracts/v0916-parity-1x-amendment-v1.md),
[ADR 0078](../../adr/0078-predecessor-comparison-for-1x-releases.md)), merged
with `ROLLING-PARITY-CONTRACTS-1113-01` and `V0916-1X-AMENDMENT-1113-01` in
#458 and #459; evidence checkpoint `241327de1`. Design background:
[design](DESIGN-rolling-parity.md) sections 3 to 7.

## 1. What P-2 delivers

One comparator, `scripts/predecessor_comparison.py`, that runs either mode of
the contract and writes one payload-free report; one semantic validator used
by the comparator and by every test; and the additive entry points it needs in
the ADR 0057 script. It produces no report of record by itself: a formal run
stays refused (`PREDECESSOR_CONTRACT_PENDING`) until the reader and executor
records below are in effect, and P-3 runs it for 1.1.13.

## 2. Modules and owners

| File | Content | Owner of the semantics it uses |
| --- | --- | --- |
| `scripts/predecessor_validation.py` (new) | the single semantic validator: pure functions over JSON and computed values, no process or Git access | the P-1 contracts |
| `scripts/predecessor_report_reader.py` (new, report reader) | versioned CLI report reader: raw report to the normalized projection, format only | ADR 0057 normalizers |
| `scripts/predecessor_comparison.py` (new) | CLI entry, Git authority, executors, staging, process runner, side results, byte comparison, report assembly, gate | ADR 0057 primitives, the validator, the reader |
| `scripts/v0916_parity_certification.py` (additive only) | public entry points, no behavior change on the terminal path | ADR 0057 |

The ADR 0057 functions stay the only implementation of what they do:

| Need | Reused as is | Additive public entry point (P-2) |
| --- | --- | --- |
| Git snapshot, canonical Golden materialization and validation | `PinnedGitReader`, `materialize_and_validate_canonical_input_authority` (rolling mode passes a descriptor computed from the candidate commit) | none |
| Case resolution and input admission | `resolve_canonical_route_input` for the v0.9.16 mode | `resolve_case` (alias of `_resolve_case`) and `admit_case_inputs`, the admission loop factored out of `resolve_canonical_route_input` with identical behavior, for ledger scenarios |
| CLI arguments | `_cli_selection_token` | `cli_arguments`, `input_option` (aliases) |
| Custody and staging | `hold_read_only_file_custody`, `materialize_execution_closure`, `write_json_exclusive_atomic`, `detached_git_worktree`, `LocalExecutionHost` | none |
| Runtime closure digest | the inventory rule inside `validate_verified_source_executor` | `runtime_closure_inventory`, factored out with identical behavior, so a closure can be measured without a pinned expectation |
| Byte comparison with approved rows and transitive proof | `compare_approved_semantic_correction_payloads`, `compare_transitive_payloads` | none |
| Per-side safety | `validate_report_sequence`, `validate_report_projection_against_compiled_authority`, `validate_semantic_report_ranges` | `normalize_raw_operation`, `normalize_raw_mutation` (aliases) for the reader |
| Digests | `canonical_json_sha256`, `load_json_reject_duplicates` | none |
| Release notes | `render_release_notes.render_release_notes` (read-only) | none |

The terminal parser `_validate_raw_report` is not reused for 1.x reports: its
empty-`Issues` and fixed-member rules are terminal format rules (F5); the
reader replaces only that format step.

## 3. The single semantic validator

`scripts/predecessor_validation.py` implements the contract's cross-document
list, each check returning typed failures (`code`, `subject`, `detail`) with the
contract's codes:

1. **Ledger**: scenario binding to the candidate policy and CLI selection;
   inputs against the case manifests and the plan's CtrlRAM bindings; debt set
   equal to the plan's 27 ids; partition and completeness; monotonicity
   against the baseline tag's ledger; every accepted gap approved in this
   release (1.1.12 board decision 60, 1.1.13 decision 96), so a gap row
   carried from an earlier release approves nothing.
2. **Report coverage**: the rolling report runs each ledger scenario exactly
   once with its route and input revision, and reports the ledger's counts,
   the routes no scenario compares with their reasons and evidence kinds,
   and every coverage change since the baseline ledger; an accepted gap is a
   change when its row is new or renewed, never judged by route id alone.
3. **Declaration**: its version, baseline and ledger digest; unique ids of
   this version; scenario ids known to the ledger or retired; each id in the
   candidate's CHANGELOG section (through `render_release_notes`).
4. **Declared changes**: every outcome other than `equal` and `invalid` and
   every coverage change other than an added scenario is declared by exactly
   one entry, subject by subject, and every declared subject happened; an
   entry's route ids are its scenarios' routes; the report names the entry
   that declares each change; a new gap's or retirement's ledger record
   cites that entry, this version and the entry's approval; each outcome
   entry reproduces both sides (output and precursor identities, stage,
   `error` codes) and each compared scope's complete computed ranges, byte
   count, digest and exact attribution.
5. **Scope evidence**: for the output and the precursor, a scope both sides
   carry has computed evidence (the complete range list, or none when the
   bytes are equal) that agrees with the identities; the report comparison
   is its projection with the first 32 ranges; a scope a side lacks has no
   evidence or comparison. This covers every outcome, rejections included,
   so a precursor difference beside a rejection is reported and declared.
   Which artifacts a side carries follows the ledger: a side of a
   `standard-merge` scenario has a precursor unless it stopped at a
   precursor stage, and a side of any other scenario has none.
6. **Source binding**: candidate commit and tree, baseline tag object and
   peeled commit, the digests of the contracts, ledger and declaration at the
   release source, the comparator script digest.
7. **v0.9.16 mode**: the amendment's plan binding; the plan's 64 routes with
   their proof kinds from the plan and the amendment, each reported once;
   evidence required for every run route; exact-output identities against
   the evidence; each correction and not-applicable row reproduced exactly;
   the transitive proof's full route and TP length bound to the plan, its
   evidence the actual result of the ADR 0057 `compare_transitive_payloads`
   run with the plan's TP length (the checks it returned, or the failure it
   raised, through `transitive_evidence`), the reported checks what that
   result fixes, a passing proof's TP output size equal to that length, and
   consistency only with a passing proof and a consistent full route; a
   proof that cannot run (the candidate TP output or a full-route output is
   missing) is asked for no bytes, and the route is `inconsistent` with
   `PREDECESSOR_UNAPPROVED_DIFFERENCE` after a typed rejection or `invalid`
   after a failure, reporting `transitive: null`; an `invalid` route
   carries a shared execution failure code; summary and result. Each
   route's consistency is its proof kind's as the contract states it; a
   precursor matters only through a not-applicable row.

`rolling_report_failures` runs items 1 to 6 for one report, and the rolling
gate is clear only when it returns nothing; `v0916_report_failures` runs
item 7. The P-1 contract tests' error-list helpers and the amendment tests'
plan-binding helper are replaced by calls to this module and deleted (their
stated deletion criterion); the P-1 reproduction test moved into this
module's tests. The comparator in stage 3 passes loaded documents,
preloaded case manifests and computed evidence in (byte comparisons from its
own complete range computation, transitive proofs from the ADR 0057
primitive); the validator does no Git, file or process access. Each test
builds a fresh valid document set, shows that it passes, then changes one
fact and expects that rule's failure and a blocked gate.

## 4. Execution flow

**Rolling.** Resolve the baseline (annotated tag, ancestor, published; a
local run takes the tag it is given) -> compute the Golden descriptor at the
candidate commit and materialize it -> validate the ledger (step 1) -> build
both executors in fresh detached worktrees with the 1.x recipe and record
their identities -> for each scenario: admit and stage the ledger inputs,
build the CtrlRAM precursor with each side's own Standard Merge when declared,
run Preview and Build per side under custody, read each report, apply
per-side safety, compute the complete byte comparison of outputs and
precursors -> classify outcomes -> validate the declaration and reproduction
(steps 2 and 3) -> assemble the report, compute the gate, write it with
`write_json_exclusive_atomic`.

**v0.9.16 1.x.** Check the amendment binding -> materialize the plan's
canonical input authority -> build the v0.9.16 executor from the executor v2
contract and the candidate with the 1.x recipe -> run the 37 bound routes with
the plan's inputs and CLI arguments -> apply each route's proof kind -> the
27 unbound routes as not covered -> result and report.

**Processes.** One runner for both modes: per-process temporary directories
under a root of at most 64 characters (refused otherwise), `TEMP`, `TMP` and
`TMPDIR` set to them, working directory in the staging root, a timeout, stdout
and stderr captured and hashed, inputs hashed before and after, and a written
report captured when present. Unlike the terminal runner, a nonzero exit is
recorded, not raised, so that a typed rejection can be told apart from a
process failure (the contract's typed-rejection rule). The two per-user
settings files are checked before and after the whole run and after each
process.

**Schema conformance without a Python validator.** The comparator builds
reports and reads declarations through typed builders and the validator. Its
tests write the reports of their synthetic runs as fixtures, which the .NET
Draft 2020-12 tests validate; a Python test checks that the builder
reproduces each fixture byte for byte. Adding `jsonschema` to CI would change
`.github/workflows/ci.yml` (another lane's R3 path) and is not needed now.

## 5. Records and order

| Record | Risk | Paths | Authority and review |
| --- | --- | --- | --- |
| `PREDECESSOR-EXECUTOR-1113-01` | R3, firmware and release authority | `docs/contracts/v0916-baseline-executor-v2.{json,schema.json,md}`; the executor sections of `predecessor-comparison-v1.{json,schema.json,md}` (compiler host pinned) and of the amendment (`baselineExecutor` in effect); contract tests | decisions 63 and 79; exact-head firmware-owner and release-owner approval (firmware and release authority), the P-0.5 lock-file diff and closure as evidence |
| `PREDECESSOR-REPORT-READER-1113-01` | R2, kind release | `scripts/predecessor_report_reader.py`; the reader rules in `predecessor-comparison-v1.{json,md}`; the report and declaration schemas from proposed to in effect, with the P-0.5 revisions; reader tests with payload-free raw-report fixtures | P-0.5 findings: issue severities, stderr-only codes, v0.9.16 without `MapId`, 1.x `AbMergeFormat` and `SourceEnvelope`; from the stage 1 review, `transitive: null` for a transitive route whose proof cannot run (the proposed report schema still requires a proof object on every transitive route) |
| `PREDECESSOR-VALIDATION-1113-01` (stage 1) | R2, kind release | `scripts/predecessor_validation.py`; the public names in `scripts/v0916_parity_certification.py`; tests `test_predecessor_validation.py` and the migrated `test_predecessor_comparison_contracts.py` and `test_v0916_parity_1x_amendment.py` | independent exact-head review; ADR 0057 tests unchanged |
| `PREDECESSOR-COMPARATOR-1113-01` (stages 2 and 3) | R2, kind release | `scripts/predecessor_comparison.py`; the behavior-identical factorings in `scripts/v0916_parity_certification.py`; `tests/scripts/test_predecessor_comparison.py`; the report fixtures and their rows in `PredecessorComparisonSchemaContractTests.cs`; each path listed only once it exists in that batch's diff | independent exact-head review; ADR 0057 tests unchanged |

Each record lists exactly the paths its own batch changes, so no record names
a file a later stage creates. Order: P-1 (merged) -> stage 1, its own batch
-> the reader and comparator records as one batch with disjoint paths (the
commander agreed) -> the executor contract activation (R3), before P-3's first formal
run. A formal run needs all four. The executor and reader contract edits
touch `predecessor-comparison-v1.*` in different batches, so they are
sequential.

**Single writer on `scripts/v0916_parity_certification.py`.** Board rule: of
WS-GOV R-1 and P-2, the batch admitted first writes and the other integrates
on its head. R-1 has not started; P-2's additions are small and additive, so
a later R-1 rebase is cheap. The commander confirms the slot at P-2 admission.

**Branch.** P-2 drafts moved from `feature/1.1.13/rolling-parity` to
`feature/1.1.13/parity-p2` from `1.1.x` `e6e991af3`, after a checksummed
backup; the P-1 copies there were identical to the trunk or older and were
dropped.

## 6. Tests

Synthetic payload-free byte fixtures only (no Golden bytes), with a fake
process runner and fake Git host, plus the negatives of design section 7:
undeclared difference; stale entry and changed range list (including beyond
the 32 reported ranges); a scenario removed and relisted as not covered
without disposition; an input revision without disposition; a new universe
route without scenario or gap; a successor route claiming the debt set; a
crash, timeout, report failure or `external-tool.process.failed` presented as
a rejection; a union of write ranges as attribution; stopped writes and
precursor-carried changes that pass only with their exact attribution; wrong
baseline; missing CHANGELOG id; report from another source or with other
digests; an `error` issue on an output; a report disagreeing with its output;
a self-widened report; a settings file present in a formal run; a rewritten
lock file; a v0.9.16 correction mismatch; a not-covered route counted as
compared; a temporary root over 64 characters. The ADR 0057 test modules run
unchanged. One test-area rehearsal against the real `v1.1.12` and a candidate
(diagnostic, not evidence) closes P-2 before P-3.

## 7. Stages and estimate

| Stage | Content | Estimate (agent working time) |
| --- | --- | --- |
| 1 | this plan; `predecessor_validation.py` (items 1 to 7 of section 3 and the gate), with isolated mutation tests; the P-1 helper migration | v4 done after the third design review (P2DR3), awaiting re-review |
| 2 | reader module and fixtures; additive parity-script entry points | 0.5 to 1 day |
| 3 | comparator orchestration: executors and identity, staging and runner, both modes, report assembly and gate; fake-host tests | 1.5 to 2.5 days |
| 4 | executor v2 contract; test-area rehearsal; review corrections | 1 to 1.5 days |

Basis: the size of the reused ADR 0057 pieces and of P-1 (four review rounds
of about half a day each). Uncertain mainly in stage 3 (process custody on
Windows, precursor handling) and in review rounds; independent reviews and the
owner's approvals are not included.

## 8. Decisions and open points

- Decided (1.1.13 board decision 96, 2026-09-27, the owner as firmware
  owner): the 11 candidate routes without canonical inputs are accepted gaps
  for 1.1.13 only, each with its own declaration entry and CHANGELOG id; the
  acceptance passes to no later version, successor or renamed route, and
  adding canonical inputs stays later work. P-3 moves them from
  `pendingAcceptedGaps` to `acceptedGaps` with `approvedInVersion` 1.1.13 and
  declares them in `predecessor-comparison-declarations/1.1.13.json`, each
  approval citing decision 96.
- Follow-up from the stage 1 review (P2DR3), a separate contract record: write
  the release scope of accepted gaps (1.1.12 board decision 60, 1.1.13
  decision 96) into `predecessor-comparison-v1.md`, which the validator
  already enforces from those decisions: each gap row is approved in the
  release it covers, and a renewal is a declared coverage change.
- Follow-up from the stage 1 review (P2DR3), required of
  `PREDECESSOR-REPORT-READER-1113-01` before the reader or the comparator is
  enabled: the report schema matches the validator's transitive rules, so
  `transitive` may be null only when the proof cannot run, a runnable proof
  is never missing, and a `consistent` route carries a passing proof.
- Decided by the commander: the reader and comparator records go as one
  batch; the parity script stays with this lane's single writer while WS-GOV
  R-1 has not started.
