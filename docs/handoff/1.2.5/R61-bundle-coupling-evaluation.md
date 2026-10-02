# R61: Built-in bundle coupling evaluation

Status: Final evaluation and owner-decision record; 2026-10-02; board decision 269.
Item: R61. R58 belongs to the Support Matrix hover fix (decision 253).
R62 is the automatic-check item, scheduled in 1.2.4 after R54 step 4 removes both NT51926 definitions.
Source recounted for this revision: the trunk at `7e321c6b4` (the same profiles, policy and Golden data as the
branch `feature/1.2.5/bundle-coupling-evaluation` it was written on). No implementation is authorized here.

Decision 267 requests this inventory and a proposed prevention rule; decision 266 already chooses removal of
Customized Replace from the NT51926 shared definitions, with evidence for the eight surviving CtrlRAM products.
Decision 269 records the three answers in section 5; this evaluation does not reopen decision 266. [D01, D10]

**Short glossary.** A **route** is one declared product choice: IC, workflow, IC Count and map.
A **fingerprint** is its reviewed definition's identity value, not a firmware-output checksum.
A **registration** connects a trusted profile to an active workflow.
A **pin** records an expected identity in policy, evidence or a test; **re-pin** means review and replace that record.
A **bundle** is an admitted package of definitions; a **family** is a file of firmware facts.
An **independently retirable feature** is functionality the owner can withdraw while preserving another feature.
That boundary is an owner decision, not something inferred from an IC name, filename or hash. [C01, C02, D02]

Source keys in brackets resolve to explicit `file:line` locations in section 6 and the inventory source tables.
Counts are static joins, not execution results. "Golden rows" means `routeEvidence` rows, including Contract Only;
it does not mean certified output cases. [C03]

## 1. Inventory and removal impact

### 1.1 Scope and counting method

The index and directory enumeration agree on **24 bundles**. Their manifests contain **124 entry occurrences**:
55 profiles, 24 family entries representing 19 distinct exact family identities, and 45 schemas.
There are 52 runtime registrations, 85 policy routes, 255 policy decisions and 85 Golden route-evidence rows.
The three unregistered profiles are the old NT51926 FW141 cascade and the two Desay AB profiles. [B01-B24, P01-P55, D03]

The twenty-fifth definition container is separate from those 24 manifest bundles:
`profiles/built-in/ctrlram-postbuild-v2/catalog.json` has 11 postbuild profiles for ten ICs.
Its selected-plan fact fingerprint serves all 54 CtrlRAM routes (44 local and ten bank routes).
That directory also holds hash-pinned `flash-map.json`. Its raw bytes/hash do not enter route identity:
local and bank producers bind the selected postbuild-plan fingerprint, whose writer takes catalog plan facts and
derived write sections, not the flash-map catalog. Flash-map admission, authoring and display uses remain separate.
This conclusion is from the inspected producers, not a fresh runtime fingerprint calculation. [C33]
Exact catalog-byte admission remains separate. This is an existing precedent for B's identity over used facts,
not an existing route-closure algorithm or a twenty-fifth trust-index bundle. [C18, C27, C28, D03]

Exactly **one bundle has multiple profile experience ids**: NT51926 CtrlRAM plus General Replace.
The assessment has three cases: the accepted NT51926 General Replace retirement, the planned Desay definition
retirement, and the unregistered NT51926 FW141 cascade profile. Desay format recognition is deliberately retained;
its dead definitions are a planned retirement, not a confirmed classification of the whole family as incidental.
The third case is incidental under section 2.1's rule; that classification is an evaluation judgment. [D01, D04, D07]
Nineteen bundles contain multiple profiles; they are all screened below, rather than assuming every same-workflow
sibling is one inseparable feature. Eight bundles' hashes feed identities used by multiple workflows, counting
Standard metadata/context counterparts and AB layout parents. That is a dependency count, not eight defects. [C04-C08]

An inline standard-library Python script parsed the index, every manifest and profile, the policy and the Golden
root manifest. It used exact `(profileId, profileVersion)` registration joins and exact policy axes/map membership.
CtrlRAM branch/count matching distinguished single, two-chip, three-chip and cascade definitions.
Ten AB bank Replace choices were joined separately to their AB layout and local CtrlRAM parents. [C04, C05, C07]
For Standard metadata/context dependencies it followed the same-IC counterpart declared in the index. [C06]

For each bundle, the script collected a set of policy route ids whose identity includes its hash either directly,
as a metadata/context binding, or through a bank definition. It counted matching Golden `routeId` rows and the
three actual decision objects. Counts are deduplicated within a row; bundle rows overlap and must not be summed.
The method assigns 75 primary routes and ten composite routes; all 85 policy rows are accounted for. [C04-C08, D03]

Schema entries are resolved from the index's materialization filenames under `docs/contracts/`.
Missing local family copies are resolved from `canonicalFirmwareFamily.source`; these are declarations of build
inputs, not missing bundles. Raw source SHA-256 and the canonical entry-array calculation matched all 124 entries
and all 24 bundle/index content hashes. No firmware file was read for that calculation. [C09, C10]

### 1.2 Table 1: every built-in bundle

Abbreviations: SM = `standard-merge`; AB = `ab-merge`; CR = `ctrlram-replace`;
GM = `general-merge`; GR = `general-replace`. Profile experiences and registration workflows are shown separately.
P keys enumerate every profile entry and resolve to its exact id/version, experience and source below.
F keys enumerate the exact family files. All schema filenames below are the actual materialization source filenames.
Their manifest destinations are `schemas/composition-profile-v2.schema.json` (CP) and
`schemas/firmware-family-v1.schema.json` (FF); shared-facts bundles contain FF only. [B01-B24]

"Primary / all" counts primary profile choices / every current identity binding the bundle hash.
"Golden / extra source" counts matching evidence rows / additional alias rows that name an affected source identity.
Extra source rows require evidence maintenance even though their own product identity need not change.
These source links are evidence dependencies, not inputs of the alias product's fingerprint. [C03, C04-C08]

| Bundle / source key | Entries: family; profiles; schema source files | Profile experiences / runtime registrations / unregistered profiles | Policy primary / all | Golden target / extra source |
| --- | --- | --- | ---: | ---: |
| B01 `nt51917-nt51927-general-merge-logical-candidate` | F01 `families/nt51927-nt51928.json`; profiles P01, P02; CP: `composition-profile-v2.5.schema.json`, FF: `firmware-family-v1.2-tp-header-subjects.schema.json` | Experiences GM; registrations P01: NT51917/GM; P02: NT51927/GM; unregistered none | 2 / 2 | 2 / 0 |
| B02 `nt51919-nt51929-nt51932-general-merge-logical-candidate` | F02 `families/nt51929-nt51932.json`; profiles P03, P04, P05; CP: `composition-profile-v2.5.schema.json`, FF: `firmware-family-v1.1-tp-header.schema.json` | Experiences GM; registrations P03: NT51919/GM; P04: NT51929/GM; P05: NT51932/GM; unregistered none | 3 / 3 | 3 / 0 |
| B03 `nt51923-nt51926-general-merge-logical-candidate` | F03 `families/nt51923-nt51926.json`; profiles P06, P07; CP: `composition-profile-v2.5.schema.json`, FF: `firmware-family-v1.2-tp-header-subjects.schema.json` | Experiences GM; registrations P06: NT51923/GM; P07: NT51926/GM; unregistered none | 2 / 2 | 2 / 0 |
| B04 `nt51928-general-merge-logical-candidate` | F01 `families/nt51927-nt51928.json`; profiles P08; CP: `composition-profile-v2.5.schema.json`, FF: `firmware-family-v1.2-tp-header-subjects.schema.json` | Experiences GM; registrations P08: NT51928/GM; unregistered none | 1 / 1 | 1 / 0 |
| B05 `nt51950-nt51951-general-merge-logical-candidate` | F04 `families/nt51950-nt51951-dp-perspective.json`; profiles P09, P10; CP: `composition-profile-v2.5.schema.json`, FF: `firmware-family-v1.3-full-image-metadata.schema.json` | Experiences GM; registrations P09: NT51950/GM; P10: NT51951/GM; unregistered none | 2 / 2 | 2 / 0 |
| B06 `nt51923-ctrlram-replace-candidate` | F05 `families/nt51923-ctrlram-replace.json`; profiles P11, P12; CP: `composition-profile-v2.9.schema.json`, FF: `firmware-family-v1-relations.schema.json` | Experiences CR; registrations P12: NT51923/CR; P11: NT51923/CR; unregistered none | 4 / 4 | 4 / 0 |
| B07 `nt51926-ctrlram-replace-candidate` | F06 `families/nt51926-ctrlram-replace.json`; profiles P13, P14, P15, P16, P17, P18; CP: `composition-profile-v2.9.schema.json`, FF: `firmware-family-v1-relations.schema.json` | Experiences CR, GR; registrations P16: NT51926/CR; P15: NT51926/CR; P17: NT51926/CR; P18: NT51926/CR; P14: NT51926/GR; unregistered P13 | 9 / 9 | 9 / 0 |
| B08 `nt51917-ctrlram-replace-alias-candidate` | F07 `families/nt51927-ctrlram-replace.json`; profiles P19, P20, P21; CP: `composition-profile-v2.9.schema.json`, FF: `firmware-family-v1-relations.schema.json` | Experiences CR; registrations P21: NT51917/CR; P19: NT51917/CR; P20: NT51917/CR; unregistered none | 6 / 6 | 6 / 0 |
| B09 `nt51927-ctrlram-replace-candidate` | F07 `families/nt51927-ctrlram-replace.json`; profiles P22, P23, P24; CP: `composition-profile-v2.9.schema.json`, FF: `firmware-family-v1-relations.schema.json` | Experiences CR; registrations P22: NT51927/CR; P23: NT51927/CR; P24: NT51927/CR; unregistered none | 6 / 6 | 6 / 2 |
| B10 `nt51928-ctrlram-replace-candidate` | F08 `families/nt51928-ctrlram-replace.json`; profiles P25, P26, P27; CP: `composition-profile-v2.9.schema.json`, FF: `firmware-family-v1-relations.schema.json` | Experiences CR; registrations P26: NT51928/CR; P25: NT51928/CR; P27: NT51928/CR; unregistered none | 6 / 6 | 6 / 0 |
| B11 `nt51929-ctrlram-replace-candidate` | F09 `families/nt51929-ctrlram-replace.json`; profiles P28, P29, P30, P31; CP: `composition-profile-v2.9.schema.json`, FF: `firmware-family-v1-relations.schema.json` | Experiences CR; registrations P28: NT51919/CR; P29: NT51919/CR; P30: NT51929/CR; P31: NT51929/CR; unregistered none | 4 / 8 | 8 / 0 |
| B12 `nt51932-ctrlram-replace-candidate` | F10 `families/nt51932-ctrlram-replace.json`; profiles P32, P33; CP: `composition-profile-v2.9.schema.json`, FF: `firmware-family-v1-relations.schema.json` | Experiences CR; registrations P32: NT51932/CR; P33: NT51932/CR; unregistered none | 2 / 4 | 4 / 0 |
| B13 `nt51950-ctrlram-replace-candidate` | F11 `families/nt51950-ctrlram-replace.json`; profiles P34, P35; CP: `composition-profile-v2.9.schema.json`, FF: `firmware-family-v1-relations.schema.json` | Experiences CR; registrations P34: NT51950/CR; P35: NT51950/CR; unregistered none | 4 / 6 | 6 / 0 |
| B14 `nt51951-ctrlram-replace-candidate` | F12 `families/nt51951-ctrlram-replace.json`; profiles P36, P37; CP: `composition-profile-v2.9.schema.json`, FF: `firmware-family-v1-relations.schema.json` | Experiences CR; registrations P36: NT51951/CR; P37: NT51951/CR; unregistered none | 4 / 6 | 6 / 2 |
| B15 `nt51923-standard-merge` | F03 `families/nt51923-nt51926.json`; profiles P38, P39; CP: `composition-profile-v2.15.schema.json`, FF: `firmware-family-v1.2-tp-header-subjects.schema.json` | Experiences SM; registrations P38: NT51923/SM; P39: NT51926/SM; unregistered none | 2 / 14 | 14 / 0 |
| B16 `nt51927-standard-merge` | F01 `families/nt51927-nt51928.json`; profiles P40, P41; CP: `composition-profile-v2.15.schema.json`, FF: `firmware-family-v1.2-tp-header-subjects.schema.json` | Experiences SM; registrations P40: NT51917/SM; P41: NT51927/SM; unregistered none | 2 / 14 | 14 / 0 |
| B17 `nt51928-standard-merge` | F13 `families/nt51927-nt51928-v1.5.json`; profiles P42; CP: `composition-profile-v2.15.schema.json`, FF: `firmware-family-v1.3-full-image-metadata.schema.json` | Experiences SM; registrations P42: NT51928/SM; unregistered none | 1 / 7 | 7 / 0 |
| B18 `nt51919-nt51929-nt51932-shared-facts` | F14 `families/nt51929-nt51932.json`; profiles none; FF: `firmware-family-v1.3-full-image-metadata.schema.json` | Experiences none; registrations none; unregistered none | 0 / 0 | 0 / 0 |
| B19 `nt51929-standard-merge` | F15 `families/nt51929-nt51932.json`; profiles P43, P44, P45; CP: `composition-profile-v2.15.schema.json`, FF: `firmware-family-v1.1-tp-header.schema.json` | Experiences SM; registrations P43: NT51919/SM; P44: NT51929/SM; P45: NT51932/SM; unregistered none | 3 / 11 | 11 / 0 |
| B20 `nt51919-nt51929-nt51932-ab-merge` | F16 `families/nt51919-nt51929-nt51932-ab-merge.json`; profiles P46, P47, P48; CP: `composition-profile-v2.17.schema.json`, FF: `firmware-family-v1.2-bank-instances.schema.json` | Experiences AB; registrations P46: NT51919/AB; P47: NT51929/AB; P48: NT51932/AB; unregistered none | 3 / 9 | 9 / 0 |
| B21 `nt51950-ab-merge` | F17 `families/nt51950-ab-merge.json`; profiles P49, P50, P51, P52, P53; CP: `composition-profile-v2.17.schema.json`, FF: `firmware-family-v1.2-ab-format.schema.json` | Experiences AB; registrations P49: NT51950/AB; P50: NT51951/AB; P53: NT51950/AB; unregistered P51, P52 | 3 / 7 | 7 / 0 |
| B22 `nt51950-nt51951-standard-merge` | F04 `families/nt51950-nt51951-dp-perspective.json`; profiles P54, P55; CP: `composition-profile-v2.16.schema.json`, FF: `firmware-family-v1.3-full-image-metadata.schema.json` | Experiences SM; registrations P54: NT51950/SM; P55: NT51951/SM; unregistered none | 6 / 18 | 18 / 0 |
| B23 `nt51923-nt51926-shared-facts` | F18 `families/nt51923-nt51926.json`; profiles none; FF: `firmware-family-v1.3-full-image-metadata.schema.json` | Experiences none; registrations none; unregistered none | 0 / 0 | 0 / 0 |
| B24 `nt51917-nt51927-shared-facts` | F19 `families/nt51927.json`; profiles none; FF: `firmware-family-v1.3-full-image-metadata.schema.json` | Experiences none; registrations none; unregistered none | 0 / 0 | 0 / 0 |

### 1.3 Table 2: feature removal exposure

Each P entry below is a candidate retirement unit, except the already decided General Replace and Desay retirements.
Decision 269 adds the unregistered NT51926 FW141 cascade profile's removal to R54 step 4.
The earlier R54 plan has not yet been amended; section 2.1 records its evidence-test consequence. [D09, D10]
The table screens every multi-profile bundle; shared and multi-workflow families follow in section 2.
The figures after each P key are **surviving identity values / policy decisions / Golden target rows** to review
after deleting that profile and withdrawing its own primary and bank-composite choices.
For an unregistered profile, no policy choice is withdrawn, but deleting its bytes still changes the whole bundle.
Deleting all members of a feature group uses the union of their withdrawn choices, not the sum of overlapping sets.
Test counts are exact textual occurrences of the current bundle hash, not numbers of tests. [C04-C08, T01]

This is an identity-change envelope, not permission to retire a profile. A surviving CtrlRAM product that needs a
removed Standard profile's metadata/context must first receive a reviewed replacement source; otherwise it must
be withdrawn too. Bank products lose admission if either parent disappears. The figures do not promise those
products remain executable. Family rewrites can additionally propagate through section 2's shared-fact graph. [C05-C08]

