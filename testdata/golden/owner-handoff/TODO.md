# Golden Evidence: Provide Now

This list contains only evidence that is still missing. Use the existing
folders below; do not create a second versioned intake tree.

## 1. Output only — base and inputs already exist

| Priority | Put this file here | Exact size | What it proves |
| --- | --- | ---: | --- |
| P0 | `ctrlram-replace/nt51926/cascade/expected.bin` | 262,144 bytes | NT51926 Common FW 1.4.1 cascade output after the tracked postbuild and Legacy Combiner 1.13 command |
| P0 | `ctrlram-replace/nt51927/2chip/expected.bin` | 262,144 bytes | NT51927 two-chip output after postbuild |
| P0 | `ctrlram-replace/nt51927/3chip/expected.bin` | 262,144 bytes | NT51927 three-chip output after postbuild |

Do **not** resend the NT51926 base, its five replacement BINs, BAT command, or
Combiner. Do **not** resend the NT51927 two-chip/three-chip bases or replacement
BINs. They are already tracked with hashes.

The `expected.bin` must come from the approved independent reference run. It
must not be generated only by the current V2 candidate. For NT51926, differences
caused by the CtrlRAM replacement operation are expected output changes; the
separate allowed post-Combiner drift is limited to the owner-marked Header CRC
and Header Copy CRC areas.

## 2. Direct AB golden still missing

Put the following files under the existing `inputs/` folder.

### NT51932

Path: `ab-merge/nt51932/inputs/`

- `dp-ab.bin` — 524,288 bytes
- `tpa.bin` — 262,144 bytes
- `tpb.bin` — 262,144 bytes
- `expected.bin` — 524,288 bytes

Instead of these four BINs, the firmware owner may approve a specific
NT51929-to-NT51932 **AB-only fact-scoped alias**. A Normal/whole-map similarity
statement is not enough.

### NT51951

Path: `ab-merge/nt51951/inputs/`

- `dp-ab.bin` — 1,048,576 bytes; this must be the complete DP container
- `tpa.bin` — 225,280 bytes
- `tpb.bin` — 225,280 bytes
- `expected.bin` — 1,048,576 bytes

Do not provide `map.txt`. Do not provide another Combiner when the case uses the
already pinned Legacy Combiner 1.13 command. C# never writes the AB header CRC.

## 3. DP Replace product goldens still missing

Each existing folder below needs exactly `base.bin`, `dp.bin`, and
`expected.bin`. The folder name is the exact base/output capacity. Keep
`dp.bin` at its original length; do not pre-pad it.

```text
dp-replace/nt51950/dp-0x40000/
dp-replace/nt51950/dp-0x80000/
dp-replace/nt51950/dp-0x100000/
dp-replace/nt51951/dp-0x40000/
dp-replace/nt51951/dp-0x80000/
dp-replace/nt51951/dp-0x100000/
```

## 4. No BIN upload required — owner decision only

- **NT51919 AB:** approve the existing manifest-declared NT51929 AB-specific
  fact alias, or reject it and then provide a direct golden set.
- **NT51929 AB:** direct golden and full-byte parity already exist; only
  firmware-owner promotion review remains.
- **NT51950 AB:** two direct goldens and Python/Legacy Combiner 1.13 full-byte
  parity already exist; only firmware-owner promotion review remains.

Use a role such as `firmware-owner`, the approval date, and the exact IC,
workflow, profile/mode, and fact scope. Do not include a personal name, email,
account id, or user-profile path.

## Not requested in this batch

- Existing Standard Merge fixtures and aliases.
- Optional CtrlRAM sweep cases beyond the three output-only cases above.
- Optional Standard Merge capacity audits.
- General Merge rows not selected for support.
- General Replace BINs. Its protected-range and mapping policy must be approved
  before a truthful golden case can be requested.

After a drop, repository intake records original technical filenames, sizes,
SHA-256, non-personal provenance, and owner approval. Accepted replay inputs
and expected outputs are then committed under the canonical workflow golden;
unreviewed payloads remain ignored.
