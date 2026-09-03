# NT51927 CtrlRAM Replace Handoff

The committed two-chip and three-chip bases are owner-provided. Their committed
CtrlRAM files were derived from those bases by repository tooling and normalized
to the physical Postbuild inputs: one shared NF, one shared VN, and per-position
Normal/MP files. They are replay fixtures, not separately supplied replacements.

For the quickest reference-parity checks, provide the complete official
outputs generated from the exact committed fixture hashes:

```text
2chip/expected.bin   262,144 bytes
3chip/expected.bin   262,144 bytes
```

For direct product goldens, each case instead needs its same-run `base.bin`,
actual physical replacement files under `inputs/`, and complete `expected.bin`.
The committed base is reused; do not provide another base. Exact filenames and sizes are listed in the root
[`README.md`](../../README.md).

NT51927 single is not requested in this batch.
