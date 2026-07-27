# ADR 0041: Keep perfect-family semantics and compile partial sharing through typed fact references

- Status: Accepted
- Date: 2026-07-27
- Accepted: 2026-07-27 by the product, architecture, and firmware owner
- Owners: Product owner + architecture owner + firmware owner
- Risk: R2 architecture contract; each firmware binding remains R3
- Amends: ADR 0039

## Context

ADR 0039 correctly distinguishes a complete perfect-like family from partial
Initial Code or TP sharing. Its first contract revision represents each partial
relationship with a dedicated schema discriminator and runtime type:
`initial-code-shared-family`, `tp-shared-family`, and any future kind.

The model must also express narrower or overlapping sharing such as TP Flash
Header, DiffDLM policy, one metadata structure, one region set, or one integrity
definition. Adding a schema branch, DTO, normalizer path, resolver branch, and
test matrix for every new label would make the relationship taxonomy grow with
the firmware catalog even though all partial relationships perform the same
operation: explicitly reference a bounded set of canonical facts.

Readable domain language remains valuable. Removing terms such as TP shared or
Initial Code shared would make evidence review harder. The problem is not those
concepts; it is treating every concept as a different runtime behavior.

## Decision

The target relationship model has two runtime forms.

### Perfect family

`PerfectFamilyRelationship` remains a strong, separately validated invariant.
Its members consume one complete modeled firmware-semantic definition and may
not carry member-specific firmware overrides. Requested identity, evidence,
publication, and identity-bearing output facts remain member-specific.

### Typed shared-fact relationship

Every partial relationship compiles through one `SharedFactRelationship`:

```text
relationshipId
role
memberIds
applicability
sharedFactReferences
reason
evidenceRefs
```

`role` is closed author-facing vocabulary such as `initial-code-shared`,
`tp-shared`, `tp-flash-header-shared`, or `diffdlm-shared`. It explains intent
but never selects runtime behavior.

`sharedFactReferences` is not an arbitrary string collection. Each entry is a
typed, exact reference to an admitted canonical fact, such as a region set,
image map, artifact part, metadata definition, TP Flash Header definition,
DiffDLM policy, integrity definition, or processor plan. Applicability is
explicit. A fact not referenced is not shared.

The normalizer resolves those references to the same immutable canonical
objects; it never clones offsets, ranges, fields, formatters, or processor
authority. Unknown references, kind mismatches, inapplicable facts, cycles,
ambiguous providers, and conflicting relationships fail closed.

Support, publication, evidence classification, workflow admission, and requested
identity are never inherited by a partial relationship. They remain separately
declared even when their current values happen to match.

## Examples

- NT51923 and NT51926 may reference the same Standard Merge region set, DPCMI
  definition, or TP Flash Header while retaining distinct CtrlRAM maps,
  Postbuild versions, and evidence.
- NT51917 and NT51927 use one `PerfectFamilyRelationship`; NT51928 is outside
  that perfect family and references only the approved Initial Code/TP facts.
- NT51927 and NT51928 use readable Initial Code and TP sharing roles, but the
  runtime contract is the exact set of referenced Initial Code/TP facts.
  NT51928 LDC and complete DP/container facts remain unreferenced and distinct.
- NT51950 and NT51951 may share TP and DiffDLM definitions without sharing
  Initial Code placement, LDC, capacity, AB layout, evidence, or publication.
- A future TP Flash Header relationship adds one typed reference and role; it
  does not add `TpFlashHeaderSharedFamilyRelationship` to every layer.

## Migration

Ticket #177 replaces the dedicated partial relationship DTO/schema/runtime
branches with the typed shared-fact form and migrates only the remaining
supported IC facts. Existing authoring documents may use a one-way loader
adapter during the ticket, but the adapter must have a named deletion criterion
and cannot become a second relationship authority.

Ticket #221 removes NT51920, NT51925, NT51930, and NT51931 production
capabilities instead of migrating their relationship data. Ticket #194 deletes
remaining consumer-side assumptions after canonical migration; #195 retains the
final compatibility-owner deletion gate.

This ADR changes no firmware bytes, ranges, CRC/Header algorithm, support
promotion, or `0.9.17` hot-fix behavior.

## Alternatives

- Keep one runtime type per sharing label: rejected because each new shared fact
  kind expands contracts and resolver code without adding distinct execution
  semantics.
- Use untyped string fact ids: rejected because wrong-kind references and
  provider drift would escape validation.
- Represent perfect families as another shared-fact list: rejected because it
  weakens the complete-equivalence invariant and permits silent omissions.
- Infer sharing from equal bytes, filenames, or golden hashes: rejected because
  sharing remains an owner-declared firmware fact.

## Consequences

- Human-readable relationship roles remain available in authoring, reports, and
  evidence.
- Domain/runtime branching stays constant as new canonical fact kinds are added.
- Exact sharing scope is reviewable and no broad family label grants unrelated
  facts.
- The current dedicated Initial Code/TP relationship implementation is
  transitional and must be migrated rather than extended with more subclasses.

## Verification

- Perfect-family tests prove complete semantic identity and reject member
  overrides.
- Shared-fact tests prove exact reference identity and reject unlisted
  inheritance.
- Role changes alone cannot alter runtime resolution.
- Wrong-kind, stale-hash, unknown-provider, cyclic, ambiguous, and
  applicability-mismatched references fail closed.
- NT51927/NT51928 retain distinct LDC/DP facts while resolving the same declared
  Initial Code and TP facts.
- NT51923/NT51926 can share a TP Flash Header reference without sharing
  CtrlRAM/Postbuild definitions.
- Support, publication, evidence, and route admission remain independent.
