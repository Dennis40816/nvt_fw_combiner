# ADR 0048: Separate headless slot readiness from inspection health

- Status: Accepted
- Date: 2026-08-02
- Accepted: 2026-08-02 by the product and architecture owner through the
  owner-approved discussion and GitHub issue #277
- Owners: Product owner + architecture owner; firmware-owner review remains
  required for any accepted-input or byte-semantic change
- Risk: R2 cross-layer Application contract; any firmware-semantic delta is R3
- Builds on: ADR 0015 and ADR 0046
- Partially supersedes: ADR 0040 only for the independent DP-before-TP
  selection decision; exact-reference and metadata-prerequisite semantics remain
- Amends: ADR 0043 and the `0.10.x` maintainability specification

## Context

The shared firmware-slot Presentation pilot can render empty, checking,
verified, warning, and error surfaces. Its generic non-AB inspection path,
however, currently publishes decoded firmware facts without a typed terminal
input-health result. A selected DP Replace input can therefore remain
`Checking` indefinitely. Standard Merge and CtrlRAM Replace have the same gap
when projected through the shared card.

AB Merge already inspects selected artifacts against a compiled input contract
and produces valid, warning, or blocking health. That behavior is exposed by a
route-specific Bootstrap projection, so copying it into each ViewModel would
create several validators and let UI state become firmware authority.

ADR 0046 already separates the reviewed `CapabilityFingerprint` from the exact
per-compilation `CompilationFingerprint`. Per-slot terminal health must bind
that existing identity boundary rather than define a third slot-specific
fingerprint.

## Decision

### Readiness and inspection are independent typed dimensions

Application owns one immutable per-slot result. It carries two independent
dimensions:

1. prerequisite and selection readiness: `NotApplicable`, `PendingInput`,
   `Blocked`, or `Ready`; and
2. selected-artifact inspection health: `Checking`, `Verified`, `Warning`, or
   `Error`.

`PendingInput` means a named prerequisite is not ready. It includes the exact
prerequisite artifact/slot identity, a typed `LoadArtifactFirst` next action,
and `CanSelect = false` for an independent dependent-slot transition.
`Required` is Presentation terminology for an applicable, dependency-ready,
empty required slot; it is not another Application readiness value and its
picker remains enabled.

An atomic headless request may contain both prerequisite and dependent
artifacts. Evaluation is order-independent and resolves the prerequisite graph
from that coherent request. What is forbidden is publishing an independent
dependent selection against an authoring revision whose prerequisite is still
pending.

`Checking` is transient. A currently selected immutable artifact either
publishes `Verified`, `Warning`, or `Error`, or its result is rejected as stale.
Presentation cannot convert file presence, decoded metadata, a prior green
surface, or a successful prior run into terminal health.

`Verified` means only that the selected source is admitted by the current
compiled input contract. `Warning` is a completed, accepted inspection with a
visible non-blocking diagnostic. `Error` is a completed blocking diagnostic.
None of these states claims that POSTBUILD, CRC/Header, final output integrity,
publication, support, or golden evidence is complete.

### Reuse one compiled input inspector

The headless projection reuses `CompiledInputArtifactInspectionService` and
its compiler-owned length/source-view policy:

- `Valid` maps to `Verified`;
- accepted `Warning` maps to `Warning`; and
- `Blocking` maps to `Error`.

No second length, range, truncation, FlashCode, or input-admission validator is
introduced. Build reopens and revalidates its immutable run-bound source; an
authoring-time inspection is display and action-readiness evidence, not a
substitute for Build validation.

### Terminal health binds ADR 0046 compilation identity

ADR 0046 remains the sole owner of `CapabilityFingerprint`,
`CompilationFingerprint`, and their versioned chain. This decision only applies
that boundary to per-slot inspection publication.

`ResolutionToken`, `AuthoringRevision`, and `FileStamp` retain their accepted
independent lifetime meanings. A terminal per-slot inspection publication must
match the active resolution token, authoring revision, slot definition,
accepted file length/SHA-256, and `CompilationFingerprint`. The sole exception
is a typed unreadable-source `Error`: it binds the selected slot/path, active
revision, route, capability, and exact compilation while retaining a null
`FileStamp`; it must never fabricate bytes or a content hash. A definition-level
artifact inspection cache may reuse content under an unchanged
`CapabilityFingerprint`, but it cannot publish terminal health until projected
against the current compilation.

Before a prerequisite permits one unique compilation, readiness may bind a
reviewed dynamic route or deterministic reviewed discovery capability, its
resolution token and capability fingerprint, the authoring revision, and a
compiler-owned discovery slot while leaving `CompilationFingerprint` absent.
The discovery capability identifies the reviewed definition and binding only;
its compiled map is not the current compilation. An unreadable prerequisite or
compilation failure publishes typed `Blocked` readiness and `CorrectSelection`,
not `Checking` or terminal inspection health. `Checking` and every terminal
inspection state require a non-null exact `CompilationFingerprint`.

