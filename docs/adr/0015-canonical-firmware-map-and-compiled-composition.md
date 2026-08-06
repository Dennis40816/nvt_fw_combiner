# ADR 0015: Use canonical firmware-family maps and one compiled composition boundary

- Status: Accepted
- Date: 2026-07-11
- Last amended: 2026-08-06
- Owners: Product owner + architecture owner + firmware reviewers
- Supersedes: ADR 0008 catalog-join ownership and the C# catalog ownership in
  ADR 0012/ADR 0013 after #194 compatibility migration; their firmware
  meanings and evidence gates remain accepted
- Amends: ADR 0003, ADR 0004, ADR 0005, ADR 0006, and ADR 0007
- Amended by: ADR 0016, ADR 0017, ADR 0018, ADR 0019, ADR 0020, ADR 0023,
  ADR 0024, ADR 0032, ADR 0042, ADR 0045, and ADR 0046

## Context

Firmware facts are currently distributed across IC support, TP flash-map, TP header, DP version,
DP Perspective, DP Replace authoring, and legacy postbuild catalogs. Built-in composition profiles
also repeat physical ranges. Bootstrap joins these sources for UI and CLI callers, while
`CompositionRunProfile` and `CompositionPlanProvenance` independently carry overlapping run
identity.

This structure cannot safely represent all IC maps, fact-scoped aliases, NT51950/NT51951 Normal
and AB variants, bounded NVT discovery, CMD/CMD-BK metadata, or AB TPA/TPB work buffers without
adding more special cases. A resolved map, profile, validation set, and byte plan must not be
mixed across topology, capacity, input metadata, or evidence revisions.

The product owner reactivated AB architecture work on 2026-07-11. This reactivation authorizes
the shared architecture and evidence intake. It does not promote an AB profile or authorize a
range, relocation field, CRC transform, output, or UI action without its normal R3 evidence and
firmware-owner gates.

## Decision drivers

- One physical source of truth for every firmware range and metadata locator.
- One profile/compiler/executor path for Normal, AB, General, and Replace workflows.
- Deterministic resolution across IC member, mode, topology, capacity, and metadata facts.
- Immutable inputs, engine-owned work buffers, exact mutation authority, and Preview/Build parity.
- Explicit evidence, alias, promotion, and release traceability.
- Declarative future extension without per-IC executable code or UI-owned firmware semantics.

## Considered options

1. Continue adding focused C# catalogs and join them in Bootstrap.
2. Add workflow-specific map and executor implementations for AB and later REG Replace.
3. Parse owner workbooks directly at production runtime.
4. Introduce canonical family/profile documents, resolve them once, compile one atomic run artifact,
   and retain one composition engine.

Option 4 is selected. The other options duplicate firmware truth, weaken review boundaries, or
make mutable evidence files an executable runtime policy source.

## Decision

### Declarative and trust boundary

Add versioned `firmware-family-v1`, `composition-profile-v2`, `profile-bundle-v1`, and
`saved-composition-rule-v2` contracts. Approved built-in family and profile JSON is canonical.
`composition-profile-v1` remains migration evidence only after v2 production loading is complete.

The production bundle loader must:

- validate the exact committed JSON Schema with a pinned Draft 2020-12 validator;
- reject duplicate, unknown, missing, noncanonical, unlisted, orphaned, or case-colliding content;
- reject path escapes, reparse points, and mutable executable paths;
- validate every schema, family, profile, and evidence hash against a closed bundle manifest; and
- validate the bundle root hash against the release/install manifest or package signature.

Processor parameters are closed, versioned typed unions. Profiles cannot declare shell commands,
host paths, arbitrary JSON parameters, scripts, or unregistered processors.

An offline workbook intake tool may emit deterministic candidate rows only into a caller-selected
empty staging directory. It opens Office files read-only, does not execute macros, rejects lock
files and overwrite/path escapes, and never edits approved profiles, schemas, bundles, or evidence
manifests. Promotion remains a reviewed repository change.