For NT51926, removing General Replace alone yields **8 / 24 / 8** survivor updates; removing the entire CtrlRAM
feature instead would leave **1 / 3 / 1** General Replace update, before its separately required retirement.
For Desay, retiring both unregistered profiles, the three Desay-only maps and their exclusive seed-anchor region set
yields **7 / 21 / 7**; these include three AB Merge and four bank CtrlRAM choices. One re-registration of all seven
is the derived batching consequence of one frozen R10-04 edit, not a separately recorded owner decision.
The family is not split and Desay format recognition stays. [B21, F17, D04, D07, C05]
Case 3, deleting P13 alone today, changes **9 / 27 / 9**. Deleting it together with R54 changes the same eight surviving
CtrlRAM routes once and adds **zero** re-registrations; deleting it after R54 costs **8 / 24 / 8** again. [B07, D01,
D09]

NT51927 CR and NT51951 CR each also have two alias-evidence rows that name their identities as sources.
Those rows are listed separately in Table 1 and need reassessment when the referenced source changes. [D03]
The earlier DP retirement removed five exclusive bundles and ten profiles while retaining 24 survivor bundles'
definition bytes and 79 survivor identities; that recorded boundary explains why package separation avoided the
NT51926 problem. It does not prove future unchanged-byte behavior. [D05]

| Bundle | Feature/profile deletions: remaining identities / decisions / Golden rows | Shared versus incidental assessment | Bundle-hash literals |
| --- | --- | --- | ---: |
| B01 | P01: 1 / 3 / 1; P02: 1 / 3 / 1 | Same GM experience; separate IC profiles. Common family deliberate; independent IC retirement is an owner question. | 2 (T01) |
| B02 | P03: 2 / 6 / 2; P04: 2 / 6 / 2; P05: 2 / 6 / 2 | Same GM experience; separate IC profiles. Common family deliberate; independent IC retirement is an owner question. | 3 (T01) |
| B03 | P06: 1 / 3 / 1; P07: 1 / 3 / 1 | Same GM experience; separate IC profiles. Common family deliberate; independent IC retirement is an owner question. | 2 (T01) |
| B05 | P09: 1 / 3 / 1; P10: 1 / 3 / 1 | Same GM experience; separate IC profiles. Common family deliberate; independent IC retirement is an owner question. | 3 (T01) |
| B06 | P11: 2 / 6 / 2; P12: 2 / 6 / 2 | CR branch/revision or IC/alias profiles share a family. Used facts deliberate; exclusive branch/map retirement is an owner question. | 1 (T01) |
| B07 | P13: 9 / 27 / 9; P14: 8 / 24 / 8; P15: 7 / 21 / 7; P16: 7 / 21 / 7; P17: 7 / 21 / 7; P18: 7 / 21 / 7 | Accepted GR retirement vs CR: P14 removal is 8 / 24 / 8. Case 3: unregistered P13 is incidental by the assessment rule; decision 269 combines removal with R54 for zero incremental re-registrations; active Golden-cited parity comparator, see case 3. FW141/FW200 facts are shared. | 2 (T01) |
| B08 | P19: 4 / 12 / 4; P20: 4 / 12 / 4; P21: 4 / 12 / 4 | CR branch/revision or IC/alias profiles share a family. Used facts deliberate; exclusive branch/map retirement is an owner question. | 1 (T01) |
| B09 | P22: 4 / 12 / 4; P23: 4 / 12 / 4; P24: 4 / 12 / 4 | CR branch/revision or IC/alias profiles share a family. Used facts deliberate; exclusive branch/map retirement is an owner question. | 1 (T01) |
| B10 | P25: 4 / 12 / 4; P26: 4 / 12 / 4; P27: 4 / 12 / 4 | CR branch/revision or IC/alias profiles share a family. Used facts deliberate; exclusive branch/map retirement is an owner question. | 1 (T01) |
| B11 | P28: 6 / 18 / 6; P29: 6 / 18 / 6; P30: 6 / 18 / 6; P31: 6 / 18 / 6 | CR branch/revision or IC/alias profiles share a family. Used facts deliberate; exclusive branch/map retirement is an owner question. | 3 (T01) |
| B12 | P32: 2 / 6 / 2; P33: 2 / 6 / 2 | CR branch/revision or IC/alias profiles share a family. Used facts deliberate; exclusive branch/map retirement is an owner question. | 1 (T01) |
| B13 | P34: 3 / 9 / 3; P35: 3 / 9 / 3 | CR branch/revision or IC/alias profiles share a family. Used facts deliberate; exclusive branch/map retirement is an owner question. | 1 (T01) |
| B14 | P36: 3 / 9 / 3; P37: 3 / 9 / 3 | CR branch/revision or IC/alias profiles share a family. Used facts deliberate; exclusive branch/map retirement is an owner question. | 1 (T01) |
| B15 | P38: 13 / 39 / 13; P39: 13 / 39 / 13 | Separate IC/alias profiles, plus CR metadata/context consumers. Used facts deliberate; sibling profile lifetime is an owner question. | 5 (T01) |
| B16 | P40: 13 / 39 / 13; P41: 13 / 39 / 13 | Separate IC/alias profiles, plus CR metadata/context consumers. Used facts deliberate; sibling profile lifetime is an owner question. | 5 (T01) |
| B17 | One SM profile plus CR counterpart: whole-family/map-set edit exposes 7 / 21 / 7 current rows before any withdrawal. | Deliberate cross-workflow context/report source; capacity alternatives are jointly reviewed, not assumed independently retirable. | 4 (T01) |
| B19 | P43: 10 / 30 / 10; P44: 10 / 30 / 10; P45: 10 / 30 / 10 | Separate IC/alias profiles, plus CR metadata/context consumers. Used facts deliberate; sibling profile lifetime is an owner question. | 8 (T01) |
| B20 | P46: 6 / 18 / 6; P47: 6 / 18 / 6; P48: 6 / 18 / 6 | AB and bank CR deliberately share layouts; separate IC/alias profiles are potential retirement units. | 8 (T01) |
| B21 | P49: 5 / 15 / 5; P50: 4 / 12 / 4; P51: 7 / 21 / 7; P52: 7 / 21 / 7; P53: 5 / 15 / 5 | Planned R10-04 retirement: two Desay profiles, three dead maps and their exclusive seed-anchor region set; family not split, format recognition retained. The dead-definition classification is an evaluation judgment, not a verified family-wide defect. One seven-product re-registration is a derived batching consequence, not a separate decision. D04/D07. | 8 (T01) |
| B22 | P54: 15 / 45 / 15; P55: 15 / 45 / 15 | Separate IC/alias profiles, plus CR metadata/context consumers. Used facts deliberate; sibling profile lifetime is an owner question. | 3 (T01) |

### 1.4 Inventory source keys

Each B key cites the manifest entry array and its trust-index item. Policy/Golden columns cite a member of the
counted set; full set membership is calculated from the arrays in D03, using section 6.2.

| Key | Bundle manifest / index | Policy / Golden target sample |
| --- | --- | --- |
| B01 | `profiles/built-in/nt51917-nt51927-general-merge-logical-candidate/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:8` | `docs/contracts/canonical-capability-policy-v1.json:186`; `testdata/golden/canonical/manifest.json:202` |
| B02 | `profiles/built-in/nt51919-nt51929-nt51932-general-merge-logical-candidate/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:38` | `docs/contracts/canonical-capability-policy-v1.json:331`; `testdata/golden/canonical/manifest.json:253` |
| B03 | `profiles/built-in/nt51923-nt51926-general-merge-logical-candidate/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:71` | `docs/contracts/canonical-capability-policy-v1.json:505`; `testdata/golden/canonical/manifest.json:319` |
| B04 | `profiles/built-in/nt51928-general-merge-logical-candidate/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:101` | `docs/contracts/canonical-capability-policy-v1.json:1288`; `testdata/golden/canonical/manifest.json:551` |
| B05 | `profiles/built-in/nt51950-nt51951-general-merge-logical-candidate/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:124` | `docs/contracts/canonical-capability-policy-v1.json:1839`; `testdata/golden/canonical/manifest.json:702` |
| B06 | `profiles/built-in/nt51923-ctrlram-replace-candidate/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:154` | `docs/contracts/canonical-capability-policy-v1.json:389`; `testdata/golden/canonical/manifest.json:275` |
| B07 | `profiles/built-in/nt51926-ctrlram-replace-candidate/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:184` | `docs/contracts/canonical-capability-policy-v1.json:563`; `testdata/golden/canonical/manifest.json:334` |
| B08 | `profiles/built-in/nt51917-ctrlram-replace-alias-candidate/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:238` | `docs/contracts/canonical-capability-policy-v1.json:12`; `testdata/golden/canonical/manifest.json:144` |
| B09 | `profiles/built-in/nt51927-ctrlram-replace-candidate/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:281` | `docs/contracts/canonical-capability-policy-v1.json:882`; `testdata/golden/canonical/manifest.json:444` |
| B10 | `profiles/built-in/nt51928-ctrlram-replace-candidate/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:320` | `docs/contracts/canonical-capability-policy-v1.json:1114`; `testdata/golden/canonical/manifest.json:509` |
| B11 | `profiles/built-in/nt51929-ctrlram-replace-candidate/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:359` | `docs/contracts/canonical-capability-policy-v1.json:273`; `testdata/golden/canonical/manifest.json:231` |
| B12 | `profiles/built-in/nt51932-ctrlram-replace-candidate/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:407` | `docs/contracts/canonical-capability-policy-v1.json:1549`; `testdata/golden/canonical/manifest.json:609` |
| B13 | `profiles/built-in/nt51950-ctrlram-replace-candidate/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:437` | `docs/contracts/canonical-capability-policy-v1.json:1723`; `testdata/golden/canonical/manifest.json:653` |
| B14 | `profiles/built-in/nt51951-ctrlram-replace-candidate/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:467` | `docs/contracts/canonical-capability-policy-v1.json:1984`; `testdata/golden/canonical/manifest.json:742` |
| B15 | `profiles/built-in/nt51923-standard-merge/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:497` | `docs/contracts/canonical-capability-policy-v1.json:389`; `testdata/golden/canonical/manifest.json:275` |
| B16 | `profiles/built-in/nt51927-standard-merge/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:527` | `docs/contracts/canonical-capability-policy-v1.json:12`; `testdata/golden/canonical/manifest.json:144` |
| B17 | `profiles/built-in/nt51928-standard-merge/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:557` | `docs/contracts/canonical-capability-policy-v1.json:1114`; `testdata/golden/canonical/manifest.json:509` |
| B18 | `profiles/built-in/nt51919-nt51929-nt51932-shared-facts/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:582` | No policy target; metadata-provider declaration `profiles/built-in/package-trust-index.json:582` |
| B19 | `profiles/built-in/nt51929-standard-merge/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:605` | `docs/contracts/canonical-capability-policy-v1.json:273`; `testdata/golden/canonical/manifest.json:231` |
| B20 | `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:641` | `docs/contracts/canonical-capability-policy-v1.json:244`; `testdata/golden/canonical/manifest.json:224` |
| B21 | `profiles/built-in/nt51950-ab-merge/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:674` | `docs/contracts/canonical-capability-policy-v1.json:1665`; `testdata/golden/canonical/manifest.json:639` |
| B22 | `profiles/built-in/nt51950-nt51951-standard-merge/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:707` | `docs/contracts/canonical-capability-policy-v1.json:1723`; `testdata/golden/canonical/manifest.json:653` |
| B23 | `profiles/built-in/nt51923-nt51926-shared-facts/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:737` | No policy target; metadata-provider declaration `profiles/built-in/package-trust-index.json:737` |
| B24 | `profiles/built-in/nt51917-nt51927-shared-facts/profile-bundle.json:8`; `profiles/built-in/package-trust-index.json:754` | No policy target; metadata-provider declaration `profiles/built-in/package-trust-index.json:754` |

Profile source keys enumerate every profile entry, including unregistered ones. Profile paths also resolve the
entry filenames listed in the manifests; versions and experiences below are read from these files.

