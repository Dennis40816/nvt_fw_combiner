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
