# ADR 0035: Scope AB topology selection to the NT51950 candidate

- Status: Accepted product policy for `v0.9.15` on 2026-07-23; runtime admission remains R3-gated
- Date: 2026-07-23
- Owners: Product owner, architecture owner, firmware owner
- Extends: ADR 0032

## Context

An IC-number selector is currently a Replace-only, profile-owned context.  It
may select an approved postbuild branch and is reconciled with verified base
firmware context.  Merge workflows have no such selector.  Nonetheless, a
Merge TP inspection can observe a FWConfig cascade count, so a shared
inspection path must not show a mismatch modal for a control the operator
cannot see or use.

The owner has separately identified NT51950 AB Code as the only candidate
whose physical one-IC versus two-IC topology changes its executable AB plan.
This is not an implication that the current NT51950 candidate is executable:
its dual-IC byte evidence and admission remain incomplete.  NT51951's stated
one- and two-IC AB geometries share its same 1 MiB output/container decision,
so IC number is not currently an execution selector for that candidate.

## Decision

### Existing workflow behaviour

All currently registered Standard Merge profiles expose no IC-number control.
General Merge and the already admitted NT51919/NT51929/NT51932 AB pilot also
expose no IC-number control.  Firmware inspection may display observed version
or FWConfig facts, but a page without a visible IC-number control may not open
a number-mismatch modal, change a selected number, or select a build plan from
TP metadata.

Replace retains its existing explicit IC-number behaviour.  This preserves the
only currently admitted flow where that operator choice is part of the profile
and request contract.

### NT51950 AB candidate

Only a later admitted NT51950 AB Merge profile may request a topology choice.
It will offer exactly `single` and `two` as an explicit operator selection and
will present an inline warning equivalent to:

> Select the physical IC topology. TP FWConfig metadata is reported for review
> only; it never changes this selection or chooses an AB build plan.

If a later visible selector is paired with an observed mismatch, the UI may
ask the operator to confirm a change.  It must never apply that change
automatically; a hidden selector may never produce that modal.

The selected topology must be bound into the typed Composition request,
Preview token, and report provenance.  It selects an already profile-declared
NT51950 AB plan; it must never be inferred from a TP/DP filename, presentation
string, source length, FWConfig count, or an observed DP version.

This is deliberately a new **AB topology-selection** capability, not a broad
exception to the current `IcNumberInputMode` rule that prohibits Merge profiles
from declaring the Replace IC-number selector.  Its schema/compiler/UI/CLI
projection must be profile-driven and must reject a selection for every other
Merge profile by default.

### Admission and evidence gates

No visible NT51950 selector, CLI option, executable profile registration, or
support-stage change occurs under this decision alone.  Before that slice may
be implemented, the following are required:

1. architecture review of the separate Merge topology-selection contract;
2. firmware-owner approval of the two explicit NT51950 AB plans, including
   512 KiB single-IC and 1 MiB two-IC output capacities expressed as half-open
   `[0, 0x80000)` and `[0, 0x100000)` ranges;
3. direct one- and two-IC byte-level golden evidence, including the distinct
   DP command origins and all TP/DP-header/DP-command write ranges; and
4. profile/compiler/request/Preview/report/UI/CLI tests proving that metadata
   cannot auto-select or mutate topology, plus the normal R3 human-review and
   release gates.

The existing 256 KiB TP maximum is not silently converted into a new exact
input-length contract by this ADR.  The currently observed `0x37000` candidate
input and the requested `0x40000` input expectation must be reconciled against
direct fixtures before profile admission.

## Consequences

- Standard Merge remains simpler and cannot receive topology branches through
  an incidental firmware inspection result.
- A visible selector always corresponds to a real, accepted request authority;
  there is no no-op or speculative NT51950 UI control.
- NT51950's future topology decision is reviewable in reports and Preview
  tokens without conflating it with Replace's existing IC-number policy.
- NT51951 stays selector-free unless later evidence proves an IC-number choice
  changes its emitted bytes or processor plan.