| Key / bundle | Exact profile id / version | Experience / family binding | Data source |
| --- | --- | --- | --- |
| P01 / B01 | `nt51917-general-merge-logical-candidate@0.1.0` | GM / F01 | `profiles/built-in/nt51917-nt51927-general-merge-logical-candidate/profiles/nt51917-general-merge-logical-candidate.json:3`; `profiles/built-in/nt51917-nt51927-general-merge-logical-candidate/profiles/nt51917-general-merge-logical-candidate.json:28`; `profiles/built-in/nt51917-nt51927-general-merge-logical-candidate/profiles/nt51917-general-merge-logical-candidate.json:41` |
| P02 / B01 | `nt51927-general-merge-logical-candidate@0.1.0` | GM / F01 | `profiles/built-in/nt51917-nt51927-general-merge-logical-candidate/profiles/nt51927-general-merge-logical-candidate.json:3`; `profiles/built-in/nt51917-nt51927-general-merge-logical-candidate/profiles/nt51927-general-merge-logical-candidate.json:28`; `profiles/built-in/nt51917-nt51927-general-merge-logical-candidate/profiles/nt51927-general-merge-logical-candidate.json:41` |
| P03 / B02 | `nt51919-general-merge-logical-candidate@0.1.0` | GM / F02 | `profiles/built-in/nt51919-nt51929-nt51932-general-merge-logical-candidate/profiles/nt51919-general-merge-logical-candidate.json:3`; `profiles/built-in/nt51919-nt51929-nt51932-general-merge-logical-candidate/profiles/nt51919-general-merge-logical-candidate.json:28`; `profiles/built-in/nt51919-nt51929-nt51932-general-merge-logical-candidate/profiles/nt51919-general-merge-logical-candidate.json:41` |
| P04 / B02 | `nt51929-general-merge-logical-candidate@0.1.0` | GM / F02 | `profiles/built-in/nt51919-nt51929-nt51932-general-merge-logical-candidate/profiles/nt51929-general-merge-logical-candidate.json:3`; `profiles/built-in/nt51919-nt51929-nt51932-general-merge-logical-candidate/profiles/nt51929-general-merge-logical-candidate.json:28`; `profiles/built-in/nt51919-nt51929-nt51932-general-merge-logical-candidate/profiles/nt51929-general-merge-logical-candidate.json:41` |
| P05 / B02 | `nt51932-general-merge-logical-candidate@0.1.0` | GM / F02 | `profiles/built-in/nt51919-nt51929-nt51932-general-merge-logical-candidate/profiles/nt51932-general-merge-logical-candidate.json:3`; `profiles/built-in/nt51919-nt51929-nt51932-general-merge-logical-candidate/profiles/nt51932-general-merge-logical-candidate.json:28`; `profiles/built-in/nt51919-nt51929-nt51932-general-merge-logical-candidate/profiles/nt51932-general-merge-logical-candidate.json:41` |
| P06 / B03 | `nt51923-general-merge-logical-candidate@0.1.0` | GM / F03 | `profiles/built-in/nt51923-nt51926-general-merge-logical-candidate/profiles/nt51923-general-merge-logical-candidate.json:3`; `profiles/built-in/nt51923-nt51926-general-merge-logical-candidate/profiles/nt51923-general-merge-logical-candidate.json:28`; `profiles/built-in/nt51923-nt51926-general-merge-logical-candidate/profiles/nt51923-general-merge-logical-candidate.json:41` |
| P07 / B03 | `nt51926-general-merge-logical-candidate@0.1.0` | GM / F03 | `profiles/built-in/nt51923-nt51926-general-merge-logical-candidate/profiles/nt51926-general-merge-logical-candidate.json:3`; `profiles/built-in/nt51923-nt51926-general-merge-logical-candidate/profiles/nt51926-general-merge-logical-candidate.json:28`; `profiles/built-in/nt51923-nt51926-general-merge-logical-candidate/profiles/nt51926-general-merge-logical-candidate.json:41` |
| P08 / B04 | `nt51928-general-merge-logical-candidate@0.1.0` | GM / F01 | `profiles/built-in/nt51928-general-merge-logical-candidate/profiles/nt51928-general-merge-logical-candidate.json:3`; `profiles/built-in/nt51928-general-merge-logical-candidate/profiles/nt51928-general-merge-logical-candidate.json:28`; `profiles/built-in/nt51928-general-merge-logical-candidate/profiles/nt51928-general-merge-logical-candidate.json:41` |
| P09 / B05 | `nt51950-general-merge-logical-candidate@0.1.0` | GM / F04 | `profiles/built-in/nt51950-nt51951-general-merge-logical-candidate/profiles/nt51950-general-merge-logical-candidate.json:3`; `profiles/built-in/nt51950-nt51951-general-merge-logical-candidate/profiles/nt51950-general-merge-logical-candidate.json:28`; `profiles/built-in/nt51950-nt51951-general-merge-logical-candidate/profiles/nt51950-general-merge-logical-candidate.json:41` |
| P10 / B05 | `nt51951-general-merge-logical-candidate@0.1.0` | GM / F04 | `profiles/built-in/nt51950-nt51951-general-merge-logical-candidate/profiles/nt51951-general-merge-logical-candidate.json:3`; `profiles/built-in/nt51950-nt51951-general-merge-logical-candidate/profiles/nt51951-general-merge-logical-candidate.json:28`; `profiles/built-in/nt51950-nt51951-general-merge-logical-candidate/profiles/nt51951-general-merge-logical-candidate.json:41` |
| P11 / B06 | `nt51923-ctrlram-replace-fw141-cascade3@0.4.0` | CR / F05 | `profiles/built-in/nt51923-ctrlram-replace-candidate/profiles/nt51923-ctrlram-replace-fw141-cascade3.json:3`; `profiles/built-in/nt51923-ctrlram-replace-candidate/profiles/nt51923-ctrlram-replace-fw141-cascade3.json:9`; `profiles/built-in/nt51923-ctrlram-replace-candidate/profiles/nt51923-ctrlram-replace-fw141-cascade3.json:22` |
| P12 / B06 | `nt51923-ctrlram-replace-fw141-single@0.4.0` | CR / F05 | `profiles/built-in/nt51923-ctrlram-replace-candidate/profiles/nt51923-ctrlram-replace-fw141-single.json:3`; `profiles/built-in/nt51923-ctrlram-replace-candidate/profiles/nt51923-ctrlram-replace-fw141-single.json:9`; `profiles/built-in/nt51923-ctrlram-replace-candidate/profiles/nt51923-ctrlram-replace-fw141-single.json:22` |
| P13 / B07 | `nt51926-ctrlram-replace-fw141-cascade@0.7.0` | CR / F06 | `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-cascade.json:3`; `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-cascade.json:9`; `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-cascade.json:22` |
| P14 / B07 | `nt51926-general-replace-dp-single-candidate@0.1.0` | GR / F06 | `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-general-replace-dp-single-candidate.json:3`; `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-general-replace-dp-single-candidate.json:25`; `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-general-replace-dp-single-candidate.json:38` |
| P15 / B07 | `nt51926-ctrlram-replace-fw141-runtime-cascade@0.4.0` | CR / F06 | `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-runtime-cascade.json:3`; `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-runtime-cascade.json:9`; `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-runtime-cascade.json:20` |
| P16 / B07 | `nt51926-ctrlram-replace-fw141-runtime-single@0.4.0` | CR / F06 | `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-runtime-single.json:3`; `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-runtime-single.json:9`; `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-runtime-single.json:20` |
| P17 / B07 | `nt51926-ctrlram-replace-fw200-runtime-single@0.4.0` | CR / F06 | `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw200-runtime-single.json:3`; `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw200-runtime-single.json:9`; `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw200-runtime-single.json:20` |
| P18 / B07 | `nt51926-ctrlram-replace-fw200-runtime-cascade@0.4.0` | CR / F06 | `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw200-runtime-cascade.json:3`; `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw200-runtime-cascade.json:9`; `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw200-runtime-cascade.json:20` |
| P19 / B08 | `nt51917-ctrlram-replace-fw132-twochip@0.3.0` | CR / F07 | `profiles/built-in/nt51917-ctrlram-replace-alias-candidate/profiles/nt51917-ctrlram-replace-fw132-twochip.json:3`; `profiles/built-in/nt51917-ctrlram-replace-alias-candidate/profiles/nt51917-ctrlram-replace-fw132-twochip.json:9`; `profiles/built-in/nt51917-ctrlram-replace-alias-candidate/profiles/nt51917-ctrlram-replace-fw132-twochip.json:20` |
| P20 / B08 | `nt51917-ctrlram-replace-fw140-threechip@0.3.0` | CR / F07 | `profiles/built-in/nt51917-ctrlram-replace-alias-candidate/profiles/nt51917-ctrlram-replace-fw140-threechip.json:3`; `profiles/built-in/nt51917-ctrlram-replace-alias-candidate/profiles/nt51917-ctrlram-replace-fw140-threechip.json:9`; `profiles/built-in/nt51917-ctrlram-replace-alias-candidate/profiles/nt51917-ctrlram-replace-fw140-threechip.json:20` |
| P21 / B08 | `nt51917-ctrlram-replace-fw141-single@0.3.0` | CR / F07 | `profiles/built-in/nt51917-ctrlram-replace-alias-candidate/profiles/nt51917-ctrlram-replace-fw141-single.json:3`; `profiles/built-in/nt51917-ctrlram-replace-alias-candidate/profiles/nt51917-ctrlram-replace-fw141-single.json:9`; `profiles/built-in/nt51917-ctrlram-replace-alias-candidate/profiles/nt51917-ctrlram-replace-fw141-single.json:20` |
| P22 / B09 | `nt51927-ctrlram-replace-fw141-single@0.3.0` | CR / F07 | `profiles/built-in/nt51927-ctrlram-replace-candidate/profiles/nt51927-ctrlram-replace-fw141-single.json:3`; `profiles/built-in/nt51927-ctrlram-replace-candidate/profiles/nt51927-ctrlram-replace-fw141-single.json:9`; `profiles/built-in/nt51927-ctrlram-replace-candidate/profiles/nt51927-ctrlram-replace-fw141-single.json:20` |
| P23 / B09 | `nt51927-ctrlram-replace-fw132-twochip@0.3.0` | CR / F07 | `profiles/built-in/nt51927-ctrlram-replace-candidate/profiles/nt51927-ctrlram-replace-fw132-twochip.json:3`; `profiles/built-in/nt51927-ctrlram-replace-candidate/profiles/nt51927-ctrlram-replace-fw132-twochip.json:9`; `profiles/built-in/nt51927-ctrlram-replace-candidate/profiles/nt51927-ctrlram-replace-fw132-twochip.json:20` |
| P24 / B09 | `nt51927-ctrlram-replace-fw140-threechip@0.3.0` | CR / F07 | `profiles/built-in/nt51927-ctrlram-replace-candidate/profiles/nt51927-ctrlram-replace-fw140-threechip.json:3`; `profiles/built-in/nt51927-ctrlram-replace-candidate/profiles/nt51927-ctrlram-replace-fw140-threechip.json:9`; `profiles/built-in/nt51927-ctrlram-replace-candidate/profiles/nt51927-ctrlram-replace-fw140-threechip.json:20` |
| P25 / B10 | `nt51928-ctrlram-replace-fw141-single@0.4.0` | CR / F08 | `profiles/built-in/nt51928-ctrlram-replace-candidate/profiles/nt51928-ctrlram-replace-fw141-single.json:3`; `profiles/built-in/nt51928-ctrlram-replace-candidate/profiles/nt51928-ctrlram-replace-fw141-single.json:9`; `profiles/built-in/nt51928-ctrlram-replace-candidate/profiles/nt51928-ctrlram-replace-fw141-single.json:22` |
| P26 / B10 | `nt51928-ctrlram-replace-fw132-twochip@0.3.0` | CR / F08 | `profiles/built-in/nt51928-ctrlram-replace-candidate/profiles/nt51928-ctrlram-replace-fw132-twochip.json:3`; `profiles/built-in/nt51928-ctrlram-replace-candidate/profiles/nt51928-ctrlram-replace-fw132-twochip.json:9`; `profiles/built-in/nt51928-ctrlram-replace-candidate/profiles/nt51928-ctrlram-replace-fw132-twochip.json:22` |
| P27 / B10 | `nt51928-ctrlram-replace-fw140-threechip@0.4.0` | CR / F08 | `profiles/built-in/nt51928-ctrlram-replace-candidate/profiles/nt51928-ctrlram-replace-fw140-threechip.json:3`; `profiles/built-in/nt51928-ctrlram-replace-candidate/profiles/nt51928-ctrlram-replace-fw140-threechip.json:9`; `profiles/built-in/nt51928-ctrlram-replace-candidate/profiles/nt51928-ctrlram-replace-fw140-threechip.json:22` |
| P28 / B11 | `nt51919-ctrlram-replace-fw200-single@0.3.0` | CR / F09 | `profiles/built-in/nt51929-ctrlram-replace-candidate/profiles/nt51919-ctrlram-replace-fw200-single.json:3`; `profiles/built-in/nt51929-ctrlram-replace-candidate/profiles/nt51919-ctrlram-replace-fw200-single.json:9`; `profiles/built-in/nt51929-ctrlram-replace-candidate/profiles/nt51919-ctrlram-replace-fw200-single.json:20` |
| P29 / B11 | `nt51919-ctrlram-replace-fw1x-cascade@0.5.0` | CR / F09 | `profiles/built-in/nt51929-ctrlram-replace-candidate/profiles/nt51919-ctrlram-replace-fw1x-cascade.json:3`; `profiles/built-in/nt51929-ctrlram-replace-candidate/profiles/nt51919-ctrlram-replace-fw1x-cascade.json:9`; `profiles/built-in/nt51929-ctrlram-replace-candidate/profiles/nt51919-ctrlram-replace-fw1x-cascade.json:20` |
| P30 / B11 | `nt51929-ctrlram-replace-fw200-single@0.3.0` | CR / F09 | `profiles/built-in/nt51929-ctrlram-replace-candidate/profiles/nt51929-ctrlram-replace-fw200-single.json:3`; `profiles/built-in/nt51929-ctrlram-replace-candidate/profiles/nt51929-ctrlram-replace-fw200-single.json:9`; `profiles/built-in/nt51929-ctrlram-replace-candidate/profiles/nt51929-ctrlram-replace-fw200-single.json:20` |
| P31 / B11 | `nt51929-ctrlram-replace-fw1x-cascade@0.5.0` | CR / F09 | `profiles/built-in/nt51929-ctrlram-replace-candidate/profiles/nt51929-ctrlram-replace-fw1x-cascade.json:3`; `profiles/built-in/nt51929-ctrlram-replace-candidate/profiles/nt51929-ctrlram-replace-fw1x-cascade.json:9`; `profiles/built-in/nt51929-ctrlram-replace-candidate/profiles/nt51929-ctrlram-replace-fw1x-cascade.json:20` |
| P32 / B12 | `nt51932-ctrlram-replace-fw200-cascade@0.5.0` | CR / F10 | `profiles/built-in/nt51932-ctrlram-replace-candidate/profiles/nt51932-ctrlram-replace-fw200-cascade.json:3`; `profiles/built-in/nt51932-ctrlram-replace-candidate/profiles/nt51932-ctrlram-replace-fw200-cascade.json:9`; `profiles/built-in/nt51932-ctrlram-replace-candidate/profiles/nt51932-ctrlram-replace-fw200-cascade.json:20` |
| P33 / B12 | `nt51932-ctrlram-replace-fw1x-single@0.3.0` | CR / F10 | `profiles/built-in/nt51932-ctrlram-replace-candidate/profiles/nt51932-ctrlram-replace-fw1x-single.json:3`; `profiles/built-in/nt51932-ctrlram-replace-candidate/profiles/nt51932-ctrlram-replace-fw1x-single.json:9`; `profiles/built-in/nt51932-ctrlram-replace-candidate/profiles/nt51932-ctrlram-replace-fw1x-single.json:22` |
| P34 / B13 | `nt51950-ctrlram-replace-fw200-single@0.5.0` | CR / F11 | `profiles/built-in/nt51950-ctrlram-replace-candidate/profiles/nt51950-ctrlram-replace-fw200-single.json:3`; `profiles/built-in/nt51950-ctrlram-replace-candidate/profiles/nt51950-ctrlram-replace-fw200-single.json:9`; `profiles/built-in/nt51950-ctrlram-replace-candidate/profiles/nt51950-ctrlram-replace-fw200-single.json:20` |
| P35 / B13 | `nt51950-ctrlram-replace-fw1x-cascade@0.7.0` | CR / F11 | `profiles/built-in/nt51950-ctrlram-replace-candidate/profiles/nt51950-ctrlram-replace-fw1x-cascade.json:3`; `profiles/built-in/nt51950-ctrlram-replace-candidate/profiles/nt51950-ctrlram-replace-fw1x-cascade.json:9`; `profiles/built-in/nt51950-ctrlram-replace-candidate/profiles/nt51950-ctrlram-replace-fw1x-cascade.json:20` |
| P36 / B14 | `nt51951-ctrlram-replace-fw200-single@0.5.0` | CR / F12 | `profiles/built-in/nt51951-ctrlram-replace-candidate/profiles/nt51951-ctrlram-replace-fw200-single.json:3`; `profiles/built-in/nt51951-ctrlram-replace-candidate/profiles/nt51951-ctrlram-replace-fw200-single.json:9`; `profiles/built-in/nt51951-ctrlram-replace-candidate/profiles/nt51951-ctrlram-replace-fw200-single.json:20` |
| P37 / B14 | `nt51951-ctrlram-replace-fw1x-cascade@0.7.0` | CR / F12 | `profiles/built-in/nt51951-ctrlram-replace-candidate/profiles/nt51951-ctrlram-replace-fw1x-cascade.json:3`; `profiles/built-in/nt51951-ctrlram-replace-candidate/profiles/nt51951-ctrlram-replace-fw1x-cascade.json:9`; `profiles/built-in/nt51951-ctrlram-replace-candidate/profiles/nt51951-ctrlram-replace-fw1x-cascade.json:20` |
| P38 / B15 | `nt51923-standard-merge-gen-flash@0.6.0` | SM / F03 | `profiles/built-in/nt51923-standard-merge/profiles/nt51923-standard-merge.json:3`; `profiles/built-in/nt51923-standard-merge/profiles/nt51923-standard-merge.json:14`; `profiles/built-in/nt51923-standard-merge/profiles/nt51923-standard-merge.json:24` |
| P39 / B15 | `nt51926-standard-merge-gen-flash@0.6.0` | SM / F03 | `profiles/built-in/nt51923-standard-merge/profiles/nt51926-standard-merge.json:3`; `profiles/built-in/nt51923-standard-merge/profiles/nt51926-standard-merge.json:14`; `profiles/built-in/nt51923-standard-merge/profiles/nt51926-standard-merge.json:24` |
| P40 / B16 | `nt51917-standard-merge-gen-flash-alias@0.7.0` | SM / F01 | `profiles/built-in/nt51927-standard-merge/profiles/nt51917-standard-merge.json:3`; `profiles/built-in/nt51927-standard-merge/profiles/nt51917-standard-merge.json:14`; `profiles/built-in/nt51927-standard-merge/profiles/nt51917-standard-merge.json:24` |
| P41 / B16 | `nt51927-standard-merge-gen-flash@0.8.1` | SM / F01 | `profiles/built-in/nt51927-standard-merge/profiles/nt51927-standard-merge.json:3`; `profiles/built-in/nt51927-standard-merge/profiles/nt51927-standard-merge.json:14`; `profiles/built-in/nt51927-standard-merge/profiles/nt51927-standard-merge.json:24` |
| P42 / B17 | `nt51928-standard-merge-gen-flash@0.9.1` | SM / F13 | `profiles/built-in/nt51928-standard-merge/profiles/nt51928-standard-merge.json:3`; `profiles/built-in/nt51928-standard-merge/profiles/nt51928-standard-merge.json:14`; `profiles/built-in/nt51928-standard-merge/profiles/nt51928-standard-merge.json:24` |
| P43 / B19 | `nt51919-standard-merge-gen-flash-alias@0.7.0` | SM / F15 | `profiles/built-in/nt51929-standard-merge/profiles/nt51919-standard-merge.json:3`; `profiles/built-in/nt51929-standard-merge/profiles/nt51919-standard-merge.json:14`; `profiles/built-in/nt51929-standard-merge/profiles/nt51919-standard-merge.json:24` |
| P44 / B19 | `nt51929-standard-merge-gen-flash@0.7.0` | SM / F15 | `profiles/built-in/nt51929-standard-merge/profiles/nt51929-standard-merge.json:3`; `profiles/built-in/nt51929-standard-merge/profiles/nt51929-standard-merge.json:14`; `profiles/built-in/nt51929-standard-merge/profiles/nt51929-standard-merge.json:24` |
| P45 / B19 | `nt51932-standard-merge-gen-flash@0.7.0` | SM / F15 | `profiles/built-in/nt51929-standard-merge/profiles/nt51932-standard-merge.json:3`; `profiles/built-in/nt51929-standard-merge/profiles/nt51932-standard-merge.json:14`; `profiles/built-in/nt51929-standard-merge/profiles/nt51932-standard-merge.json:24` |
| P46 / B20 | `nt51919-ab-merge-alias@0.4.0` | AB / F16 | `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51919-ab-merge.json:4`; `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51919-ab-merge.json:13`; `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51919-ab-merge.json:23` |
| P47 / B20 | `nt51929-ab-merge@0.4.0` | AB / F16 | `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51929-ab-merge.json:4`; `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51929-ab-merge.json:15`; `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51929-ab-merge.json:25` |
| P48 / B20 | `nt51932-ab-merge@0.4.0` | AB / F16 | `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51932-ab-merge.json:4`; `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51932-ab-merge.json:13`; `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51932-ab-merge.json:23` |
| P49 / B21 | `nt51950-ab-merge@0.8.0` | AB / F17 | `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge.json:4`; `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge.json:34`; `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge.json:44` |
| P50 / B21 | `nt51951-ab-merge@0.7.0` | AB / F17 | `profiles/built-in/nt51950-ab-merge/profiles/nt51951-ab-merge.json:4`; `profiles/built-in/nt51950-ab-merge/profiles/nt51951-ab-merge.json:23`; `profiles/built-in/nt51950-ab-merge/profiles/nt51951-ab-merge.json:27` |
| P51 / B21 | `nt51950-ab-merge-desay@0.2.1` | AB / F17 | `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge-desay.json:4`; `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge-desay.json:26`; `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge-desay.json:36` |
| P52 / B21 | `nt51951-ab-merge-desay@0.2.1` | AB / F17 | `profiles/built-in/nt51950-ab-merge/profiles/nt51951-ab-merge-desay.json:4`; `profiles/built-in/nt51950-ab-merge/profiles/nt51951-ab-merge-desay.json:26`; `profiles/built-in/nt51950-ab-merge/profiles/nt51951-ab-merge-desay.json:36` |
| P53 / B21 | `nt51950-ab-merge-cascade@0.4.0` | AB / F17 | `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge-cascade.json:4`; `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge-cascade.json:34`; `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge-cascade.json:44` |
| P54 / B22 | `nt51950-standard-merge-dp-perspective@0.8.0` | SM / F04 | `profiles/built-in/nt51950-nt51951-standard-merge/profiles/nt51950-standard-merge.json:4`; `profiles/built-in/nt51950-nt51951-standard-merge/profiles/nt51950-standard-merge.json:12`; `profiles/built-in/nt51950-nt51951-standard-merge/profiles/nt51950-standard-merge.json:22` |
| P55 / B22 | `nt51951-standard-merge-dp-perspective@0.8.0` | SM / F04 | `profiles/built-in/nt51950-nt51951-standard-merge/profiles/nt51951-standard-merge.json:4`; `profiles/built-in/nt51950-nt51951-standard-merge/profiles/nt51951-standard-merge.json:12`; `profiles/built-in/nt51950-nt51951-standard-merge/profiles/nt51951-standard-merge.json:22` |

