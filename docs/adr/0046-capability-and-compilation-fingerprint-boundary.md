# ADR 0046: Separate reviewed capability identity from per-compilation identity

- Status: Accepted
- Date: 2026-08-01
- Accepted: 2026-08-01 by the repository owner
- Owners: Product owner + architecture owner
- Risk: R2 architecture and policy contract; existing R3 firmware evidence gates remain unchanged
- Amends: ADR 0015, ADR 0038, and ADR 0043
- Amended by: ADR 0052 for exact CtrlRAM report-metadata map counterparts
- Amended: 2026-08-25 by the repository owner to keep support publication
  independent from evidence rank while preserving every R3 release gate

## Context

The first canonical tracer materialized one fixed route as one
`CompiledComposition`. Its implementation therefore used that artifact's
`CompilationFingerprint` as the route's `CapabilityFingerprint`. That equality
does not hold for an authorable capability whose reviewed definition permits
more than one compilation result.

NT51928 Standard Merge and DP Replace each remain one capability while allowing
declared `0x40000` and `0x80000` maps and selection-group choices. General
Merge/Replace accept bounded user mappings and, for General Merge, a bounded
initializer. CtrlRAM resolves a declared topology/map/processor plan. These
inputs must change the exact compiled plan and Preview/Build identity without
creating a new policy row or requiring an owner to repin unchanged capability
authority for every authoring revision.

## Decision

### CapabilityFingerprint

`CapabilityFingerprint` identifies the complete reviewed capability
definition. It references the canonical definition hash and binds every
definition-level semantic that can constrain a compilation, including:

- the stable route identity and referenced capability/profile definition;
- the closed set of allowed map variants and their applicability;
- slot, selection-group, topology, IC Count, and authoring constraints;
- compiler/lowering semantic declarations and allowed operation vocabulary;
- metadata, validation, integrity, processor, normalization, and naming
  definitions; and
- trusted-parent and resource ceilings where applicable.

It excludes the current authoring revision, selected files and `FileStamp`s,
the map chosen from an already reviewed set, selected group members, concrete
General mappings, the concrete General initializer, runtime dependency state,
and processor execution results.

Authoring, publication, and evidence policy bind `RouteId` plus this
`CapabilityFingerprint`. A definition-level semantic change changes the
fingerprint and makes all three decisions stale until reviewed supersession.
Selecting another already admitted variant or editing valid per-run authoring
state does not repin policy.

Publication recomputes the fingerprint from the dynamic compilation contract;
an independently supplied fingerprint cannot bless a different map set,
compiler semantic, or semantic-binding set. Binding a compiler result then
requires exact equality with its reviewed compiler-specific semantics:
selection groups may not disappear, logical output retains its reviewed family,
and runtime-reference routes retain their declared processor plus independently
derived typed selector, plan-template, and report-metadata bindings. Processor
authority comes only from the compiled invocation, report projection and source
identity come only from report-classification entries in the metadata plan, and
CtrlRAM report metadata additionally binds the exact Standard-profile map id
declared and admitted under ADR 0052. Capacity, IC identity, or input length
cannot select or substitute that map. The actual materialized report entries
must retain the same map id or compilation admission fails. The postbuild proof
is bound to the exact compilation. The proof also verifies
that compiled write ranges are the closed union of exact planner sections,
complete compiler mappings, processor-authorized resolved write views, typed
postbuild validation authority, and metadata-located firmware-version backup
fields, while write-section identities remain planner-derived. A profile with
no declared report-classification entry contributes no report binding to its
reviewed capability fingerprint. No channel may fill a missing binding owned
by another.

### CompilationFingerprint

`CompilationFingerprint` is the existing
`CompiledComposition.CompilationFingerprint`, also called the compiled-plan
fingerprint in earlier documents. It references the current
`CapabilityFingerprint` and adds only the exact state resolved or authored for
one compilation, including:

- the selected map/topology/IC Count result;
- selected input-group members and their applicable lowered views;
- concrete General mappings and output initializer;
- the exact selected processor/integrity plan; and
- the resulting address spaces, initializations, ordered operations,
  validations, output naming requirement, and execution admission.

One accepted authoring revision compiles at most once and produces exactly one
immutable `CompiledComposition`. Preview, Build, Memory Layout when compiled,
output naming, and the typed report consume that same instance and bind its
`CompilationFingerprint`. They do not recompile or substitute the capability
fingerprint for the compiled-plan identity.

Runtime-dependency inspection/readiness is also requested and published for
that exact pair of `CapabilityFingerprint` and `CompilationFingerprint`.
Another compilation under the same capability cannot reuse the snapshot. A
run must retain the exact current `ResolvedCapability` that supplied the
compiled instance; catalog reload cannot reconstruct or rebind an old dynamic
compilation from matching strings.

