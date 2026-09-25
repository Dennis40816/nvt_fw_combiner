# ADR 0074: Profile-owned AB Code symmetry

Status: Accepted (owner decision, 2026-09-25).

## Context

Two canonical banks do not imply completely symmetric AB Code. The former
CtrlRAM presentation enabled a bank toggle and cropped every two-bank layout,
including NT51950/951 whose Display content is not symmetric.

## Decision

The existing AB Merge profile owns optional `ab.isFullySymmetric` under
[profile schema 2.17](../contracts/composition-profile-v2.17.schema.json).
The closed object requires an explicit boolean. NT51919/51929/51932 declare
true; NT51950/51951 declare false, including their retained inactive variants.
Capability declarations continue to own AB support; no second support flag is
introduced. Missing declarations disable symmetric-bank presentation.

The compiler retains the immutable declaration in its existing typed artifact.
AB CtrlRAM composition inherits it from its exact AB layout profile, never the
local Standard profile. Application's `MemoryLayoutSnapshot.CanViewIndividualBanks`
is the single terminal decision: declared full symmetry and two canonical banks.
UI toggle, overview cropping, focus filtering, subtitle and viewport state all
consume that decision. Non-symmetric layouts show the complete output.

## Consequences

No IC names or bank-size inference enter presentation. No extra page profile,
firmware transformation or support route is added. Earlier schemas remain
unchanged. Bundle and policy identities change with the declaration; their
derived pins synchronize without changing firmware bytes, evidence disposition
or publication decisions. Broad profile-reference deduplication remains the
separately scheduled work, not a prerequisite for this bounded correction.

Schema/normalizer negatives, compiler inheritance, full-range non-symmetric
projection and real window transitions protect this contract. Independent
Golden and firmware-owner release gates remain separate.
