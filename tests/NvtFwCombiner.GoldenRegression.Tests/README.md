# Golden regression tests

This test surface is created when its owning production capability is introduced. The bootstrap commit intentionally contains no skipped, constant-only, or fake tests.

## Standard merge golden source

Normal/Standard Merge golden expectations may be derived from the approved flash-bin merge reference in `refcode/gen_flash_bin_v2`. That reference is source-only evidence for DP/TP/LD merge behavior and configuration facts.

Golden payload handling remains private:

- do not commit real firmware BINs, generated flash images, or private golden outputs;
- store only hash manifests, run reports, and owner-approved metadata in Git;
- normalize legacy inclusive ranges from the flash-bin reference into half-open profile ranges before adding production profiles;
- require owner sign-off before marking any IC/mode as supported.
