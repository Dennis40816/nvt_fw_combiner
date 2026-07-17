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
- When schema `0.2` `manifest.json` exists, the verifier checks manifest metadata, sizes, SHA-256, and base-image FWConfig Common FW version for the base firmware, replacement CtrlRAM BINs, and expected output if provided.
- The owner-authorized NT51926 TP-base self-test case archives the same-byte TP golden input and real-workflow output, plus exact half-open integrity ranges and the two-command Combiner 1.13.0 trace. Git deduplicates the copied TP input blob.
- Full-byte comparison and firmware-owner sign-off remain pending for the other cases before broader parity can be claimed.
- The TP-base case establishes only NT51926 Common FW 1.4.1 cascade admission/execution; it does not establish all-IC or V2 parity, and the other pending golden gates remain unchanged.

Required owner data per case:

- IC and IC number mode, for example `NT51927` / `3`.
- Common FW version decoded from the base image and the postbuild category used for the run.
- Base firmware BIN hash and size.
- Each replacement CtrlRAM slot id, display name, BIN hash, and size.
- Expected final output hash and size for the staged Combiner postbuild result.
- Source classification, provenance, and owner approval note.
