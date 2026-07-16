# Missing Golden BIN Folder Guide

Snapshot: 2026-07-16. This is the complete current BIN request, separated from
owner-review-only gates. Put files into the matching sibling folder under
`v0.9.9/intake/<519xx>/<workflow>/<version>/<mode>/`.

Incoming BINs remain quarantined until hash, byte, provenance, personal-data,
and firmware-owner review passes. Every accepted replay input and expected
output is then committed under the canonical `testdata/golden/<workflow>/`
fixture. Preserve project/product names; remove personal identity and metadata.

## Do not resend these existing artifacts

- Legacy Combiner 1.13.0 executable, manifest, and SHA-256.
- NT51926 1.4.1 BAT commands.
- NT51926 1.4.1 cascade base and Normal/DIFF/MP/VN/NF inputs.
- NT51927 2-chip and 3-chip bases and all sliced CtrlRAM inputs.
- NT51929 AB direct input/output golden.
- NT51950 AB BOE and Hiway direct input/output goldens.
- Existing Standard Merge goldens recorded by the repository manifests.

For NT51926 1.4.1 cascade, provide only `expected.bin` unless the official base
or input hashes differ from the tracked fixture. For NT51927 2-chip/3-chip,
provide only the corresponding `expected.bin` unless the tracked source differs.

## Common case layout

```text
<case>/
├─ base.bin
├─ inputs/
│  └─ <CtrlRAM or workflow input BINs listed below>
├─ expected.bin
├─ case.json
└─ owner-approval.md
```

`base.bin` and `expected.bin` must have the same exact released-image size for
CtrlRAM Replace. Do not crop, pad, or convert them. Keep the original technical
filename in `case.json`. Use approval roles such as `firmware-owner`, not a
personal name or email.

## CtrlRAM Replace folder tree

```text
51917/ctrlram-replace/alias-51927-postbuild-1.4.1/
  1-chip/  2-chip/  3-chip/
51919/ctrlram-replace/alias-51929-51932-postbuild-2.0.0/
  single/  cascade/
51920/ctrlram-replace/postbuild-1.3.1/
  single/  cascade/
51923/ctrlram-replace/postbuild-1.4.1/
  single/  cascade/
51926/ctrlram-replace/
  1.4.1/single/  1.4.1/cascade/
  2.0.0/single/  2.0.0/cascade/
51927/ctrlram-replace/postbuild-1.4.1/
  1-chip/  2-chip/  3-chip/
51928/ctrlram-replace/alias-51927-postbuild-1.4.1/
  1-chip/  2-chip/  3-chip/
51929/ctrlram-replace/alias-51932-postbuild-2.0.0/
  single/  cascade/
51930/ctrlram-replace/
  1.x/single/  1.x/cascade/
  2.0.0/single/  2.0.0/cascade/
51931/ctrlram-replace/postbuild-1.3.0-blocked-tool-decision/
  single/  cascade/
51932/ctrlram-replace/postbuild-2.0.0/
  single/  cascade/
51950/ctrlram-replace/postbuild-2.0.0/
  single/  cascade/
51951/ctrlram-replace/alias-51950-postbuild-2.0.0/
  single/  cascade/
```

The folder label `postbuild-x.y.z` identifies the inspected postbuild reference;
it does not claim that the base Common FW version is identical. Record the
decoded Common FW version separately in `case.json`.

### NT51920 inputs

| Mode | Files under `inputs/` | Exact bytes |
| --- | --- | --- |
| single | `normal.bin`, `mp.bin`, `vn.bin`, `nf.bin` | 10,240; 5,888; 4,120; 8,080 |
| cascade | `normal-master.bin`, `normal-slave.bin`, `mp-master.bin`, `mp-slave.bin`, `vn.bin`, `nf.bin`, `vector.bin` | 10,240; 10,240; 5,888; 5,888; 4,120; 8,080; 600 |

### NT51923 inputs

| Mode | Files under `inputs/` | Exact replacement bytes |
| --- | --- | --- |
| single | `normal.bin`, `mp.bin`, `vn.bin`, `nf.bin` | 14,336; 10,240; 5,728; 17,584 |
| cascade | single files plus `diff.bin` | `diff.bin` represents the 6,144 replacement bytes; the postbuild adapter preserves the source-layout gap required by the two BAT slices |