### Ownership and dependency direction

`NvtFwCombiner.Domain` owns pure immutable map and composition semantics:

- topology requirements and selections;
- map applicability and named resolution inputs;
- regions, image maps, metadata locators, and resolved maps;
- composition input requirements, mutable-space initialization, operations, plans; and
- the atomic compiled composition run boundary.

`NvtFwCombiner.Profiles` owns built-in family/profile semantic validation, fact-scoped alias
resolution, capability evidence, profile normalization, and compilation.

`NvtFwCombiner.Contracts` remains DTO-only. `NvtFwCombiner.Infrastructure` reads trusted bundles
and implements external adapters. Bootstrap wires bundle loading, Profiles resolution/compiler,
and Application use cases without owning firmware facts. Application reads artifacts, executes
generic metadata/validation stages, runs Preview/Build, and renders reports. Presentation and CLI
only project the Bootstrap facade.

The 2026-07-26 amendment names the Application-owned read model
`CanonicalCapabilityCatalog`. Profiles remains the only authority that normalizes, resolves, and
compiles canonical family/profile definitions. Infrastructure loads and hash-validates trusted
data through ports, while Bootstrap remains dependency injection only. The catalog is a
read-only query/session snapshot over those compiled results; it does not redefine firmware
facts, replace `CompiledComposition`, or become a second compiler.

The 2026-07-28 convergence amendment limits one firmware fact to three
firmware-semantic forms:

1. a Contracts-owned serialized DTO used only for trusted schema/version
   validation and deserialization;
2. one Domain-owned canonical immutable definition; and
3. a resolved or compiled reference that stores only definition identity,
   selected applicability, resolved/per-run state, and execution identity.

Profiles services map validated DTOs into the Domain definition and may use
private ephemeral builder/validation state, but they do not expose, cache, or
persist a second semantic model. Bootstrap/Workbench, Application, Presentation,
and CLI may project ids, readiness/status, and formatted values; they cannot
mirror canonical firmware fields, ranges, locators, formatter rules,
operations, processors, or integrity declarations. Accidental public
implementation types are not compatibility authority and receive no
replacement shim after repository callers migrate.

The 2026-08-07 #230 convergence amendment makes the trusted bundle's strict,
hash-pinned schema gateway the sole production owner of serialized required/null
shape, fixed constants, and schema-version compatibility. The production route
must pass `ProfileBundleLoader` validation and the immutable trusted projection
before `CompositionProfileNormalizer` can run. Profiles lowers admitted closed
tokens, resolves cross-references, and constructs the canonical Domain model; it
does not repeat schema constants or version-presence rules. Domain constructors
continue to own canonical firmware invariants, and compiler-semantic decisions
that affect the compiled plan remain in Profiles. Direct normalizer test access
is test-only and cannot authorize an unvalidated production DTO or recreate
schema-owned diagnostics.

The canonical compiler branches only on the closed, versioned semantic
vocabulary: operation, locator, initialization, validation, integrity, and
processor-stage kinds. Production compiler, Domain, Application, Bootstrap, and
CLI code may not select behavior by IC id, family id/name, workflow id/name, or
another data identity. IC Count, Single/Cascade, topology, map, capacity, and
family differences are canonical definition/applicability data.
Existing-vocabulary IC onboarding therefore changes trusted data and its
independent policy/evidence only; it changes no production C#.

When evidence requires behavior that the current vocabulary cannot express,
the change introduces one reviewed reusable semantic primitive with coordinated
schema, Domain, compiler, fingerprint, conformance, and evidence updates. An
IC-specific branch or workaround is not an extension mechanism. Approved
external processors remain manifest-pinned, staged, range-constrained adapters;
they are not compiler plugins.

