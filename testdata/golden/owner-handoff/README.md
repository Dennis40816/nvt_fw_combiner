# Owner Golden Handoff

Use this area to drop private validation payloads for local verification.

Rules:

- Firmware payloads dropped here are ignored by Git by default.
- Keep the tracked `CASE.md` files as instructions only.
- Prefer the requested file names in each case folder so scripts and UI smoke runs can be wired directly.
- If a payload should become a committed golden fixture, it needs owner approval, manifest hashes, and a separate reviewed change.

## Automated intake

For a new IC or mode, declare every owner-provided artifact, hash, and
candidate fact in an intake request, then run:

```text
python scripts/intake_ic_reference.py --request request.json --source-root <owner-drop-folder> --output-dir <new-empty-staging-path>
```

The command validates and snapshots only request-declared files, then writes:

- `evidence-manifest.json`
- `intake-report.json`
- `NEXT_STEPS.md`

The retired `--source` folder scan and its filename-based classification are
rejected. Candidate output is caller-selected and remains outside this
repository; the command does not make C# changes or a support claim.

## AB Code evidence

AB Code is evidence-gated separately from the existing intake command. The
current command must not infer an AB profile, memory map, Combiner binding, or
header rule from a drop folder. Use `ab-merge/<ic>/` to retain the exact
owner-provided payloads and provenance for the five `v0.9.3` targets. The
tracked `CASE.md` files state the required evidence; payloads remain ignored.

Prepare these files when available:

- flash-map workbook/export;
- flash header reference;
- `mmap.h` or equivalent memory-map header;
- postbuild BAT/CMD/script/log for TP/CtrlRAM/CRC/header processing;
- combiner source/reference code or exact tool identity;
- golden input/output BINs or private fixture hashes;
- notes with source archive, IC-number branch, expected output filename, owner, and approval status.

Current `gen_flash_bin_v2` reference coverage:

- Covered by `refcode/gen_flash_bin_v2/ic_config.json` and already mirrored by `testdata/golden/standard-merge-gen-flash`: `51920`, `51923`, `51926`, `51927`, `51928`, `51929`, `51931`, `51932`.
- Not present in the current `gen_flash_bin_v2` config: `51917`, `51919`, `51930`, `51950`, `51951`.
- Owner-confirmed Standard Merge aliases are executable without new payloads: `51917` follows `51927`, and `51919` follows `51929`. Direct files for those ICs are optional audit samples.

High-priority folders:

- `standard-merge/nt51950/`
- `standard-merge/nt51951/`
- `dp-replace/nt51950/dp-0x40000/`
- `dp-replace/nt51950/dp-0x80000/`
- `dp-replace/nt51950/dp-0x100000/`
- `dp-replace/nt51951/dp-0x40000/`
- `dp-replace/nt51951/dp-0x80000/`
- `dp-replace/nt51951/dp-0x100000/`
- `ctrlram-replace/nt51927/`
- `ctrlram-replace/nt51950/`
- `ctrlram-replace/nt51951/`
- `ab-merge/nt51919/`
- `ab-merge/nt51929/`
- `ab-merge/nt51932/`
- `ab-merge/nt51950/`
- `ab-merge/nt51951/`
