# ADR 0037: Deliver an optional perfect-family A FlashCode with one AB build

- Status: Accepted for `v0.9.15` on 2026-07-24
- Owners: Product owner, architecture owner, firmware owner
- Risk: R3
- Depends on: [ADR 0035](0035-ab-topology-operator-selection.md) and [ADR 0036](0036-output-destination-and-ab-naming-v2.md)
- Public delivery amended by: [ADR 0050](0050-atomic-output-bundle-delivery.md) on 2026-08-20

## Context

Some NT51919/NT51929/NT51932 customers require the A Code as a separate
FlashCode in addition to a completed AB Code output.  The perfect-family AB
map already declares an A-bank sequence from output offset zero through the
end of `tpa-code`; for the current NT51929 map this resolves to
`[0x00000, 0x40000)`.

Re-running Standard Merge would create a second composition path and could
diverge from the AB artifact that was just delivered.  It would also wrongly
invite Standard Merge-specific validation or postbuild semantics into an
already completed AB result.

## Decision

Before an AB Build writes any output, the desktop asks whether to include the
optional A FlashCode when the compiled output map declares these four regions,
in this exact contiguous order from offset zero:

1. `dp-a-before-cmi`
2. `a-cmi-dp-version`
3. `dp-a-after-cmi`
4. `tpa-code`

The derived byte range is `[0, tpa-code.endExclusive)`.  It is calculated from
the compiled map, not from a UI label, filename, PID, version, whole-file hash,
or numeric constant in delivery code.  The initial eligibility therefore covers
the existing NT51919/NT51929/NT51932 perfect-family maps.  NT51950 and NT51951
do not satisfy this region contract and show no A-only option; their distinct
layouts remain outside this feature.

For the public GUI and CLI delivery boundary amended by ADR 0050, requesting
the optional A FlashCode requires one atomic bundle delivery.  The canonical AB
output, the A-only artifact, and the accepted immutable sources are staged and
promoted as one directory transaction.  There is no independent A-only Save
destination and no loose A-only output.  Cancelling or failing the transaction
publishes no visible partial bundle.  The A artifact remains a direct immutable
slice of the one completed AB output.  It does not invoke Standard Merge,
re-read or modify any selected input, alter the primary AB output, calculate
CRC, execute postbuild, or infer a route from a golden artifact.

The automatic suggested A FlashCode name is:

```text
NT{ic}_FlashCode_{dp-a}{tp-a}_{date}.bin
```

`ic`, `dp-a`, `tp-a`, and the UTC `date` are the already recorded typed tokens
from the authoritative AB execution.  Display formatting such as `D06-05` is
not used in the filename.  Under ADR 0050 this is the A artifact name inside
the allocated bundle, not an independently selected destination.  Bundle
admission still rejects aliases with selected AB inputs and derives all source
identities from the accepted session.

The primary AB report and its effective override name remain unchanged under
ADR 0036.  A successful atomic-bundle receipt records the A artifact in
`DeliveryArtifacts`, including its exact filename, size, SHA-256, source range,
and committed status.  It is not a second composition run and cannot alter the
primary report's `Output` identity.  A failed bundle commit may return a typed
delivery diagnostic, but it commits neither the canonical output nor the A
artifact and cannot show the all-success completion page.

The pre-ADR-0050 direct-secondary Application seam remains characterized only
for compatibility and fail-closed diagnostics.  It is not an ordinary GUI or
CLI delivery path and does not authorize a new caller, independent destination,
or partial public delivery.  Removing that seam is a separate compatibility
change rather than part of this documentation correction.

## Consequences

- The A-only file is byte-identical to the declared prefix of the one AB image
  produced by the Build.
- The feature remains unavailable until a compiled profile explicitly exposes
  the required region contract, preventing accidental extension to a distinct
  DP layout.
- A future A-only behavior for NT51950 or NT51951 needs its own approved map,
  delivery contract, and firmware-owner review; it cannot reuse this rule.

## Verification

- Test that NT51919/NT51929/NT51932 compiled maps derive the A-only candidate
  and the current NT51929 run resolves the expected 256 KiB range.
- Test that the exported bytes exactly equal the successful AB output prefix.
- Test the typed FlashCode name, selected delivery filename/report provenance,
  and the absence of a candidate for NT51950/NT51951.
- Test that GUI and CLI A-only delivery require atomic bundle intent and cannot
  create a loose secondary artifact.
- Test cancellation, destination aliasing, and injected I/O failure before
  promotion, with no visible partial bundle and all selected sources preserved.
- Keep the direct-secondary compatibility characterization fail-closed and
  prove that its incomplete result cannot show a successful completion.
