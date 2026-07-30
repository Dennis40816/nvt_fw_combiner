# ADR 0045: Canonical source projections and FlashCode admission

- Status: Accepted
- Date: 2026-07-30
- Accepted: 2026-07-30 by the product, architecture, and firmware owner
- Owners: Product owner + architecture owner + firmware owner
- Risk: R2 architecture contract; migration of supported firmware profiles is R3
- Builds on: ADR 0011, ADR 0015, ADR 0020, ADR 0032, ADR 0043
- Amends: ADR 0011, ADR 0015, ADR 0032, ADR 0043

## Context

The canonical operation model already copies one checked source view to one
checked target view. Older input contracts nevertheless encode workflow names
such as `normal-dp-extract-with-warning` and `tp-maximum-256k`, and some
section inputs use whole-map length rules even though execution reads only one
address-bearing section. Those policies can reject a same-IC FlashCode that
contains a valid TP, Initial Code, or LDC source window.

The opposite case must remain strict. A Replace Reference and a DP AB seed are
complete containers: bytes outside the subsequently changed windows remain part
of the output. Treating those inputs as arbitrary source windows would make
uncovered output bytes unauthoritative.

No single current byte marker proves that an artifact is a complete FlashCode.
The `00 4E 56 54` marker locates a FirmwareConfig structure, ASCII `519xx` is an
IC hint, and version/complement, PID, CMI, and non-uniform checks validate only
their declared metadata or region.

## Decision

### One source-view-to-target-view model

Every built-in byte transfer remains one normal operation over an explicit
source view and target view:

```text
sourceAddressSpace + sourceRange
    -> targetAddressSpace + targetRange
```

The compiler does not add an IC/workflow-specific mapping discriminator. The
resolved ranges describe these relationships:

- **address-aligned projection**: source and target use the same firmware
  coordinates;
- **bank-relocated projection**: TPB reads the TP-native source window and
  writes it at the resolved B-bank placement delta; relocation of stored Header
  addresses remains a separate checked scalar transform;
- **payload-relative projection**: a compact CtrlRAM payload begins at source
  byte `0` and is mapped to a profile-declared target CtrlRAM range;
- **whole-container initialization/admission**: a complete source is cloned or
  copied as the authoritative container. This is not a fourth mapping kind.

These names explain resolved geometry in specifications and reports. The
operation algebra remains `copy-range`/`replace-range` with checked views.

### Built-in section sources are address-bearing

Initial Code, DP, TP, LDC, TPA, and TPB inputs use profile-declared firmware
coordinates. A section slot may therefore consume either:

- a standalone address-bearing artifact whose length covers every selected
  source view; or
- a compatible same-IC complete FlashCode whose accepted snapshot covers those
  same views.

The required readable end is the greatest end-exclusive byte used by the
selected source views, metadata structures, validations, and processor reads.
A shorter artifact is blocking. Bytes outside that bounded execution snapshot
do not become operation or processor authority. The original file length and
SHA-256 remain report evidence.

Outer length is a profile-declared diagnostic expectation for a section source,
not a universal exact-size gate. A technical file-size ceiling remains an
Application resource limit and is not firmware geometry. Unexpected extra
bytes may be ignored only when every selected read is inside the declared
snapshot; the report identifies the unused tail.

Built-in compact CtrlRAM replacement artifacts are the only current
firmware-profile inputs whose normal source coordinate begins at byte `0` and
maps to a nonzero firmware target. Padding/truncation remains an explicit
profile policy. Dynamic DiffDLM record masking is a CtrlRAM-specific compiled
scatter over the same payload-relative source and does not create another
general mapping model.

### Complete-container admission remains exact

An input is complete-container authoritative when output semantics require
bytes outside a selected section:

- Replace clones one immutable Reference;
- DP AB/AB seed operations consume the complete declared container;
- another profile may opt in only when its complete-container authority and
  accepted capacities are explicit.

Such an input must match one declared container variant. ROI/source-view
coverage cannot substitute for complete-container admission. NT51928 accepts
the owner-declared `0x40000` without-LDC and `0x80000` with-LDC Reference
variants. NT51950/NT51951 AB uses its topology-resolved complete DP AB
container. No extra tail is silently treated as part of either container.

### FlashCode is a resolved composition, not one magic signature

FlashCode means a complete container whose resolved variant contains the
required DP/Initial Code and TP parts. LDC is present only when that variant
declares it; LDC does not define FlashCode identity.

Classification occurs only after the requested IC/capability is known. A
profile may establish a FlashCode candidate from:

1. one declared complete-container capacity;
2. complete coverage of that variant's required DP and TP views;
3. profile-declared structural and content validations for those views; and
4. optional metadata consistency such as the unique FirmwareConfig NVT marker,
   version/complement relation, or IC hint.

No individual signal above is sufficient. Filename, PID, version, hash, CMI,
ASCII `519xx`, or the NVT marker cannot choose an IC, family, route, map,
support, or publication state. When declarations cannot distinguish a
complete FlashCode from another address-bearing artifact, classification is
`Unknown`; a section projection may still be admitted if its own slot contract
is satisfied.

A required source view that is absent or out of bounds is blocking. A
profile-declared non-uniform plausibility check remains warning-only unless the
profile has separate authority to identify a definitively wrong artifact kind.
Classification and operation admission stay separate typed results.

### General authoring remains explicit

General Merge and General Replace are not built-in firmware inference. Their
file mapping rows may explicitly author either a Source Slice or From File
Start preset. Both compile to the same explicit source/target ranges and remain
subject to canonical content snapshots, occupancy, protected-range, and
resource admission.

## Consequences

- Merge can reuse a compatible same-IC FlashCode as an Initial Code, DP, TP, or
  LDC source without pretending the complete file is a standalone section.
- TPA is address-aligned. TPB is the same TP-native source window plus one
  resolved bank placement delta; it is not head-to-target.
- Replace Reference and complete DP AB inputs remain fail-closed and cannot be
  weakened by an ROI-only check.
- `tp-maximum-256k` and `normal-dp-extract-with-warning` are migration
  vocabulary, not permanent canonical primitives. New profiles use one generic
  source-view-coverage contract with optional expected outer lengths.
- A section-specific exact-map-capacity rule is migrated to source-view
  coverage. `exact-resolved-map-capacity` remains valid for complete-container
  authority.
- UI and CLI display the same Application-owned coverage, plausibility,
  classification, and blocking reasons.

## Migration and verification

1. Introduce one generic compiled source-view-coverage requirement derived from
   the selected views and declared validation/metadata reads.
2. Migrate Standard Merge DP/TP and NT51928 Initial Code/LDC section slots from
   workflow-named or whole-map length rules.
3. Keep Replace References and full DP AB seeds on exact declared container
   variants.
4. Prove TPA same-address and TPB bank-relocated mappings from existing direct
   AB traces; prove compact CtrlRAM source byte `0` maps only to declared
   CtrlRAM targets.
5. Add standalone-section and same-IC-FlashCode inputs for DP, TP, Initial
   Code, and LDC; short-window, suspicious-content, unknown-classification,
   extra-tail, wrong-variant, and exact-container negative tests.
6. Preserve golden bytes, operation traces, immutable input hashes, mutation
   ranges, output names, and POSTBUILD authority.
7. Delete the old workflow-named length-rule branches only after all profiles
   and callers use the generic contract with zero-caller architecture evidence.

The migration requires scoped Polytail, independent architecture review, and
firmware-owner/golden review for every supported profile whose accepted input
set or complete-container behavior changes.
