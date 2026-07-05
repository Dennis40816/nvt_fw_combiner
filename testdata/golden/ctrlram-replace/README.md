# CtrlRAM Replace Fixture Handoff

This directory is reserved for owner-provided CtrlRAM Replace evidence.

Owner-approved committed fixtures live under `fixtures/` and are listed by `manifest.json`.
Do not commit new private firmware BIN files by default. Put unapproved local/private payloads under `private/`, update `private/manifest.json` from `manifest.template.json`, and only promote them to `fixtures/` after explicit owner approval.

Run:

```text
python scripts/verify_ctrlram_replace_fixture.py --require-fixture
```

Current behavior:

- The verifier always can run public CtrlRAM Preview/Build smoke using self-replacement inputs sliced from existing approved Standard Merge golden data.
- When `manifest.json` exists, the verifier checks manifest metadata, sizes, and SHA-256 for the base firmware, replacement CtrlRAM BINs, and expected output if provided.
- Full byte comparison still needs owner-supplied expected outputs and firmware-owner sign-off before parity can be claimed.

Required owner data per case:

- IC and IC number mode, for example `NT51927` / `3`.
- Common FW version decoded from the base image and the postbuild category used for the run.
- Base flash firmware hash and size.
- Each replacement CtrlRAM slot id, display name, BIN hash, and size.
- Expected final output hash and size for the staged Combiner postbuild result.
- Source classification, provenance, and owner approval note.