### NT51926 inputs

| Common FW / mode | Files under `inputs/` | Exact bytes |
| --- | --- | --- |
| 1.4.1 single | `normal.bin`, `mp.bin`, `vn.bin`, `nf.bin` | 11,264; 9,216; 5,728; 11,728 |
| 1.4.1 cascade | single files plus `diff.bin` | `diff.bin` 10,240; tracked inputs already exist, so only `expected.bin` is currently required |
| 2.0.0 single | `normal.bin`, `mp.bin`, `vn.bin`, `nf.bin` | 11,264; 9,216; 5,278; 11,728 |
| 2.0.0 cascade | single files plus `diff.bin` | `diff.bin` 10,240 |

For the 1.4.1 cascade post-Combiner diff, the owner-confirmed Header CRC/Header
Copy CRC scope is recorded in the case-local `EVIDENCE_REQUIRED.md`.

### NT51927 family inputs

The same slot shape applies to NT51927 and, only with exact owner-approved
CtrlRAM fact scope, NT51917 and NT51928 non-NB. NT51928 NB is excluded.

| Mode | Files under `inputs/` | Exact bytes per chip |
| --- | --- | --- |
| 1-chip | `nf-master.bin`, `normal-master.bin`, `mp-master.bin`, `vn-master.bin` | 4,048; 12,288; 9,216; 5,728 |
| 2-chip | 1-chip files plus `nf-right.bin`, `normal-right.bin`, `mp-right.bin`, `vn-right.bin` | 4,048; 12,288; 9,216; 5,728 |
| 3-chip | 2-chip files plus `nf-left.bin`, `normal-left.bin`, `mp-left.bin`, `vn-left.bin` | 4,048; 12,288; 9,216; 5,728 |

NT51927 2-chip and 3-chip inputs already exist. Their missing files are only
`expected.bin`. NT51917/NT51928 may use reviewed alias evidence instead of
duplicating BINs, but the approval must name workflow and chip count.

### NT51932 family inputs

The same slot shape applies to NT51932 and, only with exact CtrlRAM alias
approval, NT51929 and NT51919.

| Mode | Files under `inputs/` | Exact bytes |
| --- | --- | --- |
| single | `nf.bin`, `normal.bin`, `vn.bin` | 8,080; 18,944; 6,496 |
| cascade | single files plus `diff.bin` | `diff.bin` 35,840 |

AB evidence is not CtrlRAM evidence. Provide these direct cases or approve the
exact CtrlRAM-specific alias per IC and mode.

### NT51930 inputs

| Common FW / mode | Files under `inputs/` | Exact bytes |
| --- | --- | --- |
| 1.x single | `nf.bin`, `normal.bin`, `mp.bin`, `vn.bin` | 6,736; 11,264; 13,312; 6,494 |
| 1.x cascade | single files plus `diff.bin` | `diff.bin` 65,024 |
| 2.0.0 single | `nf.bin`, `normal.bin`, `vn.bin` | 6,736; 11,264; 6,496 |
| 2.0.0 cascade | single files plus `diff.bin` | `diff.bin` 65,024 |

For v0.9.9, a cascade golden must identify its actual count. Counts `14..29`
remain on the current approved `0xFE00` branch and must not be treated as a new
extended layout without separate evidence.

### NT51931 inputs — blocked before promotion

| Mode | Files under `inputs/` | Exact bytes |
| --- | --- | --- |
| single | `nf.bin`, `normal.bin`, `mp.bin`, `vn.bin` | 4,048; 10,240; 9,216; 5,728 |
| cascade | single files plus `diff.bin` | `diff.bin` 97,280 |

Place the official base/inputs/expected output here if available, together with
the exact successful tool/mode identity. The inspected official
`NT51930BASED_NORMAL_MODE` command crashes on Combiner 1.13.0; the diagnostic
`NT51931BASED_NORMAL_MODE` has unexplained 108-byte drift. No expected output is
accepted until the correct tool/mode is owner-decided.

### NT51950 family inputs

