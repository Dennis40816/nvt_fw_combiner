# ADR 0019: V2 General Merge Logical-Output Admission

- Status: Accepted
- Date: 2026-07-15
- Owners: Product owner + architecture owner
- Amends: ADR 0015 and ADR 0018

## Context

General Merge is a typed authoring workflow, not a physical firmware-map selection. A user chooses
one or more source artifacts, declares explicit half-open source-to-output copy ranges, and chooses
an arbitrary positive output capacity. The selected IC identifies the output/report context; it does
not prove a unique canonical map. Existing behavior rejects overlapping targets, initializes the
output with `0x00`, and lowers every mapping into the shared `CompositionPlan` and
`CompositionEngine`.

The current V2 runtime admission intentionally supports only resolved-map Merge and DP Replace
profiles with exactly one singleton immutable input per slot. Treating General Merge output capacity
as `requestedMapCapacity`, selecting a representative map, or relaxing that admission globally
would invent physical-map provenance and would alter existing multi-binding behavior.

## Decision

Add one narrow V2 logical-output compilation context for General Merge. It binds trusted bundle,
family, effective member, profile, and runtime mapping evidence, but deliberately does not claim a
`ResolvedFirmwareImageMap`.

The context is admitted only when the trusted V2 profile declares all of the following:

- `compositionKind = merge`, `experienceId = general-merge`, `layoutPolicy = user-defined`, and
  `inputPolicy = extensible`;
- one `output-image` with a new `runtime-request` capacity policy, blank initialized with `0x00`;
- an auxiliary `one-or-more` input slot with `per-binding` instance materialization;
- explicit `copy-range` mappings only, with `reject` overlap policy; and
- no physical-map regions, metadata validation, processor stage, CRC/header stage, normalization,
  caller-owned mutable input, executable path, or script.

A typed compile request supplies the requested output capacity, concrete immutable binding
identities and exact lengths, plus mapping IDs, sequence, source binding/range, output range,
alignment, reason, and provenance. It contains no host path, bytes, command, process argument,
or decoded firmware fact. Profiles validates and materializes every binding as one immutable
address space and lowers every mapping through the existing `V2CompositionPlanCompiler` into the
existing operation algebra. No General Merge executor, operation kind, or fallback compiler is
introduced.

The output capacity must be in `1..Int32.MaxValue`. Source and target ranges are half-open and use
checked arithmetic. Binding IDs and mapping IDs are unique; mapping sequence is deterministic and
unambiguous; source and target lengths are equal and positive; every source and target range is in
bounds; and every target is the final logical output. Before reading, Application validates that
runtime artifacts match the concrete compiled binding identities, artifact class, original
filenames, and extensions. After reading and before execution, it validates each actual byte length
against the compiled immutable space. Preview-to-Build identity includes the compiled bindings,
mappings, output capacity, chosen output filename, and the read input hashes.

Application admits this sole `V2PlanCompiled` exception only when the context is logical-output and
the promotion stage is exactly `executable-candidate`. It does not admit map-bound plan artifacts or
other promotion stages. This permits controlled candidate parity collection without promoting the
profile to supported execution.

`runtime-request` capacity is not valid on resolved-map profiles. Logical-output `memberIds` must be
canonical `NT...` identities present in the bound exact family snapshot. A candidate may snapshot an
existing exact family solely to carry that member identity, but it cannot select or claim a physical
image map. A dummy logical-only family is forbidden; a cross-family candidate requires each exact
family binding to be registered explicitly. Logical-output admission must not
bypass map-backed region access, metadata, processor, or promotion controls. General Merge does not
fall back to legacy after V2 compile selection. On 2026-07-15, the product owner accepted completed
legacy/V2 byte-parity evidence for every built-in General Merge IC. The default General Merge route
therefore selects the registered logical-output V2 profile. An invalid registered V2 request fails
only that General Merge run; it does not alter Standard Merge, Replace, CLI routing, or UI routing
for any other workflow. Persisted 0.9.2 General Merge saved-rule profile ids remain compatibility
aliases during the cutover.

## Consequences

- Existing 0.9.2 workflows retain their current behavior; pending candidate intake remains scoped to
  the requested member and cannot globally reject General Merge or other supported workflows.
- The V2 compiler gains one typed request overlay, not a second compiler or dynamic scripting API.
- A new exact schema/profile contract version is required. The existing map-bound schemas and
  runtime admission rules remain unchanged for all other workflows.
- This ADR does not add production IC support, a firmware range, an AB behavior, a CtrlRAM
  Combiner declaration, a CRC calculation, or a golden claim.

## Migration and Verification

1. Add the exact schema and normalized typed logical-output request/profile members.
2. Add V2 lowering that materializes `per-binding` inputs and explicit mappings into the existing
   plan and compilation fingerprint.
3. Extend Application's V2 binding admission for the resulting concrete immutable inputs.
4. Register an `executable-candidate` General Merge profile; do not promote it automatically.
5. Compare legacy and V2 output bytes, plans, report operation order/provenance, and Preview tokens
   for single/multiple source mappings, repeated sources, blank gaps, adjacent ranges, arbitrary
   capacities, bounds/overflow, duplicate IDs, overlap, missing/extra/swapped bindings, and failed
   output promotion.
6. Switch the workflow only after reviewed exact-parity evidence and owner acceptance. The
   logical-output profile remains `executable-candidate`, because it declares no physical firmware
   support or map authority. Delete the dynamic legacy profile construction only in that cutover
   commit.
