# ADR 0084: Prepare isolated NT51925 Standard Merge and CtrlRAM Replace candidates

- Status: Accepted for unverified candidate authoring through the owner's R3 approval of PR4b; support and release promotion remain gated
- Date: 2026-10-07
- Owners: Product owner + firmware owner + architecture owner
- Risk: R3 firmware/support capability restoration
- Amends: [ADR 0042](0042-retire-nt51920-nt51925-nt51930-nt51931.md), NT51925 candidate authoring only

## Context and authority

PR4b implements the 2026-10-14 amendment below (owner direction relayed
2026-10-14 01:20). It opens the nine NT51925 routes for authoring as
`available/candidate/contract-only`. It supersedes earlier statements below
that authoring remains unavailable. Outputs retain `_UNVERIFIED`; all profile
promotion blockers remain. This accepts candidate authoring, not support or
release promotion.

On 2026-10-07 the owner requested NT51925 Standard Merge and CtrlRAM Replace
local preparation for NFC 1.2.2, based on v1.2.1. Core integration is targeted
at 1.3.0; 1.2.x becomes a correction line afterwards. Owner isolation decision
Decision 334 accepts a separate NT51925 map, family and candidate bundles for this
preparation. PR4b accepts unverified candidate authoring as stated above;
support and release promotion retain their R3 gates.

[ADR 0042](0042-retire-nt51920-nt51925-nt51930-nt51931.md) retired NT51920,
NT51925, NT51930 and NT51931. This proposal concerns only NT51925 and the two
named workflows. The other three ICs remain retired. NT51925 AB Merge, General
Merge, General Replace and DP Replace remain excluded.

The `925&926` workbook sheet describes limited normal TP header facts, not a
complete NT51925 TP map, CtrlRAM ranges, topologies, postbuild behavior or output
parity. At that time no repository TP flash-map version listed NT51925; the 2026-10-10 amendments record the isolated owner-declared maps. Legacy Combiner
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
The 2026-10-10 amendments below replace that provisional geometry with the
owner's TP overview; read the geometry there.

## Considered options

1. Join NT51925 to the existing NT51923/NT51926 family now. This would change
   established capability identities before an official NT51925 map proves
   identical facts and could disturb Saved Rules. Rejected.
2. Give NT51925 its own map, family and candidate bundles. Initial provisional
   numeric declarations were replaced by the owner TP overview and postbuild
   flow in the 2026-10-10 candidate geometry amendment. This preserves v1.2.1
   NT51923/NT51926 identities and closes execution until independent evidence
   arrives. Selected by owner decision 334 (2026-10-07).

## Restoration boundary (PR4b)

Profiles owns isolated firmware declarations; the trusted package index owns
candidate registrations; the canonical capability policy owns exact-route
authoring, publication and evidence decisions; Application owns snapshot and
execution admission. UI and CLI consume those typed decisions. The existing
profile/compiler, catalog and admission owners are reused or extended. No
second semantic path or IC-specific presentation rule is authorized.

NT51925 Standard Merge and CtrlRAM Replace are Available/Candidate/ContractOnly
routes. They can create authoring snapshots and execute with valid inputs and
runtime dependencies. Standard uses the shared open-candidate admission;
Replace uses the existing runtime-reference candidate admission. Every output
retains `_UNVERIFIED`, and all promotion blockers remain. A declaration,
structural check or policy row implies neither Supported nor approved nor
Golden-verified firmware. Dated proposals below preserve their recorded scope;
PR4b supersedes their blocked-authoring and execution statements.

The isolated Standard candidate declares a 262,144-byte `flash` image with a
196,608-byte TP prefix, a forbidden zero-filled gap before the DP window, and
TP-then-DP operation order. It has no topology selector. Its geometry follows
the owner TP overview and postbuild flow; normal FlashCode naming remains a
candidate declaration pending independent expected-output evidence.

The isolated CtrlRAM candidates provisionally declare Common FW intervals
`[1.0.0,2.0.0)` and `[2.0.0,+infinity)`, Single and generic Cascade plans,
and 196,608-byte TP-work and 262,144-byte full-Flash capacities. The four
version/topology layouts, CtrlRAM prefixes and postbuild bounds now follow the
owner TP overview and postbuild flow. Operation order, CRC/header behavior and
naming still require independent expected-output evidence and owner review. Filename, PID, hash or Golden
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
Candidate Replace geometry follows the later owner TP overview and postbuild
amendment; Replace stays deferred and blocked.

