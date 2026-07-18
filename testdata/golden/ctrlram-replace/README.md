# CtrlRAM Replace Fixture Handoff

This directory is reserved for owner-provided CtrlRAM Replace evidence.

Owner-approved committed fixtures live under `fixtures/`. The original fixture
set is listed by `manifest.json`; dated direct-evidence intakes are listed by
`manifest.20260717.json` and `manifest.20260718.json`. The repository verifier
checks every inventory, size, SHA-256 value, and the final intake's exact-case
relationships without promoting runtime support.

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
- The final owner intake is complete. Remaining full-byte comparison, command
  reconstruction, route convergence, and independent review are repository-owned
  gates; no additional owner input is required.
- The 2026-07-17 snapshot contains owner-approved public golden payloads and
  preserves technical project and `AUTO_PRJ` filenames. It is evidence intake,
  not a runtime-support promotion.
- The official owner system may provide only the final expected firmware and physical CtrlRAM inputs. Do not ask the owner to fabricate or rename that final output as `base.bin`. Expected-only intake may use the documented expected-derived sentinel audit, but it is range/processor evidence rather than independent base-backed parity and cannot promote support by itself.
- The 2026-07-18 final intake records exact NT51926, NT51930, NT51931,
  NT51932, and NT51951 cases. Most provide Standard Merge DP/TP inputs rather
  than a pre-Replace FlashCode; only NT51931 includes a direct reference
  FlashCode. The manifest keeps those base kinds distinct and lists every
  unresolved tool/provenance gate.
- The owner-authorized NT51926 TP-base self-test case lives under `fixtures/derived/20260717`; it archives the same-byte TP golden input and real-workflow output, plus exact half-open integrity ranges and the two-command Combiner 1.13.0 trace. Git deduplicates the copied TP input blob and keeps it distinct from the HackMD intake inventory.
- Full-byte comparison and independent R3 review remain pending for the other
  cases before broader parity can be claimed.
- The TP-base case now establishes full-output V2-candidate parity with its archived Legacy Combiner 1.13.0 result for NT51926 Common FW 1.4.1 cascade. The owner intake also establishes V1/V2 parity for the exact full-Flash self-replacement shape and byte-for-byte tail preservation. It does not establish all-IC parity, an independent pre-replacement product base, or runtime promotion.

Required evidence fields for any future case:

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
