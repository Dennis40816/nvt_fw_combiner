# ADR 0072: Admit Event Buffer configuration separately from firmware effects

- Status: Accepted (bounded configuration-admission design; runtime integration pending)
- Date: 2026-09-14
- Authority: owner-approved Config behavior and implementation request; independent design review by `/root/v116_config_architecture`
- Risk: R2 configuration boundary; firmware integration retains R3 evidence gates

## Context

The owner-approved [Config reference and decisions](../ui/v1.1.x-custom-options-layout-handoff.md#event-buffer-format-identity-and-alias--2026-09-14)
allow selecting a predefined Unique ID, changing its alias, and adding/removing
recognition bytes. Those are not permission to create firmware profiles or edit
their effects. Existing ShellPreferenceFileStore silently uses defaults after
invalid input and swallows persistence failures; that behavior is intentionally
unsuitable for firmware-affecting configuration.

## Decision

Application owns configuration admission. A pure admission call accepts one
opaque canonical scope ID, that scope's supported identity/display-label catalog,
and draft entries. It returns either immutable normalized configuration or typed
issues. Identity keys are exact; aliases affect display only. Each identity and
recognition byte occurs at most once in that scope. Missing/invalid entries and
out-of-byte-range values fail admission. An empty recognition set is valid and
matches nothing. Empty aliases use the canonical display label. A valid nonmatch
is returned as no match, not interpreted here as a Common firmware decision.

The catalog is supplied by canonical declarations, not a second hardcoded
Application registry. This first unit introduces no Desay address, built-in
identity catalog, automatic support promotion, scope hierarchy, or overlapping
scope resolver. Its successful result means **configuration-valid for the supplied
scope**, not execution-supported or Build-authorized.

Subsequent units reuse ILocalFileStore bounded stable reads and atomic writes,
with a finite versioned JSON codec and source-generated serialization. UI and
CLI consume the same Application owner. Save validates and persists before
publishing a new immutable snapshot; invalid drafts or save failures preserve
the old effective state. Invalid external configuration yields an explicit
affected-scope blocker, not Common/default/stale-snapshot fallback. Restore
defaults affects only the draft. A running operation retains its captured state.

Profiles/Domain remain the sole owners of geometry, applicability, metadata
locations and output effects. AB integration must consume an accepted primary
observation before selecting a declared variant; it must not merely repaint
Info while retaining an old compilation. Preserve the distinction in
[ADR 0046](0046-capability-and-compilation-fingerprint-boundary.md): catalog
semantics belong to reviewed capability authority, and the effective choice and
configuration provenance must invalidate stale run/inspection acceptance.
Do not put alias text into firmware plan semantics. Exact JSON/report and
runtime selection contracts need their own concrete admission before wiring.

## Rejected options and consequences

- Reusing preference fallback would silently change Build behavior after a bad
  file. Reuse low-level IO, not its policy.
- An arbitrary rules engine, per-page ID registry or UI address branch would
  duplicate firmware authority. The editor only configures admitted identities.
- Global overlap machinery is unnecessary in the first single-scope unit.
  Later catalog integration must validate any actual overlapping scope conflict
  before claiming wider applicability.

## Implementation and verification boundary

First unit: Application's EventBufferFormatConfiguration and its admission
function, with behavioral tests for membership, aliases, exact identity keys,
byte boundaries, duplicate/conflicting values, empty sets, null input, and
caller-mutation isolation. There is no filesystem, runtime selection or UI
implementation in this unit. Later persistence, profile selection, UI and report
units retain their own tests and independent review. This ADR does not certify
Golden output, an integration candidate, or a release.
