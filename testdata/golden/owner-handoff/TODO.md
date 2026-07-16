# Golden Evidence: What to Provide

Use only the existing paths below. This list separates repository-derived
fixtures from direct owner evidence so that a sliced file is never described as
an owner-supplied product golden.

## A. CtrlRAM Replace

### A1. NT51926 Common FW 1.4.1 cascade

Existing evidence:

- owner-provided complete base firmware: 262,144 bytes;
- repository-derived `normal`, `diff`, `mp`, `vn`, and `nf` replay fixtures,
  sliced from that same base;
- pinned BAT command and Legacy Combiner 1.13 tool/hash.

The existing replay source files are tracked at:

- [`base/nt51926-2ic-csot-toyota-d02t06-jira0597-20260622.bin`](../ctrlram-replace/fixtures/20260705/base/nt51926-2ic-csot-toyota-d02t06-jira0597-20260622.bin)
- [`inputs/nt51926-cascade-self-20260705/`](../ctrlram-replace/fixtures/20260705/inputs/nt51926-cascade-self-20260705/)

Choose one evidence level.

#### Minimum: close the existing self-replacement replay

Put this file here:

```text
ctrlram-replace/nt51926/cascade/expected.bin   262,144 bytes
```

It must be the complete final output produced by the official legacy flow using
the exact committed base and sliced-input hashes. Confirm that those exact
inputs were used. This proves reference parity for the replay fixture; it does
not turn the sliced inputs into independently supplied replacement BINs.

#### Preferred: direct product replacement golden

Put one same-run set here:

```text
ctrlram-replace/nt51926/cascade/
├─ base.bin                  complete official base, 262,144 bytes
├─ inputs/
│  ├─ normal.bin             11,264 bytes
│  ├─ diff.bin               10,240 bytes
│  ├─ mp.bin                  9,216 bytes
│  ├─ vn.bin                  5,728 bytes
│  └─ nf.bin                 11,728 bytes
└─ expected.bin              complete final output, 262,144 bytes
```

These five inputs must be the actual replacement files used by the official
run, not slices recreated from `base.bin`.

### A2. NT51927 two-chip and three-chip

Existing evidence:

- owner-provided two-chip and three-chip base firmware images;
- repository-derived per-chip CtrlRAM replay fixtures sliced from each base;
- pinned NT51927 Postbuild flow.

The existing replay source files are tracked at:

- [`base/nt51927-2ic-csot1560-d09t0d-jira0251-20260617.bin`](../ctrlram-replace/fixtures/20260705/base/nt51927-2ic-csot1560-d09t0d-jira0251-20260617.bin)
- [`inputs/nt51927-2chip-self-20260705/`](../ctrlram-replace/fixtures/20260705/inputs/nt51927-2chip-self-20260705/)
- [`base/nt51927-3ic-tm-tl177xfks03-gm-d08t9b-20260703.bin`](../ctrlram-replace/fixtures/20260705/base/nt51927-3ic-tm-tl177xfks03-gm-d08t9b-20260703.bin)
- [`inputs/nt51927-3chip-self-20260705/`](../ctrlram-replace/fixtures/20260705/inputs/nt51927-3chip-self-20260705/)

Minimum replay evidence is the independent complete output for each exact
committed fixture:

```text
ctrlram-replace/nt51927/2chip/expected.bin   262,144 bytes
ctrlram-replace/nt51927/3chip/expected.bin   262,144 bytes
```

For a direct product golden, use the committed base identified above and
provide `expected.bin` plus the actual physical replacement inputs from the
same official run. Do not create another `base.bin` folder.

Two-chip input folder:

```text
ctrlram-replace/nt51927/2chip/inputs/
├─ nf.bin                     8,080 bytes
├─ normal-master.bin         12,288 bytes
├─ mp-master.bin              9,216 bytes
├─ vn.bin                     5,728 bytes
├─ normal-slave-r.bin        12,288 bytes
└─ mp-slave-r.bin             9,216 bytes
```

Three-chip has one shared NF/VN input plus the three Normal/MP positions:

```text
ctrlram-replace/nt51927/3chip/inputs/
├─ nf.bin                    12,112 bytes
├─ normal-master.bin         12,288 bytes
├─ mp-master.bin              9,216 bytes
├─ vn.bin                     5,728 bytes
├─ normal-slave-r.bin        12,288 bytes
├─ mp-slave-r.bin             9,216 bytes
├─ normal-slave-l.bin        12,288 bytes
└─ mp-slave-l.bin             9,216 bytes
```

