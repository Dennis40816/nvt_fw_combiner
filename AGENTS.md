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

## Branch, PR, review, and merge rules

- `main` is stable. Agents must not push implementation or documentation changes directly to `main` unless the owner explicitly requests an emergency single-file administrative edit.
- Development happens on the active milestone branch, for example `0.1.0`, or on a `feature/<topic>` branch created from the active milestone branch.
- Work reaches `main` only by pull request review and merge. A merge commit, squash merge, or rebase merge is acceptable only when it preserves the reviewed change set and the owner-approved milestone intent.
- Keep PR scope reviewable. Avoid PRs that mix unrelated UI, core, dependency, release, and documentation work; also avoid tiny PRs that cannot be validated independently.
- Every PR must identify scope, risk class, affected layers, contracts/profiles/ICs, verification commands, residual evidence gaps, and whether human firmware review is required.
- Agent-authored PRs require a reviewer other than the implementer. The implementer must run Polytail before requesting review; the reviewer must apply Polytail before approval.
- Agent-authored PRs must request Codex review by commenting `@codex review` or the owner-requested equivalent. Inspect thread-aware Codex review comments, address actionable findings, rerun required checks, and request re-review after fixes.
- `R2` changes require architecture/contract review. `R3` changes require human firmware-owner review and byte-level evidence before merge.
- Do not merge with failing required checks, unresolved P0/P1 review findings, missing required tests, undocumented schema/profile drift, or private golden evidence gaps disguised as TODOs.
- Merge PRs to `main` only after required CI is green and actionable Codex feedback is addressed or explicitly documented as non-actionable.
- If a connector/tool cannot open a PR, push only to the milestone branch and leave a clear review handoff with commit SHA, changed files, risks, and commands run. The owner or Codex must still merge to `main` through PR review.

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

- Display: DP whole/declared partitions; TP whole-only when visible.
- TP HW: only TP CtrlRAM named regions/groups; DP whole-only.
- TP FW: only declared non-CtrlRAM TP regions; DP whole-only; CtrlRAM denied by default.
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
- UI should be modern, minimal, and low-reading-cost. Top-level product navigation is limited to Settings, Merge, and Replace unless the owner explicitly expands it.
- UI top-level navigation uses top tabs.
- Merge and Replace must share a consistent Memory coverage before/after visualization in the same layout position. The Memory coverage area is visual-first, with table details as supporting information.
- Firmware inputs are represented as slot cards.
- Preview/Build results open report modals for diagnostics and evidence review.
- UI implementation must support a bilingual architecture.
- Release work should keep `main` capable of producing a self-contained `.exe` folder that does not require a separate C#/.NET runtime install before distribution.

## Required workflow

Before editing:

1. Read the issue, nearest instructions, relevant spec/ADR/schema/skill, and actual tests/evidence.
2. State affected layers, composition kind, experience, IC/mode/address spaces, and invariants.
3. Keep non-mechanical changes reviewable; split changes over roughly 500 lines when practical.

After editing:

1. Format changed files and run the narrowest meaningful tests.
2. Apply the repository `polytail` skill to every non-trivial change.
3. Run `python scripts/verify.py --all` before claiming completion when the environment supports it.
4. Report commands/results, firmware/profile/protocol/release impact, and missing private evidence.
5. For branch work, prepare PR-ready notes: summary, changed files, risk class, tests, required reviewers, and merge target.

## Definition of done

A change is incomplete while it has placeholders, duplicate semantics, magic offsets, broad suppressions, swallowed errors, fake tests, failing checks, undeclared mutations, documentation/schema drift, missing required human review, or an unreviewed path into `main`.

## High-risk human gate

Human approval is required for profile/schema/protocol breaking changes; memory ranges/atomicity/access; CRC/checksum/header algorithms/order; processor read/write ranges; golden outputs; release signing/publishing/permissions/secrets; branch protection/CODEOWNERS; network/process authority; and new runtime dependencies.