## 2. Shared families and shared facts

### 2.1 Classification rule

**Deliberate sharing** means two consumers use the same reviewed memory or metadata fact, or a composite explicitly
depends on both parents. A change to that used fact should invalidate both approvals.
**Incidental co-location** means deleting an unused sibling profile or feature-only map changes another consumer's
identity solely because the package or family file includes both. The same container can have both kinds. [C02, C04-C08]

The following classification distinguishes accepted retirements from this assessment's incidental-coupling judgments.

**Case 1 — NT51926 General Replace versus CtrlRAM.** General Replace binds a separate map and DP region set;
the CtrlRAM profiles bind their own FW141/FW200 maps. Their common raw family hash and bundle hash force eight
CtrlRAM identities to change when the unrelated definitions are removed. Decision 266 already chooses removal
and one re-registration of those eight products, including the plan-closure pin. [B07, F06, P14, D01, C30]

**Case 2 — Planned Desay definition retirement, with shared recognition retained.** F17
`nt51950-ab-merge@0.7.4` contains two different kinds of content:

- Dead definitions: three Desay maps, `nt51950-ab-merge-1024k-seed-anchors` used only by those maps, and two
  unregistered Desay profiles (P51/P52).
- Deliberately shared facts: the `abFormatPolicy` format `desay`, recognition values 151/166, and three Desay-format
  variants pointing to the Common maps. Common AB authoring consumes that family policy. [F17, C29]

Decisions 195, 213 and 227, and the allocation's R10-04, already decide the map/profile retirement and preservation
of the AB format policy. The family is not split. One re-registration of its seven routes is a derived consequence
of batching the R10-04 edits; the cited decisions settle retirement and retained recognition, not that batching rule.
Calling the dead definitions incidental would be an assessment judgment; the verified facts
are their bindings and the accepted planned retirement. This is not an owner question. [D04, D07]

**Case 3 — Unregistered NT51926 FW141 cascade profile, with an active evidence oracle.** P13
`nt51926-ctrlram-replace-fw141-cascade@0.7.0` has no runtime registration. It is unused by runtime registration,
not unused by firmware evidence: `Nt51926CtrlRamReplaceCandidateProfileTests.cs:574-585` compiles P13;
`:244` names `RoutedV2MatchesCompiledCandidateForOwnerApprovedSelfReplacementAsync`, and `:330-343` executes
the candidate and asserts complete routed/candidate output and hash equality. The same test applies the direct
Golden case's allowed-difference contract at `:348-357`. The root Golden manifest names it as `testReference`
at `testdata/golden/canonical/manifest.json:381`; the case's `testDisposition.evidenceRefs` names it at
`testdata/golden/canonical/NT51926/ctrlram-replace/fw1.4.1/cascade-2/`
`nt51926-fw141-cascade2-auto-prj-597-20260717/provenance/case.json:12`.
An architecture test also reads P13's path at `BootstrapCliBoundaryTests.BootstrapStructure.cs:192`. [D09]

Deleting this runtime-unregistered declaration is incidental coupling under the assessment rule, but edits a
firmware-evidence test and removes its P13 parity oracle unless it is retargeted. The registered candidate is
P15 `nt51926-ctrlram-replace-fw141-runtime-cascade@0.4.0`, also the routed profile asserted at `:315`.
Reading the profiles and compiler shows it does **not compile the same operations** for this route:
P13 declares one processor operation with five staged source bindings; P15 lowers runtime `ReplaceRange` mappings
followed by its postbuild processor with narrowed authority. Retargeting to P15 would compare the routed path
against its own profile path and needs explicit evidence review; existing output equality is not operation equality.
The P13 link at `docs/governance/v0.9.9-legacy-retirement-evidence.md:257` would also break.
Preserve that dated record's meaning through an explicitly reviewed link disposition. [C34, D09]

Decision 269 chooses removal with R54: the same eight survivors re-register once, adding **0 / 0 / 0**
incremental identity/decision/Golden-row updates; a later deletion would add **8 / 24 / 8**.
The question put to the owner did **not mention the Golden-cited parity oracle or the dated link**.
This omission must be presented to the firmware owner at R54 step 4's R3 approval, alongside the test/oracle
disposition and unchanged complete outputs, approved differences and exact write ranges.
Decision 269 settles removal timing; it does not certify an oracle replacement. [P13, D01, D09, D10]

The other multi-profile bundles in Table 2 have **potential sibling coupling**: removing an IC, firmware branch or
topology profile changes peers' package identity. No accepted independent-retirement decision was found for those
siblings in the task's named sources. Classifying their feature boundary is deferred to the owner; shared geometry
alone does not establish that all sibling profiles must share an approval lifetime. [B01-B24, P01-P55]

### 2.2 Exact shared-family cases

Families are identified by id, version and raw hash, not id alone. Thus the older General Merge family
`nt51929-nt51932@1.1.1`, Standard family `@1.2.2` and shared-facts provider `@1.4.0` are distinct.
Their explicit metadata references connect them; similar names do not. [F02, F15, F14, C11]

The next table includes every exact family with multiple bound profiles, multiple workflow consumers, retired DP
mode declarations, or a shared-facts provider role. A family with one profile and jointly reviewed capacity variants
is included where its legacy mode declarations matter. "Propagation" is the conservative current file-reference
rehash envelope: change that family, update every exact family reference and bound profile, then collect affected
bundle identities. It is not a calculated future used-node closure and is not an instruction to remove shared facts.
Deleting a feature-only map can have this same envelope; deleting a genuinely used fact can block its consumers
instead of merely updating their identities. [C10-C12]

| Family | Bound profile experiences / map modes | Classification and current file-rehash envelope: identities / decisions / Golden rows; test literals | Exact data sources |
| --- | --- | --- | --- |
| F01 `nt51917-nt51927-nt51928-canonical-container@1.4.1`; bundles B01, B04, B16 | GM,SM (5 profiles) / SM | Deliberate exact family sharing across SM/GM; candidate unused map `nt51928-standard-merge-512k` and its feature region set (R10-01 A-03); 84 / 252 / 84; 52 literals | `profiles/built-in/nt51927-standard-merge/families/nt51927-nt51928.json:3`; `profiles/built-in/nt51927-standard-merge/families/nt51927-nt51928.json:511` |
| F02 `nt51929-nt51932@1.1.1`; bundles B02 | GM (3 profiles) / SM | Deliberate common facts for IC/alias/branch profiles; exclusive sibling definitions remain potential coupling; 3 / 9 / 3; 3 literals | `profiles/built-in/nt51919-nt51929-nt51932-general-merge-logical-candidate/families/nt51929-nt51932.json:3`; `profiles/built-in/nt51919-nt51929-nt51932-general-merge-logical-candidate/families/nt51929-nt51932.json:107` |
| F03 `nt51923-nt51926@1.2.1`; bundles B03, B15 | GM,SM (4 profiles) / SM | Deliberate exact family sharing across SM/GM; whole-file scope can include unused facts; 16 / 48 / 16; 7 literals | `profiles/built-in/nt51923-standard-merge/families/nt51923-nt51926.json:3`; `profiles/built-in/nt51923-standard-merge/families/nt51923-nt51926.json:337` |
| F04 `nt51950-nt51951-dp-perspective@1.4.3`; bundles B05, B22 | GM,SM (4 profiles) / DP (retired),SM | Deliberate exact family sharing across SM/GM; whole-file scope can include unused facts; 20 / 60 / 20; 6 literals | `profiles/built-in/nt51950-nt51951-standard-merge/families/nt51950-nt51951-dp-perspective.json:3`; `profiles/built-in/nt51950-nt51951-standard-merge/families/nt51950-nt51951-dp-perspective.json:391` |
| F05 `nt51923-ctrlram-replace@0.4.1`; bundles B06 | CR (2 profiles) / CR | Deliberate common facts for IC/alias/branch profiles; exclusive sibling definitions remain potential coupling; 4 / 12 / 4; 1 literals | `profiles/built-in/nt51923-ctrlram-replace-candidate/families/nt51923-ctrlram-replace.json:3`; `profiles/built-in/nt51923-ctrlram-replace-candidate/families/nt51923-ctrlram-replace.json:774` |
| F06 `nt51926-ctrlram-replace@0.7.1`; bundles B07 | CR,GR (6 profiles) / CR,GR | Confirmed incidental GR-only map/region set within CR family; shared CR facts deliberate; 9 / 27 / 9; 2 literals | `profiles/built-in/nt51926-ctrlram-replace-candidate/families/nt51926-ctrlram-replace.json:3`; `profiles/built-in/nt51926-ctrlram-replace-candidate/families/nt51926-ctrlram-replace.json:620` |
| F07 `nt51927-ctrlram-replace@0.6.1`; bundles B08, B09 | CR (6 profiles) / CR | Deliberate common facts for IC/alias/branch profiles; exclusive sibling definitions remain potential coupling; 12 / 36 / 12; 2 literals | `profiles/built-in/nt51927-ctrlram-replace-candidate/families/nt51927-ctrlram-replace.json:3`; `profiles/built-in/nt51927-ctrlram-replace-candidate/families/nt51927-ctrlram-replace.json:975` |
| F08 `nt51928-ctrlram-replace@0.4.1`; bundles B10 | CR (3 profiles) / CR | Deliberate common facts for IC/alias/branch profiles; exclusive sibling definitions remain potential coupling; 6 / 18 / 6; 1 literals | `profiles/built-in/nt51928-ctrlram-replace-candidate/families/nt51928-ctrlram-replace.json:3`; `profiles/built-in/nt51928-ctrlram-replace-candidate/families/nt51928-ctrlram-replace.json:1261` |
| F09 `nt51929-ctrlram-replace@0.5.1`; bundles B11 | CR (4 profiles) / CR | Deliberate common facts for IC/alias/branch profiles; exclusive sibling definitions remain potential coupling; 8 / 24 / 8; 3 literals | `profiles/built-in/nt51929-ctrlram-replace-candidate/families/nt51929-ctrlram-replace.json:3`; `profiles/built-in/nt51929-ctrlram-replace-candidate/families/nt51929-ctrlram-replace.json:417` |
| F10 `nt51932-ctrlram-replace@0.4.1`; bundles B12 | CR (2 profiles) / CR | Deliberate common facts for IC/alias/branch profiles; exclusive sibling definitions remain potential coupling; 4 / 12 / 4; 1 literals | `profiles/built-in/nt51932-ctrlram-replace-candidate/families/nt51932-ctrlram-replace.json:3`; `profiles/built-in/nt51932-ctrlram-replace-candidate/families/nt51932-ctrlram-replace.json:532` |
| F11 `nt51950-ctrlram-replace@0.6.2`; bundles B13 | CR (2 profiles) / CR | Deliberate common facts for IC/alias/branch profiles; exclusive sibling definitions remain potential coupling; 6 / 18 / 6; 1 literals | `profiles/built-in/nt51950-ctrlram-replace-candidate/families/nt51950-ctrlram-replace.json:3`; `profiles/built-in/nt51950-ctrlram-replace-candidate/families/nt51950-ctrlram-replace.json:288` |
| F12 `nt51951-ctrlram-replace@0.6.2`; bundles B14 | CR (2 profiles) / CR | Deliberate common facts for IC/alias/branch profiles; exclusive sibling definitions remain potential coupling; 6 / 18 / 6; 1 literals | `profiles/built-in/nt51951-ctrlram-replace-candidate/families/nt51951-ctrlram-replace.json:3`; `profiles/built-in/nt51951-ctrlram-replace-candidate/families/nt51951-ctrlram-replace.json:288` |
| F13 `nt51917-nt51927-nt51928-canonical-container@1.5.2`; bundles B17 | SM (1 profiles) / DP (retired),SM | Candidate unused map `nt51927-standard-merge-256k` (R10-01 A-03); shared region sets and DP metadata require consumer review; 7 / 21 / 7; 4 literals | `profiles/built-in/nt51928-standard-merge/families/nt51927-nt51928-v1.5.json:3`; `profiles/built-in/nt51928-standard-merge/families/nt51927-nt51928-v1.5.json:299` |
| F14 `nt51929-nt51932@1.4.0`; bundles B18 | no profile (0 profiles) / DP (retired),SM | Deliberate metadata/full-image provider; residual DP applicability is historical, not active admission; 85 / 255 / 85; 65 literals | `profiles/built-in/nt51919-nt51929-nt51932-shared-facts/families/nt51929-nt51932.json:3`; `profiles/built-in/nt51919-nt51929-nt51932-shared-facts/families/nt51929-nt51932.json:212` |
| F15 `nt51929-nt51932@1.2.2`; bundles B19 | SM (3 profiles) / SM | Deliberate common facts for IC/alias/branch profiles; exclusive sibling definitions remain potential coupling; 21 / 63 / 21; 19 literals | `profiles/built-in/nt51929-standard-merge/families/nt51929-nt51932.json:3`; `profiles/built-in/nt51929-standard-merge/families/nt51929-nt51932.json:707` |
| F16 `nt51919-nt51929-nt51932-ab-merge@0.3.1`; bundles B20 | AB (3 profiles) / AB | Deliberate common facts for IC/alias/branch profiles; exclusive sibling definitions remain potential coupling; 9 / 27 / 9; 8 literals | `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/families/nt51919-nt51929-nt51932-ab-merge.json:3`; `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/families/nt51919-nt51929-nt51932-ab-merge.json:254` |
| F17 `nt51950-ab-merge@0.7.4`; bundles B21 | AB (5 profiles) / AB | Planned retirement of three Desay maps, their exclusive seed-anchor region set and two unregistered profiles; family not split; shared `desay` recognition 151/166 and three Common-map variants retained; one seven-route re-registration is derived if batched, not independently decided (D04/D07); 7 / 21 / 7; 8 literals | `profiles/built-in/nt51950-ab-merge/families/nt51950-ab-merge.json:3`; `profiles/built-in/nt51950-ab-merge/families/nt51950-ab-merge.json:595` |
| F18 `nt51923-nt51926@1.3.2`; bundles B23 | no profile (0 profiles) / DP (retired),SM | Deliberate metadata/full-image provider; residual DP applicability is historical, not active admission; 0 / 0 / 0; 0 literals | `profiles/built-in/nt51923-nt51926-shared-facts/families/nt51923-nt51926.json:3`; `profiles/built-in/nt51923-nt51926-shared-facts/families/nt51923-nt51926.json:136` |
| F19 `nt51927@1.4.2`; bundles B24 | no profile (0 profiles) / DP (retired),SM | Deliberate metadata/full-image provider; residual DP applicability is historical, not active admission; 0 / 0 / 0; 0 literals | `profiles/built-in/nt51917-nt51927-shared-facts/families/nt51927.json:3`; `profiles/built-in/nt51917-nt51927-shared-facts/families/nt51927.json:136` |