`nf.bin` and `vn.bin` are the physical files consumed once by Postbuild and
reused for multiple logical destinations. Do not provide per-chip NF/VN copies
or fabricate duplicate provenance.

### A3. NT51950 and NT51951 direct product cases

No direct CtrlRAM product golden is currently committed for these cases.

For each selected IC and mode, provide a complete same-run base and final output
of identical full-firmware size. Record the actual Common FW version and
single/cascade selection; the repository must not infer them from a filename.

Single case:

```text
ctrlram-replace/nt51950/single/       or nt51951/single/
├─ base.bin
├─ inputs/
│  ├─ normal.bin             23,552 bytes
│  ├─ vn.bin                  8,444 bytes
│  └─ nf.bin                 10,768 bytes
└─ expected.bin
```

Cascade case:

```text
ctrlram-replace/nt51950/cascade/      or nt51951/cascade/
├─ base.bin
├─ inputs/
│  ├─ normal.bin             23,552 bytes
│  ├─ diff.bin                5,120 bytes
│  ├─ vn.bin                  8,444 bytes
│  └─ nf.bin                 10,768 bytes
└─ expected.bin
```

NT51951 may instead use NT51950 evidence only if the firmware owner approves an
exact CtrlRAM-specific fact alias for the selected mode and Common FW version.
The existing general statement that NT51951 follows NT51950 postbuild is not by
itself a direct product golden.

### CtrlRAM comparison rule

The V2 plus approved external-Combiner result must compare full-byte with
`expected.bin`. Header CRC/Header Copy CRC ranges describe the external
processor's allowed staged mutations; they do not permit weakening the final
golden comparison. CtrlRAM replacement regions are declared replacement
operations, not CRC drift.

## B. AB Merge

### B1. NT51932 direct golden or alias decision

Put a direct set under `ab-merge/nt51932/inputs/`:

```text
dp-ab.bin       524,288 bytes
tpa.bin         262,144 bytes
tpb.bin         262,144 bytes
expected.bin    524,288 bytes
```

Alternatively, approve a specific NT51929-to-NT51932 **AB-only fact-scoped
alias**. Normal/whole-map similarity is not sufficient.

### B2. NT51951 direct golden

Put this set under `ab-merge/nt51951/inputs/`:

```text
dp-ab.bin     1,048,576 bytes; complete DP container
tpa.bin         225,280 bytes
tpb.bin         225,280 bytes
expected.bin  1,048,576 bytes
```

Do not provide `map.txt`. Do not resend Combiner when this case uses the pinned
Legacy Combiner 1.13 command. C# never calculates or writes the AB header CRC.

### B3. AB cases that do not need another BIN upload

- **NT51919:** approve the existing manifest-declared NT51929 AB-specific fact
  alias, or reject it and then provide a direct golden set.
- **NT51929:** a direct owner golden and full-byte reference parity exist; only
  firmware-owner promotion review remains.
- **NT51950:** two direct owner goldens and Python/Legacy Combiner 1.13
  full-byte parity exist; only firmware-owner promotion review remains.

## C. DP Replace direct product goldens

Each existing folder needs one same-run `base.bin`, actual `dp.bin`, and
independent complete `expected.bin`:

```text
dp-replace/nt51950/dp-0x40000/
dp-replace/nt51950/dp-0x80000/
dp-replace/nt51950/dp-0x100000/
dp-replace/nt51951/dp-0x40000/
dp-replace/nt51951/dp-0x80000/
dp-replace/nt51951/dp-0x100000/
```

The folder name is the exact base/output capacity. Keep `dp.bin` at its actual
source length; do not pre-pad or crop it. Existing Standard Merge fixtures do
not substitute for these Replace product goldens.

## D. Not requested yet

- NT51926 single and NT51927 single CtrlRAM cases.
- Optional CtrlRAM sweep cases for the other ICs.
- Optional additional Standard Merge capacities.
- General Merge rows not selected for support.
- General Replace BINs. Protected ranges, mapping envelope, overlap/alignment,
  and TP-postbuild trigger policy must be approved before a truthful case can
  be requested.

## What happens after you provide the files

1. We classify every file as owner-provided or repository-derived.
2. We record original filename, exact size, SHA-256, source, IC/mode/version,
   tool/command identity, confidentiality class, and owner approval scope.
3. We run the legacy reference and V2 workflow on separate staging copies.
4. We compare the complete output, not only CRC/header fields.
5. Every intentional byte change must map to a declared operation/range.
6. Accepted, privacy-scrubbed replay inputs and outputs are committed to the
   canonical golden fixture. Runtime promotion remains a separate human gate.
