# ADR 0004: Model Replace personas and General layouts as authoring policies

- Status: Accepted for repository bootstrap
- Date: 2026-06-25
- Owners: Product owner + architecture owner + Display/TP firmware reviewers

## Context

Display, TP hardware, and TP firmware engineers reason about different parts of the same IC memory map. A separate memory model or executor per team would drift. General customer requests also require flexible placement without granting arbitrary scripting authority.

## Decision

Keep one canonical IC region catalog and apply deny-by-default `RegionAccessRule` policies per experience:

- **Display** — DP whole or declared partitions; TP whole-only when offered.
- **TP HW** — named TP CtrlRAM regions/groups only; DP whole-only.
- **TP FW** — declared non-CtrlRAM TP regions only; DP whole-only; CtrlRAM blocked by default.
- **General Replace** — explicit mappings only inside profile-enabled ranges; protected ranges remain blocked.

General Merge and General Replace share one `ExplicitMapping` contract. Their only difference is blank versus reference initialization and the compiled operation kind (`copy-range` versus `replace-range`). Canvas drag and exact table/manual input are two views over one mapping state.

Persona checks live in the profile/compiler/application layer. UI merely displays allowed controls and issues; the executor sees only validated operations.

## Rejected alternatives

- Separate Display/TP engines: duplicates semantics and future fixes.
- Filename-based region inference: unsafe and non-deterministic.
- Arbitrary scripts or user-supplied worker paths: bypasses review, write ranges, and reproducibility.
- UI-only restrictions: CLI or malformed requests could bypass them.

## Verification

- Display rejects any partial TP mapping.
- TP HW rejects non-CtrlRAM TP targets and partial DP.
- TP FW rejects CtrlRAM targets and partial DP.
- General mappings reject protected/out-of-bounds/misaligned/overlapping targets.
- Canvas/table serialization round-trips exactly.
- All accepted mappings compile to the same operation algebra used by fixed profiles.
