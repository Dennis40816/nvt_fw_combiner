# Reference Code Instructions

These rules apply to `refcode/`.

- This directory is evidence, not production code. Do not import, compile, package, execute in release paths, or silently copy implementation from it.
- The only permitted code snapshots are `gen_flash_bin_v2/` and `ab_code_combiner/`.
- Do not add NFCG TypeScript/JavaScript source, Node packages, submodules, firmware BIN files, expected outputs, caches, virtual environments, or executables.
- Do not edit a snapshot during a product change. Reference refreshes use a dedicated PR with source provenance, reviewed hash updates, and a behavior-difference report.
- Every included source file must appear in its `SOURCE_MANIFEST.json` with SHA-256; moving behavior into production requires an explicit source citation and regression test.
- Legacy inclusive ranges must be normalized to half-open ranges in production profiles and documented in the porting PR.
