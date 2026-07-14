# Golden regression tests

This project contains golden regression tests for standard merge behavior derived from approved reference configuration facts and owner-approved fixture BINs.

## Standard merge golden source

Normal/Standard Merge golden expectations may be derived from the approved flash-bin merge reference in `refcode/gen_flash_bin_v2`. That reference is source-only evidence for DP/TP/LD merge behavior and configuration facts.

Golden payload handling is constrained:

- do not commit real firmware BINs, generated flash images, or private golden outputs outside approved `testdata/golden/` fixture directories;
- store manifests with source provenance, file sizes, SHA-256 hashes, run reports, and owner-approved metadata in Git;
- normalize legacy inclusive ranges from the flash-bin reference into half-open profile ranges before adding production profiles;
- require owner sign-off before marking any IC/mode as supported.

The current `testdata/golden/standard-merge-gen-flash` cases verify complete output bytes plus SHA-256 for ICs declared by `gen_flash_bin_v2/test/test_ic_config.json`. They are firmware parity regression evidence for standard merge copy ranges, copy order, fill byte, and source artifact sizes.

`testdata/golden/standard-merge-reference/nt51950` is a separately classified owner-approved reference fixture. Its complete output is checked through the canonical V2 workbench path, but its third-party script does not define or widen the canonical NT51950 map.

DP Replace golden self-replacement uses the same owner-approved Standard Merge output as the base image and the corresponding golden DP input as the replacement. The run must reproduce the base bytes, preserve the output SHA-256, and leave the Replace report `OutputDifferences` table empty. CtrlRAM Replace golden-backed self-replacement is covered by UI smoke because it exercises the workbench postbuild path: the report may contain `PostbuildCrcHeader` rows only when they are accepted, and a postbuild-clean self-replacement must return to an empty difference table.
