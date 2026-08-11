# Canonical Capability Policy v1

`canonical-capability-policy-v1.json` is the sole publication and evidence policy for exact compiled capability routes. Its normative structure is `canonical-capability-policy-v1.schema.json`.

## Route identity

Every row identifies one exact tuple of `icId`, `workflowId`, `icCountVariant`, and `mapVariant`. `routeId` must equal the canonical `CapabilityRouteIdentity` derived from that tuple. Routes are unique by `routeId`; omission is not an implicit default or an inferred unsupported route.

`capabilityFingerprint` is the reviewed capability-definition fingerprint for the route. It covers the allowed map variants, selection groups, and compiler semantics. It is not the per-run `CompilationFingerprint`.

## Pinned decisions

Each route contains three independent decisions:

- `authoring`: whether the exact capability may be selected for authoring.
- `publication`: whether it is supported, candidate, internal, or test-only. The retired `unclassified` value is invalid.
- `evidence`: direct golden, approved alias, synthetic oracle, contract-only, or missing.

Every decision repeats the exact `routeId` and `capabilityFingerprint`. The loader must reject a mismatch; a decision for one capability definition cannot silently authorize another. `decisionId` is a stable revision identity and `sourceReference` is traceability evidence.

A source reference that says `owner-approved` does not itself create owner approval. Publication, authoring, or evidence changes remain R3 and require the recorded human authority and independent evidence defined by repository governance.

## Validation and publication

The repository verifier validates the JSON against the normative schema. Runtime loading additionally pins the exact LF-normalized file SHA-256, rejects unknown members, derives `routeId`, checks decision pins, and enforces closed decision values.

The release package ships the hash-pinned JSON runtime policy. The prose and schema remain repository contract authorities and contribute to the repository schema digest; they are not required runtime payloads. The retired standalone support-publication policy must not be restored.
