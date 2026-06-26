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

## Mandatory architecture boundaries

- `NvtFwCombiner.Domain` is pure and does not reference filesystem, process, UI, Avalonia, JSON serialization, or infrastructure.
- `NvtFwCombiner.Application` owns use-case policy through ports; it does not start processes or render UI.
- `NvtFwCombiner.Infrastructure` implements filesystem, staging, process, profile, and report adapters; it does not redefine firmware semantics.
- UI and CLI create typed requests and call the same application services.
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
- Python may modify only a host-created staging copy. It never modifies the user's source BIN or final output path.
- The host independently diffs staged before/after bytes and rejects every change outside declared write ranges.
- Never add real firmware BIN files, credentials, generated releases, or private golden data to Git.

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

## Definition of done

A change is incomplete while it has placeholders, duplicate semantics, magic offsets, broad suppressions, swallowed errors, fake tests, failing checks, undeclared mutations, documentation/schema drift, or missing required human review.

## High-risk human gate

Human approval is required for profile/schema/protocol breaking changes; memory ranges/atomicity/access; CRC/checksum/header algorithms/order; processor read/write ranges; golden outputs; release signing/publishing/permissions/secrets; branch protection/CODEOWNERS; network/process authority; and new runtime dependencies.