Each exact route has a stable `RouteId` composed only from IC, workflow, IC Count variant, and map
variant. Integrity, processor, artifact, metadata, operation, and other executable semantics are
part of the separate `CapabilityFingerprint`. Authoring, publication, and evidence decisions bind
both values. A fingerprint change makes prior decisions stale and requires review; it never
silently inherits authority.

Trust admission first verifies the separately pinned SHA-256 of each exact raw
document. Line-ending, whitespace, encoding, or property-order normalization
cannot make different document bytes satisfy that trust check. The remaining
semantic content identities use one versioned, deterministic, language-neutral
canonical encoding and one implementation owner. The fingerprint chain is:

```text
trusted document exact-byte hash
  -> canonical definition hash
  -> CapabilityFingerprint
  -> compiled-plan fingerprint
  -> run/Preview identity
```

Each layer references the lower-layer hash and appends only state introduced by
that layer. It does not serialize the same fields, ranges, locators, operations,
processors, or integrity facts again. Reflection, runtime/dictionary iteration
order, C# type names, storage paths, and JSON paths cannot affect an identity.
An IC, family, or workflow may not define a separate hash writer.

`RouteId`, `ResolutionToken`, `AuthoringRevision`, and `FileStamp` retain their
accepted meanings outside the firmware-semantic fingerprint chain.
`FileStamp` is the captured identity of one selected external file and contains
its accepted byte length and SHA-256; path and filesystem timestamp are
non-authoritative hints. It is not a canonical-definition hash or
`CapabilityFingerprint`. A fingerprint-format change explicitly bumps the
format version; affected authoring, publication, and evidence policy becomes
stale and is reviewed and repinned rather than silently accepted.
Cross-language vectors lock the canonical encoding and chain.

The #194 capability-bound formats are
`nfc.compiled-composition.profile-v2.v7` and
`nfc.compiled-composition.profile-v2-logical-output.v3`. They reference the
reviewed `CapabilityFingerprint` and append exact compilation state; they do
not repeat trusted bundle/profile definition provenance already covered by the
capability identity. The unbound migration formats remain v5/v1 only for
artifacts that have no capability admission identity.

Every runtime invariant has one validating owner:

1. the Infrastructure-owned trusted schema gateway validates serialized shape,
   required/null fields, primitive types, closed discriminators, fixed
   constants, schema-version compatibility, and local bounds before the
   Contracts DTO enters Profiles;
2. Domain canonical construction validates firmware-semantic ranges, overlap,
   reference kinds, cycles, applicability, family/topology rules, and
   definition completeness;
3. resolution/compiler validates only unique selection and lowering against an
   already valid canonical definition;
4. Application validates selected inputs, authoring revision, readiness,
   stale-result identity, and runtime dependencies. Runtime-dependency requests
   and snapshots bind both `CapabilityFingerprint` and the exact
   `CompilationFingerprint`; and
5. the processor host validates staging, actual before/after mutation,
   declared write authority, and tool identity.

Downstream layers accept typed validated results and do not reimplement the
same rule, message, or issue code. Assertions and contract tests may prove the
upstream boundary. The engine and processor host continue to enforce their
local memory-safety, staging, and execution preconditions; those checks reject
invalid state without becoming alternate firmware-fact, policy, or diagnostic
owners. An unvalidated wire DTO cannot bypass canonical
normalization/construction or enter resolution, compilation, Application, or
execution.

Application exposes capability-centered use cases only: resolve/query a
capability, create/update an authoring session, inspect selected artifacts,
Preview/Build one compiled capability, retrieve its typed report, and refresh
runtime dependencies. Standard Merge, AB Merge, General Merge, DP Replace,
CtrlRAM Replace, and General Replace do not retain separate service/request/
result hierarchies. Their differences are canonical workflow definitions, slot
and authoring policies, typed mapping/slot child state, and compiled
operations.

