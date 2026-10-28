# Canonical Capability Policy v1

`canonical-capability-policy-v1.json` is the sole publication and evidence policy for exact compiled capability routes. Its normative structure is `canonical-capability-policy-v1.schema.json`.

## NT51925 unverified authoring (PR4b, 2026-10-28)

Owner decision 334 (2026-10-07) isolated nine NT51925 Standard Merge/CtrlRAM
Replace routes from the 85-route v1.2.1 catalog. PR4b opens all nine under
[ADR 0084](../adr/0084-restore-nt51925-standard-merge-and-ctrlram-replace.md).
Catalog `1.25.0` now has 94 routes, all 94 with `Available` authoring and zero
`Unavailable` authoring decisions. NT51925 participates in both workflow
selectors; the catalog, Standard Merge and CtrlRAM Replace available-IC
counts are each 11. Publication and evidence remain independent of authoring.

| NT51925 routes | Authoring | Publication | Evidence | Output name |
| --- | --- | --- | --- | --- |
| One Standard Merge map-set route | Available | Candidate | ContractOnly | Application adds `_UNVERIFIED` before `.bin`. |
| Eight CtrlRAM Replace routes (two FW lines, Single/Cascade, TP-work/full-Flash) | Available | Candidate | ContractOnly | `nt51925-ctrlram-replace_UNVERIFIED.bin`, override forbidden. |

The five profiles remain `executable-candidate` with all three `map`, `golden`
and `human-review` promotion blockers. Standard uses the existing shared
`CompiledComposition.IsV2StandardMergeOpenCandidate` execution admission;
Replace uses the existing runtime-reference candidate admission. Authoring
and execution require valid inputs and runtime dependencies. This grants no
support upgrade, Golden certification or release approval. The 94-route
publication counts remain 63 Supported, 20 Candidate, ten Internal and one
TestOnly; evidence remains 26 DirectGolden, seven ApprovedAlias, four
SyntheticOracle and 57 ContractOnly. No other IC's route or trust identity
changes. No other NT51925 workflow is registered.

### Version-conditioned Standard map set

