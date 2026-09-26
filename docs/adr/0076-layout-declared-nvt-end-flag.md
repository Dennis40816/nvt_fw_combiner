# ADR 0076: Layout-declared NVT end flag

Status: Proposed (NVT-END-FLAG-1113-01 with its release pin companion
NVT-END-FLAG-RELEASE-PIN-1113-01; owner decisions 30, 35 and 37 of
2026-09-26; independent design review ACCEPT-WITH-CHANGES of revision 2 and
ACCEPT of revision 3 with the findings incorporated; firmware-owner and
release-owner attestations required).

Amends: [ADR 0015](0015-canonical-firmware-map-and-compiled-composition.md),
section "Canonical firmware resolution", and
[ADR 0016](0016-typed-firmware-metadata-values.md), only for which NVT marker
locates the canonical FWConfig Backup of a layout that declares its end flag.
Retains [ADR 0012](0012-cmi-dp-and-nvt-fwconfig-metadata.md) (Backup at the
end flag terminal `T - 0xFFF`) and the Dynamic DiffDLM postcondition of
[ADR 0031](0031-ctrlram-profile-intervals-and-build-plan-authority.md).

## Context

ADR 0015 fixed the canonical FWConfig source as the unique NVT Backup: one
complete `00 4E 56 54` marker, `unique` selection, result offset `-0xFFC`.
Profiles bound the search to a template range, but the compatibility reader
counted markers over the whole image. Display OSD content of NT51950/NT51951
can hold a complete marker, so a 512 KiB Standard Base became an invalid AB
Base and a captured envelope failed its FWConfig Backup validation
(`BUG-20260926-osd-tail-nvt-marker-blocks-base`). All 198 committed Golden
BINs carry markers only at the end flag right after each bank's FWConfig
Backup. For NT51919/NT51929/NT51932 that position is IC-Count-derived; the
other ICs' declared TP sections end at their end flag.

## Decision

1. A layout declares its NVT end flag when every canonical FWConfig Backup
   NVT locator its map selects has a search range exactly as long as the
   marker, at one position, with `unique` selection and offset `-0xFFC`. A
   map declares it for all such locators or for none (family build rejects
   a partial or split declaration).
2. Where a layout declares its end flag, only the marker there locates the
   Backup; a complete marker anywhere else is neither counted nor rejected.
   This applies to every use of the marker: CtrlRAM Base classification (each
   AB bank at its bank-local end flag), FWConfig Backup validations, CtrlRAM
   admission and inspection, and TP-input reads in Standard, General and AB
   Merge. DP Replace is retired; its declared locator follows the same rule.
3. NT51950 and NT51951 declare the end flag `flash [0x36FFC,0x37000)` in their
   Standard/DP Replace/General, CtrlRAM Replace and AB layouts (for AB, each TP
   input and bank-locally each bank). The Backup start stays `0x36000`.
4. Resolving a declaration either succeeds or fails. It succeeds without a
   declaration only for a family in the named migration inventory
   (`FirmwareNvtEndFlagMigration`), whose readers keep their existing template
   locators and existing compatibility read, unchanged and not newly
   authorized. A missing, inconsistent or unavailable declaration fails and
   the Backup is unreadable; it never falls back to a whole-image search.
5. The inventory lists the pending runtime families (the NT51917/923/926/
   927/928 fixed-layout families of the later small R3 item, and the
   NT51919/929/932 families, including their AB family, of the later
   count-derived item). It may only shrink through admitted changes; tests
   pin it and the named reader inventory. When it is empty, the compatibility
   state and the no-declaration reader overloads are deleted in the same batch.
6. The trusted profile-selective resolution resolves only a profile's required
   structures, so AB map resolution is unchanged. The generic public
   `ResolveMap(inputs)` resolves every selected structure: with TP A/B inputs it
   now also requires each input's end-flag marker, and without them it stays
   pending as before; no production code calls it.

## Rejected options

- Counting markers inside the declared TP section: an OSD-half marker at a
  B-bank TP-section offset would still be AB evidence.
- Deriving the end flag as "TP section end minus 4" in code: not declared for
  NT51919/929/932, whose declared TP section ends at `0x40000`.
- Falling back to a whole-image search when a declaration cannot be resolved:
  fails open; only the named migration inventory keeps the old read.
- A new explicit end-flag field or `end-flag` selection kind (deferred, not
  rejected): self-describing and extensible to count-derived positions, but it
  needs a firmware-family schema version, normalizer, Domain selection kind
  and trust-index materialization changes. Revisit with the count-derived
  NT51919/929/932 item.

## Consequences

- No output byte, write range, order, CRC/header, padding, truncation or
  naming changes. Four family documents change, so family, profile, bundle,
  trust-index, capability-policy and Golden-manifest pins are re-issued; the
  release package scripts re-pin the policy and trust-index digests under the
  separate release record, which needs a release-owner attestation besides the
  firmware-owner attestation of the main record.
- Saved General Merge rules bound to the previous NT51950/NT51951 family
  identity no longer admit (owner decision 37; General Merge is hidden and
  will be re-implemented).
- NT51950/NT51951 inputs are accepted with extra markers away from the end
  flag and rejected when the end flag lacks the marker even if one exists
  elsewhere.
- A marker exactly at a bank's end flag is evidence by rule: an OSD-half
  marker at `0x76FFC` of a 512 KiB NT51950 Standard Base makes it AB with an
  invalid B bank (accepted boundary).

## Verification

The two former fail-closed regressions pass; negative cases reject a missing
end-flag marker for the same reason as a missing marker; boundary tests pin
`0x76FFC`; declaration, inventory and reader-inventory tests pin the scope;
every applicable certified NT51950/NT51951 Golden output case executes with
unchanged expected bytes (the marker scan is auxiliary); firmware-owner and
release-owner attestations are recorded at the exact final-evidence head.
