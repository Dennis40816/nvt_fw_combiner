# ADR 0084: Prepare isolated NT51925 Standard Merge and CtrlRAM Replace candidates

- Status: Proposed
- Date: 2026-10-07
- Owners: Product owner + firmware owner + architecture owner
- Risk: R3 firmware/support capability restoration
- Amends: [ADR 0042](0042-retire-nt51920-nt51925-nt51930-nt51931.md), NT51925 only, if accepted

## Context and authority

On 2026-10-07 the owner requested NT51925 Standard Merge and CtrlRAM Replace
local preparation for NFC 1.2.2, based on v1.2.1. Core integration is targeted
at 1.3.0; 1.2.x becomes a correction line afterwards. Owner isolation decision
Decision 334 accepts a separate NT51925 map, family and candidate bundles for this
preparation. This restoration ADR remains Proposed. Support, firmware output
and release have no R3 approval.

[ADR 0042](0042-retire-nt51920-nt51925-nt51930-nt51931.md) retired NT51920,
NT51925, NT51930 and NT51931. This proposal concerns only NT51925 and the two
named workflows. The other three ICs remain retired. NT51925 AB Merge, General
Merge, General Replace and DP Replace remain excluded.

The `925&926` workbook sheet describes limited normal TP header facts, not a
complete NT51925 TP map, CtrlRAM ranges, topologies, postbuild behavior or output
parity. No repository TP flash-map version lists NT51925. Legacy Combiner
1.13.0 has no IC-specific mode or chip-ID check, so successful invocation is
not NT51925 support evidence.

Evidence update (2026-10-08): the owner supplied a private NT51925 data set
(postbuild flows, map headers, sample images) and no independent Golden output.
The data set sits in the private asset repository, and this repository holds no
copy. It holds a layout that is consistent with the provisional geometry below.
The owner has not yet confirmed the production layout. On 2026-10-08 the
delivered Combiner and Combiner 1.13.0 gave byte-identical output on the samples
that were checked. This does not prove support. The sample identities, the sample count and the delivered Combiner hash are kept in the private repository and are not in a public manifest yet. Until they are, treat this as an unverified observation that no one can reproduce from this repository. This update changes
no decision, route state or gate in this ADR.
The owner also set the first scope to Standard Merge only (relayed 2026-10-08).
The CtrlRAM Replace candidates below stay declared and are deferred until the
owner supplies Golden cases, expected in the week of 2026-10-12. The owner plans
to support different layouts by Common FW version. That decision needs a later
amendment of this ADR, and this update does not change the geometry below.

## Considered options

1. Join NT51925 to the existing NT51923/NT51926 family now. This would change
   established capability identities before an official NT51925 map proves
   identical facts and could disturb Saved Rules. Rejected.
2. Give NT51925 its own map, family and candidate bundles while treating copied
   numbers as provisional declarations. This preserves v1.2.1 NT51923/NT51926
   identities and closes execution until independent evidence arrives. Selected
   by owner decision 334 (2026-10-07).

## Proposed restoration boundary

Profiles owns isolated firmware declarations; the trusted package index owns
candidate registrations; the canonical capability policy owns exact-route
authoring, publication and evidence decisions; Application owns snapshot and
execution admission. UI and CLI consume those typed decisions. The existing
profile/compiler, catalog and admission owners are reused or extended. No
second semantic path or IC-specific presentation rule is authorized.

NT51925 Standard Merge and CtrlRAM Replace are Candidate/ContractOnly routes
with authoring unavailable. They may appear as blocked declarations, but cannot
create an authoring snapshot, resolve into an executable session, execute, or
Build. The runtime-reference candidate exception used by other CtrlRAM routes
does not admit these NT51925 routes. No candidate execution exception is added.
A declaration, structural check or policy row implies neither Supported nor
approved nor Golden-verified firmware.

The isolated Standard candidate provisionally declares a `0x40000` `flash`
image: TP `[0x00000,0x3C000)`, forbidden zero-filled gap
`[0x3C000,0x3E000)`, and DP `[0x3E000,0x40000)`, in TP-then-DP order. It
has no topology selector. Normal FlashCode naming is a provisional declaration.

