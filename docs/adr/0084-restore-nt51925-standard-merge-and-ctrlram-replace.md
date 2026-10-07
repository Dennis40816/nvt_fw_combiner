# ADR 0084: Restore NT51925 for Standard Merge and CtrlRAM Replace

- Status: Proposed
- Date: 2026-10-07
- Owners: Product owner + firmware owner + architecture owner
- Risk: R3 firmware/support capability restoration
- Amends: [ADR 0042](0042-retire-nt51920-nt51925-nt51930-nt51931.md), NT51925 only, if accepted
- Superseded by: None

## Context

On 2026-10-07 the owner requested NT51925 Standard Merge and CtrlRAM Replace
in NFC 1.2.2, based on v1.2.1. Core integration is targeted at NFC 1.3.0;
1.2.x becomes a correction line afterwards. This request authorizes local
preparation. This decision remains pending the owner's R3 approval and does
not declare NT51925 supported or certify firmware output.

ADR 0042 retired NT51920, NT51925, NT51930 and NT51931. The proposed amendment
is limited to NT51925 and these two workflows. NT51920, NT51930 and NT51931
remain retired. NT51925 AB Merge, General Merge, General Replace and the
retired DP Replace experience are excluded.

The repository's NT51925 evidence is the `925&926` sheet in
`docs/references/tddi-flash-header/TDDI_Flash_Header.xlsx`. It describes the
normal TP header; it does not establish the complete TP map, CtrlRAM ranges,
topologies, postbuild behavior or output parity. None of the repository's TP
flash-map versions lists NT51925. Legacy Combiner 1.13.0 has no IC-specific
mode or chip-ID check, so a successful invocation cannot prove NT51925 support.

The reason for restoration is the owner's explicit request, subject to the
official NT51925 map or mmap, version-specific postbuild scripts and independent
Golden cases still to be supplied. Equality with NT51926 is not restoration
authority and is not assumed to be an established firmware fact.

## Considered options

1. **Option A (default local preparation):** add NT51925 to the existing
   NT51926 shared family facts. Reuse canonical regions and metadata through
   NT51925-specific Standard applicability and candidate profiles. Prepare the
   two Common FW intervals and Single/Cascade CtrlRAM plans using the existing
   NT51926 geometry and processor contract. Official evidence must confirm each
   borrowed fact before promotion.
2. **Option B:** author a separate NT51925 profile/family when the official map,
   mmap or postbuild scripts differ. Record each difference in its canonical
   owner and test it independently; do not change NT51926 to accommodate it.
   A different header, range, capacity, topology, integrity sequence or naming
   contract requires this option or an explicitly reviewed fact-scoped split.

## Proposed decision

Prepare option A locally, without changing the planner/executor or candidate
execution rules. Profiles owns firmware semantics; the package trust index
owns registrations; the capability policy owns publication and evidence;
Application owns resolution and execution admission. The Standard registration
projects the existing compiler candidate artifact for declaration and metadata
without promoting its eligibility; its earlier executable-only adapter guard
cannot be used to mislabel this preparation as Supported. UI and CLI consume these
same typed results. Owner search disposition is `extend-owner` for declarations
and `reuse` for the compiler, executor and presentation projections.

NT51925 profiles remain `executable-candidate`, with evidence and human-review
promotion blockers. Exact policy routes are `Available`, `Candidate` and
`ContractOnly`. The Available policy decision does not override execution
admission. The resolved-map Standard Merge candidate can compile into the
canonical snapshot and supply inspection metadata, but exact route resolution,
authoring-session admission and Build remain unavailable. The existing runtime-reference CtrlRAM
candidate execution exception remains applicable; execution is not Golden
certification. No candidate admission exception is added by this proposal.

The prepared Standard image is 0x40000 bytes in `flash`: TP
`[0x00000,0x3C000)`, a forbidden zero-filled gap `[0x3C000,0x3E000)`, and DP
`[0x3E000,0x40000)`, copied in the existing TP-then-DP order. It uses the existing
normal FlashCode naming contract. Standard has no topology selector.

CtrlRAM prepares Common FW `[1.0.0,2.0.0)` using the 1.4.1 contract and
`[2.0.0,+infinity)` using the 2.0.0 contract, with declared Single and generic
Cascade plans. It accepts the existing 0x3C000 TP-work and 0x40000 full-Flash
capacities. The host clones the immutable reference and constrains postbuild
to the existing exact write ranges. Operation order, CRC/header fields, padding,
truncation and naming rules are retained. These intervals and topologies are
provisional NT51925 declarations, not deductions from input metadata.

NT51926 regions, operations, metadata, processor commands, output bytes and
naming behavior must remain unchanged. Bundle and capability fingerprints
change when the shared declaration identity changes; existing policy decisions,
Golden cases, expected bytes and approved difference bounds retain their scope.

## Owner evidence

Before NT51925 can be declared supported, the owner must supply and review:

- The official NT51925 TP flash map or mmap.
- The postbuild `.bat` for each Common FW version, including 1.4.1 and 2.0.0.
- The FW versions and topologies actually in use, including Cascade IC counts.
- Whether NT51925 matches NT51926 or differs, with every difference identified.
- Independent Golden cases: Standard Merge at least one single-chip case;
  CtrlRAM Replace one case for each FW version and each topology.

All applicable complete-output Golden comparisons and exact write-range audits
must pass. NT51926 Goldens, synthetic tests and the shared header workbook do
not replace NT51925 evidence. The owner's final R3 approval must name the
firmware-owner role and the exact reviewable source. Until then, promotion,
integration approval and release remain pending.

## Compatibility and evidence intake

The new owner intake instructions are
[`standard-merge/nt51925/CASE.md`](../../testdata/golden/owner-handoff/standard-merge/nt51925/CASE.md)
and [`ctrlram-replace/nt51925/CASE.md`](../../testdata/golden/owner-handoff/ctrlram-replace/nt51925/CASE.md).
Only empty input placeholders are prepared. Unreviewed BINs remain ignored;
no payload, expected hash or owner certification is invented. Same-run physical
postbuild inputs and final expected FlashCode follow the current handoff intake
contract; the historical request for `base.bin` is not revived.

## Verification and remaining gates

- Profile/contract and catalog tests must prove the two workflow boundary,
  both FW intervals, both topologies and both reference capacities.
- Support Matrix and affected UI smoke tests must show Candidate/Contract Only
  and preserve the three retired ICs' exclusion.
- Existing NT51926 tests and Golden comparisons run without changing expected
  output or declared difference bounds. Report missing fixtures or tools as
  unexecuted evidence, never as a pass.
- Independent semantic review, official owner evidence, direct NT51925 Golden
  execution, exact range review, protected integration checks and final R3
  owner approval are required before support promotion or release.
