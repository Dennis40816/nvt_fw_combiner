# v0.9.9 Owner Evidence Batch

## Scope and safety

This is the complete owner-evidence request for the current v0.9.9 retirement
and candidate-promotion work. It is an R0 handoff inventory, not a support
claim. Dropping evidence here does not change a profile, range, CRC rule,
processor authority, runtime route, or release status.

The directory skeleton is tracked so it is available after a new clone.
Incoming payloads below `intake/` remain ignored only while they are unreviewed.
Every accepted golden input/output required to replay a case must then be
promoted into a tracked workflow fixture under `testdata/golden/`, with
manifested path, size, SHA-256, provenance, owner approval, and privacy review.

Project and product identifiers remain part of technical provenance. Personal
names, email addresses, account ids, home/user-profile paths, Office/PDF author
metadata, and other personal identifiers must be removed before promotion.
Use a role/team label such as `firmware-owner` in tracked approval records.
Licensed tools, credentials, signing material, and unapproved archives are not
golden fixtures; record tool version and SHA-256 instead.

Canonical path shape:

```text
v0.9.9/intake/<519xx>/<workflow>/<firmware-or-profile-version>/<mode>/
```

For example, the NT51926 case requested first is:

```text
v0.9.9/intake/51926/ctrlram-replace/1.4.1/cascade/
```

The exact currently missing BIN filenames, sizes, and all CtrlRAM/DP/AB/
General folders are listed in
[`intake/missing-goldens/README.md`](intake/missing-goldens/README.md).

## Files required in every terminal case folder

Use the original filenames when provenance depends on them. Otherwise these
canonical names make intake deterministic:

```text
base.bin                    immutable base/reference image, when applicable
inputs/                     immutable replacement/source BINs
expected.bin                independently approved final output
case.json                   IC, workflow, version, mode, source and owner
tool.json                   tool version and SHA-256; no executable required
combiner-command.txt        exact ordered argv or an unedited postbuild log
allowed-diffs.json          approved half-open output ranges
owner-approval.md           approval role/team, date, scope and boundary
source/                     optional XLSX, mmap.h, BAT/CMD and source notes
```

Repository intake will calculate SHA-256 values. The approval authority should
identify the source archive/ticket without personal identifiers, original
output filename, Common FW version if relevant, IC-count selection, expected
tool version, and whether the evidence approves bytes only or also approves a
runtime support route.

An alias case must state the exact shared fact. An IC-family name or Normal-mode
similarity is not enough. Evidence from AB Merge cannot prove CtrlRAM Replace,
and Standard Merge evidence cannot prove Replace.

## P0 — required to retire the remaining v1 authorities

### NT51926 CtrlRAM Replace, Common FW 1.4.1 cascade

Folder:

```text
51926/ctrlram-replace/1.4.1/cascade/
```

Please provide:

- `base.bin`: exact 262,144-byte firmware used by the reference run;
- `inputs/normal.bin`: 11,264 bytes (`0x2C00`);
- `inputs/diff.bin`: 10,240 bytes (`0x2800`);
- `inputs/mp.bin`: 9,216 bytes (`0x2400`);
- `inputs/vn.bin`: 5,728 bytes (`0x1660`);
- `inputs/nf.bin`: 11,728 bytes (`0x2DD0`);
- `expected.bin`: exact 262,144-byte final output from the approved reference
  run, not an output generated only to match the current V2 candidate;
- approval of the allowed read/write authority and final expected-output hash;
- whether the approval covers byte parity only or also permits later runtime
  exposure. Evidence intake itself will not promote support.

The repository already has the direct base, five sliced inputs, exact 1.4.1 BAT
commands, Combiner 1.13.0 executable, and pinned SHA-256. Do not send them again
unless the official originals differ. The essential new file is the independent
`expected.bin` with non-personal provenance and approval. The owner confirmed
on 2026-07-16 that post-Combiner byte differences are limited to the marked
Header CRC and Header Copy CRC areas; the case-local checklist records the
candidate half-open ranges and fail-closed interpretation.

