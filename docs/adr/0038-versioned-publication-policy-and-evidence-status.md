# ADR 0038: Versioned publication policy and evidence status

- Status: Accepted
- Date: 2026-07-25
- Owners: Product owner, architecture owner
- Risk: R2
- Follows: [ADR 0022](0022-canonical-contract-schema-materialization.md)
- Amended by: ADR 0046 for capability-definition versus per-compilation identity
- Amended by: #195 removes the superseded standalone publication-policy runtime

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

The checked-in publication source is the `publication` decision on each exact
route in
[`canonical-capability-policy-v1.json`](../contracts/canonical-capability-policy-v1.json).
The canonical capability policy schema, hash-pinned loader, and catalog
materialization validate publication together with the route and its expected
`CapabilityFingerprint`. Each decision contains:

1. one stable, exact `routeId`;
2. one status: `supported`, `candidate`, `internal`, or `test-only`; and
3. an immutable decision id plus owner-decision provenance.

No row is a wildcard. A new IC Count or route map-axis value creates a new
route and cannot materialize until an owner-approved publication decision
references that route. Under ADR 0046, a route may instead reference one reviewed closed
map-variant-set definition. Adding or removing a member then changes
`CapabilityFingerprint` and stales policy without creating another logical
row; selecting an existing member changes only `CompilationFingerprint`. A
missing policy row is a fail-closed materialization error.

The canonical route renderer length-frames the IC, workflow, IC Count, and map
axes before joining them, so hyphens inside one axis cannot collide with an
adjacent axis. Consumers still treat `routeId` as opaque. Integrity identity is
represented in `routeId` by its complete lowercase 64-hex SHA-256 digest;
truncated digests are not route identities. AB integrity derives from the
canonical compiled operation representation rather than a second offset/range
model. It covers both external-processor operations and host-side
`transform-scalar` relocation, including the relocation fields and delta
already owned by the compiled plan. CtrlRAM integrity derives from the selected
canonical postbuild plan and binds its selector, commands, blocks, staged
sources, permitted writes, and map capacity.

The policy JSON is the only authority for publication status. It cannot set
authoring availability, execution admission, profile selection, firmware
ranges, processor authority, UI visibility, or evidence classification. The
initial reviewed rows record the owner-confirmed NT51919 General Merge
`test-only` decision and NT51950/NT51951 AB `candidate` decisions.
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

The completed canonical migration loads the policy only after its exact raw
bytes match the reviewed SHA-256; line-ending, whitespace, and encoding
normalization are not permitted. Application validates the typed decisions and
materializes them against the same current route snapshot.
The Bootstrap catalog host serializes reload and resolution, then atomically
publishes a non-blocking immutable reporting snapshot; #207 exposes an
Application-owned immutable `ICanonicalSupportMatrixQuery` over that exact
shared publication. Infrastructure declares the
policy as both build and publish content at the same relative path used by the
loader. Tests prove the deployed file exists, the publish metadata remains
closed, and the query does not alter firmware execution or UI state. Settings
renders the #207 read-only IC-by-workflow disclosure without reclassifying
authoring, execution, publication, evidence, or blockers.

The stale standalone NT51919 General Replace publication row is not carried
into the canonical policy: there is no admitted exact route to bind. Its
absence does not open the workflow, and any future route requires a canonical
capability definition plus a new owner-approved publication decision.

## Consequences

- Publication decisions become versioned, attributable, and reviewable without
  duplicating firmware facts in profiles or UI catalogs.
- Profile promotion, registry presence, UI selection, and evidence cannot
  silently expand the public support claim.
- Settings and CLI may later display the same headless projection without
  maintaining another support list.
- A route with incomplete exact identity or policy coverage fails catalog
  materialization and produces a system diagnostic rather than an inferred
  support claim.

## Rejected options

- Deriving publication from profile promotion, UI selectability, executable
  registration, or passing tests.
- Storing publication status in every profile.
- Wildcard policy decisions for future IC Count or map variants.
- Storing evidence status in publication policy.

## Migration and verification

The canonical policy and Support Matrix must prove:

1. every policy route resolves exactly once in the same catalog snapshot;
2. routes without a decision fail materialization and the retired
   `unclassified` token is rejected;
3. no policy value changes authoring or execution;
4. evidence uses only the five declared values and fixed precedence;
5. route, policy, evidence, and diagnostic snapshots are immutable;
6. a policy hash or schema mismatch rejects the source before materialization;
7. packaged headless query output uses the same projection;
8. a standalone `publicationPolicy` payload cannot return to runtime or the
   release manifest; and
9. no Presentation or UI-selection behavior changes in this ticket.