The one Standard route retains map variant `nt51925-common-fw-256k` and route id
`route-7-nt51925-14-standard-merge-13-selector-free-22-nt51925-common-fw-256k`.
Common FW below 2.0.0 uses `nt51925-standard-merge-256k` (the 1.x map, from
1.0.0); from 2.0.0 it uses `nt51925-standard-merge-fw2-256k`. The TP FWConfig
version selects the exact map under the shared interval contract in
[firmware-family-v1](firmware-family-v1.md#common-fw-image-map-intervals).
Unreadable versions still fail with `profile.v2.compile.common-fw-version-required`.
Both maps retain identical operations. PR4a read-only classification and
metadata use agreement across all declared maps; an explicit classification
map never substitutes for authoring version selection. Declared metadata must
match the route's allowed map ids, profile id/version and trusted bundle hash;
load failures return typed unavailable results.

Changing TP content invalidates retained exact selection. CtrlRAM report
metadata retains the `nt51925-standard-merge-256k` anchor. PR4b supersedes only
the nine authoring decision ids with the `nt51925-open-authoring-v3` suffix;
publication and evidence decision identities and values remain unchanged.
The profile reason correction re-pins NT51925 profile/bundle and dependent
capability identities; it changes no copy operation or expected output.

Evidence scope and difference bounds are recorded in the
[support matrix](../architecture/supported-ic-matrix.md). In particular,
golden coverage: FW version less than or equal to 1.4.1. This is separate from
the 2.0.0 map boundary and does not promote route evidence or support.

## Historical active admission (catalog 1.17.0)

Schema `1.1` and catalog `1.17.0` retire all 14 DP Replace policy routes.
The active workflow set is exactly `standard-merge`, `ab-merge`,
`ctrlram-replace`, `general-merge`, and `general-replace`. The runtime policy
loader rejects old schema `1.0`, `dp-replace`, and unknown workflows even when
the row identity, three decision bindings, and source hash are self-consistent.
Generic historical route identities remain valid as traceability values; they
do not confer active admission.

The 79 surviving routes keep their exact fingerprints and decision IDs,
values, and source references. There remain 64 formal-supported routes, with
26 direct-Golden, seven approved-alias, four synthetic-oracle, and 42
contract-only decisions across the complete active catalog. Golden removes
only the 14 retired `routeEvidence` rows; all 40 cases, expected bytes,
difference bounds, alias facts, and redistribution authority remain unchanged.
The versioned entries below describe their historical catalog decisions;
DP availability and internal-publication statements are superseded by `1.17.0`.

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
routes contain 28 `DirectGolden`, nine `ApprovedAlias`, five
`SyntheticOracle`, and 22 `ContractOnly` decisions. Across all 89 routes, the
counts are 28, nine, five, and 47 respectively. In particular, input-only
multi-IC cases are not promoted to approved aliases. The NT51950 single,
NT51951 single, and NT51951 two-IC TP-work routes are `ContractOnly`: execution
from the original TP bases succeeds, but the owner full-Flash expected prefixes
contain DP-origin bytes and are not independent TP-only expected outputs. The
NT51929 DP route is also `ContractOnly` because its retained Golden case belongs
to Standard Merge, not DP Replace. All 14 DP routes remain `Unavailable` and
`Internal` until the `1.1.0` owner decision; General routes retain their
existing internal or test-only policy.

`Supported` is the owner-approved publication state for the exact route. It
does not by itself prove a clean release package, signing, clean-machine smoke,
or complete route-evidence cross-link. Those release gates remain independent.

## Pinned decisions

Catalog `1.16.2` supersedes 92 exact route fingerprints after the reviewed
full-image metadata family migration in `1.1.10`. The existing compiled and
dynamic inventory producers supplied all 93 current fingerprints; one route
retains its prior fingerprint. The change binds the updated family, profile
and bundle hashes. Authoring, publication and evidence decisions retain their
values and source references. Canonical Golden `routeEvidence` pins follow
those exact fingerprints; cases, expected bytes and redistribution authority
remain unchanged.

Catalog `1.11.0` transitions the six AB IC/topology routes to profile-owned
optional DP selection for v1.1.4. NT51950 retains distinct single/cascade axes
within `nt51950-ab-merge-maps`; each axis admits only its original physical map.
The four selector-free routes retain their original singleton map-set IDs.
Fingerprints come from the existing dynamic inventory, not per-run output.

Authoring and publication values remain unchanged. The expanded definitions
are provisionally `ContractOnly`: previous DirectGolden/ApprovedAlias/
SyntheticOracle decisions cover Normal inputs, not all new Dummy selections.
All historical Normal cases, alias facts, expected bytes and release execution
obligations remain intact in the canonical Golden cases. Updating the six
current `routeEvidence` bindings does not revoke that case-level evidence or
certify Dummy. Independent map-based complete-output verification and ordinary
Golden regression are still required before final acceptance. Evidence labels
must be reviewed against their actual scope, never raised automatically from
a passing compilation test.

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

The 1.1.11 AB CtrlRAM display-context binding reuses the exact Standard
companion already declared by the local route. This changes definition
fingerprints for the existing NT51919, NT51950 and NT51951 single/cascade AB
routes, including their matching canonical manifest `routeEvidence` pins.
It does not change any decision value or source reference, expected output
bytes/hash, evidence rank or publication status. The binding identifies
read-only bank section context; it grants no new execution or input authority.

The repository verifier validates the JSON against the normative schema. Runtime loading additionally pins the exact LF-normalized file SHA-256, rejects unknown members, derives `routeId`, checks decision pins, and enforces the active workflow vocabulary and closed decision values.

The release package ships the hash-pinned JSON runtime policy. The prose and schema remain repository contract authorities and contribute to the repository schema digest; they are not required runtime payloads. The retired standalone support-publication policy must not be restored.
