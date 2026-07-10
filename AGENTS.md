# NVT FW Combiner Agent Instructions

## Mission

Build a deterministic firmware-image composition tool. Correct byte behavior, one unified execution model, explicit address spaces/ranges, constrained external processing, traceability, and golden regression take precedence over UI speed or code volume.

## Instruction precedence

1. Follow this file for repository-wide rules.
2. Follow the nearest nested `AGENTS.md` for files below that directory.
3. Follow approved ADRs, schemas, profiles, and executable tests when they are more specific.
4. `refcode/` is immutable evidence only: exactly two Python snapshots and no NFCG TypeScript source.

## Canonical sources

- `SPEC.md` — product and high-level engineering source of truth.
- `docs/adr/`, `docs/architecture/`, `docs/contracts/` — durable decisions and versioned contracts.
- `profiles/` — IC facts and declarative composition behavior.
- `src/` — production code; `tests/` and `testdata/` — executable evidence.
- `.agents/skills/polytail/` — mandatory anti-slop completion/review workflow.

## Canonical commands

```text
python scripts/verify.py --structure-only
python scripts/verify.py --all
python -m pytest                  # narrow run from tools/crc-worker
```

Do not invent a second canonical repository verification entry point.

## Execution cadence

### Preflight

Before a non-trivial change:

1. Run `git status --short --branch` and record existing user changes without modifying or staging them.
2. State the risk class (`R0`-`R3`), affected layers, acceptance criteria, required evidence/human gate, narrow test, and final verification gate.
3. Inspect the relevant source, contract, profile, and test once before editing. Do not begin with repeated broad searches or speculative test runs.

### Test ladder

1. Run formatting and the narrowest affected test first.
2. Add affected contract, integration, or golden tests only when the change crosses those boundaries.
3. Run `python scripts/verify.py --all` once as the final local gate for `R1`-`R3` changes when the environment supports it. It is not a diagnostic retry command.
4. An `R0` documentation/governance-only change with no executable contract, command, or fixture impact may finish with `python scripts/verify.py --structure-only`; PR CI remains authoritative for the complete repository gates.

Use `docs/governance/development-execution-workflow.md` to select the narrow test. Do not add a parallel verifier for convenience.

### Commit and handoff gate

- Every completed, independently verifiable editing phase must be committed autonomously on the current non-`main` branch before another editing phase begins. A phase is one coherent code-and-test slice, documentation decision, evidence inventory, UI slice, or release change that can be reviewed on its own.
- A phase is ready to commit when its scope is frozen, its phase-local test passes, `git diff --check` and the reviewed diff contain no generated/private payloads, and residual evidence gates are recorded. `python scripts/verify.py --all` remains the final `R1`-`R3` handoff/PR gate; it is not required before every intermediate phase commit.
- Stage only the explicit files owned by the completed phase. Never use `git add -A` or `git add -u`; do not stage, amend, reset, or revert another agent's changes.
- Keep code, tests, documents, generated output, and evidence intake in separate commits unless they are required to validate one coherent behavior change. Use a Conventional Commit message that names the phase outcome.
- Do not commit exploratory output, temporary staging data, real firmware, or a work-in-progress checkpoint that cannot be independently reviewed. If another agent's overlapping uncommitted changes make the phase boundary unclear, stop and request direction instead of mixing work.
- `R3` work may be committed only on a non-`main` branch with its human-review and evidence gaps explicit; it must not be represented as complete or merged before those gates pass.

### Retry budget

1. On the first failure, capture the command/result and classify it as invocation, input/evidence, assertion, or environment failure.
2. Retry only when the command, input, code, or environment has materially changed; state that change before rerunning.
3. Do not run the same failing command more than once after its first failure. Use a smaller diagnostic or report the blocker instead of escalating immediately to a full verification run.
4. For a recurring multi-step diagnostic, add or improve a focused tested script rather than repeatedly composing large ad hoc shell commands.

## Branch, PR, review, and merge rules

