# NT51929 AB Merge Handoff

No BIN upload is currently requested for NT51929.

The canonical `nt51929-ab-t05-d06` case in
`testdata/golden/canonical/NT51929/ab-merge/t05-d06/topology-unscoped/nt51929-ab-t05-d06/provenance/case.json`
records the supplied DP_AB, TPA, TPB, and expected output with full
V2/reference parity. It remains an executable candidate; firmware-owner review
is still required before runtime exposure.

The canonical inventory applies this case directly to NT51929 and records the
owner-approved NT51919 and NT51932 fact-scoped aliases. Neither alias is a
direct product golden or a runtime support promotion.

The existing 256 KiB `initial code` / `TPFW` / `FlashCode` Combiner archive is
a Normal case, not AB evidence. Do not substitute it for the required AB
container and both TP bank inputs.

Only if a new or topology-specific case is intentionally opened, provide these
files under `inputs/`:

- `dp-ab.bin`, `tpa.bin`, `tpb.bin`, and `expected.bin`; and
- `provenance.json` listing each original filename, SHA-256, source
  archive/ticket, expected output filename, owner, and approval date.

No new CRC, range, or alias fact is inferred from a filename or from this
handoff layout.