The isolated CtrlRAM candidates provisionally declare Common FW intervals
`[1.0.0,2.0.0)` and `[2.0.0,+infinity)`, Single and generic Cascade plans,
and `0x3C000` TP-work and `0x40000` full-Flash capacities. Copied CtrlRAM
prefixes, operation order, postbuild write bounds, CRC/header handling and
naming require official NT51925 confirmation. Filename, PID, hash or Golden
identity cannot supply version or topology authority. Any future external
processor execution must modify only host-created staging copies and pass an
exact declared-range diff.

NT51923 and NT51926 retain every v1.2.1 profile, map, family, bundle, route
and capability identity, operation, metadata, processor command, output byte
and naming behavior. Their policy decisions, Golden cases, expected bytes and
approved difference bounds keep their existing scope. Saved Rules referring to
those exact definitions continue to resolve without migration. A later family
join requires the official NT51925 map proving identical canonical facts and
separate owner approval; this proposal neither prepares nor authorizes it.

## Amendment (2026-10-09): version-keyed Standard Merge TP maps

Status of this amendment: Proposed, together with the rest of this ADR.

Owner decision (relayed 2026-10-08): the NT51925 layouts V1 and V2 are Common FW
major versions. Both are supported. For now they split at 2.0.0: V1 below 2.0.0,
V2 from 2.0.0. The first scope is Standard Merge only. The Single and Cascade
topologies are the only TP Overview variants, as for NT51926. A Cascade holds up
to three ICs.

Decision proposed here:

- The built-in TP flash-map catalog may hold more than one entry for one IC. Each
  entry has an effective Common FW version. A missing version means 1.0.0.
- The catalog selects an entry with the rule that the postbuild profile catalog
  already uses. It takes the entry with the highest effective version that is not
  above the input version. A version below 1.0.0 is refused. An IC with several
  entries and an unreadable version is refused. The catalog never guesses.
- An IC with exactly one entry behaves as before. Its output bytes do not change.
  Only NT51925 gets a second entry, and only after the official map is confirmed.
- The version comes from the Common FW fields of the FWConfig in the TP input.
  Filename, PID, version text, hash or a Golden observation cannot supply it.
- A later map, for example from 2.5.0, is one new catalog entry with its owner
  evidence. It needs no code change. Versions 2.0.0 to 2.4.x keep using the 2.0.0
  entry. Adding such an entry is a firmware change and follows the gates below.
- An entry that says "map not provided" is a slot like a map. It has an IC, a first
  Common FW version and a reason. The slots of one IC, maps and markers together,
  form one list. Each IC list starts at 1.0.0. One version is used once. The
  selector takes the slot with the highest version that is not above the input.
  If that slot is a marker, the selector refuses the IC and returns its reason.
  An IC may have only markers. An IC with several slots needs a readable version.
- NT51925 now has owner-declared maps at both slot starts, as recorded in the
  NT51925 maps amendment below. The borrowed NT51926 catalog values stay removed.
  NT51925 candidate routes retain their promotion blockers and publish no executable output.
- End flags are declared per layout (ADR 0076). NT51925 declares the same end
  flag in both Common FW lines and in the Standard Merge and CtrlRAM Replace
  candidate families, so the base Backup can supply the version before selection.
- Limit: every CtrlRAM Replace image map of one IC must resolve the same end
  flag before the reader can read its base version. A future Common FW line
  needing a different position first needs a version-reading design independent
  of a chosen map; that remains outside this amendment.
- Limit: General Replace keeps its compiled capability when the user changes only
  the Base. The retained capability is not checked again against the DP regions of a
  different map. NT51925 now has two catalog maps, with identical DP declarations and blocked
  candidate execution. No supported route exercises different DP declarations today. The change
  that adds a second map to one IC must add that check and a planner test with a
  swapped Base.