For CtrlRAM, the capability-level postbuild binding identifies the reviewed
profile/selector plan template. A concrete topology may expand that template;
the resulting processor invocation, write sections, validations, and other
topology-specific state belong to the exact `CompilationFingerprint`.

The capability-bound V2 formats implemented by #194 are
`nfc.compiled-composition.profile-v2.v7` for map-bound compilation and
`nfc.compiled-composition.profile-v2-logical-output.v3` for logical output.
Both append only exact compilation state after the capability fingerprint.
Unbound v5/v1 remain migration formats and do not carry capability admission.

The semantic chain is therefore:

```text
trusted document exact-byte hash
  -> canonical definition hash
  -> CapabilityFingerprint
  -> CompilationFingerprint
  -> run/Preview identity
```

Each level references the preceding identity and adds only new state. No level
reserializes lower-level ranges, fields, operations, or processor authority.

### Route map-variant axis

For a fixed-map capability, the route's map-variant axis may identify that
single map. When an accepted capability intentionally owns a closed set of
resolved map variants, such as NT51928 dual capacity, the axis identifies the
reviewed variant-set definition. The selected physical map is then
compilation state. Adding or removing a member of the set changes
`CapabilityFingerprint`; choosing an existing member changes only
`CompilationFingerprint`.

### Evidence boundary for NT51928

No current project supplies an owner-approved complete golden for the NT51928
dual-capacity Standard Merge or DP Replace capability. Those routes remain
`ContractOnly`, not `DirectGolden`. Existing byte, changed-input, and
source-projection regression tests are contract/oracle evidence only and cannot
upgrade the evidence rank. A future complete project golden requires the normal
firmware-owner review and an exact current capability/compilation binding before
evidence can be upgraded.

The 2026-08-25 owner decision separately publishes the exact NT51928 Standard
Merge route as `Supported + Available`; its evidence remains honestly
`ContractOnly`. The NT51928 DP Replace route remains `Internal + Unavailable`.
`Supported + ContractOnly` is a product-publication decision, not a claim of
independent byte parity and not release certification. It does not waive the
firmware-owner, exact write-range, packaged Windows processor, clean-machine,
signing, or release-owner R3 gates.

## Consequences

- Dynamic authoring remains one stable capability and policy row while every
  exact compilation stays deterministic and reviewable.
- NT51928 keeps one Standard Merge capability and one DP Replace capability as
  required by ADR 0043.
- General mappings and initializer edits invalidate Preview/Build through a new
  `CompilationFingerprint` and `AuthoringRevision`, not through policy churn.
- The pilot equality between capability and compilation fingerprints is a
  migration defect. Ticket #194 removes it before dynamic routes become
  canonical.
- A General Replace diagnostic Preview is available only after one exact
  canonical route has compiled and its required stage/runtime readiness can be
  evaluated for that compilation. A target with no canonical General Replace
  route or golden evidence fails route admission before any runtime probe and
  cannot fabricate a plan-only report.
- This decision and its 2026-08-25 amendment do not change firmware ranges,
  output bytes, processor write authority, or evidence rank. Publication is an
  independent exact-route policy decision and cannot rewrite evidence.

## Verification

- Definition changes stale authoring/publication/evidence policy.
- Selecting another admitted map or selection-group member retains
  `CapabilityFingerprint` and changes `CompilationFingerprint`.
- General mapping or initializer edits retain capability policy identity and
  change compilation and Preview/Build identity.
- One authoring revision resolves and compiles once; naming, layout, reports,
  UI, and CLI share the same immutable artifact.
- Deterministic vectors cover both fingerprint formats and the chained
  relationship.
- Same-capability/different-compilation readiness snapshots are stale, and a
  catalog reload cannot rebind the old compiled instance.
- Dynamic publication rejects fingerprint/contract incoherence; compilation
  rejects missing selection groups, logical-family drift, processor drift, or
  missing typed plan/report bindings. Raw adapter strings cannot attest
  processor, report-slot, tool-binding, or write-range authority.
- Application Preview/Build admission requires the exact compiled object owned
  by the accepted `ResolvedCapability`, not another instance with matching
  fingerprint strings.
- NT51928 Support Matrix evidence remains `ContractOnly` until an approved
  complete project golden exists, even while the exact Standard Merge route is
  published as supported.

## Non-goals

- No new executable extension mechanism or arbitrary compiler callback.
- No per-IC fingerprint writer.
- No inference of support, evidence, IC, topology, or map from filenames,
  lengths, hashes, or test observations.
- No firmware-semantic, range, processor, CRC, Header, naming, or UI change.
