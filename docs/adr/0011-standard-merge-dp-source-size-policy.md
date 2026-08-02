# ADR 0011: Extract Declared DP Ranges From Nonstandard Standard Merge Inputs

- Status: Partially superseded by ADR 0045 for the workflow-specific
  `ExtractDeclaredRange` restriction; generic source-view behavior, warning
  policy, and firmware-owner evidence gates remain accepted
- Date: 2026-07-10
- Amended: 2026-07-30 by ADR 0045 for canonical source-view coverage
- Amended by: ADR 0045
- Owners: Product owner + firmware owner + architecture owner

## Context

Normal Standard Merge DP artifacts can carry unrelated trailing container bytes and therefore do not always have a stable total size. The executor previously treated the owner golden size as the immutable input address-space length, blocking otherwise usable DP artifacts. The actual byte operation copies only the profile-declared DP source range.

## Decision

Add `InputOversizePolicy.ExtractDeclaredRange` and non-blocking `ExpectedInputLengths` to `AddressSpace`.

- The declared address-space length for a normal Standard Merge DP input is the maximum byte end required by its profile `CopyRange` operation.
- The owner-approved outer-container sizes become the optional profile-declared expected input lengths. When omitted, the selected map capacity is materialized as the sole expectation for backward compatibility. A DP input that reaches the declared end is accepted; a total length outside the expected set emits `input.address-space.length-unexpected` and the executor uses only the declared prefix range.
- A declared expected-length set contains one to eight positive, strictly ascending values. Each value must be at least the greatest end-exclusive profile source view. This set is independently carried by the compiled V2 input requirement, plan address space, and compilation fingerprint.
- The original compatibility compiler permits this policy only for the fixed
  `standard-merge` `dp-input`. ADR 0045 supersedes that workflow-specific
  restriction with one generic source-view-coverage contract: Initial Code,
  DP, TP, LDC, TPA, and TPB section inputs are address-bearing and may come
  from any accepted immutable artifact that covers every selected read.
- The former `tp-maximum-256k` outer-length gate is not canonical firmware
  geometry. A TP source must cover its declared TP views; a compatible same-IC
  FlashCode may provide them. Application technical file ceilings remain
  separate resource policy.
- NT51950/NT51951 DP Perspective profiles do not use this policy. They retain `AllowedInputLengths` for `0x40000`, `0x80000`, and `0x100000` because their operation copies the full selected DP container.

## Consequences

- No output range, copy order, integrity stage, or source file is changed. The executor creates a host-owned range-sized work buffer and never mutates the supplied DP BIN.
- Preview tokens include expected input lengths, so preview approval cannot be replayed across a policy change.
- Report warnings preserve the original artifact size while making it explicit that only the declared code range participated in the composition.

## Verification

- Domain test: expected outer containers suppress the warning, a sufficient unexpected container produces declared-range output and a warning, and a too-short input fails.
- Profile test: only Standard Merge `dp-input` can enable the extraction policy; runtime sources and non-DP flows are rejected.
- Original compatibility evidence covered NT51926 DP with one trailing byte
  producing the existing golden output hash and a warning. ADR 0045 migration
  evidence now proves the same output through generic source-view coverage;
  absent an explicitly declared optional outer-length diagnostic, the
  non-authoritative tail does not create a warning.
- Existing DP Perspective tests continue to reject unapproved full-container sizes.
- Source-view migration tests accept standalone address-bearing section
  artifacts and compatible same-IC FlashCode containers, while a missing
  required view remains blocking.

## Supersession boundary

`InputOversizePolicy.ExtractDeclaredRange` remains a valid engine mechanism,
but `normal-dp-extract-with-warning` and `tp-maximum-256k` are compatibility
names scheduled for deletion by the ADR 0045 migration ticket. Whole-container
Reference and DP AB inputs remain exact declared variants and do not inherit
section-source leniency.

## Residual Gate

The owner-provided rule is implemented for normal Standard Merge. New IC profiles or any change to declared DP ranges still require IC-specific evidence, a private or tracked golden comparison, and firmware-owner review before support promotion.