General mappings, CtrlRAM selections, and other mode-specific drafts may use
typed child state inside the shared session contract, but they cannot define a
second execution/readiness/report pipeline. UI and CLI consume the same
Application contracts without workflow facades of their own. Bootstrap remains
wiring and cannot replace the deleted services with a broad gateway.

External processor infrastructure similarly has one staged host and one adapter
per reviewed protocol family, not per IC, workflow, topology, or stage.
Canonical processor-plan data owns the tokenized plan, staging bindings,
read/write authority, and tool identity. The compiled capability carries a
typed stage; Infrastructure performs manifest resolution, execution lifecycle,
independent diff, and mutation enforcement without firmware-identity branches.

Every cache is an optional, bounded performance adapter. Disabling or clearing
it cannot change resolution, compiled bytes, readiness, diagnostics, evidence,
publication, or support. The allowed cache categories are the immutable trusted
catalog snapshot, artifact inspection cache, bounded Hex viewport/page cache,
and runtime-dependency snapshot. Workflow/UI code cannot create another cache
of those facts.

Keys include the complete applicable definition/content hashes,
`CapabilityFingerprint`, `FileStamp`, topology/selection identity, and
environment generation. IC, mode, filename, or path alone is never sufficient.
Authoring sessions retain selected-file identity and revision, not complete BIN
payloads or cache ownership. Each cache declares capacity/lifetime,
invalidation owner, and stale-publication tests. A miss recomputes through the
same canonical pipeline and never enables a fallback locator, route, policy, or
support result.

Within one accepted operation/revision, orchestration evaluates each expensive
step once and passes its immutable result to every consumer. Inspection,
resolution, compilation, engine execution, and each declared processor stage
are not rerun by output naming, Memory Layout, reporting, UI, or CLI. This
single-evaluation contract is separate from optional cross-operation caches.

The 2026-07-29 Memory Layout amendment makes the layout snapshot a disposable,
immutable Application projection rather than another firmware model. Its pure
projector reads one `ResolvedCapability`, the matching `ActiveSessionSnapshot`,
and optionally the exact `CompiledComposition` instance already owned by that
capability. It cannot perform I/O, resolution, compilation, execution, or
cache publication.

Physical geometry remains the exact `FirmwareRegion` references and resolved
half-open ranges from the canonical map. Initialization plus admitted ordered
operations produce transient before/after coverage; no copied region
definition, guessed range, color, pixel width, or renderer state enters the
Application result. A selected artifact whose placement is not resolved is a
non-geometric pending or blocked item with typed prerequisite and next action.
Each blocked item also retains an opaque issue reference owned by the exact
inspection or validation result and pins it to the same resolution token,
authoring revision, slot definition, selected path, and file stamp. The
projection does not copy diagnostic text or firmware facts.
Content role, workflow disposition, endpoint/bank identity, diagnostics,
observed change, selection, focus, and declared processor effect remain
orthogonal typed dimensions. Primary segments may own subordinate kept-range
details, but those details are not canonical regions and cannot become a
second map. Presentation alone maps these typed facts to colors, patterns,
icons, labels, hover details, and responsive geometry.

Authoring is one shared `Available` or `Unavailable` decision for UI and CLI. Publication is the
separate explicit policy `Supported`, `Candidate`, `Internal`, or `TestOnly`; missing publication
policy is a materialization error. Evidence is independently classified as `DirectGolden`,
`ApprovedAlias`, `SyntheticOracle`, `ContractOnly`, or `Missing`. `Missing` evidence can coexist
with an authoring-ready route and does not by itself prevent deterministic BIN Build, but
`Supported` plus `Missing` is a certification inconsistency that blocks promotion, CI,
certification, and release.

The catalog loads at startup and changes only through an explicit Application `Reload Catalog`
use case. A complete candidate is validated before atomic publication. Duplicate exact routes,
conflicting content hashes, corrupt content, or stale/missing policy reject the candidate; there
is no last-writer-wins behavior. A running process retains its last-known-good snapshot after a
failed reload. Cold start without a valid snapshot blocks every Build through one typed diagnostic
shared by UI and CLI.