## Amendment (2026-10-10): NT51925 maps

Status: Proposed. Owner decision (relayed 2026-10-10): the owner's postbuild
flow and TP overview declare NT51925 addresses and sizes.

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
omits DIFF. On 2026-10-10 the host compared both Cascade layouts with those real
BINs: the declared end flag is found once at the declared position, every
declared region except Customer Info (blank in these BINs) holds data where the
layout says so, and no other firmware data
lies outside the declared regions except the 1.x vector table and data area, which
are not catalog regions. This is a layout check, not a byte-exact
Golden run. Unknowns remain the 1.x Single `.data` position, whether a two-chip
2.0.0 Cascade uses all declared DIFF bytes (in the real BINs the data reaches only
past the middle of that region), and the unused gaps before and after the 2.0.0 Header Copy
(blank in the real BINs). No Project ID is invented.

Candidate Replace routes stay blocked as Candidate/ContractOnly. Their geometry
follows the owner TP overview and postbuild flow; execution contracts remain
subject to independent expected-output evidence and owner review. Standard Merge remains
blocked pending its later version-conditioned image-map work. This amendment
makes no support, approval or Golden claim and does not change the ADR status.

### Later items and completed follow-ups

- Optional issue in the display record: triggered by this map change and the
  PR #602 marker-display finding; the record and discovery propagation are
  implemented here, while rendering the reason in the pending UI is deferred
  because it requires more than a small binding.

## Amendment (2026-10-10): candidate geometry from the TP overview

Status: Proposed. Owner decisions at 14:49 and 14:59 replace the borrowed
NT51926 candidate geometry with the owner's TP overview and postbuild flow.
The TP region is 196,608 bytes, full flash is 262,144 bytes, and every layout
uses the end flag at `0x2FFFC`. The existing Common FW slot and topology
applicability model selects four layouts; no second selection path is added.

Verification level: 1.x Single has postbuild evidence only, with no real BIN.
1.x Cascade agrees with postbuild plus two real BINs (owner-reported).
2.0.0 Single has postbuild evidence only, with no real BIN.
2.0.0 Cascade agrees with postbuild plus three real BINs (owner-reported).
No layout has an independent expected output (Golden). These observations
make no support, approval or Golden claim; the candidate remains Proposed
and Blocked, with Unavailable/Candidate/ContractOnly policy and every existing
promotion blocker retained.

Only 1.x Cascade declares CtrlRAM_S and the Vec Table; only 2.0.0 Cascade
declares DIFF. Only 2.0.0 has a Header Copy. For 1.x, TP SVN uses the existing
canonical definition at offset `0x24` in the original TP header, whose format
is unchanged; the removed NT51926 Header Copy locator supplies no authority.
The Standard Merge TP SVN binding stays unchanged. No `.data` or Project ID
region is declared. The unknown 1.x Single `.data` position is not guessed.

The TP prefix [0x0, 0x30000) is copied whole, including the content from
0x2EA80. (owner 2026-10-14 20:58)

CtrlRAM_S keeps the `explicit-range` physical constraint because the range
exists in the TP overview. The FW 1.x cascade runtime profile makes it
read-only, since the postbuild flow declares no replacement source for it.

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

## Amendment (2026-10-14): unverified candidate execution

Status: Accepted for unverified candidate authoring through the owner's R3
approval of PR4b.
Support and release promotion remain gated. This amendment changes no firmware
byte, no region and no existing IC.

Owner direction (relayed 2026-10-14 01:20): build NT51925 as far as the declared
layout facts allow, and do not wait for independent Golden output. Correct it
later if a fact turns out wrong. Tests and the remaining documents follow the
construction.

Owner 2026-10-14 16:47 confirmed: the TP Overview DIFF row applies to the
Cascade layout only. The 2.0.0 Single layout has no DIFF CtrlRAM.

Facts that exist today:

- Four layouts are declared (Common FW 1.x and 2.0.0, Single and Cascade). The
  declared regions follow the owner TP overview and the postbuild flow. These
  two sources agree on all four layouts.
- Real images were compared for the two Cascade layouts only (owner-reported).
  The two Single layouts have postbuild evidence and no real image.
- No layout has an independent expected output (Golden).

### What this amendment supersedes

Every statement in this ADR that says the NT51925 routes are blocked, deferred,
or cannot create a snapshot, execute or Build is superseded. This covers the
"Proposed restoration boundary" paragraph (including "No candidate execution
exception is added"), the blocked and deferred statements of the 2026-10-09 and
2026-10-10 amendments, and the 2026-10-08 scope decision that deferred CtrlRAM
Replace until Golden cases arrive. The old text stays as history. The gates in
"Required owner evidence and gates" stay in force for promotion, integration and
release.

### Accepted candidate decision

1. **Unverified candidate state.** The nine NT51925 routes are authorable
   and executable with authoring `available`, publication `candidate` and
   evidence `contract-only`. The last two values do not change. Together the
   three values mean: built from declared facts, with no independent Golden
   result.
2. **Promotion stage.** The five NT51925 profiles move from `compilable` to
   `executable-candidate`. The three blockers (official map and postbuild,
   independent Golden, owner R3 approval) stay in the profiles. A profile with
   blockers cannot reach `supported`.
3. **Admission.** The eight CtrlRAM Replace routes use the existing
   runtime-reference candidate exception. They need no new path. The Standard
   Merge route needs one bounded admission. The admission rule exists in two
   places today (`CapabilityPublicationCoherence` and `CompositionRunRequest`).
   The change keeps one shared rule. The condition keys on typed profile facts
   (workflow, promotion stage, compile context, blocker kinds), not on an IC
   name. No other resolved-map profile becomes admitted. Standard Merge also
   needs open decision 1 below before it can run.
4. **Label.** Every result from these routes says that it is unverified. The UI
   shows the word. The run report records route, capability fingerprint,
   publication, evidence and the verification status. The output name carries
   the marker `_UNVERIFIED` before `.bin`. For a typed naming renderer
   (`normal-flashcode-v1`, `tp-firmware-v1`), Application adds it and rejects a
   custom file name. For a static name, the profile declares the marked name
   and sets `allowOverride` to false. This changes two contracts: the additive
   fields of `composition-report-v1`, and the output naming rule. The existing
   disclosure, report and naming code carries the label. No second label path
   is added. The label follows the evidence value. A supported route whose
   evidence is `contract-only` today therefore also shows "Unverified", because
   it also has no independent Golden result. Open decision 5 covers the
   wording.
5. **Build.** Build does not read publication or evidence. A route that is
   `available` therefore has no publication or evidence blocker. Build still
   needs a valid execution admission, valid inputs and ready runtime
   dependencies, such as the CtrlRAM postbuild tool. A release may close Build
   for candidate routes with one explicit gate in the shared readiness code.
   Adding that gate is a later owner decision.
6. **Fingerprints.** NT51925 profile hashes, bundle hashes, capability
   fingerprints and policy decision pins change in the pull request that opens
   the routes, and again at each later correction of these profiles. No other
   IC changes a fingerprint, a policy row or a bundle hash.
7. **What does not change.**
   - The support matrix shows NT51925 as candidate. No support, approval or
     Golden claim is made.
   - NT51920, NT51930 and NT51931 stay retired.
   - No other IC changes a byte or a name.

### Values that stay unverified

Each value below rests on the owner TP overview or the postbuild flow alone, or
has no source.

- The 1.x Single `.data` position. The layout leaves it unmapped and
  write-forbidden, and Replace carries it over from the reference.
- Whether a two-chip 2.0.0 Cascade uses all declared DIFF bytes.
- The 2.0.0 ranges `[0x2E660, 0x2E980)` and `[0x2EA80, 0x2F000)`. They hold no
  declared item, and Standard Merge copies them as they are.
- The TP SVN source in the 2.0.0 Standard Merge. The Standard Merge reads it at
  offset `0x24` of the TP header. The 2.0.0 CtrlRAM maps read it from the Header
  Copy.
- The Customer Info range, which is blank in the real images.
- Every Single layout, which has no real image.
- Independent expected output for every layout.

### Open decisions for the owner

Each item has a default. Work continues on the default until the owner decides.
A default does not approve the R3 change it names. That change waits for the
owner's R3 approval before it merges.

1. **Version-conditioned image map for Standard Merge.** The 2026-10-09 decision
   says to add it before the Standard Merge candidate can run. It is a profile
   schema change. The declared copy plan is the same for both Common FW lines:
   TP `[0, 0x30000)` and DP `[0x3E000, 0x40000)`. One map expresses it.
   Default: follow the 2026-10-09 decision and add the version condition. The
   owner may instead amend that decision and require the version condition at
   the first Standard Merge difference between the two lines.
2. **192K TP-work routes.** Default: their output is the TP-work image only.
   Extending it to a full 256K image adds an undeclared behavior.
3. **File name override.** Default: an override cannot remove the unverified
   marker, and the run report carries the label.
4. **Build for release builds.** Default: Build is open and labelled unverified.
   The owner may require the explicit gate in item 5 above.
5. **Wording.** The owner approves the label text and the marker text when the
   user interface and naming changes are reviewed.

### Verification for this amendment

- The pull requests that open the routes change, in the same pull request, the
  tests that pin the blocked state, and the documents that say blocked:
  `docs/contracts/canonical-capability-policy-v1.md`,
  `docs/architecture/supported-ic-matrix.md` and
  `docs/architecture/ic-workflow-flowcharts.md`.
- The identity of every other IC (profile, bundle, fingerprint, policy row, and
  output name) is unchanged and tested as unchanged.
- A write-range audit of the processor staging copy is required before any
  promotion, as in "Required owner evidence and gates".

## Implementation boundary (2026-10-14): Standard image-map version condition

Task 925-F implements the version condition required by the 2026-10-09 decision.
At that boundary, the policy remained `unavailable/candidate/contract-only`.
PR4b now opens authoring as `available/candidate/contract-only`; this earlier
construction record does not limit the later owner decision.

One dynamic Standard map-set route selects the existing first map for Common FW
1.x and `nt51925-standard-merge-fw2-256k` from 2.0.0, using the TP input's declared
FWConfig locator. The shared interval rule and value live in Domain; postbuild
and TP flash-map adapters continue to consume that same rule. The new exact-hash
family schema successor is selected by the NT51925 Standard bundle alone.

The two maps share all physical and metadata sets and compile identical copy
operations. Selection retains a typed version rather than TP SHA. Retained exact
authoring capabilities are reselected when TP content changes. Fact aliases in
version-conditioned families and source-envelope execution with version slots
are rejected pending explicit contracts; report-only metadata retains the first
map as a fixed layout declaration. The family and capability-policy contracts
own the detailed rules. Other IC identities and output semantics stay unchanged.

## PR4b evidence and naming boundary (2026-10-28)

Common FW below 2.0.0 uses the 1.x map; from 2.0.0 it uses the 2.0.0 map.
The `1.4.1` in `nfc.nt51925.ctrlram-postbuild-fw1.4.1` and `...-fw141-...`
profile ids is naming only and serves `[1.0.0, 2.0.0)`; no identifier changes.
Separately, golden coverage: FW version less than or equal to 1.4.1.
An owner-certified official output is an evidence level, not a support upgrade.
For applicable certified no-op cases, the no-op scenario is byte-for-byte equal
to the official output. Single-chip and 3-IC are unverified. The 256 KiB Replace
input shape has no official before/after file.

The 2.0.0 Single map has no DIFF CtrlRAM. `[0x27A00, 0x2A200)` in TP flash
remains unmapped and write-forbidden. The four FW 2.0.0 Replace CRC words
`[0x1C,0x20)`, `[0xFC,0x100)`, `[0x2E99C,0x2E9A0)` and
`[0x2EA7C,0x2EA80)` form a known difference bound with a found root cause:
official postbuild runs Combiner twice from zeroed CRC fields; NFC Replace
starts from the post-built reference. These differences are not a pass.
No Golden case, expected bytes, evidence rank or support claim changes here.