The same slot shape applies to NT51950 and, only with exact CtrlRAM alias
approval, NT51951.

| Mode | Files under `inputs/` | Exact bytes |
| --- | --- | --- |
| single | `nf.bin`, `normal.bin`, `vn.bin` | 10,768; 23,552; 8,444 |
| cascade | single files plus `diff.bin` | `diff.bin` 5,120 |

Standard Merge, DP Replace, and AB goldens do not prove these CtrlRAM outputs.

## DP Replace missing direct product goldens

Each folder needs `base.bin`, `dp.bin`, and `expected.bin`. Preserve the actual
`dp.bin` length; do not pre-pad it. The folder capacity is the exact base/output
container capacity.

```text
51950/dp-replace/profile-0.5.1/
  dp-0x40000/  dp-0x80000/  dp-0x100000/
51951/dp-replace/profile-0.5.1/
  dp-0x40000/  dp-0x80000/  dp-0x100000/
```

These six are direct product/hardware audit goldens. Existing deterministic
profile regression is not a substitute when a product golden claim is desired.

## AB Merge missing BINs versus review-only rows

| IC | BIN request |
| --- | --- |
| NT51919 | No new BIN required if firmware owner approves the existing manifest-declared AB fact-scoped alias to the direct NT51929 golden. Otherwise provide `dp-ab.bin` (524,288), `tp-a.bin` (262,144), `tp-b.bin` (262,144), and `expected.bin` (524,288). |
| NT51929 | No new BIN required; direct owner golden and full-byte Python parity already exist. Firmware-owner promotion review remains. |
| NT51932 | Provide `dp-ab.bin` (524,288), `tp-a.bin` (262,144), `tp-b.bin` (262,144), and `expected.bin` (524,288), or an explicitly approved AB-specific fact alias. |
| NT51950 | No new BIN required; two direct goldens and Python/Legacy Combiner 1.13 full-byte parity already exist. Firmware-owner promotion review remains. |
| NT51951 | Provide the full DP container `dp-ab.bin` (1,048,576), `tp-a.bin` (225,280), `tp-b.bin` (225,280), and `expected.bin` (1,048,576). |

Use the existing AB folders:

```text
51919/ab-merge/profile-0.1.0/direct-or-alias/
51929/ab-merge/profile-0.1.0/product-golden/
51932/ab-merge/profile-0.1.0/direct-or-alias/
51950/ab-merge/profile-0.1.1/owner-review/
51951/ab-merge/profile-0.2.0/product-golden/
```

AB never uses `map.txt`; NT51950/NT51951 use the full DP container as base;
Legacy Combiner owns AB header CRC and C# never writes it.

## General Replace golden template

General Replace cannot have a truthful per-IC BIN list until the release scope
and mapping envelope are approved. For each selected IC/version/mode, copy the
tracked template folder and provide:

```text
_templates/519xx/general-replace/VERSION-REQUIRED/MODE-REQUIRED/
├─ base.bin
├─ inputs/
│  ├─ mapping-001.bin
│  └─ mapping-NNN.bin
├─ mappings.json
└─ expected.bin
```

`mappings.json` must record source range, target half-open range, alignment,
overlap decision, protected-range decision, and whether TP touch triggers
postbuild. This remains blocked by `_shared/general-replace-policy/` and
`_shared/release-scope/`; no range is inferred from a supplied BIN.

## General Merge parity folders

The 13 tracked `519xx/general-merge/profile-current/parity/` folders each need
the exact request/mapping JSON, every source BIN, current-route expected output,
and expected report JSON for any row selected for support. Unselected rows may
remain support-neutral candidates and do not require fabricated goldens.

## Optional additional Standard Merge capacities

Only provide these if they are selected for release exposure:

```text
51950/standard-merge/profile-0.5.1/dp-0x80000/
51950/standard-merge/profile-0.5.1/dp-0x100000/
51951/standard-merge/profile-0.5.1/dp-0x40000/
51951/standard-merge/profile-0.5.1/dp-0x100000/
```

Each needs the exact Standard Merge inputs and independent `expected.bin`. The
already manifested NT51950 `0x40000` and NT51951 `0x80000` cases do not need to
be resent.