### Canonical firmware resolution

A family/map owns physical facts only: capacity, address spaces, region hierarchy and half-open
ranges, semantic owner/kind, explicit reserved or unmapped gaps, metadata locators, evidence, and
non-relaxable safety lower bounds. Newly modeled physical rows are read-only by default.

For FWConfig facts, the product owner decision of 2026-07-12 fixes the canonical source as the
unique NVT Backup at terminal `T - 0xFFF`. In a `firmware-family-v1` marker-relative locator this
is marker `00 4E 56 54`, `unique` selection, and result offset `-0xFFC` from marker start. A
flash-map primary address is TP Overview/evidence only and cannot be a metadata fallback or runtime
prerequisite. Compatibility readers enforce the same rule until V2 metadata lowering is executable.

Resolution separates:

- `TopologyRequirement`: none, single, cascade, or exact count;
- `TopologySelection`: requested or derived value, source, and required exact count;
- `MapApplicability`: member, mode, topology, capacity, Common FW category, and metadata predicates;
- `ResolutionInputs`: requested selections plus immutable artifact payloads snapshotted and hashed
  by Domain; and
- `FirmwareMapResolutionResult`: pending, uniquely resolved, or rejected.

Topology-independent behavior is applicability with no topology requirement; it is not an alias.
Shared RegionSets are direct references. Only owner-approved inheritance uses a fact-scoped alias.
Aliases never infer a whole map, processor, range, capacity, or capability, and resolution rejects
cycles, ambiguity, and applicability leaks.

Before artifact evaluation, Profiles normalizes the source family into one immutable
`FirmwareFamilyResolutionDefinition`. This Domain aggregate binds the family id, version, trusted
content hash, candidate maps, and only the metadata sets selected by those maps. It validates
family-global structure identity, candidate-scoped structure/field references, typed predicate
representability, and map-specific locator geometry. Artifact binding ids are derived from reachable
structures rather than declared a second time. Public lookup always starts with a map id, so a
globally known structure cannot bypass that candidate's metadata-set selection.

The resolution definition is a normalized resolution subset, not a second DTO for the complete
`firmware-family-v1` document. Profiles resolves fact aliases and validates source members,
capabilities, and evidence before constructing it; unresolved aliases and capability policy never
enter Domain map selection. Common FW category and derived-topology rules remain pending until their
closed derivation contracts exist. Normalized alias/evidence provenance must also be modeled before
the full resolver can be promoted as complete.

Application reads artifact bytes before the Domain boundary. Domain accepts no artifact-reader port,
defensively snapshots every payload, and computes the only accepted hash/length identity from that
snapshot. Caller-supplied decoded facts and derived selections are forbidden. Metadata locators are
a closed discriminated model: absolute range, region-relative, or
marker-relative. Marker-relative rules declare a bounded search range, exact marker, approved
match policy/cardinality, checked relative result, expected structure, and allowed result region.
Marker evaluation tests every byte start whose complete marker fits the bounded range, so overlapping
matches count. A terminal expected count cannot exceed the bounded candidate-start count
`searchLength - markerLength + 1`. Zero, ambiguous, out-of-range, or structurally invalid results are
never guessed.

A `ResolvedFirmwareImageMap` atomically records the selected physical map, every predicate and
locator outcome, resolver-owned decoded/derived facts, input artifact hashes, and alias/evidence
chain. Candidate outcomes stay private to one candidate and are discarded unless that map is the
unique result. Resolved maps never retain artifact bytes. They do not grant workflow execution; a
profile is the only owner of executable workflow policy and promotion state.

### Profile and compiled composition boundary

The canonical pipeline is:

```text
firmware-family-v1
  -> normalized FirmwareFamilyResolutionDefinition

FirmwareFamilyResolutionDefinition + ResolutionInputs
  -> ResolvedFirmwareImageMap

composition-profile-v2 + ResolvedFirmwareImageMap + compile request
  -> Domain-owned canonical composition definition
  -> CompiledComposition
  -> one CompositionPlan / one CompositionEngine
```

`CompositionProfileDefinition` is the Domain-owned canonical composition
definition produced by direct Contracts-to-Domain normalization. Within that
normalization seam, Profiles owns schema-version parsing and may use only
private ephemeral validation state; it cannot return a consumer-visible fourth
semantic model. Profiles retains its separately declared trusted-catalog,
resolution, admission, and compiler responsibilities. The canonical v2 definition owns slots, logical
views, experience access, mutable initializers, ordered operations, validators,
processor stages, output naming, evidence, and promotion state. It references
canonical map region/view ids and cannot relax map safety.

The compiler returns one immutable `CompiledComposition`, which is the only Application run
boundary. It contains the sole `CompositionPlan`, profile/bundle/map identity and hashes, complete
selection and locator outcomes, alias/evidence provenance, validation stages, output-space and
naming requirements, promotion/eligibility verdict, and compilation fingerprint.

`CompositionRunRequest` must accept this compiled artifact, immutable artifact bindings, output
options, and an optional Preview token. It must not accept a separately supplied plan and profile
metadata. Preview and Build bind the compiled fingerprint, normalized input hashes, and output
options; any changed bundle, map resolution, metadata fact, input, or output option invalidates
approval. The duplicate `CompositionRunProfile` boundary is removed.

### Composition engine amendment

Every mutable address space has exactly one engine-owned `Blank` or immutable-source `Clone`
initializer, and exactly one mutable space is selected as final output. Callers cannot seed mutable
work buffers. This supports TPA and TPB staging while preserving immutable inputs.

The existing operation algebra remains the only byte executor. It is extended with one generic,
closed scalar transform for relocation: source and target ranges, width, byte order, checked signed
addend, optional expected-before value, reject-on-overflow policy, and provenance. This is not an
expression DSL and cannot call IC-specific code.

Input acceptance and normalization become typed plan requirements rather than
address-space geometry. ADR 0045 reduces address-bearing Initial Code, DP, TP,
LDC, TPA, and TPB section admission to one view-covering requirement with
optional expected outer lengths. Every selected source view, metadata read,
validation read, and processor read must remain inside the declared execution
snapshot. A compatible same-IC FlashCode may provide the same section views.
The actual immutable source hash/length remains evidence, ignored trailing
bytes are reported, and coverage never grants padding or changes an operation
range.

Exact complete-container admission remains separate for Replace Reference and
complete DP AB seeds. Bounded payloads, owner-approved padding, and CtrlRAM
truncation-with-warning retain their fail-closed constraints. Compact CtrlRAM
is the only current built-in payload-relative source; TPB is a TP-native source
window plus a resolved bank placement delta. The original workflow-named
Normal-DP/TP length rules and declared-prefix AB compatibility lowerings are
deleted only after R3 golden and firmware-owner migration gates pass.

### Capability, promotion, and future workflows

Family documents may state technical capability evidence, but profile promotion is the sole
execution authority. Each profile shape records a monotonic promotion stage and explicit blockers:

```text
Known -> MapResolvable -> Inspectable -> Authorable
-> Compilable -> ExecutableCandidate -> Supported
```

Migration preserves every profile's pre-migration stage, golden hashes, operation traces, and
blockers. Migration alone never promotes a profile to `Supported`.

Saved-rule promotion is represented now as a constrained v2 profile overlay bound to parent
bundle/profile/map hashes and compiled by the same compiler. Future REG Replace is represented as
a pending capability over canonical Register regions, with no executable profile or UI exposure
until owner evidence exists. Neither creates a new executor.