- `main` is stable. Agents must not push implementation or documentation changes directly to `main` unless the owner explicitly requests an emergency single-file administrative edit.
- Each planned release has one owner-selected version integration branch named with the exact version, for example `0.8.1`. Tightly coupled work for that release commits directly to this branch.
- An independently reviewable feature must use `feature/<version>/<topic>`, created from the version integration branch. Its reviewed merge target is that same version branch, never `main`.
- Only after the version branch scope, final verification, and review gates are complete may it open a PR to `main`. A merge commit, squash merge, or rebase merge is acceptable only when it preserves the reviewed version-branch change set and owner-approved release intent.
- Keep PR scope reviewable. Avoid PRs that mix unrelated UI, core, dependency, release, and documentation work; also avoid tiny PRs that cannot be validated independently.
- Every PR must identify scope, risk class, affected layers, contracts/profiles/ICs, verification commands, residual evidence gaps, and whether human firmware review is required.
- Agent-authored PRs require a reviewer other than the implementer. The implementer must run Polytail before requesting review; the reviewer must apply Polytail before approval.
- Agent-authored PRs must request Codex review by commenting `@codex review` or the owner-requested equivalent. Inspect thread-aware Codex review comments, address actionable findings, rerun required checks, and request re-review after fixes.
- `R2` changes require architecture/contract review. `R3` changes require human firmware-owner review and byte-level evidence before merge.
- Do not merge with failing required checks, unresolved P0/P1 review findings, missing required tests, undocumented schema/profile drift, or private golden evidence gaps disguised as TODOs.
- Merge PRs to `main` only after required CI is green and actionable Codex feedback is addressed or explicitly documented as non-actionable.
- If a connector/tool cannot open a feature PR, push the feature branch and leave a clear handoff for merge into its version branch. If it cannot open the final PR, push only the version branch and leave a handoff with commit SHA, changed files, risks, commands run, and `main` as the intended target.

## Mandatory architecture boundaries

- `NvtFwCombiner.Domain` is pure and does not reference filesystem, process, UI, Avalonia, JSON serialization, or infrastructure.
- `NvtFwCombiner.Application` owns use-case policy through ports; it does not start processes or render UI.
- `NvtFwCombiner.Infrastructure` implements filesystem, staging, process, profile, and report adapters; it does not redefine firmware semantics.
- UI and CLI create typed requests and call the same application services.
- `NvtFwCombiner.Cli` remains a thin process entry point. `NvtFwCombiner.Bootstrap` may route commands, but new CLI command groups must be implemented as focused command handlers so `CliApplication` stays a router. CLI and Bootstrap handlers must not duplicate firmware semantics.
- Every workflow uses one composition planner/executor. Merge initializes blank bytes; Replace clones a required reference image.
- General Merge and General Replace compile `explicitMappings` to normal operations; arbitrary scripts and per-run executable paths are forbidden.
- All ranges are half-open `[start, endExclusive)` and name their address space.
- Profiles own regions, atomicity, access, mappings, overlap, processors, validations, and output naming.

## Locked experience rules

- DP Replace: DP whole/declared partitions only; no TP-persona Replace categories are exposed.
- CtrlRAM Replace: only regions tagged `tp-ctrlram` or approved CtrlRAM groups may be replaced.
- General Replace: explicit mappings only inside the profile-approved safety envelope.
- General Merge: extensible BIN bindings and explicit mappings over a blank image.
- Experience/personal labels control authoring policy, never byte-execution branches.

## Firmware safety rules

- Never change a range, offset, operation order, atomicity, patch, checksum/header rule, padding, or naming token without evidence and tests.
- `unknown` integrity behavior is not `none` and cannot compile as supported behavior.
- Input/reference artifacts are immutable. Use named work buffers and atomic output promotion.
- Python, legacy `combiner.exe`, and every external processor may modify only a host-created staging copy. They never modify the user's source BIN or final output path.
- The host independently diffs staged before/after bytes and rejects every change outside declared write ranges.
- Never add real firmware BIN files, credentials, generated releases, or private golden data to Git, except owner-approved golden fixtures under `testdata/golden/` with manifest paths, sizes, hashes, source provenance, and human approval recorded.
- For standard merge regression, prefer owner-approved `gen_flash_bin_v2` / `gen_flash` fixtures under `testdata/golden/standard-merge-gen-flash/` as golden evidence when they cover the IC behavior being implemented.

