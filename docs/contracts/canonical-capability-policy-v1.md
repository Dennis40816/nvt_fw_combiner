# Canonical Capability Policy v1

`canonical-capability-policy-v1.json` is the sole publication and evidence policy for exact compiled capability routes. Its normative structure is `canonical-capability-policy-v1.schema.json`.

## Route identity

Every row identifies one exact tuple of `icId`, `workflowId`, `icCountVariant`, and `mapVariant`. `routeId` must equal the canonical `CapabilityRouteIdentity` derived from that tuple. Routes are unique by `routeId`; omission is not an implicit default or an inferred unsupported route.

`capabilityFingerprint` is the reviewed capability-definition fingerprint for the route. It covers the allowed map variants, selection groups, and compiler semantics. It is not the per-run `CompilationFingerprint`.

For runtime-reference CtrlRAM routes with Standard-profile report
classification, the semantic bindings include the exact admitted report map,
report slot projection, source profile, and trusted bundle identity. Adding the
map binding changes the reviewed definition fingerprint even though output
bytes do not change. Catalog `1.8.0` therefore supersedes the 23 affected
CtrlRAM route fingerprints and their three pinned decisions; the 10 reportless
CtrlRAM routes and all unrelated routes retain their prior fingerprints.

Catalog `1.9.0` records the 2026-08-24 owner decision to hide ordinary DP
Replace authoring through the initial `1.0.0`. All 14 exact `dp-replace` rows
are `Unavailable` with source reference
`owner-decision:2026-08-24:dp-replace-hidden-until-1.1.0`. Their compiled
definitions, fingerprints, publication decisions, evidence decisions, and
Golden regression authority are unchanged. The owner will decide retirement
or renewed exposure at `1.1.0`; no UI- or CLI-local override may reopen them.

Catalog `1.10.0` records the 2026-08-25 formal-support decision against the
current 89-route executable inventory. All 64 exact Standard Merge, AB Merge,
and CtrlRAM Replace routes are `Available` and `Supported`, including the 11
new CtrlRAM routes whose immutable base is the declared TP work-image prefix.
The corresponding full-flash and TP-prefix routes remain separate exact
identities even when they execute the same effective TP address range.

Evidence classification remains independent of publication. The 64 formal
routes contain 31 `DirectGolden`, nine `ApprovedAlias`, five
`SyntheticOracle`, and 19 `ContractOnly` decisions. Across all 89 routes, the
counts are 31, nine, five, and 44 respectively. In particular, input-only
multi-IC cases are not promoted to approved aliases, and the NT51929 DP route
is `ContractOnly` because its retained Golden case belongs to Standard Merge,
not DP Replace. All 14 DP routes remain `Unavailable` and `Internal` until the
`1.1.0` owner decision; General routes retain their existing internal or
test-only policy.

`Supported` is the owner-approved publication state for the exact route. It
does not by itself prove a clean release package, signing, clean-machine smoke,
or complete route-evidence cross-link. Those release gates remain independent.

## Pinned decisions

Each route contains three independent decisions:

- `authoring`: whether the exact capability may be selected for authoring.
- `publication`: whether it is supported, candidate, internal, or test-only. The retired `unclassified` value is invalid.
- `evidence`: direct golden, approved alias, synthetic oracle, contract-only, or missing.

Every decision repeats the exact `routeId` and `capabilityFingerprint`. The loader must reject a mismatch; a decision for one capability definition cannot silently authorize another. `decisionId` is a stable revision identity and `sourceReference` is traceability evidence.

The canonical Golden manifest must contain one `routeEvidence` row for every
current policy route and pin the same exact `(routeId,
capabilityFingerprint)`. A policy label never substitutes for that executable
cross-link, and evidence may not be inferred from a filename.

A source reference that says `owner-approved` does not itself create owner approval. Publication, authoring, or evidence changes remain R3 and require the recorded human authority and independent evidence defined by repository governance.

## Validation and publication

The repository verifier validates the JSON against the normative schema. Runtime loading additionally pins the exact LF-normalized file SHA-256, rejects unknown members, derives `routeId`, checks decision pins, and enforces closed decision values.

The release package ships the hash-pinned JSON runtime policy. The prose and schema remain repository contract authorities and contribute to the repository schema digest; they are not required runtime payloads. The retired standalone support-publication policy must not be restored.
