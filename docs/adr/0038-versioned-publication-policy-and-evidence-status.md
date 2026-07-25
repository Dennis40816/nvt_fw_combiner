# ADR 0038: Versioned publication policy and evidence status

- Status: Accepted
- Date: 2026-07-25
- Owners: Product owner, architecture owner
- Risk: R2
- Follows: [ADR 0022](0022-canonical-contract-schema-materialization.md)

## Context

Current registries, trusted profiles, UI catalogs, and golden records answer
different questions.  They can establish route inventory, whether a route is
authorable or executable, and what evidence exists.  None of those facts is a
product-publication decision.  Deriving a Settings support label from any one
of them would silently promote a route when a profile, test, or UI entry
changes.

The 0.10.x target requires `CanonicalCapabilityCatalog` to own exact route
identity and to keep authoring availability, execution admission, evidence
classification, and publication status independent.  A durable, reviewable
source is therefore required for the one remaining product-policy dimension.

## Decision

The checked-in source contract is
[`support-publication-policy-v1.json`](../contracts/support-publication-policy-v1.json),
validated by
[`support-publication-policy-v1.schema.json`](../contracts/support-publication-policy-v1.schema.json).
It is versioned and each decision contains:

1. a stable `routeId` reference owned by the future
   `CanonicalCapabilityCatalog`; the referenced route represents its exact IC,
   workflow, IC Count applicability, and map variant, rather than a UI label
   or a display-list position;
2. one publication status: `supported`, `candidate`, `internal`, `test-only`,
   or explicitly recorded `unclassified`; and
3. an immutable decision id plus owner-decision provenance, recorded date,
   record reference, and rationale.

No policy row is a wildcard.  A new IC Count or map variant is a new canonical
route and remains `unclassified` until an owner-approved row references its
own `routeId`.  A missing policy row also resolves to `unclassified`.  The
repository source-contract validator and the future materializer both reject
duplicate decision ids, duplicate route ids within one policy version,
whitespace-only provenance, malformed provenance, or a route reference that
cannot be resolved in the same canonical-catalog snapshot.

The policy JSON is the only authority for publication status.  It cannot set
authoring availability, execution admission, profile selection, firmware
ranges, processor authority, UI visibility, or evidence classification.  The
initial rows record the already confirmed NT51919 General Merge and General
Replace `test-only` decisions and the NT51950/NT51951 AB `candidate`
decisions.  These are publication classifications only; they do not open,
block, or otherwise alter execution.

`EvidenceStatus` is resolved separately and has exactly these values, listed
from strongest to weakest:

| Value | Sole source class |
| --- | --- |
| `DirectGolden` | Exact-route entry in the canonical golden manifest. |
| `ApprovedAlias` | Explicit fact-scoped alias declaration that names the approved evidence target. |
| `SyntheticOracle` | Explicit synthetic-oracle declaration for the route. |
| `ContractOnly` | An admitted canonical capability contract with no stronger declared evidence source. |
| `Missing` | No eligible declared source above. |

For a requested canonical `routeId`, the resolver evaluates eligible
declarations in the table order and returns the first available class.  An
exact golden, synthetic oracle, or admitted capability contract is eligible
only when its declaration names that same `routeId`.  An alias is eligible only
when its declaration names the requested route as `sourceRouteId`, names an
explicit `targetRouteId`, and its named fact scope is proven applicable to the
requested source route's IC Count and map variant.  A mismatched route, count,
map variant, or fact scope is not a weaker evidence result: it is ineligible
and cannot be considered.

Every eligible declaration exposes a stable source-declaration id.  The
resolved result retains the chosen declaration id and, for an alias, the target
route id and fact-scope id.  It does not read publication-policy rows, infer a
class from a filename, test name, profile-promotion field, or whole-file hash.
The fixed order means a weaker source cannot downgrade a stronger one while
the matrix is enumerated.

The checked-in policy document is the reviewable source for the first
implementation slice.  A later R2 materialization slice must include it in a
hash-closed trusted bundle or equivalent release-pinned policy snapshot before
runtime Settings consumes it.  Until then it has no runtime, UI, or release
admission effect.

## Consequences

- Product publication decisions become inspectable, versioned, and attributable
  without duplicating firmware facts in profiles or UI catalogs.
- The future `SupportMatrix` can join independent policy and evidence facts by
  canonical route id, while reporting an unmatched route as `unclassified`.
- Settings can show status and evidence independently without treating
  `test-only` as a failed executable capability.
- A release cannot accidentally enlarge its support claim when a profile or
  golden manifest is expanded.

## Rejected options

- Deriving support from profile `promotion`, UI selectability, an executable
  registry, or a passing golden test.  These are different authorities and
  would create implicit promotion.
- Storing status in every profile.  It duplicates a product decision across
  map/profile variants and makes a policy review depend on firmware edits.
- Allowing a policy wildcard for all future IC Count/map variants.  It would
  classify a newly introduced byte route without an owner decision.
- Letting publication policy carry an evidence status.  It would create a
  second evidence catalog and enable accidental evidence downgrades.

## Migration and verification

The policy-source slice introduces no runtime reader.  The eventual
materializer and `CanonicalCapabilityCatalog` slice must prove:

1. every policy `routeId` resolves exactly once in its catalog snapshot;
2. every current catalog route without a row is `unclassified`;
3. no policy value changes authoring availability or execution admission;
4. evidence source precedence produces only the five declared values, rejects
   wrong-route/wrong-scope aliases, and retains the strongest eligible
   declaration; and
5. policy/evidence changes are recorded in the SupportMatrix and CI output
   with policy and evidence provenance.

The implementation requires independent architecture/contract review before a
runtime reader or trusted-bundle materialization is merged.
