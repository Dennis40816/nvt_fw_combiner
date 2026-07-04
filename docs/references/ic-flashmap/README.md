# IC FlashMap Reference Evidence

This directory contains owner-approved text evidence extracted from `IC FlashMap.7z`.

Included:

- `postbuild/` legacy postbuild BAT files used to verify Combiner command order and arguments.
- `mmap/` legacy memory-map headers used to verify address ranges and header locations.
- `SOURCE_MANIFEST.json` source archive provenance plus per-file SHA-256 hashes.

Excluded:

- `IC_FlashMap.xlsx`, because profile facts should be ported into reviewed profile/catalog code.
- `combiner_1.13.0/Combiner.exe`, because approved runtime binaries are managed under `external-tools/`.
- Generated firmware outputs and private BIN inputs.

These files are evidence only. Production code must not execute or package anything from this directory.
