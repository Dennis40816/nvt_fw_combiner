# Support Publication Policy Contract 1.0

The executable schema is
[`support-publication-policy-v1.schema.json`](support-publication-policy-v1.schema.json).
The preserved initial owner-approved snapshot is
[`support-publication-policy-v1.0.0.json`](support-publication-policy-v1.0.0.json),
and the current successor is
[`support-publication-policy-v1.json`](support-publication-policy-v1.json).

## Purpose and ownership

This contract records the product-publication classification of one exact
capability route. It is neither a composition profile nor an evidence manifest.
Only this policy may produce `supported`, `candidate`, `internal`, `test-only`,
or an explicit `unclassified` decision.

A route with no matching decision is `unclassified`. That default must not be
replaced by an inference from UI, registry, profile, golden, or test state.

`routeId` is an opaque stable semantic reference owned by the canonical route
catalog. Its identity includes IC, workflow, IC Count applicability, map
variant, and any distinct integrity route. The policy does not repeat those
fields, derive routes from display labels, or use wildcards.

The current canonical renderer length-frames each non-integrity axis before
joining it, preventing a hyphen inside one value from creating the same id as a
different axis tuple. When integrity applies, the route suffix is the complete
lowercase 64-hex SHA-256 digest of the canonical integrity identity; truncation
is forbidden. AB integrity identity covers external processors and host-side
scalar relocation by referencing the canonical compiled operation semantics.
CtrlRAM integrity identity binds the selected canonical postbuild plan,
including selector, command, block, staged-source, permitted-write, and map
capacity semantics. Neither route maintains a second copy of offsets, ranges,
scalar fields, or processor authority. Readers must not parse either
representation to recover firmware facts.

`decisionId` is immutable. A changed decision receives a new id and policy
version and may name the decisions it supersedes. Readers reject duplicate
decision ids, duplicate route ids, whitespace-only provenance, unknown fields,
invalid statuses, and unresolved route references. A policy that declares
`supersedesPolicyVersion` must declare `supersedesPolicySha256` and be
validated with the hash-pinned prior snapshot of the same `policyId`, version,
and SHA-256. Every `supersedesDecisionIds` entry must exist in that verified
prior snapshot; syntax alone never proves lineage. Infrastructure loads the
oldest required packaged policy first and validates the ordered chain before
Application materializes the current snapshot.

The checked-in bytes are the reviewable source. Runtime loading must verify the
pinned SHA-256 over the exact raw bytes before deserializing an immutable
snapshot. Line-ending, whitespace, or encoding normalization is forbidden.
Application validation independently enforces the policy semantic-version and
ISO `yyyy-MM-dd` provenance-date syntax after deserialization, so adapter
choice cannot bypass those rules. A hash mismatch, schema error, duplicate
identity, invalid supersession, or unresolved route blocks materialization.

## Independence from execution and evidence

Publication policy never changes authoring availability, execution admission,
map resolution, processor authority, firmware ranges, or UI route selection.

Evidence is resolved by a separate canonical evidence snapshot:

| Evidence status | Required source |
| --- | --- |
| `DirectGolden` | Exact-route canonical golden declaration. |
| `ApprovedAlias` | Explicit fact-scoped alias naming an approved target route. |
| `SyntheticOracle` | Exact-route synthetic-oracle declaration. |
| `ContractOnly` | Exact admitted execution contract with no stronger source. |
| `Missing` | No eligible source above. |

The fixed precedence is `DirectGolden` > `ApprovedAlias` >
`SyntheticOracle` > `ContractOnly` > `Missing`. Exact declarations must name
the requested route. An alias must name source and target routes and an
applicable fact scope. The selected result retains its declaration provenance.

The initial #170 evidence adapter is intentionally empty, so only
`ContractOnly` and `Missing` may result until an authoritative
golden/alias/oracle adapter is implemented. It does not reinterpret
`GoldenVerified`, profile promotion, filenames, test names, or file hashes as
direct evidence.

## Headless baseline

The completed baseline exposes one immutable, non-UI Support Matrix query over
the union of selectable, executable, and publication sources. Each row reports
authoring availability, execution admission, publication status, evidence
classification, and provenance independently.

Unclassified routes, unresolved exact variants, selectable/non-executable
routes, and unsafe executable/non-selectable routes remain explicit diagnostics.
They never alter existing composition execution.

See [ADR 0038](../adr/0038-versioned-publication-policy-and-evidence-status.md)
for the architecture decision and migration gates.
