# ADR 0039: Separate perfect-like families from shared-part relationships

- Status: Accepted
- Date: 2026-07-26
- Accepted: 2026-07-26 by the product, architecture, and firmware owner
- Owners: Product owner + architecture owner + firmware owner
- Risk: R2 architecture contract; each firmware binding remains R3
- Amends: ADR 0015, ADR 0017, ADR 0025, and ADR 0032
- Amended by: ADR 0040, ADR 0041, and ADR 0042

## Context

The accepted `0.10.x` design originally described every family relationship as
fact-scoped. That safely prevents a TP relationship from importing DP, LDC,
processor, or support facts, but it gives the term `perfect family` no stronger
meaning than any other alias. The firmware owner clarified that a perfect-like
family exists precisely because its members have the same modeled firmware
semantics. Requiring member-specific maps, offsets, metadata bindings, and
workflow definitions for such a family would preserve the duplication that the
IC-first model is intended to remove.

NT51927 and NT51928 demonstrate the separate partial-sharing case. They share
the Initial Code part, the TP part, and metadata owned by those parts, while
NT51928 retains its own LDC and complete DP/container map. They are not a
perfect-like family.

Evidence, publication, and product identity answer different questions from
firmware semantics. Two perfect-like members may therefore share one firmware
definition while retaining different direct-golden provenance or publication
decisions.

## Decision

The canonical relationship model has two distinct forms.

### Perfect-like family

A `perfect-like-family` owns one complete set of modeled firmware semantics for
its declared scope. Its members reference the same canonical artifact parts,
maps, metadata structures, workflow geometry, integrity behavior, processor
behavior, and topology/IC Count rules. Member-specific copies or overrides of
those semantics are forbidden.

Member identity, display name, evidence provenance, publication state, and an
output token that necessarily contains the requested IC identity remain
member-specific. They do not make the firmware semantics different.

NT51919, NT51929, and NT51932 are the first owner-confirmed perfect-like family.
Their shared firmware definitions are family-owned rather than represented as
three offset catalogs or a chain of firmware-semantic aliases. Direct and alias
evidence remain accurately classified per requested member.

NT51917 and NT51927 are another owner-confirmed perfect family. NT51928 is not a
member: it shares only the approved Initial Code and TP facts with NT51927 and
retains distinct LDC and complete DP/container semantics.

If a firmware-semantic difference is later established, the affected member is
removed from the perfect-like family or the relationship is replaced by one or
more explicit shared-part relationships. A perfect-like member cannot carry a
silent exception.

### Shared-part relationships

Partial relationships remain named and fact-scoped. A relationship shares only
its explicit canonical fact references; a globally canonical metadata
definition is reused independently and is not turned into a family fact:

- `initial-code-shared-family` shares Initial Code geometry and its metadata;
- `tp-shared-family` shares TP geometry and its metadata; and
- future partial relationships must name their exact canonical owner and scope.

ADR 0041 supersedes the implementation shape, not these domain meanings.
Initial Code shared, TP shared, TP Flash Header shared, and similar names remain
author-facing roles, while one typed `SharedFactRelationship` carries the exact
canonical fact references at runtime. New partial-sharing concepts do not add
new relationship subclasses or schema discriminators.

NT51917 inherits the complete NT51927 definition through their perfect-family
relationship. NT51917, NT51927, and NT51928 therefore participate in the
Initial-Code-shared and TP-shared fact scopes, while NT51928 remains outside
the perfect family. The relationships reference only the common Initial Code
and TP regions. The one globally canonical DPCMI definition for declared
consumers and the all-IC FirmwareConfig definition are bound through each
applicable map independently; neither is owned by this partial family.
NT51928's LDC and complete DP/container map remain distinct.

NT51950 and NT51951 retain only their approved TP-sharing relationship. Their
Initial Code, LDC, capacity, topology-dependent placement, and AB distinctions
remain explicit.

### Runtime and migration

Resolution materializes one immutable family- or part-owned definition and
retains the requested member identity. It never clones offsets or fields.
Existing ADR 0017 fact aliases remain valid for partial sharing and evidence
provenance; they are not the target representation of perfect-like firmware
semantics.