The current candidate declaration expects the five CtrlRAM destinations plus
the reviewed FWConfig backup, copied header, and header CRC words to be the
only writable output areas. Treat that as a proposal to approve or correct,
not as owner evidence derived from this checklist.

### Remaining CtrlRAM Replace cases

Each selected case needs base/replacement inputs, independent `expected.bin`,
exact tool/command evidence, allowed-diff proof, and firmware-owner approval.

```text
51926/ctrlram-replace/2.0.0/single/
51926/ctrlram-replace/2.0.0/cascade/

51927/ctrlram-replace/VERSION-REQUIRED/single/
51927/ctrlram-replace/VERSION-REQUIRED/2-chip/
51927/ctrlram-replace/VERSION-REQUIRED/3-chip/

51917/ctrlram-replace/ALIAS-SCOPE/non-nb/
51928/ctrlram-replace/ALIAS-SCOPE/non-nb/

51932/ctrlram-replace/VERSION-REQUIRED/single/
51932/ctrlram-replace/VERSION-REQUIRED/cascade/
51929/ctrlram-replace/ALIAS-SCOPE/
51919/ctrlram-replace/ALIAS-SCOPE/

51930/ctrlram-replace/1.x/single/
51930/ctrlram-replace/1.x/cascade/
51930/ctrlram-replace/2.0.0/single/
51930/ctrlram-replace/2.0.0/cascade/

51931/ctrlram-replace/VERSION-REQUIRED/single/
51931/ctrlram-replace/VERSION-REQUIRED/cascade/

51950/ctrlram-replace/VERSION-REQUIRED/single/
51950/ctrlram-replace/VERSION-REQUIRED/cascade/
51951/ctrlram-replace/ALIAS-SCOPE/single/
51951/ctrlram-replace/ALIAS-SCOPE/cascade/

51920/ctrlram-replace/VERSION-REQUIRED/single/
51920/ctrlram-replace/VERSION-REQUIRED/cascade/
51923/ctrlram-replace/VERSION-REQUIRED/single/
51923/ctrlram-replace/VERSION-REQUIRED/cascade/
```

Special owner decisions still needed:

- NT51917 and NT51928 non-NB: approve the exact NT51927 fact scope per
  mode/count or provide direct evidence. NT51928 NB stays excluded.
- NT51919 and NT51929: approve CtrlRAM-specific alias facts; AB evidence is not
  transferable.
- NT51930: provide each released 1.x/2.0 count branch. Counts above 13 remain
  closed without direct evidence.
- NT51931: identify and hash the correct tool/mode. The inspected
  `NT51930BASED_NORMAL_MODE` path crashes with Combiner 1.13.0; the diagnostic
  `NT51931BASED_NORMAL_MODE` path avoids the crash but has an unexplained
  108-byte non-CRC drift. Neither is promotable without an owner decision.
- NT51951: approve the exact CtrlRAM facts shared with NT51950 or provide a
  direct case.

### Cross-cutting CtrlRAM and General Replace policy

```text
_shared/ctrlram-base-contract/
_shared/tp-fw-version-edit/
_shared/general-replace-policy/
_shared/release-scope/
_templates/519xx/general-replace/VERSION-REQUIRED/MODE-REQUIRED/
```

Please provide:

- whether a CtrlRAM base is a TP slice that is reinserted or a full-flash
  container, including exact offsets and capacity rules;
- TP firmware-version editable fields/ranges, source-to-backup relationship,
  expected outputs, and owner approval for every released category;
- General Replace protected ranges, explicit-mapping safety envelope,
  alignment/overlap rules, and when TP mappings trigger postbuild;
- the v0.9.9 IC/workflow/mode release scope. Unselected cases remain candidate
  or unsupported rather than being inferred.

## P1 — required only for selected candidate support promotion

### AB Merge

