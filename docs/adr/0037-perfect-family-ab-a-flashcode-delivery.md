# ADR 0037: Deliver an optional perfect-family A FlashCode with one AB build

- Status: Accepted for `v0.9.15` on 2026-07-24
- Owners: Product owner, architecture owner, firmware owner
- Risk: R3
- Depends on: [ADR 0035](0035-ab-topology-operator-selection.md) and [ADR 0036](0036-output-destination-and-ab-naming-v2.md)
- Bundle-mode public delivery additionally governed by: [ADR 0050](0050-atomic-output-bundle-delivery.md) on 2026-08-20

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

The desktop keeps the A-only and bundle choices independent.  With bundle mode
off, choosing the optional A FlashCode opens two native Save dialogs in
sequence: first the primary AB FlashCode name, then the A FlashCode name.
Cancelling either dialog commits no output.  Omitting A-only opens only the
primary AB Save dialog.

With bundle mode on, ADR 0050 applies: the canonical AB output, an enabled
A-only artifact, and the accepted immutable sources are staged and promoted as
one directory transaction.  Bundle mode has no independent A-only Save
destination, and cancellation or failure publishes no visible partial bundle.
The CLI exposes A-only delivery only through this bundle mode;
`--include-a-flashcode` requires `--bundle-parent`.

In both delivery modes, the A artifact is a direct immutable slice of the one
completed AB output.  It does not invoke Standard Merge, re-read or modify any
selected input, alter the primary AB output, calculate CRC, execute postbuild,
or infer a route from a golden artifact.

The automatic suggested A FlashCode name is:

```text
NT{ic}_FlashCode_{dp-a}{tp-a}_{date}.bin
```

`ic`, `dp-a`, `tp-a`, and the UTC `date` are the already recorded typed tokens
from the authoritative AB execution.  Display formatting such as `D06-05` is
not used in the filename.  In loose desktop delivery, the operator-selected
destination is the effective secondary-output identity.  It may atomically
replace an unrelated file, but it must not alias a selected AB input or the
primary AB output.  In bundle mode, the same typed name identifies the artifact
inside the allocated bundle rather than an independent destination.

The primary AB report and its effective override name remain unchanged under
ADR 0036.  The report records the A artifact in `DeliveryArtifacts`, including
its exact filename, size, SHA-256, source range, and commit status.  It is not a
second composition run and cannot alter the primary report's `Output` identity.

For loose desktop delivery, both destinations are checked against all selected
inputs and against each other before the primary output is committed.  If an
unexpected secondary I/O failure occurs after primary commit, the report
records the primary output plus an uncommitted A delivery artifact and an
error; the UI opens that report rather than showing the all-success completion
page.  For bundle delivery, one successful atomic receipt records every
committed artifact; a failed promotion publishes no visible partial bundle and
cannot show the all-success completion page.

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
- Test loose desktop destination alias rejection before primary commit, plus a
  post-primary A-only I/O failure that remains visible as partial delivery and
  cannot show a successful completion.
- Test that CLI A-only delivery requires bundle intent and cannot create a
  loose secondary artifact.
- Test bundle cancellation and injected I/O failure before promotion, with no
  visible partial folder and all selected sources preserved.
