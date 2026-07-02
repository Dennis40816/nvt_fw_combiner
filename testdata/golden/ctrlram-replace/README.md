# CtrlRAM Replace Fixture Handoff

This directory is reserved for owner-provided CtrlRAM Replace evidence.

Do not commit private firmware BIN files by default. Put local/private payloads under `private/`, update `private/manifest.json` from `manifest.template.json`, then run:

```text
python scripts/verify_ctrlram_replace_fixture.py --require-private
```

Current behavior:

- The verifier always can run public CtrlRAM Preview/Build smoke using self-replacement inputs sliced from existing approved Standard Merge golden data.
- When `private/manifest.json` exists, the verifier checks manifest metadata, sizes, and SHA-256 for the base firmware, replacement CtrlRAM BINs, and expected output if provided.
- Full private byte comparison still needs owner-supplied expected outputs and firmware-owner sign-off before parity can be claimed.

Required owner data per case:

- IC and IC number mode, for example `NT51927` / `3`.
- Base flash firmware hash and size.
- Each replacement CtrlRAM slot id, display name, BIN hash, and size.
- Expected final output hash and size for the staged Combiner postbuild result.
- Source classification, provenance, and owner approval note.