The following edge table makes the propagation inputs explicit. An arrow names the referenced exact family;
no arrow means no external family triple was found. The C12 counts follow these file-level edges transitively.

| Family source | Referenced exact family keys / data anchors |
| --- | --- |
| F01 | F14 `profiles/built-in/nt51927-standard-merge/families/nt51927-nt51928.json:237` |
| F02 | F15 `profiles/built-in/nt51919-nt51929-nt51932-general-merge-logical-candidate/families/nt51929-nt51932.json:95` |
| F03 | F01 `profiles/built-in/nt51923-standard-merge/families/nt51923-nt51926.json:273`; F14 `profiles/built-in/nt51923-standard-merge/families/nt51923-nt51926.json:301` |
| F04 | F01 `profiles/built-in/nt51950-nt51951-standard-merge/families/nt51950-nt51951-dp-perspective.json:167`; F14 `profiles/built-in/nt51950-nt51951-standard-merge/families/nt51950-nt51951-dp-perspective.json:223` |
| F05 | F14 `profiles/built-in/nt51923-ctrlram-replace-candidate/families/nt51923-ctrlram-replace.json:760` |
| F06 | F14 `profiles/built-in/nt51926-ctrlram-replace-candidate/families/nt51926-ctrlram-replace.json:606` |
| F07 | F14 `profiles/built-in/nt51927-ctrlram-replace-candidate/families/nt51927-ctrlram-replace.json:961` |
| F08 | F14 `profiles/built-in/nt51928-ctrlram-replace-candidate/families/nt51928-ctrlram-replace.json:1247` |
| F09 | F14 `profiles/built-in/nt51929-ctrlram-replace-candidate/families/nt51929-ctrlram-replace.json:403` |
| F10 | F14 `profiles/built-in/nt51932-ctrlram-replace-candidate/families/nt51932-ctrlram-replace.json:518` |
| F11 | F14 `profiles/built-in/nt51950-ctrlram-replace-candidate/families/nt51950-ctrlram-replace.json:274` |
| F12 | F14 `profiles/built-in/nt51951-ctrlram-replace-candidate/families/nt51951-ctrlram-replace.json:274` |
| F13 | F01 `profiles/built-in/nt51928-standard-merge/families/nt51927-nt51928-v1.5.json:263`; F14 `profiles/built-in/nt51928-standard-merge/families/nt51927-nt51928-v1.5.json:237` |
| F14 | None |
| F15 | F01 `profiles/built-in/nt51929-standard-merge/families/nt51929-nt51932.json:634`; F14 `profiles/built-in/nt51929-standard-merge/families/nt51929-nt51932.json:666` |
| F16 | F15 `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/families/nt51919-nt51929-nt51932-ab-merge.json:187`; F14 `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/families/nt51919-nt51929-nt51932-ab-merge.json:226` |
| F17 | F01 `profiles/built-in/nt51950-ab-merge/families/nt51950-ab-merge.json:489`; F14 `profiles/built-in/nt51950-ab-merge/families/nt51950-ab-merge.json:567` |
| F18 | F01 `profiles/built-in/nt51923-nt51926-shared-facts/families/nt51923-nt51926.json:111`; F14 `profiles/built-in/nt51923-nt51926-shared-facts/families/nt51923-nt51926.json:91` |
| F19 | F01 `profiles/built-in/nt51917-nt51927-shared-facts/families/nt51927.json:90` |

The registered-profile map screen has two different counts; neither can be reduced to only the five maps
previously named in this analysis. Per bundle it finds **25 map occurrences in 11 bundles**:
B01 2, B02 3, B03 2, B04 2, B05 6, B16 1, B17 1, B18 1, B21 3, B23 2, B24 2.
Per exact `(familyId, familyVersion, familyContentHash)` it finds **13 maps**: three Desay maps in F17,
F01's `nt51928-standard-merge-512k`, F13's `nt51927-standard-merge-256k`, all three F02 Standard maps,
F14's one map, and the two maps each in F18 and F19. [B01-B24, F01-F19]

The five F14/F18/F19 maps have declared `fullImageMetadataViews` inspection consumers, not registered profiles.
R62 counts those admitted views as consumers (section 5.4); its strict profile-only family screen would otherwise
misclassify them. The exact-family count then falls from 13 to 8; the per-bundle count falls from 25 to 20.
F02's three maps and the General Merge copies in B01-B05 cannot borrow another bundle's registrations.
Decision 235 covers region sets, recipes and metadata structures, **not image-map references**.
The 15 B01-B05 map occurrences are deliberate canonical-family carriers today, without a declared map-consumer
mechanism; R62 baselines that debt instead of inventing a new reference kind. [D08, C13, C14]

F01/F13 remain candidates, not approved retirements. R10-01's A-03 compares those containers.
This screen does not prove that relationships or region sets are unused: F13's
`nt51927-standard-merge-flash` region set also serves another map. Review fact consumers before removal.
The frozen per-bundle list, classifications and next-change disposition are specified in section 5.4. [C32]

### 2.3 Cross-bundle dependencies and legacy facts

The two widest shared schemas extend the blast radius beyond a single bundle: `composition-profile-v2.9.schema.json` and
`firmware-family-v1-relations.schema.json` are each materialized into B06-B14.
Changing either source file changes those nine bundle hashes and the union of **55 routes / 165 decisions /
55 Golden rows**, including ten bank routes and General Replace. The route sets are deduplicated;
editing both schemas together does not double this count. [B06-B14, C09, C10, D03]

Other shared schema source files also span bundles. Each row unions actual consumers; rows overlap and cannot be
summed. Provider-only bundles can still have admission/view consequences beyond their route count. [C09, C13]

| Shared schema source | Materialized bundles | Routes / decisions / Golden rows |
| --- | --- | ---: |
| `composition-profile-v2.5.schema.json` | B01-B05 | 10 / 30 / 10 |
| `composition-profile-v2.15.schema.json` | B15, B16, B17, B19 | 46 / 138 / 46 |
| `composition-profile-v2.17.schema.json` | B20, B21 | 16 / 48 / 16 |
| `firmware-family-v1.1-tp-header.schema.json` | B02, B19 | 14 / 42 / 14 |
| `firmware-family-v1.2-tp-header-subjects.schema.json` | B01, B03, B04, B15, B16 | 33 / 99 / 33 |
| `firmware-family-v1.3-full-image-metadata.schema.json` | B05, B17, B18, B22, B23, B24 | 27 / 81 / 27 |

The remaining single-bundle schemas are `composition-profile-v2.16.schema.json` (B22: 18 / 54 / 18),
`firmware-family-v1.2-bank-instances.schema.json` (B20: 9 / 27 / 9), and
`firmware-family-v1.2-ab-format.schema.json` (B21: 7 / 21 / 7).
An index materialization filename without a matching manifest schema entry is not a materialized schema consumer:
in particular, B18's declared `composition-profile-v2.13.schema.json` has no CP entry. [B01-B24]

The postbuild catalog is another cross-product container: its eleven profiles serve ten ICs and all 54 CtrlRAM
routes. Its fingerprint covers the selected plan and write authority, rather than every unrelated catalog profile;
the loader still admits the exact catalog bytes. This separates fact identity from package admission today. [C18, C27,
C28]

The three bundles named shared-facts contain no profiles or runtime registrations. They still have explicit
metadata-provider declarations and full-image inspection views. Their bundle hashes bind provider admission,
rather than a direct policy route definition; zero in Table 1 does not mean unused. [B18, B23, B24, C11, C13]

The DPCMI provider `nt51929-nt51932@1.4.0` is deliberately shared through exact metadata references.
The canonical-container `@1.4.1` owns firmware-config and TP-header/SVN facts used by other families.
The graph table's broad rehash envelopes come from raw whole-file references; changing an unused structure can
therefore cause collateral identity updates even inside a deliberately shared provider. Preserve shared facts,
but do not use "shared-facts" as a blanket exemption for unrelated feature-only definitions. [F01, F14, C11, C12]

Standard and General Merge deliberately share exact families in three cases: canonical-container `@1.4.1`,
NT51923/26 `@1.2.1`, and NT51950/51 DP-perspective `@1.4.3`. Logical-output profiles still bind those families
by id/version/hash. A physical map is not thereby a General Merge output choice. Copying the same canonical source
into distinct packages preserves package separation but does not isolate a change to the common family file. [C14, F01,
F03,
F04]

NT51917/NT51927 CtrlRAM deliberately share `nt51927-ctrlram-replace@0.6.1` through the declared canonical-family
materialization source. Their product/profile approvals remain distinct; deleting an NT51917-only profile need not
rewrite the NT51927 package, but changing their common family facts changes both packages. [B08, B09, F07, C09]

The five Standard bundles also serve CtrlRAM's exact report metadata or display-only memory context.
That dependency is intentional and is explicitly included in the identity value. A narrow identity must retain the
used source profile/family/map facts while excluding unrelated sibling definitions in that source bundle. [C06-C08]
Both AB bundles serve AB Merge and composite bank CtrlRAM Replace. Their layout/local-parent dependencies are
deliberate; a Desay-only deletion in the NT51950 layout bundle is not a change to the Common bank facts. [C05, F16, F17]

Residual `dp-replace` modes occur in five exact families: canonical-container `@1.5.2`, DP-perspective `@1.4.3`,
and all three shared-facts families. No DP runtime registration or policy route remains in this snapshot.
Those are shared/historical map applicability or metadata definitions, not active DP products.
In DP-perspective the Standard and DP modes also have different metadata bindings. A mode label alone is insufficient
to decide whether a removable definition is unrelated; evaluate the actual map and structure consumers. [F04, F13, F14,
F18,
F19, D03]
This document proposes no cleanup of those legacy facts. Their retained metadata was an explicit prior-retirement
boundary, and deleting them could change full-image inspection as well as remaining package identities. [D05, C13]

## 3. What identity covers today

### 3.1 Bundle content hash

For every entry, SHA-256 covers the **raw file bytes**, including JSON whitespace and line endings.
The source may be a materialized canonical family or schema, but the manifest fixes its destination identity. [C09, C10]
The bundle hash algorithm is `sha256-rfc8785-entry-array-v1`. It:
1. Sorts the complete entry array ordinally by entryId, kind, path, schemaId, then contentHash.
2. Emits each entry with keys contentHash, entryId, kind, path, schemaId in that order, as compact UTF-8 JSON.
3. SHA-256 hashes that array. Every listed schema, profile and family participates. [C10]

The hash excludes the manifest's bundleId, bundleVersion, hashAlgorithm label, trustAnchorBindingId and raw formatting,
and excludes registrations, index/policy/Golden-root bytes. Those have separate admission or source identities.
A manifest-entry reorder leaves this content hash unchanged; a rename, unused entry deletion or entry formatting
change does not. Admission separately checks algorithm/schema, content hash and trust anchor. [C10, C15, C16]

### 3.2 Route identity inputs

The common fingerprint function hashes length-framed fields in this order:
format id; IC; workflow; IC Count; route map; definition id; definition version; trusted definition SHA-256;
compiler semantic id; allowed-map set; semantic-binding set.
Sets are distinct, ordinally sorted, include their count and indexed values.
Each field is framed as `label-length:label=value-length:value;`; the final text is UTF-8 SHA-256.
The format id is `nfc.capability-definition.v1`. [C01]

| Producer | Trusted definition and additional bindings | Source |
| --- | --- | --- |
| Static map-bound Standard/AB | Profile id/version, whole bundle hash, selected map, map-bound compiler id, input selection-group member slot ids | C04 |
| Dynamic selection-group or source-envelope | Profile id/version, whole bundle hash, reviewed allowed map subset (or exact map), map-bound compiler id, input selection-group member slot ids | C07 |
| General Merge | Profile id/version, whole bundle hash, generic allowed map, logical-output compiler id, family id binding | C07 |
| General Replace | Profile id/version, whole bundle hash, exact allowed map, `RuntimeReferenceReplaceCompilerSemanticId`, empty semantic-binding set | C07 |
| Local CtrlRAM | Profile id/version, whole CtrlRAM bundle hash, exact map, runtime-reference compiler id; processor id, selector token, postbuild-plan identity; optional report and memory-context bindings | C08 |
| Bank CtrlRAM | Composite definition id/version/hash, layout map, bank compiler id; bank-definition hash, local selector and postbuild-plan identity, optional local memory context | C05, C17 |

For local CtrlRAM, report bindings include source profile id/version, **whole Standard bundle hash**, report
space-to-slot projections and exact report map id. Memory context includes IC, source profile id/version,
**whole Standard bundle hash**, map id and address-space id. [C06, C08]

The postbuild-plan identity includes processor/IC/tool binding, firmware filename, assembly kind, effective Common FW
version, firmware-config write route, selector kind/branch/count bounds and capacity; ordered command and block
definitions (ids, command family/mode/CRC argument, source kind/file/offset/range/staged artifact id); and derived
maximum write sections (id, range and optional source range). It hashes framed UTF-8 text with its own versioned
format. It covers definitions of processing authority, not the firmware payload. [C18]

The bank definition hash is a second framed SHA-256 identity. It includes definition id/version and compiler id,
each parent's profile id/version, **bundle content hash**, profile entry hash, family hash, member and map;
bank capacity/local slice/finalization fields are also included under the constructor's explicit condition.
Parent bundle ids/versions are retained provenance but are not appended by this hash writer.
This is why changing either parent bundle changes a bank product even when its exact profile entry is unchanged.
The ordinary common fingerprint then includes that composite hash, not merely one layout bundle hash. [C17]

Policy binds every current identity in the route row and in authoring, publication and evidence decisions.
Catalog loading rejects a computed mismatch. Golden evidence joins evidence id, route id and fingerprint;
alias source identities are additionally recorded. Policy choices and Golden root hashes are not inputs of the
route fingerprint function. [C01, C03, C19, D03]

### 3.3 Required coverage versus over-inclusion

The goal is: **the definitions this product uses are exactly the reviewed ones**.
Keep exact product axes, profile/version, allowed alternatives, compiler semantics, processing authority,
used metadata/map/context facts, both bank parents and all transitive referenced definitions.
The must-keep list also includes family-level authority that a walk from `mapBinding` alone can miss:

- `abFormatPolicy`: the retained Desay recognition and Common-map variants are used by Common AB authoring. [F17, C29]
- `familyRelationships`: nonempty in four exact families (F01, F04, F13, F14).
- `factAliases`: nonempty in four exact families (F02, F15, F16, F19).
- `fullImageMetadataViews`: nonempty in five exact families (F04, F13, F14, F18, F19), with inspection consumers
  beyond composition maps. [C13]
- `members`: present in all 19 exact families; declared member identity, capabilities and applicability must remain
  covered.

These are counted family-level facts, not permission to include only reachable maps or to drop fields merely
because no map names them. A genuine change to any used definition must continue invalidating every affected
consumer's review. ADR 0046 makes authoring, publication and evidence decisions stale together; decision 269
does not change that contract. [C02, F01-F19]