Ticket #175 defines the one global FirmwareConfig General Parameters structure
and pilots its TP binding through NT51927/NT51928. Ticket #177 migrates remaining
metadata bindings for the admitted ICs, including family-owned
NT51919/NT51929/NT51932 DPCMI and the remaining partial relationships. ADR 0042
and #221 exclude NT51920, NT51925, NT51930, and NT51931 rather than migrating
their facts. No migration promotes support or changes firmware bytes.

The #175 profile document that temporarily contains NT51917, NT51927, and
NT51928 is a canonical metadata carrier plus member-specific map container. It
is not the runtime representation of `initial-code-shared-family` or
`tp-shared-family`. Ticket #177 exclusively owns those named, fact-scoped
relationships, their isolation enforcement, and the NT51917/NT51927/NT51928
migration slice.

## Implementation status (2026-07-27)

Ticket #177 implements the family-owned portion of this decision in the strict
firmware-family relations contract:

- NT51919/NT51929/NT51932 select one family-owned 256 KiB map and one canonical
  DPCMI definition through `perfect-like-family`;
- NT51917/NT51927 declare one perfect-like relationship; their common map also
  participates with NT51928 in separate Initial-Code-shared and TP-shared
  relationships that reference only `dp-code` and `tp-code`;
- the globally canonical DPCMI definition for declared consumers and the
  all-IC FirmwareConfig definition are reused independently of partial family
  membership, and NT51928 `ldc-code` belongs to neither partial relationship;
- NT51950/NT51951 declare only TP sharing through `tp-overlay`; DPCMI,
  FirmwareConfig placement, LDC, capacity, topology, and AB
  remain explicit;
- owner-backed DPCMI bindings resolve at `0x401A` for NT51919/29/32,
  `0x3C01C` for NT51917/27/28, and `0x3E014` for NT51923/26. The transitional
  #177 implementation also contains NT51920/30/31 bindings, but ADR 0042 makes
  them historical implementation evidence that #221 removes rather than
  target family data; and
- trusted composition compilation resolves only metadata required by map
  selection. Inspection-only family metadata remains available to the
  reference-only Metadata Plan/Inspector and does not make an unrelated
  Standard Merge wait for an artifact.

The normalizer now rejects unknown members, same-kind overlapping
relationships, incomplete perfect-like membership, member-specific
perfect-like aliases/capabilities, unequal shared-region geometry, and
unavailable or unequal shared metadata-definition references. This completes
the transitional family/data binding slice only. ADR 0041 replaces the
dedicated partial-sharing runtime shapes; #221 removes retired members before
#177 is considered complete against the target contract. Profile/consumer
convergence and removal of remaining migration adapters remain ticket #194;
support promotion, UI, processor behavior, and firmware bytes are unchanged.

## Alternatives

- Treat every relationship as fact-scoped: rejected because `perfect-like`
  would have no useful invariant and would require repeated member bindings.
- Let perfect-like members override individual facts: rejected because the
  resulting relationship would not be perfect and exceptions would be hidden.
- Infer perfect-like membership from equal hashes, filenames, or current
  golden bytes: rejected because family membership is an owner-declared fact.
- Merge evidence/publication with family semantics: rejected because direct
  product evidence and support decisions remain member- and route-specific.

## Consequences

- The profile contract must distinguish perfect-like membership from
  fact-scoped shared-part relationships before #177 completes.
- One family definition can serve several requested IC identities without
  duplicating firmware geometry.
- Validators reject member-level semantic overrides inside a perfect-like
  family.
- Shared Initial Code or TP metadata does not leak LDC, processor, topology, or
  support facts.
- Current execution and output bytes remain unchanged during migration.

## Verification

- Perfect-like members resolve the same canonical definition references while
  retaining distinct requested member identity and evidence/publication facts.
- A member-level map, metadata, processor, integrity, topology, or workflow
  override inside a perfect-like family is rejected.
- NT51917/NT51927 resolve one perfect-family map; that map and NT51928 resolve
  identical Initial Code and TP region facts while NT51928 resolves its
  distinct LDC/DP map.
- Cross-part inheritance, undeclared family membership, cycles, and ambiguous
  providers fail closed.
- Route identity, capability fingerprint, report provenance, golden
  classification, and publication policy remain deterministic.
