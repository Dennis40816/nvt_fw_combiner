# ADR 0052: Bind CtrlRAM report metadata to an exact Standard map

- Status: Accepted
- Date: 2026-08-22
- Accepted: 2026-08-22 by the repository owner
- Owners: Product owner + architecture owner
- Risk: R2 architecture and package-policy contract; no firmware-byte change
- Amends: ADR 0046

## Context

CtrlRAM Replace reports reuse metadata structures and report classifications
owned by the same IC's Standard profile. The previous adapter searched Standard
registrations at runtime and selected a candidate from reference capacity or TP
input length. That heuristic duplicated map-selection policy outside the
profile compiler and could silently choose another map with the same capacity.
It also made a missing counterpart appear as a late warning instead of a
package-admission defect.

The package already has one hash-pinned runtime-registration authority, and the
Standard profile/compiler already has one exact map-materialization authority.
The missing contract is only the explicit cross-workflow reference between
them.

## Decision

Package trust-index schema `1.1` adds optional `reportMetadataMapId`, permitted
only on `ctrlram-replace` runtime registrations.

- If the exact same-IC Standard profile contains report-classification
  metadata, every corresponding CtrlRAM registration must declare one exact
  map id.
- If that Standard profile contains no report-classification metadata, the
  CtrlRAM registration must omit the field.
- The field is a reference only. Map geometry, capacity, selection groups,
  metadata structures, purposes, and formatters remain owned by the Standard
  profile/family and canonical profile compiler.
- For a Standard profile with a selection group, admission compiles only its
  existing bounded selection states (none and the declared group) and accepts
  the unique candidate whose compiled map id equals the declared reference.
  Capacity and TP input length never identify the counterpart.
- Infrastructure validates the complete trust-index candidate before route
  publication. It resolves the exact same-IC Standard registration, compiles
  the declared map through the existing Standard registration/compiler path,
  keeps only report-classification entries, and rebases their input slot to the
  immutable CtrlRAM reference base.
- Missing Standard registration, missing or extraneous field, unknown map,
  cross-IC map, same-capacity substitute, or materialized-map mismatch rejects
  the complete candidate. No partial CtrlRAM registry is published.
- The admitted immutable plan is stored on the CtrlRAM route. The adapter
  returns it directly; it does not search, rank, deduplicate, or fall back.
- ADR 0046 capability semantics bind the declared report map id. Application
  independently derives the actual map id from the materialized metadata plan
  and rejects a mismatch during compilation binding.

The reviewed built-in inventory has 25 CtrlRAM runtime registrations: 19
reportful and 6 reportless. Those registrations project 33 canonical routes:
23 reportful and 10 reportless. Capability-policy catalog `1.8.0` supersedes
only the 23 affected route fingerprints and their three pinned decisions.

## Consequences

- CtrlRAM report metadata has one explicit, reviewable counterpart and no
  capacity-, filename-, PID-, hash-, or input-length inference.
- The package trust index does not become a firmware map catalog.
- Startup fails closed before authoring selectors can expose an incoherent
  CtrlRAM registry.
- A defense-in-depth Application check prevents an admitted route from being
  rebound to metadata materialized from another map.
- Output bytes, ranges, operation order, processor authority, report values,
  evidence rank, publication status, naming, and UI remain unchanged.

## Verification

- Schema and loader tests accept the 19 exact declarations and 6 exact
  omissions, and reject wrong types, invalid tokens, extraneous workflow use,
  missing, unknown, cross-IC, and same-capacity substitute maps.
- Registry tests require all 25 registrations to validate before publication.
- Canonical catalog tests require 23 reportful route bindings and 10 reportless
  omissions.
- Runtime binding tests reject a metadata plan whose actual map differs from
  the reviewed route map.
- Existing CtrlRAM capacity boundaries, plans, reports, and golden byte tests
  remain unchanged and pass the repository gate.

## Rejected options

- Keep the capacity/input-length heuristic: ambiguous and duplicates compiler
  authority.
- Put map geometry in the CtrlRAM registration: creates a second firmware-fact
  owner.
- Hard-code an IC-to-map table in C#: blocks data-only onboarding and recreates
  per-IC workflow logic.
- Treat a missing counterpart as an empty report: hides package drift and can
  misclassify output differences.
