# ADR 0005: Model Replace personas and General layouts as authoring policies

- Status: Accepted for repository bootstrap
- Date: 2026-06-25
- Amended: 2026-07-29 for canonical LDC terminology
- Amended: 2026-09-21 for the owner-approved 1.1.10 DP Replace retirement
- Owners: Product owner + architecture owner + DP/CtrlRAM firmware reviewers
- Amended by: ADR 0015, ADR 0047

## Context

DP and CtrlRAM workflows reason about different parts of the same IC memory map. A separate memory model or executor per workflow would drift. General customer requests also require flexible placement without granting arbitrary scripting authority.

## Decision

Keep one canonical IC region catalog and apply deny-by-default `RegionAccessRule` policies per experience:

- **DP Replace (historical; retired in 1.1.10)** — DP whole or declared partitions, including
  profile-declared LDC replacement slots when LDC is replaced separately from
  the DP BIN. `LD` is legacy source terminology only.
- **CtrlRAM Replace** — named CtrlRAM regions/groups tagged `tp-ctrlram` only.
- **General Replace** — explicit mappings only inside profile-enabled ranges; those ranges are not limited to the DP or CtrlRAM persona categories, while protected ranges remain blocked.

General Merge and General Replace share one `ExplicitMapping` contract. Their only difference is blank versus reference initialization and the compiled operation kind (`copy-range` versus `replace-range`). Canvas drag and exact table/manual input are two views over one mapping state.

Persona checks live in the profile/compiler/application layer. UI merely displays allowed controls and issues; the executor sees only validated operations.

The 1.1.10 amendment removes the dedicated `dp-replace` experience from active
authoring, typed execution services, CLI dispatch, runtime registrations and
packaged profiles. An old trusted declaration remains readable, but the existing
Profiles compiler rejects that exact retired experience before returning any
compiled artifact or public plan. Existing earlier compilation failures remain
terminal; this does not introduce another policy in the shared engine.

The structural reference-clone/replace-range lowering remains shared by legal
General and CtrlRAM declarations. Canonical DP/LDC/TP regions, DPCMI, Perfect
family disclosure and full-image metadata survive through their existing owners.
Historical DP Report/History records retain their identities and display facts;
reading them does not restore execution authority. This supersedes the earlier
decision to retain hidden DP execution and does not grant AB Replace support.

## Rejected alternatives

- Separate DP/CtrlRAM engines: duplicates semantics and future fixes.
- Filename-based region inference: unsafe and non-deterministic.
- Arbitrary scripts or user-supplied worker paths: bypasses review, write ranges, and reproducibility.
- UI-only restrictions: CLI or malformed requests could bypass them.

## Verification

- Retired DP declarations return no compiled artifact; otherwise valid
  declarations reaching the compiler success exit report
  `profile.v2.plan.retired-experience`. Historical records remain readable.
- Shared Replace lowering still rejects unauthorized artifact/region-owner
  pairs, including the DP/LDC bounds previously exercised through DP fixtures.
- CtrlRAM Replace rejects non-CtrlRAM targets.
- General mappings reject protected/out-of-bounds/misaligned/overlapping targets.
- Canvas/table serialization round-trips exactly.
- All accepted mappings compile to the same operation algebra used by fixed profiles.