```text
51919/ab-merge/profile-0.1.0/direct-or-alias/
51929/ab-merge/profile-0.1.0/product-golden/
51932/ab-merge/profile-0.1.0/direct-or-alias/
51950/ab-merge/profile-0.1.1/owner-review/
51951/ab-merge/profile-0.2.0/product-golden/
```

AB invariants:

- NT51950 and NT51951 use the full DP container as the base.
- AB does not require `map.txt`.
- C# never calculates or writes the AB header CRC.
- Python reference parity is compared against the exact Legacy Combiner 1.13
  command.
- NT51919 already has manifest-declared fact-scoped parity to the direct
  NT51929 golden. It needs firmware-owner approval of that AB-specific fact
  scope; a new BIN is optional unless the alias is rejected.
- NT51932 still needs direct evidence or an owner-approved fact-scoped alias.
  Normal/whole-map alias evidence is insufficient.
- NT51929 already has a direct owner-approved input/output golden and full-byte
  Python reference parity. It needs firmware-owner promotion review, not another
  product BIN set.
- NT51950 already has two direct owner-approved cases and Python/Combiner byte
  parity. Its remaining item is firmware-owner runtime-promotion review, not a
  request to regenerate those existing bytes.
- NT51951 still needs a direct product golden plus tool/command trace and owner
  review. Synthetic topology evidence is not product approval.

For a new AB product golden include the input container(s), exact expected
output, original filename, exact tool identity/hash, exact command/log, allowed
diff, provenance, and firmware-owner approval.

### General Merge

All 13 current profiles are executable candidates. Promotion of a selected
row needs current-vs-V2 byte/report parity and an explicit support decision:

```text
51917/general-merge/profile-current/parity/
51919/general-merge/profile-current/parity/
51920/general-merge/profile-current/parity/
51923/general-merge/profile-current/parity/
51926/general-merge/profile-current/parity/
51927/general-merge/profile-current/parity/
51928/general-merge/profile-current/parity/
51929/general-merge/profile-current/parity/
51930/general-merge/profile-current/parity/
51931/general-merge/profile-current/parity/
51932/general-merge/profile-current/parity/
51950/general-merge/profile-current/parity/
51951/general-merge/profile-current/parity/
```

This is not a request to promote all 13. Put the chosen release rows in
`_shared/release-scope/`; unselected rows stay support-neutral.

## P2 — optional direct audit/product goldens

Standard Merge already has owner-approved golden coverage for the currently
selected routes and aliases. These folders are only needed if the additional
capacities are selected for release:

```text
51950/standard-merge/profile-0.5.1/dp-0x80000/
51950/standard-merge/profile-0.5.1/dp-0x100000/
51951/standard-merge/profile-0.5.1/dp-0x40000/
51951/standard-merge/profile-0.5.1/dp-0x100000/
```

Direct DP Replace product goldens remain useful for hardware/product audit but
are not substitutes for AB or CtrlRAM evidence:

```text
51950/dp-replace/profile-0.5.1/dp-0x40000/
51950/dp-replace/profile-0.5.1/dp-0x80000/
51950/dp-replace/profile-0.5.1/dp-0x100000/
51951/dp-replace/profile-0.5.1/dp-0x40000/
51951/dp-replace/profile-0.5.1/dp-0x80000/
51951/dp-replace/profile-0.5.1/dp-0x100000/
```

## Acceptance sequence after the drop

1. Intake computes hashes and records provenance while unreviewed payloads stay
   ignored.
2. The current tool and the independent reference run from immutable inputs on
   separate staging copies.
3. Full output bytes and allowed differences are compared.
4. A firmware owner reviews tool identity, command order, read/write authority,
   expected hash, and alias scope.
5. Accepted replay inputs/outputs are privacy-scrubbed and committed as a
   manifested workflow golden; tool executables and personal identifiers stay
   out of Git.
6. Byte evidence may close a candidate blocker. Runtime/support promotion is a
   separate reviewed change.