## Product direction guardrails

- Prioritize completing core/Application/CLI capability before UI wiring when the owner asks for spec development. Call out when C# core is ready for UI integration.
- Current implementation priority is normal Merge and normal Replace for DP Replace and CtrlRAM Replace workflows. AB Code Merge is deferred unless the owner explicitly reactivates it.
- Standard/normal Merge must include NT51950 and NT51951 after the owner provides the memory map. Do not infer their merge map from AB evidence.
- Replace flows are expected to require legacy `combiner.exe` CRC/header recalculation. Use the external combiner runner model and wait for owner-supplied invocation, version, ranges, and golden evidence before production implementation. The owner identified 932 common FW postbuild as the reference behavior to inspect.
- Input BIN size mismatch policy is profile-declared and integrity-aware: no-CRC/no-processor flows, such as DP-only Replace, may pad shorter immutable replacement/source inputs only with an explicit profile padding byte. CtrlRAM Replace may truncate oversized immutable replacement/source inputs only with an explicit profile truncation policy whose operations target `tp-ctrlram` regions, and must surface a report/CLI warning. Runtime/request mappings, mutable work buffers, reference/base firmware, and non-CtrlRAM CRC/combiner flows must keep exact input length. Unapproved oversized input still fails closed.
- UI should be modern, minimal, and low-reading-cost. Top-level product navigation is limited to Settings, Merge, and Replace unless the owner explicitly expands it.
- UI top-level navigation uses top tabs.
- Merge and Replace must share a consistent Memory coverage before/after visualization in the same layout position. The Memory coverage area is visual-first, with table details as supporting information.
- Firmware inputs are represented as slot cards.
- Replace UI must include an explicit IC num selector/input in addition to persona and file slots. First usable UI supports `single` and `cascade` IC num input modes; `numeric` is reserved in contracts for future IC exceptions and must not be enabled without owner evidence.
- Preview/Build results open report modals for diagnostics and evidence review.
- First UI release hides Saved Rules until the saved-rule workflow is implemented and reviewed.
- UI implementation must support a bilingual architecture with English as the initial default language.
- UI typography uses the bundled `fonts:Inter#Inter` family for English/Latin text, Microsoft JhengHei UI with Noto Sans CJK TC/Noto Sans TC fallbacks for Traditional Chinese, and Cascadia Mono/Consolas only for fixed-width technical values.
- Release work should keep `main` capable of producing a self-contained `.exe` folder that does not require a separate C#/.NET runtime install before distribution.

## Required workflow

Before editing:

1. Read the issue, nearest instructions, relevant spec/ADR/schema/skill, and actual tests/evidence.
2. State affected layers, composition kind, experience, IC/mode/address spaces, and invariants.
3. Keep non-mechanical changes reviewable; split changes over roughly 500 lines when practical.

After editing:

1. Format changed files and run the narrowest meaningful tests.
2. Apply the repository `polytail` skill to every non-trivial change.
3. Apply the final gate defined by the test ladder: `--all` for `R1`-`R3`; `--structure-only` is permitted only for qualifying `R0` documentation/governance changes.
4. Report commands/results, firmware/profile/protocol/release impact, and missing private evidence.
5. For branch work, prepare PR-ready notes: summary, changed files, risk class, tests, required reviewers, and merge target.

## Definition of done

A change is incomplete while it has placeholders, duplicate semantics, magic offsets, broad suppressions, swallowed errors, fake tests, failing checks, undeclared mutations, documentation/schema drift, missing required human review, or an unreviewed path into `main`.

## High-risk human gate

Human approval is required for profile/schema/protocol breaking changes; memory ranges/atomicity/access; CRC/checksum/header algorithms/order; processor read/write ranges; golden outputs; release signing/publishing/permissions/secrets; branch protection/CODEOWNERS; network/process authority; and new runtime dependencies.