The over-inclusive inputs are the whole bundle hashes (primary, Standard counterpart, and bank parents), whole
family files that contain unused feature-only maps/structures, and raw profile bytes that embed a whole-family hash.
Entry path/id/schemaId changes are also package identity changes even when the used semantic definitions are equal.
Raw whitespace/evidence-text changes have the same effect. These are useful exact-byte package controls, but are
broader than a product's used-definition boundary. The examples in sections 1-2 establish that distinction;
they do not authorize ignoring those inputs today. [C10, C14, C17, F06, F17]

Current identity has no selected firmware files, authored General mappings, per-run processor results or input/output
firmware hashes. The accepted contract separates those from reviewed definition identity; no narrowing option should
silently move them into it or remove actual definition-level constraints. [C01, C02]

## 4. Options, migration and evidence

This section retains the alternatives and their analysis as the decision rationale.
Decision 269 selects D's rule/check, joint P13 removal and next-change cleanup; B and C were not selected.
This R0 handoff records the decision and R62 design; it does not implement an executable check.
Path classification, not "packaging only" wording, determines implementation risk. [A01]
No option may change firmware bytes, ranges, CRC or header behavior, padding, truncation, ordering or naming. [A07]

### 4.1 A: packaging rule only

**Selected rule (answer i):** one bundle holds the in-use definitions of one feature.
Reject unregistered profiles and maps with no registered profile consumer; retain genuinely shared facts through
decision 235's accepted reference contract. R62 specifies the per-bundle unit, inspection consumers and baseline. Use
`regionSetReference`, `recipeReference` and the amended
`metadataProviderFamilies` mechanism; do not invent another shared-facts provider path. [D08]

An automatic check must distinguish feature-owned definitions from genuinely shared facts and verify declared
consumers, including materialized canonical-family sources. A workflow-only check catches NT51926 GR/CR but
misses the obsolete CtrlRAM profile and the planned Desay dead definitions. It must preserve retained Desay format
recognition, shared metadata, Standard context and bank-parent authority. IC names and matching Golden outputs
do not declare feature ownership. The selected rule leaves current identity computation unchanged.
Answer iii defers existing cleanup to the next bundle change, so an unbaselined first-day check cannot pass.
R62's frozen, shrinking baseline makes that timing executable without permitting new debt (section 5.4).

**One-time re-pin:** splitting a touched bundle changes its entry array and all bound current identities in Table 1.
Splitting a family changes its raw hash, every bound profile and potentially incoming metadata references in section 2.
Moving the same raw family bytes to a shared package can still change entry-array identities and admission artifacts.
No hashes should be mechanically treated as renewed evidence. [C10-C17]

The two touched bundle sets expose 16 current identities / 48 policy decisions / 16 Golden target rows before
General Replace retires; after its withdrawal, 15 / 45 / 15 survive. Those sets are disjoint at this snapshot.
The two NT51926 cases use the same eight-route survivor set, not two additional sets.
The gross survivor review is **15 / 45 / 15**. If combined with R54 and R10-04 in their respective frozen edits,
D adds **0 / 0 / 0 incremental re-registrations** to those retirements; Desay batching is a derived consequence.
The eight NT51926 routes are all `direct-golden`; B21's seven are all `contract-only`.
For B21 the available migration proof is before/after complete output and exact write-range comparison,
not comparison against independently certified Golden outputs. That gap remains explicit. [D01, D04, D07, D03]
In the unselected every-profile split scenario, the 19 multi-profile bundles touch 83 / 249 / 83 current
rows before any retirement or family propagation. That is a counted scenario, not a recommended migration scope.
Proactive splitting was not selected; an exact future split total still depends on the actual feature boundary.
[B01-B24, D03]

**Risk and authority:** profiles/index/families/Golden data are R3 firmware-owner paths.
Policy/trust contracts are R3 firmware-owner plus release-owner; release pins/packaging need release-owner.
An ADR or ordinary design document is R2; ordinary checker tooling is R2. Placing this structure check in
`scripts/repository_contract_validation.py` is an R3 governance-owner change because that file owns CI verdicts.
Policy re-pins require both firmware-owner and release-owner under the contracts classification.
A complete adopted A retains every affected R3 role. [A02-A06]

**Firmware-owner evidence needed:** a before/after definition and dependency inventory, an old/new identity table,
unchanged surviving definition semantics, complete output comparisons under existing approved Golden contracts,
and exact write-range comparisons for each surviving affected product. Demonstrate unrelated feature deletion no
longer changes peers and a used shared-fact change still does. Check source/admission/prebuilt/package identity
consistency. Preserve existing evidence ranks and difference allowances; missing direct output evidence remains a
reported human/evidence gate, not something a packaging change can supply. Decision 266 fixes these requirements
for the eight NT51926 survivors already. [D01, C02, A07]

### 4.2 B: identity over the used closure

Replace the whole-bundle value in product identity with a versioned closure digest: the profile, exact family/maps,
schemas and transitive facts it uses. Preserve axes, allowed alternatives, compiler/processor contracts and both
bank parents. Include used report metadata and memory context, not just the execution profile.

**Important distinction:** a closure of selected *files* is not a closure of selected *facts*.
Hashing the profile plus its whole family and schemas excludes sibling profile entries, but still binds the
NT51926 General Replace map and the Desay maps through the raw family file.
Even excluding those nodes from a new digest leaves the raw profile's `familyContentHash` changing when the
whole family is rehashed. B needs either A's family separation or a reviewed semantic projection/binding migration,
with precisely defined map, region-set, metadata-reference, alias and validation closure. It cannot be achieved
safely by substituting one hash argument in the fingerprint call. [C10, C14, C17, F06, F17]

Logical-output profiles, map-set alternatives, referenced metadata definitions, Standard report/context sources
and bank definitions require explicit closure rules. Section 3.3's must-keep list is a concrete risk inventory:
`abFormatPolicy`, `familyRelationships` (four families), `factAliases` (four), `fullImageMetadataViews` (five)
and `members` can escape a closure that only follows `mapBinding`.
Omitting a used definition, such as AB format recognition, would let it change without invalidating review.
A future format must fail closed on unresolved, ambiguous or undeclared dependencies.
Whether descriptive evidence text and formatting remain in product identity is a separate exact-byte-versus-semantic
choice; package admission should continue protecting the original reviewed bytes. [C02, C11, C14]

**What still needs the bundle hash:**

- Trusted bundle admission compares expected content hash/trust anchor, recomputes the entry array and verifies raw
  entry snapshots. Closed package inventory and mutation checks still use the package definition. [C10, C15, C16]
- Prebuilt acceptance matches indexed bundle directory/version/contentHash tuples, trust-index raw SHA and
  manifest-set identity; it admits each embedded manifest against the bundle hash. Keep those checks. [C21, C22]
- Compiled provenance, current compiler-contract matching and Saved Rule parent bindings retain bundle identities.
  Saved Rule v2 `parentBinding` compares bundle id, version and hash, plus profile/family/map identity.
  Changing a General Merge bundle invalidates users' previously saved Customized Merge rules with
  `V2ParentNarrowingInvalid`; they must be saved again. Count this as migration cost.
  Separate package provenance from product closure identity through the existing owners; do not erase the checks
  or add another firmware execution path. [C23-C25, C31]
- Decision 266 explicitly includes the NT51926 plan-closure pin. The nine bundle constants at
  `CtrlRamV2PlanClosureProfileTests.cs:13-21` remain admission/plan evidence to assess, even if route identity narrows.
  Fifteen current route-fingerprint literal occurrences are separate from T01's 65 bundle-hash occurrences:
  13 in `CtrlRamDirectTpGoldenExecutionTests.cs:20-68`, one in `CanonicalCapabilityCatalogMigrationTests.cs:90`,
  and one in `BuiltInCanonicalCapabilityPolicyTests.cs:41`. [D01, C30, T02]
- Release pins cover policy raw SHA, index raw SHA and the Golden release allowlist's raw SHA. They do not simply
  equal one route or bundle hash. A new policy/index would still require their existing producer and reviewed
  package/prebuilt artifacts to be synchronized. Every policy re-pin also needs the release-owner role under
  `docs/governance/authority-policy.json:45-49`, alongside firmware-owner. [C26, A03]

**One-time re-pin (unselected B):** gross exposure is **85 products / 255 decisions / 85 Golden target rows**,
plus alias-source fields and literal identity fixtures. After R54 withdraws General Replace it is **84 / 252 / 84**.
If batched into the same frozen sources as both R54 and R10-04, the incremental survivor updates are **69 / 207 / 69**:
84 survivors minus the 15 routes those retirements already update. With R54 alone that increment is **76 / 228 / 76**.
Landing B later repeats those retirement-route reviews and cannot claim the batched increment. Staging only selected
products needs an approved
migration seam and deletion milestone; this report does not propose indefinite dual identity semantics. [D03, D01, A07]
Bundle-hash literal tests can remain for admission checks; tests of product identity and proofs must follow the
new approved contract. Section 1's literal search is not a complete census of those new-format changes. [T01, C23-C25]

**Risk:** R3 firmware-owner for Domain/Profiles/Application/Infrastructure semantic owners, R3 firmware-owner plus
release-owner for identity/policy contracts, R2 ADR design, and applicable release/governance roles if their owners
change. An algorithm advertised as narrower must not weaken any existing approval requirement. [A02-A06]

**Firmware-owner evidence needed:** independent closure review for every consumer kind; negative cases for changed
used definitions, hidden transitive changes, unresolved references and tampering; positive independence cases for
removed unused definitions; old/new identities; complete applicable output and write-range comparisons; package
tamper/admission and prebuilt equivalence evidence. The required proof is both that outputs stayed the same and that
no reviewed definition was dropped from identity coverage. No such migration has been executed here. [C02, A07]

### 4.3 C: retain the mechanism and document the consequence

Keep current packages and identity calculation. State that any entry change in a mixed package, and any whole-family
change propagated to its consumers, requires renewed review even when another feature is being removed. [C10, C14]
C adds no algorithm migration or one-time identity updates; each future edit incurs Table 1/2's exposure and section
2's dependency propagation. It does not satisfy the owner's stated wish to separate the NT51926 definitions;
decision 266 remains applicable. [D01]

**Risk:** an explanatory handoff is R0; a normative identity/approval contract is R3 under the contract or governance
patterns. A future actual definition deletion remains R3. [A01-A04]
**Evidence:** no new output proof is needed just to describe unchanged behavior, but every later affected retirement
still needs the firmware owner's output, write-range and identity review. C cannot waive that evidence. [D01, A07]

### 4.4 D: A with in-use feature definitions and bounded cleanup

This is the refinement of A selected by decision 269, with the commander's R62 design in section 5.4.

1. Keep one feature's in-use definitions in its owning bundle; declare feature ownership through the existing
   semantic owner. R62 evaluates each bundle and resolves registered-profile and admitted inspection consumers.
   Reject unregistered profiles and unused maps subject only to the frozen next-change baseline.
2. Remove P13 with General Replace in R54 step 4, as answer ii decides; re-register the eight survivors once.
   Present the Golden-cited comparator, differing P15 operations and dated-link consequence to its R3 firmware owner.
   Complete the already planned Desay dead-definition retirement in R10-04; retain its shared format recognition.
3. Reuse decision 235 and R10-01's `regionSetReference` / `recipeReference` contract and amended
   `metadataProviderFamilies` declarations for truly shared definitions. Preserve the approved S1 → trusted raw
   snapshot → S1′ JSON expansion and re-validation → DTO → normalizer flow. No second semantic provider path.
   R10-02/03 already include six bank Replace re-registrations; A-01/A-02 migrate when their bundle next changes
   or General Merge authoring is redone. These are accepted arrangements, not new owner questions. [D08]
4. Implement R62 in 1.2.4 after R54 step 4, using section 5.4's unit, consumers and frozen shrinking baseline.
   Include materialized canonical-family sources and preserve all section 3.3 authority.
5. Do not proactively split the other existing bundles. Tidy each bundle when it next changes for another reason.
   **Proposed coordination:** batch same-version edits to one bundle (R10-04, R42, R47, R10-02/03) into one
   re-registration, using one frozen source and the union of routes/evidence. Answer iii decides next-change
   cleanup, not a blanket same-version batching policy. [D07, D08]
6. Keep current identity computation (answer i). B was not selected; any future closure migration needs separate
   authority and complete proof obligations.

**Gross / incremental cost:** D's combined survivor review is **15 / 45 / 15**, with **0 / 0 / 0** incremental
updates if batched with R54 and R10-04. Those retirements still re-register **8 / 24 / 8** NT51926 survivors
and **7 / 21 / 7** B21 survivors once. This totals **15 / 45 / 15** surviving updates,
not zero work. The eight NT51926 routes have direct Golden evidence; all seven B21 routes are contract-only.
B21 needs complete before/after output and exact write-range comparisons; independent Golden evidence is absent.
Removing P13 later would add another **8 / 24 / 8**. [B07, B21, D01, D03, D04, D07, D09]

**Risk/evidence:** the same applicable R3 roles and proof requirements as A, including release-owner for policy
re-pins and governance-owner for a check in `repository_contract_validation.py`.
Batching does not waive exact-source evidence, actual applicable Golden execution or owner review. [A02-A07, C02]

## 5. Owner decisions (2026-10-02, board decision 269)

Only the following three answers are owner decisions. The inventory and alternatives above remain the record of
why they were asked. R61 replaces the evaluation's provisional item number; R58 is the Support Matrix hover fix.
The commander allocates the new automatic check as R62 in 1.2.4 after R54 step 4.
R62's detailed design below is the commander's specification within the answers, not another owner answer. [D10]

### 5.1 Answer i: rule plus automatic check; identity unchanged

A bundle holds the definitions of one feature, and only definitions in use. An unregistered profile or a map no
registered profile uses fails the check. Genuinely shared facts use decision 235's reference mechanism.
How the identity value is computed does not change.

Not chosen: B, a new used-definition identity calculation; C, keeping only a description of today's coupling.

Consequence: the existing identity/admission writers remain authoritative. The check must define consumers and
grandfathered findings because answer iii defers existing cleanup. Decision 235 covers region sets, recipes and
metadata structures, not image maps; carrier maps cannot be declared used merely by borrowing another bundle's
registered profile. R62 resolves that debt with a frozen list and next-change enforcement.

### 5.2 Answer ii: remove P13 with R54

Remove `nt51926-ctrlram-replace-fw141-cascade` together with R54, with one registration of the eight routes for
both removals. That settles timing and yields gross **8 / 24 / 8**, incremental **0 / 0 / 0** versus R54 alone.

Not chosen: retaining P13 now and paying another **8 / 24 / 8** if it is deleted after R54.

Consequence omitted from the question put to the owner: P13 is the executable comparator in the direct-Golden-cited
`RoutedV2MatchesCompiledCandidateForOwnerApprovedSelfReplacementAsync` test, not merely a test-only dead file.
Its helper compiles P13 at `Nt51926CtrlRamReplaceCandidateProfileTests.cs:580` and asserts routed/candidate equality
at `:342-343`; `manifest.json:381` and the case's `provenance/case.json:12` cite that test (full paths in D09).
Removing P13 edits firmware evidence and removes that parity oracle unless retargeted. Registered P15
`nt51926-ctrlram-replace-fw141-runtime-cascade@0.4.0` is the candidate, but reading shows different operations:
P13's staged-source processor versus P15's runtime mappings and narrowed postbuild.
Retargeting would reduce comparison independence; it is not evidence-equivalent by declaration.
The dated record's P13 link at `docs/governance/v0.9.9-legacy-retirement-evidence.md:257` would break.
Present all of this, with the oracle/test and dated-link dispositions, at R54 step 4's R3 firmware-owner approval;
by decision 269 the owner may revise answer ii at that approval.
The re-registration count remains eight once. See case 3 and D09/C34 for the verified chain.

### 5.3 Answer iii: tidy on the next change

Existing bundles are not split proactively. A bundle is tidied when it next changes for another reason.

Not chosen: B, proactively splitting by IC/firmware branch; C, applying the rule only to new bundles forever.

Consequence: existing debt needs a shrinking baseline; otherwise the first run fails on today's definitions.
One seven-route R10-04 re-registration is a derived result if its retirement edits are batched.
Combining all same-version edits into one re-registration remains proposed coordination, not an owner decision.

