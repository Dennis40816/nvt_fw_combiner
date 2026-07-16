# NT51927 CtrlRAM Replace Handoff

The committed two-chip and three-chip bases are owner-provided. Their committed
per-chip CtrlRAM files were sliced from those bases by repository tooling; they
are derived replay fixtures, not separately supplied replacement files.

For the quickest reference-parity checks, provide the complete official
outputs generated from the exact committed fixture hashes:

```text
2chip/expected.bin   262,144 bytes
3chip/expected.bin   262,144 bytes
```

For direct product goldens, each case instead needs its same-run `base.bin`,
actual per-chip replacement files under `inputs/`, and complete
`expected.bin`. Exact canonical filenames and sizes are listed in the root
[`TODO.md`](../../TODO.md).

NT51927 single is not requested in this batch.
