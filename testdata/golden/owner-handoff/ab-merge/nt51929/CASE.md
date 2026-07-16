# NT51929 AB Merge Handoff

No BIN upload is currently requested for NT51929.

The tracked `nt51929-ab-t05-d06` fixture in
`testdata/golden/ab-merge/manifest.json` now records the supplied DP_AB, TPA,
TPB, and expected output with full V2/reference parity. It remains an
executable candidate: firmware-owner review and any required member-specific
alias decision are still needed before runtime exposure.

The manifest applies this case directly to NT51929 and names only NT51919 as a
fact-scoped alias. NT51932 is explicitly not established by this product
golden.

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