The alternatives' original cost labels were gross exposure, not all additional work. Recounted tuples below are
products / policy decisions / Golden target rows; Golden includes Contract Only and does not imply certification.
Incremental figures subtract the union of already reviewed survivor routes only if edits share frozen sources.
A later separate landing repeats reviews; it cannot use the batched increment.

| Scenario | Gross current exposure | Survivors after R54 withdrawal | Incremental if batched with R54 only | Incremental if batched with R54 and R10-04 |
| --- | ---: | ---: | ---: | ---: |
| Selected D: NT51926 plus Desay cleanup | 16 / 48 / 16 | 15 / 45 / 15 | 7 / 21 / 7 | 0 / 0 / 0 |
| Unselected B: new identity format | 85 / 255 / 85 | 84 / 252 / 84 | 76 / 228 / 76 | 69 / 207 / 69 |
| Unselected proactive split of 19 multi-profile bundles | 83 / 249 / 83 | 82 / 246 / 82 | 74 / 222 / 74 | 67 / 201 / 67 |

D's actual survivor re-registration work is **15 / 45 / 15**; 16 includes General Replace before withdrawal.
The rule and check of answer i and the next-change policy of answer iii add **0 / 0 / 0** immediate
re-registrations on their own.
The split row is a package-only envelope; family rewrites can propagate beyond it. Saved Customized Merge rules
can also require re-saving (section 4.2). These scenarios describe analysis, not selected implementation scope.

### 5.4 R62 specification: per-bundle consumer check with a shrinking baseline

**Unit.** Evaluate each admitted manifest bundle, including family bytes materialized from a canonical source.
A profile is registered only by an exact `(profileId, profileVersion)` join in that bundle's trust-index entry.
A map is keyed by bundle plus exact family identity and `mapId`. Do not deduplicate findings across bundles:
the rule governs each bundle's owned definitions, and another package's consumer cannot justify a local carrier.
The exact-family screen is useful diagnosis, but would hide unused copies in General Merge carriers.

**Consumers.** Registered profiles consume their exact `mapBinding.mapIds` (including approved map-set alternatives),
not every map in a `logicalOutputBinding` family. Preserve explicit Standard report/context and bank-parent bindings
through their existing registered owners. An admitted `fullImageMetadataViews` declaration also counts as a map
consumer only in its owning bundle with an exact `metadataProviderFamilies` declaration and a resolvable view/map.
This is the commander's interpretation of in-use shared facts: full-image inspection is an existing active
consumer, and dropping it would alter retained metadata behavior. It is not a registration or new reference kind.
A view copied into B05 without provider admission does not qualify; nor do a filename, test caller, Golden row,
raw family reference to a metadata structure, or the label "shared-facts" alone. [C13, C14, D08]

**Frozen baseline.** Freeze the following **28 screened entries at source 05271cb1d**: 25 map occurrences and
three unregistered profiles. This is 23 current violations (20 maps plus three profiles) and five legitimate
provider observations that must continue passing consumer validation. The four classes are **6 planned
retirements, 2 candidates, 15 deliberate carriers and 5 providers**. Class labels do not create consumers.
The entries below enumerate the complete frozen baseline inventory; a grouped row has one entry per id and bundle.
Only the 23 violations are grandfathered exceptions. The five provider rows are audit records, never exceptions;
losing an admitted view consumer fails even if its map key remains in this inventory.

| Bundle / exact family | Definition ids | Entries | Class / disposition |
| --- | --- | ---: | --- |
| B01 / F01 | `nt51927-standard-merge-256k`, `nt51928-standard-merge-512k` | 2 | Deliberate carrier; tidy at B01's next change, coordinated with R10-01 A-03 timing |
| B02 / F02 | `nt51919-standard-merge-256k`, `nt51929-standard-merge-256k`, `nt51932-standard-merge-256k` | 3 | Deliberate carrier; next B02 change / R10-01 A-01 |
| B03 / F03 | `nt51923-standard-merge-256k`, `nt51926-standard-merge-256k` | 2 | Deliberate carrier; next B03 change / R10-01 A-02 |
| B04 / F01 | `nt51927-standard-merge-256k`, `nt51928-standard-merge-512k` | 2 | Deliberate carrier; next B04 change / R10-01 A-03 |
| B05 / F04 | `nt51950-standard-merge-256k`, `nt51950-standard-merge-512k`, `nt51950-standard-merge-1024k`, `nt51951-standard-merge-256k`, `nt51951-standard-merge-512k`, `nt51951-standard-merge-1024k` | 6 | Deliberate carrier; next B05 change; copied views have no local provider admission |
| B07 / F06 | P13 `nt51926-ctrlram-replace-fw141-cascade@0.7.0` | 1 | Planned retirement, R54 step 4; evidence-oracle obligation in answer ii |
| B16 / F01 | `nt51928-standard-merge-512k` | 1 | Candidate, R10-01 A-03; consumer review before removal |
| B17 / F13 | `nt51927-standard-merge-256k` | 1 | Candidate, R10-01 A-03; preserve the region set's other consumers |
| B18 / F14 | `nt51919-nt51929-nt51932-perfect-map-256k` | 1 | Provider; admitted full-image view, not an unused-map exception |
| B21 / F17 | P51 `nt51950-ab-merge-desay@0.2.1`, P52 `nt51951-ab-merge-desay@0.2.1` | 2 | Planned retirement, R10-04; retain shared Desay recognition |
| B21 / F17 | `nt51950-ab-desay-single-1024k`, `nt51950-ab-desay-cascade-1024k`, `nt51951-ab-desay-1024k` | 3 | Planned retirement, R10-04; remove exclusive seed-anchor region set too |
| B23 / F18 | `nt51923-standard-merge-256k`, `nt51926-standard-merge-256k` | 2 | Provider; admitted full-image views, not unused-map exceptions |
| B24 / F19 | `nt51917-standard-merge-256k`, `nt51927-standard-merge-256k` | 2 | Provider; admitted full-image views, not unused-map exceptions |

**Enforcement.** Baseline keys fix bundle, kind/profile id/version or exact family/map identity; retain source hashes
as provenance. It may only shrink: no new entry, wildcard, rename, version/hash rollover or reclassification may
carry an exception into a changed bundle. A finding outside the baseline fails immediately, even in an otherwise
unchanged bundle. All new bundles must pass without exceptions. Existing feature boundaries in Table 2 remain
analysis, not automatically inferred defects; changed bundles must declare the one-feature ownership and show use.

When a bundle next changes for another reason, remove all of its baseline entries in the same reviewed change.
A violation must be retired or have a real, approved consumer; a provider entry can leave after revalidation proves
its admitted consumer. If unresolved, that bundle's change fails. A change includes manifest/profile/family/schema
entry bytes (also canonical materialization) or registration/provider declarations, not edits to this handoff.
Removing a baseline provider observation never removes the view or weakens ordinary consumer validation.
Retained shared region/recipe/metadata facts use decision 235; removing carrier maps must not remove those facts.
Do not add image-map reference semantics under the guise of this check.

**Order.** In 1.2.4, R54 step 4 removes General Replace and P13 together and completes its R3 evidence approval.
R62 then activates from this frozen list with P13 already removed: at most **27 entries / 22 exceptions**.
It precedes R10-04's next B21 edit; R10-04 removes its five listed entries and exclusive seed anchors while preserving
shared recognition. After both retirements the list is at most **22 entries / 17 exceptions** (provider records
remain five unless their bundles have also changed). If R10-04 has already landed, remove those entries before
activation; never restore a resolved finding. Other earlier cleanups likewise shrink, rather than reset, the list.
R62 activation itself does not force unrelated bundles to change. Future R10-02/03 changes and shared-source
materialization must honor the same next-change rule, using one approved dependency/evidence inventory.

**Acceptance and authority.** Report baseline inventory, violations, legitimate consumers and removed entries
separately. Exercise rejection of new unused maps/unregistered profiles, borrowed cross-bundle registrations,
unadmitted views, changed-bundle exceptions and baseline growth; preserve admitted full-image views and referenced
shared facts. The shrink-only comparison uses the committed predecessor baseline, not a list regenerated from the
candidate. This is a specification; no check or baseline file is implemented here.
Joining a required check, including a verdict in `scripts/repository_contract_validation.py`, is an **R3
governance-owner change**, with independent review and the owner's final approval naming that role.
Profile/policy/evidence cleanup retains its firmware-owner and applicable release-owner gates. [A02-A07]

## 6. Limits, reproducibility and source evidence

### 6.1 What was and was not verified

Verified by read-only standard-library parsing/hashing: inventory counts, every declared entry's raw source hash,
every entry-array/index content hash, unique profile/registration joins, policy coverage, dependency-set counts and
Golden row joins. No real firmware bytes or firmware-byte hashes are reproduced in this document.

No build, test, repository verifier, C# catalog/materializer execution, fresh runtime fingerprint calculation,
Golden output execution, byte comparison, write-range execution audit, prebuilt rebuild or release smoke was run.
Nothing was committed, pushed, published or fetched. No network access or other-worktree write was used.
The R54 feasibility report's historical experiment is recorded evidence only; it is not a run on this source. [D06]

Inferences, explicitly bounded:

- Desay dead definitions have an accepted planned retirement, while its shared recognition remains.
  P13's incidental runtime-coupling classification is an assessment judgment; decision 269 decides its removal
  with R54, while its active evidence-oracle disposition remains an R3 firmware-review obligation.
  Other sibling units and the F01/F13 maps remain candidates. The nineteen-container screen intentionally over-includes
  candidates.
- Identity-change consequences are inferred from the hash writers and joins; the actual C# identities were not
  recomputed. Table 2 assumes retained dependent functionality receives a reviewed replacement source when necessary.
- Shared-family propagation follows every exact triple in the **whole family file**, including unused definitions.
  Its envelopes are conservative file-rehash consequences, not claims that every product executes every referenced
  metadata structure.
- T01's 65 bundle-hash literal occurrences exclude runtime-assembled and synthetic/mutation hashes, saved external
  rules and private evidence. T02 separately counts fifteen current route-fingerprint literal occurrences;
  neither literal census proves complete test or external-rule migration coverage.
- A "zero" route count for a shared-facts provider excludes its metadata-view/admission identity and indirect changes
  through family references. Its full consumer graph is covered separately.
- R10-02/03 are allocation items at `docs/handoff/1.1.14/1.2.x-allocation.md:211`; this worktree has no
  standalone R10-02/R10-03 reports. Their mechanism and migration are cited from decision 235 and R10-01.
- Future totals depend on owner grouping, actual splits and the implementation source frozen for review.

### 6.2 Reproduction recipe

This final revision independently recounted source 05271cb1d with inline standard-library scripts
(`python -B -`) in the declared test-area temp environment. No prior review count was used as a computed result.
Only JSON definitions, text source and test text were read; the algorithm never opens BIN files.
Python modules: `json`, `pathlib`, `hashlib`, `collections`; direct shell `rg` supplied source searches.
All current bundle hashes and route fingerprints were searched literally in repository test text;
generated bin/obj content was excluded. All 24 bundle hashes and 124 raw entry hashes matched.

The calculation is reproducible by the following steps, without running a product verifier:

1. Enumerate `profiles/built-in/*/profile-bundle.json` and compare directory ids with `index.bundles`.
2. Resolve each entry to its local file or the index's declared canonical-family/schema source.
   SHA-256 the raw bytes and compare `entry.contentHash`.
3. Sort entries by `(entryId, kind, path, schemaId, contentHash)`; project the five keys in the C10 order.
   Encode `json.dumps(array, ensure_ascii=False, separators=(',', ':'))` as UTF-8 and hash it.
   Compare both manifest and index hashes. Current hash-field strings are ASCII; this is an emulation of the current
   validated array, not a replacement general RFC 8785 implementation.
4. Join every registration to a profile by exact id/version. Read `experience.experienceId` separately.
   Count unmatched profiles; do not infer workflow from a filename.
5. Join policy by exact IC/workflow and map or declared mapVariantSetId; General Merge uses `generic`.
   For CtrlRAM, single-chip matches 1-ic, two-chip 2-ic, three-chip 3-ic, and cascade its non-single count interval.
   Confirm each row has one definition, with the ten bank rows handled by C05/C17's parent join.
6. Add each local CtrlRAM product to the same-IC Standard bundle's dependency set when reportMetadataMapId or
   memoryLayoutContextMapId is declared. Add bank products to both parent bundle sets and declared local memory context;
   a local report-only counterpart does not enter the bank fingerprint through that report binding.
7. For each resulting unique route-id set, count policy rows, actual authoring/publication/evidence decision objects
   and Golden target rows. Separately count alias rows whose sourceRouteId is affected but whose routeId is outside
   the set. Withdraw a feature's direct and composite choices before counting Table 2's remaining set.
8. Key families by exact id/version/hash. Recursively extract exact familyId/familyVersion/familyContentHash triples.
   Propagate incoming references to a fixed point, then bound profiles and containing bundles; union their product
   sets. This yields the conservative family envelopes, not used-node semantics.
9. Search every current bundle hash in textual `tests/` inputs; count distinct file:line occurrences and retain
   their locations. The total is 65 occurrences. Separately search all current policy route fingerprints for
   fifteen literal occurrences (T02). Review meaning before changing any expected value.
10. Count postbuild profiles/distinct ICs and join all CtrlRAM routes, including banks. For every actual schema
    entry, group the index's materialization source filename and union its bundles' route sets; ignore a schema
    filename with no corresponding manifest entry. This yields section 2.3's schema envelopes.
11. Count nonempty family-level fields by exact identity. Subtract registered profiles' exact map bindings
    separately per bundle and per exact family: 25 occurrences / 11 bundles versus 13 exact-family maps.
    Resolve admitted provider/view consumers locally, subtract their five maps, and retain the 20 per-bundle
    exceptions. Join the three unregistered profiles and classify all 28 screened entries in section 5.4.
12. Union B07 and B21, withdraw General Replace, then subtract their disjoint eight- and seven-route survivor sets
    from the all-route and nineteen-multi-profile scenarios. Count decision objects and Golden joins for each set:
    gross 85 / 255 / 85 and 83 / 249 / 83; batched increments 69 / 207 / 69 and 67 / 201 / 67.
    Enumerate every exact bundle-hash and route-fingerprint test literal, preserving distinct file:line occurrences.

For explicit byte-array mechanics, step 3 uses:

```python
fields = ("entryId", "kind", "path", "schemaId", "contentHash")
ordered = sorted(manifest["entries"], key=lambda entry: tuple(entry[key] for key in fields))
projected = [
    {key: entry[key] for key in ("contentHash", "entryId", "kind", "path", "schemaId")}
    for entry in ordered
]
payload = json.dumps(projected, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
digest = hashlib.sha256(payload).hexdigest()
```

### 6.3 Code, authority and decision sources

