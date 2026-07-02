# Owner Golden Handoff

Use this area to drop private validation payloads for local verification.

Rules:

- Firmware payloads dropped here are ignored by Git by default.
- Keep the tracked `CASE.md` files as instructions only.
- Prefer the requested file names in each case folder so scripts and UI smoke runs can be wired directly.
- If a payload should become a committed golden fixture, it needs owner approval, manifest hashes, and a separate reviewed change.

Current `gen_flash_bin_v2` reference coverage:

- Covered by `refcode/gen_flash_bin_v2/ic_config.json` and already mirrored by `testdata/golden/standard-merge-gen-flash`: `51920`, `51923`, `51926`, `51927`, `51928`, `51929`, `51931`, `51932`.
- Not present in the current `gen_flash_bin_v2` config: `51917`, `51919`, `51930`, `51950`, `51951`.

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
