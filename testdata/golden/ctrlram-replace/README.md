# CtrlRAM Replace Fixture Handoff

This directory is reserved for owner-provided CtrlRAM Replace evidence.

Owner-approved committed fixtures live under `fixtures/`. The original fixture
set is listed by `manifest.json`; the owner-approved 2026-07-17 intake is listed
separately by `manifest.20260717.json`. The repository verifier checks both
inventories, sizes, and SHA-256 values.

Do not commit new private firmware BIN files by default. Put unapproved
local/private payloads under `private/`, update `private/manifest.json` from
`manifest.template.json`, and only promote them to `fixtures/` after explicit
owner approval.

Run:

```text
python scripts/verify_ctrlram_replace_fixture.py --require-fixture
```

Current behavior:

- The verifier always can run public CtrlRAM Preview/Build smoke using self-replacement inputs sliced from existing approved Standard Merge golden data.
- When schema `0.2` `manifest.json` exists, the verifier checks manifest metadata, sizes, SHA-256, and base-image FWConfig Common FW version for the base firmware, replacement CtrlRAM BINs, and expected output if provided.
- Full byte comparison still needs owner-supplied expected outputs and firmware-owner sign-off before parity can be claimed.
- The 2026-07-17 snapshot contains owner-approved public golden payloads and
  preserves technical project and `AUTO_PRJ` filenames. It is evidence intake,
  not a runtime-support promotion.
- The official owner system may provide only the final expected firmware and physical CtrlRAM inputs. Do not ask the owner to fabricate or rename that final output as `base.bin`. Expected-only intake may use the documented expected-derived sentinel audit, but it is range/processor evidence rather than independent base-backed parity and cannot promote support by itself.
- The owner-authorized NT51926 TP-base self-test case lives under `fixtures/derived/20260717`; it archives the same-byte TP golden input and real-workflow output, plus exact half-open integrity ranges and the two-command Combiner 1.13.0 trace. Git deduplicates the copied TP input blob and keeps it distinct from the HackMD intake inventory.
- Full-byte comparison and firmware-owner sign-off remain pending for the other cases before broader parity can be claimed.
- The TP-base case establishes only NT51926 Common FW 1.4.1 cascade admission/execution; it does not establish all-IC or V2 parity, and the other pending golden gates remain unchanged.

Required owner data per case:

- IC and IC number mode, for example `NT51927` / `3`.
- Common FW version decoded from the available official firmware and the postbuild category used for the run.
- Each physical replacement CtrlRAM slot id, display name, BIN hash, and size;
  a Postbuild file reused at several logical destinations is listed once.
- Base firmware BIN hash and size when a genuine pre-replacement base exists.
- Expected final output hash and size for the staged Combiner postbuild result.
- Source classification, provenance, and owner approval note.

Schema `0.2` remains the stronger base-backed private-fixture format. When no
pre-replacement base exists, use the separately reviewed expected-only intake
contract. Do not put the expected firmware into the schema `0.2` `base` field.
