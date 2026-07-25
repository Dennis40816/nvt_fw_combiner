# ADR 0038: Versioned publication policy and evidence status

- Status: Accepted
- Date: 2026-07-25
- Owners: Product owner, architecture owner
- Risk: R2
- Follows: [ADR 0022](0022-canonical-contract-schema-materialization.md)

## Context

Current selectable catalogs, executable registries, trusted profiles, and
golden records answer different questions. They establish route inventory,
whether a route is authorable or executable, and what evidence exists. None of
those facts is a product-publication decision. Deriving a support label from
any one of them would silently promote a route when a profile, test, or UI entry
changes.

The 0.10.x target requires one exact route identity over IC, workflow, IC Count,
map variant, and any integrity route. Authoring availability, execution
admission, evidence classification, and publication status remain independent.

## Decision

The checked-in publication source is
[`support-publication-policy-v1.json`](../contracts/support-publication-policy-v1.json),
validated by
[`support-publication-policy-v1.schema.json`](../contracts/support-publication-policy-v1.schema.json).
Each decision contains:

1. one stable, exact `routeId`;
2. one status: `supported`, `candidate`, `internal`, `test-only`, or explicitly
   recorded `unclassified`; and
3. an immutable decision id plus owner-decision provenance.

No row is a wildcard. A new IC Count, map variant, or integrity route creates a
new route and remains `unclassified` until an owner-approved decision references
that route. A missing policy row also resolves to `unclassified`.

The policy JSON is the only authority for publication status. It cannot set
authoring availability, execution admission, profile selection, firmware
ranges, processor authority, UI visibility, or evidence classification. The
initial rows record the owner-confirmed NT51919 General Merge and General
Replace `test-only` decisions and NT51950/NT51951 AB `candidate` decisions.
Those classifications do not open, block, or otherwise alter execution.

Evidence resolves separately in this fixed strongest-to-weakest order:

| Value | Required canonical source |
| --- | --- |
| `DirectGolden` | Exact-route declaration in the canonical golden manifest. |
| `ApprovedAlias` | Explicit fact-scoped alias declaration naming an approved target route. |
| `SyntheticOracle` | Explicit synthetic-oracle declaration for the exact route. |
| `ContractOnly` | Exact admitted execution contract with no stronger declaration. |
| `Missing` | No eligible declaration above. |

An evidence declaration is eligible only for its exact route. An alias must
name source and target route ids and a fact scope applicable to the source
route's IC, workflow, IC Count, and map variant. The result retains the selected
declaration id and, for an alias, the target route and fact-scope ids.

The Support Matrix is an Application-owned, immutable reporting projection. It
joins exact route references with the publication and evidence snapshots; it
does not copy firmware facts or grant execution. During migration its
denominator is the union of selectable, executable, and publication sources.
Every unresolved or divergent source remains a fail-closed diagnostic.

This source-contract phase does not yet load the policy at runtime. The same
#170 branch adds the hash-closed loader, materializer, headless query, and
package proof before the ticket can be integrated.

## Consequences

- Publication decisions become versioned, attributable, and reviewable without
  duplicating firmware facts in profiles or UI catalogs.
- Profile promotion, registry presence, UI selection, and evidence cannot
  silently expand the public support claim.
- Settings and CLI may later display the same headless projection without
  maintaining another support list.
- A route with incomplete exact identity or policy coverage remains visible as
  `unclassified` rather than being inferred away.

## Rejected options

- Deriving publication from profile promotion, UI selectability, executable
  registration, or passing tests.
- Storing publication status in every profile.
- Wildcard policy decisions for future IC Count or map variants.
- Storing evidence status in publication policy.

## Migration and verification

The completed #170 baseline must prove:

1. every policy route resolves exactly once in the same catalog snapshot;
2. routes without a decision remain `unclassified`;
3. no policy value changes authoring or execution;
4. evidence uses only the five declared values and fixed precedence;
5. route, policy, evidence, and diagnostic snapshots are immutable;
6. a policy hash or schema mismatch rejects the source before materialization;
7. packaged headless query output uses the same projection; and
8. no Presentation or UI-selection behavior changes in this ticket.