Worker generation and authoring revision are different lifetimes. Generation
suppresses or cancels obsolete background work and may advance for a repeated
refresh. Authoring revision advances only when authoring inputs change. The
#182 DP pilot may keep a dedicated Presentation adapter for this distinction;
#208 must replace it with the six canonical `AuthoringSessionState` instances
and delete that adapter rather than preserve a second session owner.

### Workflow application

- **DP Replace:** Reference is inspected before dependent DP/Initial Code/LDC
  selection. Until then dependent slots are `PendingInput` with selection
  disabled. Reference resolution determines the exact map variant and
  applicability; NT51928 `0x40000` still makes LDC `NotApplicable`, while
  `0x80000` makes it selectable under the declared selection group.
- **Standard Merge:** required Initial Code/DP/TP/LDC section inputs use the
  compiled source-view contract and publish terminal typed health. Profile-
  declared non-uniform plausibility remains warning-only.
- **AB Merge:** the existing route-specific valid/warning/blocking projection
  migrates to the shared Application result without changing complete DP AB
  admission, TPA/TPB placement, topology, bytes, names, or reports.
- **NT51950/NT51951 dependent DP inspection:** TP FirmwareConfig remains the
  exact metadata prerequisite for topology-dependent DPCMI placement. The
  dependent picker is disabled only from typed readiness; Presentation contains
  no IC-specific TP-first rule.
- **CtrlRAM Replace:** the compiler-owned concrete binding length plus
  `truncate-ctrlram` normalization provides the declared-prefix behavior: a
  source shorter than the binding blocks, while accepted trailing bytes warn
  and are excluded from the immutable execution snapshot. Input health remains
  independent from POSTBUILD/runtime dependency readiness.

General Merge/Replace retain their accepted content-authoritative snapshot and
mapping contracts. They may later render through the same Presentation anatomy,
but issue #277 does not redefine their authoring semantics.

### Layer ownership and UI boundary

- Domain and Profiles own prerequisite graphs, map/selection-group semantics,
  compiled input requirements, and the unique compiled composition.
- Application owns authoring transitions, per-slot readiness/inspection
  projection, stable issues/actions, file identity, and stale-result rejection.
- Infrastructure and Bootstrap may read immutable bytes and wire ports only;
  they do not classify terminal health or retain route-specific validators.
- CLI and Presentation consume the same Application result. UI owns localized
  text, icons, colors, focus, accessibility, and file-picker interaction only.

Issue #182 consumes this result for the DP Replace shared-card pilot. Issue
#208 owns bounded per-route desktop adoption. Neither ticket may reopen the
headless state or fingerprint definitions. #208 also owns deletion of the
temporary DP pilot authoring-revision adapter after all six desktop workflows
publish through their canonical Application sessions.

## Alternatives

- Treat a selected file as immediately verified: rejected because file
  presence does not prove compiled range, size, or structural admission.
- Keep `Checking` as a generic terminal state: rejected because it hides
  whether inspection completed and cannot drive deterministic Build admission.
- Add ViewModel-specific validators: rejected because UI would become firmware
  authority and CLI could disagree.
- Keep one fingerprint for both reviewed capability and actual compilation:
  rejected because policy approval and per-run selection have different
  identities and invalidation lifetimes.
- Forbid atomic CLI requests until prerequisite files are submitted in a
  particular order: rejected because request ordering is not a firmware fact;
  only the coherent dependency result matters.

## Consequences

- The shared card can show a real green-check `Verified` state instead of a
  cosmetic or permanently checking state.
- Required-but-empty and prerequisite-pending remain visibly and behaviorally
  distinct.
- Existing AB inspection semantics become reusable rather than duplicated.
- Changing Base, TP, IC Count, map variant, selected slots, mappings, or source
  content cannot leave a stale verified surface or enable Build.
- Current specs, architecture documents, issue bodies, and reciprocal ADR
  lifecycle metadata must use the two fingerprint meanings consistently.

## Verification and evidence

- Application tests cover both state dimensions, `CanSelect`, typed next
  actions, atomic request order independence, and stale publication.
- Standard Merge, AB Merge, DP Replace, and CtrlRAM Replace tests prove each
  selected input reaches a terminal typed health result without Avalonia.
- Existing ADR 0046 fingerprint vectors remain authoritative; focused tests
  prove terminal slot health rejects a different `CompilationFingerprint`.
- Existing route golden bytes, operation traces, source ranges, processor write
  ranges, output names, and reports remain unchanged.
- Architecture tests reject Presentation validation, Bootstrap semantic
  ownership, a duplicate inspector, and workflow-specific replacement services.
- Any accepted-input, firmware-range, byte, or processor-authority difference
  stops the R2 ticket and requires firmware-owner R3 review and evidence.