| Key | File:line | Claim supported |
| --- | --- | --- |
| C01 | `src/NvtFwCombiner.Application/Capabilities/CapabilityDefinitionFingerprint.cs:29; :64; :80; :112` | Identity inputs, set normalization and framing |
| C02 | `docs/adr/0046-capability-and-compilation-fingerprint-boundary.md:33; :45; :51` | Accepted reviewed-definition and per-compilation boundary |
| C03 | `scripts/canonical_golden_validation.py:459; :483; :567; :689` | Golden policy join and alias-source fields |
| C04 | `src/NvtFwCombiner.Infrastructure/Composition/CanonicalCompiledRouteInventory.cs:72` | Static identity producer |
| C05 | `src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.Banks.cs:35; :72; src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalog.BankDefinitions.cs:14; :98` | Composite parent joins and identity |
| C06 | `src/NvtFwCombiner.Infrastructure/Composition/CtrlRamV2RouteRegistry.cs:81; :109; :145; :180` | Same-IC Standard report/context dependency |
| C07 | `src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.cs:100; :204; :269; :295; :328; :479` | Dynamic map-set/envelope/General identity producers |
| C08 | `src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.cs:355; :389; src/NvtFwCombiner.Application/MemoryLayout/MemoryLayoutContextMap.cs:33` | CtrlRAM processor/report/context identity bindings |
| C09 | `eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleTrustIndex.tasks:510; :516; :545` | Canonical-family/schema source resolution and hashing |
| C10 | `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleEntryArrayHasher.cs:21; :34; :45; :48; src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleFileSnapshot.cs:70; :146` | Whole-array and raw-entry hashing |
| C11 | `src/NvtFwCombiner.Infrastructure/Composition/BuiltInCanonicalMetadataDefinitionResolver.cs:21; src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2Bundle.cs:331` | Provider selection and exact family metadata reference resolution |
| C12 | `F01-F19 family-reference table in section 2` | Exact data edges used by the conservative propagation calculation |
| C13 | `src/NvtFwCombiner.Infrastructure/Composition/CanonicalFullImageMetadataInventory.cs:15; :25; src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2Bundle.cs:308; :324; src/NvtFwCombiner.Application/Capabilities/CanonicalFullImageMetadataModels.cs:30` | Provider/view admission identities |
| C14 | `src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalogFactory.cs:274; :305; src/NvtFwCombiner.Profiles/V2/CompositionProfileNormalizer.CompilationContext.cs:48` | Exact family hash binding for physical and logical profiles |
| C15 | `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleManifestNormalizer.cs:10; :16; :91` | Manifest schema/algorithm/hash admission |
| C16 | `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleLoader.cs:21; :133; src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleInventoryVerifier.cs:23` | External trust-anchor and closed inventory checks |
| C17 | `src/NvtFwCombiner.Domain/Composition/RuntimeReferenceBankReplaceV2CompilationContext.cs:53; :81; :99; src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalog.BankDefinitions.cs:139` | Composite hash inputs and trusted source construction |
| C18 | `src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.Fingerprint.cs:10; :27; :47; :98; :114; :138; :179` | Postbuild-plan identity and maximum write authority |
| C19 | `src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalogSource.cs:127; src/NvtFwCombiner.Infrastructure/Capabilities/BuiltInCanonicalCapabilityPolicy.cs:139` | Policy and computed identity admission |
| C20 | `docs/contracts/profile-bundle-v1.schema.json:1; docs/contracts/profile-bundle-package-trust-index-v1.schema.json:1; profiles/built-in/package-trust-index.json:1` | Current packaging declaration contracts; no retirement-unit field |
| C21 | `src/NvtFwCombiner.Infrastructure/Bundles/AcceptedPrebuiltProfileCatalog.cs:62; :69; :89` | Prebuilt trust/bundle tuple and manifest admission |
| C22 | `src/NvtFwCombiner.Infrastructure/Bundles/BuiltInProfileBuildAdmissionIdentity.cs:58; :69` | Raw index and manifest-set build identity |
| C23 | `src/NvtFwCombiner.Application/Capabilities/CanonicalDynamicCapabilityModels.cs:165; :333; :370` | Compilation provenance and fingerprint contract checks |
| C24 | `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.Banks.cs:454` | Exact bank parent compilation-source matching |
| C25 | `src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2Bundle.cs:287` | Saved Rule bundle/profile/family parent binding |
| C26 | `scripts/release_source_pins.py:11; :38; :77` | Reviewed policy/index/allowlist release source pins |
| C27 | `src/NvtFwCombiner.Infrastructure/ExternalTools/BuiltInPostbuildProfileCatalog.cs:8; :21; :26; profiles/built-in/ctrlram-postbuild-v2/catalog.json:60` | Separate postbuild catalog and exact-byte admission; eleven profiles for ten ICs |
| C28 | `src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.cs:358; :399; :441; src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.Banks.cs:75; :78` | Selected-plan fingerprints in all local and bank CtrlRAM routes |
| C29 | `profiles/built-in/nt51950-ab-merge/families/nt51950-ab-merge.json:5; :10; :18; :206; :597; :624; :652; src/NvtFwCombiner.Application/Capabilities/CanonicalAbAuthoringDefinition.cs:78; :82; src/NvtFwCombiner.Application/Authoring/AbMergeAuthoringExperience.Format.cs:68; :103; :259` | Desay recognition and Common-map variants are shared; exclusive seed anchors belong to three retiring maps |
| C30 | `tests/NvtFwCombiner.Bootstrap.Tests/CtrlRamV2PlanClosureProfileTests.cs:13; :15; :21` | Nine plan-closure bundle pins, including NT51926 |
| C31 | `src/NvtFwCombiner.Infrastructure/Composition/SavedCompositionRuleV2Admission.cs:70; :91; :95; :97` | Exact Saved Rule v2 parent comparison and bundle id/version/hash |
| C32 | `profiles/built-in/nt51927-standard-merge/families/nt51927-nt51928.json:54; :546; :562; profiles/built-in/nt51928-standard-merge/families/nt51927-nt51928-v1.5.json:301; :318; :351` | F01/F13 candidate maps and region-set consumers |
| C33 | `src/NvtFwCombiner.Infrastructure/FlashMaps/BuiltInTpFlashMapCatalog.Loader.cs:9; :10; :19`; `src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.Fingerprint.cs:17; :27; :47; :107`; `src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.cs:358; :399; :441`; `src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.Banks.cs:75` | Pinned flash-map admission is separate; route identity uses selected postbuild plan facts, not raw flash-map bytes/hash |
| C34 | `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-cascade.json:17; :197; :233`; `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-runtime-cascade.json:16; :119; :157`; `profiles/built-in/package-trust-index.json:205`; `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.cs:82; :134; :145` | P13 resolved-map processor with staged sources versus registered P15 runtime mappings plus narrowed postbuild; different operations for the comparison route |
| A01 | `docs/governance/authority-policy.json:250` | R0 non-consumed handoff prose |
| A02 | `docs/governance/authority-policy.json:15` | R3 firmware-owner paths |
| A03 | `docs/governance/authority-policy.json:43` | R3 contracts: firmware-owner and release-owner |
| A04 | `docs/governance/authority-policy.json:126` | R3 approval/governance authority |
| A05 | `docs/governance/authority-policy.json:192; :208` | R2 design and ordinary tooling |
| A06 | `docs/governance/authority-policy.json:57; :85; :97; :156` | Release paths/source pins and repository verdict authority |
| A07 | `AGENTS.md:121; :129; :130; :135; :136; :137; :146; :155; :186; :188; :193` | One owner, migration deletion boundary, risk gates and actual Golden execution |
| D01 | `docs/handoff/1.2.x.md:725; :739; docs/handoff/1.2.4/R54-retirement-plan.md:1` | Decisions 266/267 and retirement inventory |
| D02 | `AGENTS.md:143` | Declared facts are not inferred from filename, hash or observation |
| D03 | `profiles/built-in/package-trust-index.json:6; docs/contracts/canonical-capability-policy-v1.json:6; testdata/golden/canonical/manifest.json:140` | Full input arrays for computed inventory, decisions and evidence counts |
| D04 | `docs/handoff/1.1.12.md:1347; :1358; docs/handoff/1.2.x.md:157; :161; :301` | Decisions 195/213/227: retire Desay maps/profiles, preserve AB format policy |
| D05 | `docs/governance/change-records/DP-REPLACE-RETIREMENT-DATA-110-01.json:45; :52` | Exclusive-package DP retirement and retained survivor definitions |
| D06 | `docs/handoff/1.2.4/R54-option-a-feasibility.md:14; :153` | Static coupling explanation and separately recorded experiment |
| D07 | `docs/handoff/1.1.14/1.2.x-allocation.md:211; :214; docs/handoff/1.2.x.md:161; :301` | R10-04 Desay retirement and retained format policy; same-version R3 changes |
| D08 | `docs/handoff/1.2.x.md:385; :388; :393; :404; docs/handoff/1.2.1/R10-01.md:87; :100; :101; :238; :248; :251; :255` | Decision 235's accepted references, A-03/A-04, six bank routes, next-change migration and evidence boundary |
| D09 | `tests/NvtFwCombiner.Bootstrap.Tests/Nt51926CtrlRamReplaceCandidateProfileTests.cs:244; :315; :330; :342; :343; :348; :574; :580`; `tests/NvtFwCombiner.Architecture.Tests/BootstrapCliBoundaryTests.BootstrapStructure.cs:192`; `testdata/golden/canonical/manifest.json:377; :381`; `testdata/golden/canonical/NT51926/ctrlram-replace/fw1.4.1/cascade-2/nt51926-fw141-cascade2-auto-prj-597-20260717/provenance/case.json:12`; `docs/governance/v0.9.9-legacy-retirement-evidence.md:257`; `docs/handoff/1.2.4/R54-retirement-plan.md:1` | Runtime-unregistered P13 is a Golden-cited executable parity oracle; deletion edits firmware evidence and breaks a dated link. P15 is the registered retarget candidate but compiles different operations (C34). The owner question omitted these consequences; present them at R54 step 4 R3 firmware approval. |
| D10 | Owner answers supplied for this revision: 2026-10-02, board decision 269 (commander records the board) | Only the three decisions in section 5; R61/R62 allocation supplied by the commander, not a fourth owner answer |
| T01 | `Literal hash occurrence table in section 6.4` | All located current bundle-hash test literals |
| T02 | `tests/NvtFwCombiner.Bootstrap.Tests/CtrlRamDirectTpGoldenExecutionTests.cs:20; :24; :28; :32; :36; :40; :44; :48; :52; :56; :60; :64; :68; tests/NvtFwCombiner.Bootstrap.Tests/CanonicalCapabilityCatalogMigrationTests.cs:90; tests/NvtFwCombiner.Infrastructure.Tests/Capabilities/BuiltInCanonicalCapabilityPolicyTests.cs:41` | Fifteen current route-fingerprint literal occurrences, separate from bundle hashes |

### 6.4 Exact current bundle-hash test occurrences

T01 lists every located literal, with repeated occurrences retained as separate file:line locations.
A literal in a retired-feature positive test may need retirement coverage instead of a new expected hash.
No expected value is changed by this evaluation.

| Bundle | Occurrences | Test source locations |
| --- | ---: | --- |
| B01 | 2 | `tests/NvtFwCombiner.Bootstrap.Tests/GeneralMergeV2CandidateProfileTests.cs:14, :22` |
| B02 | 3 | `tests/NvtFwCombiner.Bootstrap.Tests/GeneralMergeV2CandidateProfileTests.cs:70, :78, :86` |
| B03 | 2 | `tests/NvtFwCombiner.Bootstrap.Tests/GeneralMergeV2CandidateProfileTests.cs:30, :38` |
| B04 | 1 | `tests/NvtFwCombiner.Bootstrap.Tests/GeneralMergeV2CandidateProfileTests.cs:46` |
| B05 | 3 | `tests/NvtFwCombiner.Bootstrap.Tests/GeneralMergeV2CandidateProfileTests.cs:54, :62`; `tests/NvtFwCombiner.Bootstrap.Tests/SavedRuleCliCommandTests.TestSupport.cs:55` |
| B06 | 1 | `tests/NvtFwCombiner.Bootstrap.Tests/CtrlRamV2PlanClosureProfileTests.cs:14` |
| B07 | 2 | `tests/NvtFwCombiner.Bootstrap.Tests/CtrlRamV2PlanClosureProfileTests.cs:15`; `tests/NvtFwCombiner.Bootstrap.Tests/ReplaceCliCommandTests.General.cs:630` |
| B08 | 1 | `tests/NvtFwCombiner.Bootstrap.Tests/CtrlRamV2PlanClosureProfileTests.cs:13` |
| B09 | 1 | `tests/NvtFwCombiner.Bootstrap.Tests/CtrlRamV2PlanClosureProfileTests.cs:21` |
| B10 | 1 | `tests/NvtFwCombiner.Bootstrap.Tests/CtrlRamV2PlanClosureProfileTests.cs:17` |
| B11 | 3 | `tests/NvtFwCombiner.Bootstrap.Tests/AbCtrlRamReferencePlanTests.cs:701`; `tests/NvtFwCombiner.Bootstrap.Tests/AbCtrlRamRuntimeWiringTests.cs:482`; `tests/NvtFwCombiner.Bootstrap.Tests/CtrlRamV2PlanClosureProfileTests.cs:16` |
| B12 | 1 | `tests/NvtFwCombiner.Bootstrap.Tests/CtrlRamV2PlanClosureProfileTests.cs:18` |
| B13 | 1 | `tests/NvtFwCombiner.Bootstrap.Tests/CtrlRamV2PlanClosureProfileTests.cs:19` |
| B14 | 1 | `tests/NvtFwCombiner.Bootstrap.Tests/CtrlRamV2PlanClosureProfileTests.cs:20` |
| B15 | 5 | `tests/NvtFwCombiner.Bootstrap.Tests/BuiltInV2StandardMergeRoutingTests.cs:105, :106`; `tests/NvtFwCombiner.Bootstrap.Tests/CanonicalSourceProjectionBuiltInBundleTests.cs:10`; `tests/NvtFwCombiner.Bootstrap.Tests/CanonicalV2StandardMergeGoldenTests.cs:14, :15` |
| B16 | 5 | `tests/NvtFwCombiner.Bootstrap.Tests/BuiltInV2StandardMergeRoutingTests.cs:103, :107`; `tests/NvtFwCombiner.Bootstrap.Tests/CanonicalSourceProjectionBuiltInBundleTests.cs:11`; `tests/NvtFwCombiner.Bootstrap.Tests/CanonicalV2StandardMergeGoldenTests.cs:16, :17` |
| B17 | 4 | `tests/NvtFwCombiner.Bootstrap.Tests/BuiltInV2StandardMergeRoutingTests.cs:108`; `tests/NvtFwCombiner.Bootstrap.Tests/CanonicalSourceProjectionBuiltInBundleTests.cs:12`; `tests/NvtFwCombiner.Bootstrap.Tests/CanonicalV2StandardMergeGoldenTests.cs:18, :19` |
| B18 | 1 | `tests/NvtFwCombiner.Bootstrap.Tests/SharedDpcmiMetadataProviderTests.cs:19` |
| B19 | 8 | `tests/NvtFwCombiner.Bootstrap.Tests/BuiltInV2StandardMergeRoutingTests.cs:104, :109, :110`; `tests/NvtFwCombiner.Bootstrap.Tests/CanonicalSourceProjectionBuiltInBundleTests.cs:13`; `tests/NvtFwCombiner.Bootstrap.Tests/CanonicalSourceProjectionByteShapeTests.cs:16`; `tests/NvtFwCombiner.Bootstrap.Tests/CanonicalV2StandardMergeGoldenTests.cs:11, :12, :13` |
| B20 | 8 | `tests/NvtFwCombiner.Bootstrap.Tests/AbCtrlRamAuthoringTests.cs:533`; `tests/NvtFwCombiner.Bootstrap.Tests/AbCtrlRamReferencePlanTests.cs:696`; `tests/NvtFwCombiner.Bootstrap.Tests/AbCtrlRamRegistryTests.cs:115`; `tests/NvtFwCombiner.Bootstrap.Tests/AbCtrlRamRuntimeWiringTests.cs:478`; `tests/NvtFwCombiner.Bootstrap.Tests/AbDummyDpCompilationTests.cs:100`; `tests/NvtFwCombiner.Bootstrap.Tests/AbMergeGoldenRegressionTests.cs:18`; `tests/NvtFwCombiner.Bootstrap.Tests/CanonicalSourceProjectionBuiltInBundleTests.cs:14`; `tests/NvtFwCombiner.Bootstrap.Tests/Nt51919Nt51929Nt51932AbMergeSupportedProfileTests.cs:12` |
| B21 | 8 | `tests/NvtFwCombiner.Bootstrap.Tests/AbDummyDpCompilationTests.cs:99`; `tests/NvtFwCombiner.Bootstrap.Tests/AbMergeAuthoringDefinitionTests.cs:81`; `tests/NvtFwCombiner.Bootstrap.Tests/AbMergeFormatVariantProfileTests.cs:14`; `tests/NvtFwCombiner.Bootstrap.Tests/AbMergeGoldenRegressionTests.cs:20`; `tests/NvtFwCombiner.Bootstrap.Tests/CanonicalSourceProjectionBuiltInBundleTests.cs:15`; `tests/NvtFwCombiner.Bootstrap.Tests/Nt51950AbDpRegionTests.cs:17`; `tests/NvtFwCombiner.Bootstrap.Tests/Nt51950AbMergeCandidateProfileTests.cs:15`; `tests/NvtFwCombiner.Bootstrap.Tests/Nt51951AbMergeCandidateProfileTests.cs:14` |
| B22 | 3 | `tests/NvtFwCombiner.Bootstrap.Tests/BuiltInV2StandardMergeRoutingTests.cs:252`; `tests/NvtFwCombiner.Bootstrap.Tests/CanonicalSourceProjectionBuiltInBundleTests.cs:16`; `tests/NvtFwCombiner.Bootstrap.Tests/Nt51950Nt51951V2StandardMergeGoldenTests.cs:10` |
| B23 | 0 | None found |
| B24 | 0 | None found |
