# v0.9.16 parity 1.x amendment v1

`v0916-parity-1x-amendment-v1.json` records what the
[v0.9.16 1.x mode](predecessor-comparison-v1.md#v0916-1x-mode) of the
predecessor comparison adds to the immutable ADR 0057 plan
[`v0916-parity-certification-v1.json`](v0916-parity-certification-v1.md). It
implements [ADR 0078](../adr/0078-predecessor-comparison-for-1x-releases.md)
(Proposed). Its normative schema is `v0916-parity-1x-amendment-v1.schema.json`.

The amendment is read only by the 1.x mode; the rolling mode never reads it.
The terminal compare, attestation and finalize chain of ADR 0057 never reads
it either, and the plan, its schemas and the parity workflow contract stay
unchanged. The document states `certification: none` and `terminal: false`;
a 1.x-mode result is `consistent`, `inconsistent` or `invalid`, never `pass`.

Its rows decide which exact differing bytes and which exact baseline rejection
leave a route consistent with v0.9.16. That is firmware-owner authority: a change to the amendment is an R3 pull
request of its own, separate from changes to the predecessor comparison
contract, and needs the owner's approval of its last push naming the
`firmware-owner` role, with the byte and Golden evidence and the exact write-
range audit that role requires (ADR 0080 items 4 and 7). Board decision 64's
rule that a release declaration cites the board decision instead of a separate
approval is the approval form of release declarations; it does not replace
that exact-head firmware-owner approval.

## Plan binding

The amendment binds the plan by the parts it relies on, not by the plan's raw
bytes, which change whenever the release workflow's digest pins are
resynchronized. Those pins live only in the plan's `candidateAuthority`
member, which the 1.x mode never reads:

- the JCS SHA-256 of the whole plan without its `candidateAuthority` member,
  which covers the baseline, policy binding, canonical input authority,
  selection, counts, report bindings, input alias, the NT51951 correction and
  the transitive routes;
- for review, the JCS SHA-256 of `canonicalInputAuthority` alone (the Golden
  snapshot, the 27 unbound route ids and the CtrlRAM bindings), the
  capability-policy SHA-256 the plan binds, and the v0.9.16 tag object and
  peeled commit.

A mismatch in any of these means the amendment no longer applies and the 1.x
mode stops with `PREDECESSOR_AMENDMENT_MISMATCH` before any route runs. The
comparator batch must also show that the 1.x mode reads nothing from the
excluded member.

## Recorded evidence

The rows transcribe the payload-free 1.1.12 comparison evidence, which the
amendment binds by size and SHA-256: the local comparison
(`docs/handoff/1.1.12/parity/v0916-local-comparison.json`) and its
explanations (`docs/handoff/1.1.12/parity/explanations.json`). That evidence
is a historical, non-certifying observation: its v0.9.16 side was the 1.1.12
probe build and its candidate was an intermediate source, not the exact
`v1.1.12` release. The values count only once the 1.x mode's own milestone
run, with the approved v0.9.16 executor, reproduces them; a different value is
a failure that needs a new owner decision, never a silent update.

## Approved semantic corrections

`approvedSemanticCorrections` extends the plan's single NT51951 row. Each row
names one exact plan route and capability fingerprint and binds both output
identities, the exact differing byte count and the complete list of half-open
differing ranges. The 1.x mode checks a row with the ADR 0057 rule
(`compare_approved_semantic_correction_payloads`): equal sizes, both hashes,
the count and the complete range list, so every byte outside the ranges must
still equal v0.9.16. A row covers exactly its values (`scope:
exactly-these-values`); it is never widened into a pattern.

The one row today is the NT51950 FW1.x cascade TP-work route
(`route-7-nt51950-15-ctrlram-replace-4-2-ic-36-nt51950-ctrlram-fw1x-cascade-tp-work`),
approved by 1.1.12 board decision 12, which approves the NT51951 and NT51950
2-IC cascade Diff NF preservation differences. v0.9.16 writes the whole Diff
CtrlRAM record `[0x33200, 0x34600)`; 1.x writes `[0x33200, 0x33B10)` and keeps
the base bytes of the Diff NF tail (0.10.1, #188). Both outputs are 225,280
bytes (v0.9.16 `cfae1591…`, 1.x `a239645d…`), and the 2,816 differing bytes
lie in `[0xA11C, 0xA120)`, `[0xA130, 0xA134)`, `[0x2D428, 0x2D42C)`,
`[0x2D43C, 0x2D440)` and `[0x33B10, 0x34600)`, the same ranges and count as
the plan's NT51951 row.

Attribution, range by range:

- **The Diff NF tail** `[0x33B10, 0x34600)` is `stopped-write-preserved-bytes`,
  supported by cited evidence: the alias case's fact scope declares the
  writable Diff CtrlRAM prefix `[0x0000, 0x0910)` and the reference-preserved
  Diff NF tail `[0x0910, 0x1400)` of the record at `0x33200`, and the 1.x
  profile's `diff-ctrlram` region copies only the active prefix and preserves
  the tail.
- **The four CRC words** are `derived-field`, with the cause **not
  independently verified**. The existing evidence shows what they are and who
  writes them: the NT51951 source case's `allowedByteDifferenceContract`
  classifies the same four offsets as header, header-copy, copied-header and
  copied-header-copy CRC words; the 1.x profile makes the regions
  `header-crc-a11c`, `header-crc-a130` and `header-copy` writable only by the
  declared Legacy Combiner postbuild processor, which recalculates them; and
  v0.9.16 and 1.x register the same Legacy Combiner 1.13.0. No evidence in the
  repository shows independently that these words change because the Diff NF
  tail they cover differs. Decision 12 accepts this exact difference, so the
  row accepts exactly these 16 bytes as part of it and nothing wider.

The row also carries the case manifests of its canonical binding and its
observation, which names the recorded evidence, the probe build of v0.9.16
and the 1.x sources observed (`1c37bd718` and `badc545b0`). The 1.x output
`a239645d…` is also the output that the Bootstrap test
`CtrlRamDirectTpGoldenExecutionTests` pins for the NT51951 twin route on the
same inputs.

## Canonical bindings not applicable to v0.9.16

`baselineNotApplicable` records a canonical binding that v0.9.16 cannot run.
The one row today is the NT51950 FW1.x cascade full-flash route
(`route-7-nt51950-15-ctrlram-replace-4-2-ic-39-nt51950-ctrlram-fw1x-cascade-full-flash`),
decided by 1.1.12 board decision 62: the plan builds its base with
`nt51950-standard-merge-512k` from the NT51951 AUTO_PRJ-599 inputs, giving the
524,288-byte precursor `ff9ad012…` on both sides, which is also the input
case's recorded base; v0.9.16 then rejects it at the CtrlRAM Replace Preview
with `profile.v2.compile.map-selection-invalid`, because the route's map
declares 262,144 bytes. The row binds that binding, the precursor identity,
the baseline's rejecting stage and issue code, and the candidate precursor
and output identities.

The candidate output `1536d344…` is a historical observation, not formal
evidence: the 1.1.12 local comparison recorded it for the intermediate source
`badc545b0`, where the candidate admits the base as a Display-OSD envelope and
writes the registered-Combiner output of the NT51951 2-IC route (the plan's
NT51951 row names the same identity). The 1.1.13 milestone run must reproduce
it on the exact candidate, and the firmware owner confirms the value, or a
different one, at that run. The row is counted apart from equal and
different, says nothing about other NT51950 inputs or bindings, and is
revisited at the 2.0.0 terminal rebinding. The rolling mode compares the same
scenario normally.

## Baseline executor (reserved)

The v1 baseline executor contract's locked restore fails with `NU1004`. Board
decision 63 approves a second executor contract that re-resolves the missing
Windows runtime section, pins the complete lock-file diff and runtime closure
with the cause of every difference, and blocks formal runs while any
difference is unexplained; board decision 79 pins its compiler host to
runtime 10.0.11. The P-0.5 spike has produced that evidence. The contract itself is admitted by its own R3 pull request with the owner's
exact-head firmware-owner approval; until then
`baselineExecutor.status` is `pending-executor-record`, `contract` is `null`,
and no formal 1.x-mode run is possible. The v1 executor contract stays with
the terminal plan.

## Lifetime

When the owners decide RO-1 for 2.0.0, the amendment is folded into the
rebound 2.0.0 plan (option A) or deleted together with the terminal chain
(option B).
