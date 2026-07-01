# Reference Code

`refcode/` contains evidence only. Production projects must never import, compile, package, or dynamically load files from this directory.

## Included Python references

1. `gen_flash_bin_v2/` — source-only snapshot of the standard DP/TP/LD merge Python implementation from `Dennis40816/NFCG/testdata/reference/FlashCodeGenerator/gen_flash_bin_v2`.
2. `ab_code_combiner/` — source-only snapshot of the user-provided current AB merge implementation, including NT51950/NT51951 CRC behavior.
3. `flashmap/` — owner-provided IC FlashMap workbook, postbuild scripts, mmap headers, HSI combiner guide, and an evidence copy of Combiner 1.13.0 used to derive CtrlRAM Replace postbuild profiles.

The NFCG TypeScript application code is deliberately **not** copied into `refcode/`. It remains a concept and behavior reference through repository paths and fixed source identifiers in `REFERENCE_MANIFEST.json`.

Firmware inputs, expected outputs, generated files, caches, and credentials are excluded.

The runtime Combiner executable is packaged under `external-tools/legacy-combiner/`; production code must not load executables from `refcode/`.

## Rules

- Treat legacy ranges as source facts, not as the new profile format. Normalize them to half-open `[start, end)` ranges during migration.
- Cite the relevant reference manifest entry in any PR that ports behavior.
- Add a byte-level regression test before declaring migrated behavior complete.
- Keep reference refreshes in isolated PRs; never mix snapshot refresh with product-semantic changes.
- Do not modify reference source to make production tests pass. Record discrepancies and resolve them in production code or an approved ADR.