- One internal selector, `BuiltInCommonFwSelector`, picks the postbuild profile and
  the TP flash map for one Common FW version. CtrlRAM Replace, General Replace and
  the memory-layout display call it, so they cannot disagree on the rule or on
  which map is refused. The display record now carries an optional issue for a refused slot;
  the UI pending-layout projection remains a later presentation change. The by-IC catalog lookups remain
  for ICs with exactly one slot that is a map, and return nothing for any other IC.
- Standard Merge does not read the TP flash-map catalog today. Its image maps
  declare no Common FW condition. The NT51925 Standard Merge candidate cannot run
  yet, so a version condition for image maps is a later item. It changes the
  profile schema and needs R3 approval from the owner. Owner decision 2026-10-09:
  do it later, and add it before the NT51925 Standard Merge candidate can run.
  It is not part of this amendment.

The version-slot model is unchanged by the NT51925 maps amendment below.
Candidate Replace geometry remains provisional and Replace stays deferred.

## Amendment (2026-10-10): NT51925 maps

Status: Proposed. Owner decision (relayed 2026-10-10): the owner's postbuild
flow is the single authority for NT51925 addresses and sizes.

The layout key is the Common FW slot and IC count: 1.x starts at 1.0.0 and
2.0.0 starts at 2.0.0 under the existing greatest-applicable-slot rule. One IC
selects Single; two and three ICs select the same Cascade layout. Single-only
and multi-chip-only rows express the four layouts in the existing catalog.
Both candidate families declare the same NVT end flag for all selected maps,
using an exact marker-length search, unique selection and the canonical
Backup result offset. The Common FW Backup read therefore resolves before
map selection. Catalog ranges remain authoritative when a provisional
postbuild command plan is supplied.

Verification scope: 1.x Cascade agrees with the owner's postbuild flow and two
real BINs; 2.0.0 Cascade agrees with that flow and three real BINs. Both Single
layouts have postbuild evidence only and no real BIN. The 2.0.0 Single layout
omits DIFF. BIN agreement is owner-reported; this local change does not perform
private BIN verification. Unknowns remain the 1.x Single `.data` position,
whether a two-chip 2.0.0 Cascade uses all declared DIFF bytes, and the unused
gaps before and after the 2.0.0 Header Copy. No Project ID is invented.

Candidate Replace routes stay blocked as Candidate/ContractOnly. Their other
geometry and execution contracts remain provisional. Standard Merge remains
blocked pending its later version-conditioned image-map work. This amendment
makes no support, approval or Golden claim and does not change the ADR status.

### Later items and completed follow-ups

- Optional issue in the display record: triggered by this map change and the
  PR #602 marker-display finding; the record and discovery propagation are
  implemented here, while rendering the reason in the pending UI is deferred
  because it requires more than a small binding.

## Required owner evidence and gates

Before NT51925 promotion, obtain and review:

- The official NT51925 TP flash map or mmap.
- NT51925 postbuild `.bat` files for Common FW 1.4.1 and 2.0.0, and any
  additional owner-declared version.
- Actual FW versions and topologies, including Cascade IC counts, and every
  difference from NT51923/NT51926.
- Independent complete-output Golden evidence: at least one single-chip
  Standard Merge case and four CtrlRAM cases covering both declared FW
  contracts and Single/Cascade topology.

The [Standard Merge](../../testdata/golden/owner-handoff/standard-merge/nt51925/CASE.md)
and [CtrlRAM Replace](../../testdata/golden/owner-handoff/ctrlram-replace/nt51925/CASE.md)
handoffs request same-run physical inputs, official final output and provenance.
They request no `base.bin` reconstructed from expected output. Unreviewed BINs
remain outside Git; placeholders and provisional numbers supply no firmware
evidence.

Independent semantic and firmware-owner review, complete output comparison
under an approved NT51925 contract, exact processor write-range audit,
protected checks and final owner R3 approval of the exact source are required
before support promotion, integration or release. NT51923 and NT51926
regression evidence must remain valid without re-pinning their definition
identities. Missing owner evidence is unexecuted, never a passing Golden case.
