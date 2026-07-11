# ADR 0015: Use canonical firmware-family maps and one compiled composition boundary

- Status: Accepted
- Date: 2026-07-11
- Owners: Product owner + architecture owner + firmware reviewers
- Supersedes: ADR 0008 catalog-join ownership after compatibility migration
- Amends: ADR 0003, ADR 0004, ADR 0005, ADR 0006, and ADR 0007

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

### Canonical firmware resolution

A family/map owns physical facts only: capacity, address spaces, region hierarchy and half-open
ranges, semantic owner/kind, explicit reserved or unmapped gaps, metadata locators, evidence, and
non-relaxable safety lower bounds. Newly modeled physical rows are read-only by default.

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
  -> normalized CompositionProfileDefinition
  -> CompiledComposition
  -> one CompositionPlan / one CompositionEngine
```

`CompositionProfileDefinition` remains the normalized typed Profiles model, but no longer owns a
second physical region map. A v2 profile owns slots, logical views, experience access, mutable
initializers, ordered operations, validators, processor stages, output naming, evidence, and
promotion state. It references canonical map region/view ids and cannot relax map safety.

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

Input acceptance and normalization become typed plan requirements rather than address-space
geometry. Exact, bounded/view-covering, owner-approved padding, CtrlRAM truncation-with-warning,
and fixed Normal DP extraction-with-warning retain their existing fail-closed constraints.

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

All 13 selectable ICs receive complete evidence/catalog resolution coverage. This is not a claim
that every IC/workflow is authorable, compilable, executable, or supported.

## Consequences

### Positive

- One answer for physical ranges, CMD/FWConfig/NVT locators, profile behavior, and run provenance.
- Normal, AB, General, Replace, saved-rule, and future Register policy share one compiler/engine.
- Topology/capacity aliases cannot silently mix map, metadata, processor, or promotion facts.
- Preview approval is bound to every executable input and policy decision.
- Future IC and profile additions are reviewed data changes rather than workflow-specific code.

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

## Verification

- Schema and production bundle-loader strictness and trust-anchor tests.
- Evidence-row disposition, all-IC resolution, alias, topology, capacity, and locator tests.
- Map/profile mismatch, nested-region access, promotion blocker, saved-rule, and REG pending tests.
- Mutable initialization, scalar relocation, processor authority, and immutable-source tests.
- Preview/Build fingerprint mismatch tests across bundle, map, metadata, input, and output changes.
- Full current Normal golden bytes, naming, operation trace, and processor trace parity.
- Evidence-gated AB relocation/CRC/order golden and negative tests.
- Architecture tests enforcing one compiler/executor and firmware-free UI/CLI/Bootstrap layers.
- Polytail, `python scripts/verify.py --all`, Codex review, and required human firmware review.
