# Support Publication Policy Contract 1.0

The executable schema is
[`support-publication-policy-v1.schema.json`](support-publication-policy-v1.schema.json).
The initial owner-approved source document is
[`support-publication-policy-v1.json`](support-publication-policy-v1.json).

## Purpose and ownership

This contract records the product-publication classification of an exact
canonical capability route.  It is neither a composition profile nor an
evidence manifest.  Only this policy may produce the public status
`supported`, `candidate`, `internal`, `test-only`, or an explicit
`unclassified` decision.

A route with no matching policy decision is `unclassified`; this default is
not a promotion failure and must not be replaced by a UI, registry, profile,
golden, or test inference.  `unclassified` may also be recorded explicitly
when its owner provenance is useful.

`routeId` is an opaque stable semantic reference supplied by
`CanonicalCapabilityCatalog`.  The catalog owns the route's IC, workflow, IC
Count applicability, and map variant.  The policy must not copy those fields,
derive a route from a visible label, or use a wildcard.  The future
materializer rejects a missing or ambiguous `routeId`; a newly added route has
no matching decision until the owner adds one.

`decisionId` is immutable.  A changed decision receives a new `decisionId`, a
new `policyVersion`, and may list the superseded decision ids.  The repository
source-contract validator and the future policy reader reject duplicate
decision ids, duplicate route ids, and whitespace-only provenance values even
though JSON Schema cannot express property-level uniqueness.

The checked-in document is the reviewable source for the first policy slice.
It is not yet a runtime-loaded or release-pinned artifact.  A later R2 slice
must materialize and hash-close it with the selected capability catalog before
Settings, CLI, CI, or release output treats it as live policy.

## Independence from execution and evidence

Publication policy never changes authoring availability, execution admission,
map resolution, processor authority, firmware ranges, or UI route selection.
It is a product decision made after those technical facts are independently
resolved.

`EvidenceStatus` comes from the canonical evidence resolver, not this JSON:

| EvidenceStatus | Required canonical source |
| --- | --- |
| `DirectGolden` | Exact-route canonical golden-manifest entry. |
| `ApprovedAlias` | Explicit fact-scoped alias declaration naming the accepted evidence target. |
| `SyntheticOracle` | Explicit synthetic-oracle declaration for the route. |
| `ContractOnly` | Admitted canonical capability contract with no stronger declared source. |
| `Missing` | No eligible source above. |

For one requested canonical `routeId`, the fixed precedence is exactly the
table order: `DirectGolden` > `ApprovedAlias` > `SyntheticOracle` >
`ContractOnly` > `Missing`.  Direct-golden, synthetic-oracle, and admitted
capability-contract declarations are eligible only when they name the requested
route id exactly.  An alias is eligible only when it names the requested route
as `sourceRouteId`, names an explicit `targetRouteId`, and its fact scope is
proven applicable to the selected IC Count and map variant.  A mismatched
route or scope is ignored, not downgraded to an alias result.

Every selected evidence declaration contributes its stable declaration id to
the resolved result; an alias also records its target route id and fact-scope
id.  The resolver retains the strongest eligible classification.  A policy row
can therefore be `test-only` with `DirectGolden`, or `supported` with an
evidence gate still visible; status and evidence answer different questions.

See [ADR 0038](../adr/0038-versioned-publication-policy-and-evidence-status.md)
for the architecture decision and migration gates.