The legacy migration covered all 13 then-selectable ICs for evidence/catalog
resolution. ADR 0042 removes NT51920, NT51925, NT51930, and NT51931 from the
`0.10.x` production capability set, so their legacy coverage cannot materialize
as a target selectable, executable, or published route.

## Consequences

### Positive

- One answer for physical ranges, CMD/FWConfig/NVT locators, profile behavior, and run provenance.
- Normal, AB, General, Replace, saved-rule, and future Register policy share one compiler/engine.
- Topology/capacity aliases cannot silently mix map, metadata, processor, or promotion facts.
- Preview approval is bound to every executable input and policy decision.
- Future IC and profile additions are reviewed data changes rather than workflow-specific code.

Within the vocabulary implemented by the end of `0.10.x`, onboarding is a versioned,
hash-closed data bundle plus a package trust-index entry. Runtime scripts, executable plugins,
dynamic assemblies, and UI-generated trusted content are outside this boundary. A future IC
authoring UI may produce only an untrusted candidate; independent review, CI, firmware evidence,
and trust promotion remain required before publication.

### Negative / trade-offs

- The migration crosses Domain, Profiles, Contracts, Infrastructure, Application, Bootstrap, UI,
  CLI, tests, schemas, packaging, and release provenance.
- A trusted JSON Schema validator and an offline workbook intake dependency require pinned versions
  and explicit dependency review.
- Compatibility adapters are required until every current normal golden and operation trace passes.

### Risks and mitigations

- Broad byte regression -> migrate one family/profile at a time and compare full output and traces.
- Premature support promotion -> preserve promotion stage/blockers independently from map coverage.
- Mutable policy bundle -> anchor bundle hash in trusted release/install authority.
- Alias overreach -> permit only typed fact scopes with direct evidence and parity tests.
- AB processor overreach -> separate R3 branch, exact write authority, staged diff, golden, and owner gate.

## Compatibility and migration

1. Add ADRs, contracts, evidence convergence, and strict intake/loader tests.
2. Add Domain map/resolution types and all family facts behind compatibility projections.
3. Add profile v2 loading and normalized `CompositionProfileDefinition`.
4. Add `CompiledComposition`, multi-buffer initialization, scalar relocation, and run-boundary changes.
5. Migrate metadata, postbuild, UI/CLI/report projections and current Normal/Replace profiles.
6. Delete old catalogs and adapters only after byte, naming, operation, processor, and UI/CLI parity.
7. Implement AB behavior on a separate R3 feature branch with its own evidence gates.
8. Materialize the canonical capability catalog route by route, beginning with the owner-approved
   NT51929 Standard Merge tracer, and remove migration-only `Unclassified`, duplicate support
   identities, and compatibility projections only after every canonical route has explicit policy.

## Verification

- Schema and production bundle-loader strictness and trust-anchor tests.
- Production bundle promotion requires real-environment evidence that final-file and intermediate-directory
  reparse points are rejected, plus an immutable-root guarantee and immediate open-time snapshot or handle
  revalidation that closes the resolver-to-read TOCTOU interval.
- Evidence-row disposition, all-IC resolution, alias, topology, capacity, and locator tests.
- Map/profile mismatch, nested-region access, promotion blocker, saved-rule, and REG pending tests.
- Mutable initialization, scalar relocation, processor authority, and immutable-source tests.
- Preview/Build fingerprint mismatch tests across bundle, map, metadata, input, and output changes.
- Full current Normal golden bytes, naming, operation trace, and processor trace parity.
- Evidence-gated AB relocation/CRC/order golden and negative tests.
- Architecture tests enforcing one compiler/executor and firmware-free UI/CLI/Bootstrap layers.
- Catalog tests for stable route identity, fingerprint staleness, policy/evidence independence,
  duplicate rejection, explicit reload, atomic publication, last-known-good retention, and
  cold-start fail-closed diagnostics shared by UI and CLI.
- Polytail, `python scripts/verify.py --all`, Codex review, and required human firmware review.
