# R08-01: B-bank CRC and postbuild mode comparison

Date: 2026-10-03. Source: `8479dee8ee95dbcee9f38ecf4793e70858c9ff41`.
Branch: `feature/1.2.5/r08-01-b-bank-comparison`.
Status: preliminary. This evaluation rests on the C# product, the recorded history and the owner's documents; the
pinned Python implementation (`third-party/nvt_combiner`, not initialized in this checkout) was not inspected and no
Combiner was run, so R08-01 is not closed until its mode, parameters and read/write contract are checked. R08-02 and the
firmware-owner answers remain open.

## 1. Purpose, scope and evidence labels

This evaluation supplies the comparison requested by R08-01 so the firmware owner can decide R08-02.
It does not select a production mode, authorize implementation, change support, or replace R07-02 parity.
The allocated sequence is R08-01 -> R08-02 -> R31-02/03 -> R07-02 -> R07-03. [T, A]

The scope is the B bank of AB images for NT51919/NT51929/NT51932 and NT51950/NT51951, including the
AB Merge declarations and the bank Replace processing needed to compare CRC-only with full postbuild.
The policy has AB Merge routes for those five ICs. Other supported ICs have no AB Merge policy row in
this source. Retained special Desay declarations are inventoried without enabling them. [POL, IDX, D213]

Evidence labels used throughout:

- **Declared fact**: a profile, map, processor contract, policy decision or approved case declaration.
- **Observed source**: a static code reading checked here; it does not prove fresh execution or hardware safety.
- **Historical observation**: a checked repository record of an earlier experiment or source investigation.
- **Evaluation**: a consequence, option or recommendation derived here; it is not an accepted firmware contract.
- **Unknown**: evidence absent or an owner fact still unresolved, particularly under decision 238.

All citations use repository-relative `file:line` locations in section 8. Tables give profile/map locations
directly. All address ranges below are half-open. `delta` means the declared A-to-B placement difference;
bank-local file offsets, processor work-buffer offsets and final `flash` offsets are distinguished.

**Inspection limit:** the pinned `third-party/nvt_combiner` directory is empty in this checkout; its submodule
is not initialized. Its gitlink is `6490edf3a357f6047a1e3f3aa1c1d70a767e68eb`, matching the source record.
No network retrieval was attempted. Published-Python behavior is therefore reported only as far as the
records establish it. The older uploaded `refcode/ab_code_combiner` is a separate reference, not that
published implementation. [PY, D187, REF]

Decision 187 keeps legacy C Combiner 1.13.0 in use until behavior comparison; intake commit `d7b08b92...`,
product version `2.0.0.1` and legacy banner `1.13.0.0` are different identifiers. The published source pin
does not identify a deployed Python executable. [D187, PY, ROAD, R07]

**Declared boundary:** decision 238 leaves readers, timing, required Header-copy generation, same-TP A/B
CRC equality and supporting evidence unknown. The owner's understanding of mismatch handling is
"ignore and continue", with the checked CRCs uncertain. The unnamed extension of decision 161 is an
owner statement, not safety evidence. Existing allowances stay; each extended combination needs its own
owner confirmation. No option below assumes that Header copy is unused or safe. [D238]

## 2. Current AB Merge modes by family

The labels **R**, **H**, **C** and **F** below are comparison labels, not proposed CLI identifiers.
**R** is relocation-only assembly; **H** is relocation plus B Header CRC finalization;
**C** is the proposed CRC-only treatment of a changed B image; **F** is full bank-local postbuild.
R and H assemble pre-existing TP inputs. F changes selected content of an immutable AB reference.
C is not a registered B-bank mode in the inspected AB profiles/invocation catalog. [P19, P29, P32, P50, P51, INV]

### 2.1 Perfect family: NT51919 / NT51929 / NT51932

**Declared fact:** each profile clones its own `tp-b-input` into `tp-b-work`, capacity `0x40000`.
It seeds the DP container, overlays A, adds `delta=0x40000` to three B U32 little-endian address words,
then overlays the relocated B TP section. The read/overlay section is bank-local `[0x7000,0x40000)`;
the final B section is `flash [0x47000,0x80000)`. There is no external processor, CRC refresh or
Header-copy operation in this AB Merge sequence. Copy-purpose metadata bindings do not add operations.
[P19, P29, P32, FP]

| IC / profile / map | B mode, derivation and order | Processor write authority / import | Golden coverage and limits | Checked profile and map locations |
| --- | --- | --- | --- | --- |
| NT51919; `nt51919-ab-merge-alias@0.4.0`; `nt51919-ab-merge-512k` | R: DP seed -> A TP -> B-local clone relocation -> B TP overlay | No processor; scalar writes `[0x7164,0x7168)`, `[0x7168,0x716C)`, `[0x716C,0x7170)` in `tp-b-work`; copy its TP section to B | `nt51919-ab-t05-d06-alias` references G29; approved family facts, not a direct NT51919 product Golden | `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51919-ab-merge.json:99,116,266,274`; `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/families/nt51919-nt51929-nt51932-ab-merge.json:39,90,134,256` |
| NT51929; `nt51929-ab-merge@0.4.0`; `nt51929-ab-merge-512k` | R; same declared sequence and bank geometry | No processor; same three scalar words and TP-section copy | G29 `nt51929-ab-t05-d06`, complete 512 KiB output; no certification of changed-content B postbuild, cascade or boot/recovery | `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51929-ab-merge.json:128,185,393,411,465,475`; `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/families/nt51919-nt51929-nt51932-ab-merge.json:39,90,134,283` |
| NT51932; `nt51932-ab-merge@0.4.0`; `nt51932-ab-merge-512k` | R; same declared sequence and bank geometry | No processor; same three scalar words and TP-section copy | `nt51932-ab-t05-d06-alias` references G29; direct-case bytes only; named NT51932 configuration parity is separate | `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51932-ab-merge.json:90,98,248,250,253,256`; `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/families/nt51919-nt51929-nt51932-ab-merge.json:39,90,134,310` |

**Historical/source fact:** the recorded legacy `NT51932BasedMergeABMode` adds those three addresses
and calls neither CRC nor Header-copy routines. The recorded Header CRC read interval is bank-local
`[0x7104,0x7128)`, outside all three relocated words; Header CRC is stored at `[0x7100,0x7104)` and
DLM CRC 0 at `[0x7118,0x711C)`. Relocation therefore does not itself change this Header CRC's input.
This conclusion is specific to that source and family, not a general rule for all integrity fields. [LC, R31]

### 2.2 Partial family: NT51950 / NT51951

**Declared fact:** H clones B's own TP input into `tp-b-work`, capacity `0x37000`, adds the placement
delta only to DIFF address `[0xA120,0xA124)`, and overlays bank-local TP `[0xA000,0x37000)`.
It stages the completed output A/B banks as immutable named artifacts and copies them into the private
`ab-combiner-work` transport image. It runs the input-output-file invocation, then imports only three
four-byte slices: B ILM address, B DLM address and B Header CRC. ILM/DLM CRC words are not these addresses.
No Header-copy window is imported from the processor result; its existing bytes arrive in the TP overlay.
Other processor output is not imported. The declared processor read view is the entire transport image
`[0,2*delta)`; the immutable A/B artifacts each contain one bank of length `delta`. These transport read
bounds are distinct from the Header CRC algorithm's read interval. [P50, PC50, P51, PD50, PD51, INV]

| IC / profile / map | B mode and geometry | Processor invocation, exact authority and import | Golden coverage / missing coverage | Checked profile and map locations |
| --- | --- | --- | --- | --- |
| NT51950 single; `nt51950-ab-merge@0.8.0`; `nt51950-ab-merge-512k` | H; delta `0x40000`; B TP `flash [0x4A000,0x77000)`; transport `[0,0x80000)` | `nfc-nt51950-ab-merge-combiner-v1`: `NT51950BASED_MERGE_AB_MODE CRC8 <a-bank> <b-bank> <output> 0x40000`; allowed writes/imports in transport: `[0x4A100,0x4A104)`, `[0x4A110,0x4A114)`, `[0x4A130,0x4A134)` | G50B/G50H complete 512 KiB outputs; G50O complete 1 MiB source-envelope output, still this map and delta. No changed-content bank Replace or Header-copy consumer certification | `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge.json:25,110,132,140,189,206,217,224`; `profiles/built-in/nt51950-ab-merge/families/nt51950-ab-merge.json:145,169,194,679` |
| NT51950 cascade; `nt51950-ab-merge-cascade@0.4.0`; `nt51950-ab-merge-1024k` | H; delta `0x80000`; B TP `flash [0x8A000,0xB7000)`; transport `[0,0x100000)` | `nfc-nt51951-ab-merge-combiner-v1`: same argv family, final argument `0x80000`; allowed writes/imports `[0x8A100,0x8A104)`, `[0x8A110,0x8A114)`, `[0x8A130,0x8A134)` | No direct cascade AB Golden; route evidence is contract-only. Single outputs do not pin this geometry | `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge-cascade.json:85,101,106,109,179,220,228,261,268`; `profiles/built-in/nt51950-ab-merge/families/nt51950-ab-merge.json:320,442,467,696` |
| NT51951; `nt51951-ab-merge@0.7.0`; `nt51951-ab-merge-1024k` | H; delta `0x80000`; same B TP and transport intervals as previous row | `nfc-nt51951-ab-merge-combiner-v1`; same exact three `0x8A...` words; DIFF was already relocated by the host | Two G51 workflow aliases of G50B/G50H; no independent NT51951 product bytes; `0x80000` placement is synthetic topology evidence | `profiles/built-in/nt51950-ab-merge/profiles/nt51951-ab-merge.json:46,54,59,62,111,115,126,133`; `profiles/built-in/nt51950-ab-merge/families/nt51950-ab-merge.json:320,442,467,723` |
| Retained NT51950 special Desay; `nt51950-ab-merge-desay@0.2.1`; `nt51950-ab-desay-single-1024k` and `nt51950-ab-desay-cascade-1024k` | H declaration; delta `0x40000`; transport `[0,0x80000)`, despite 1 MiB map capacity | `nfc-nt51950-ab-merge-combiner-v1`; same three `0x4A...` words; TP-only imports | Unregistered profile; no direct Golden for these maps. Retirement is R10-04; not restored by this evaluation | `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge-desay.json:77,93,98,101,171,212,248`; `profiles/built-in/nt51950-ab-merge/families/nt51950-ab-merge.json:145,194,597,624` |
| Retained NT51951 special Desay; `nt51951-ab-merge-desay@0.2.1`; `nt51951-ab-desay-1024k` | H declaration; delta `0x40000`; transport `[0,0x80000)` | Uses the NT51950 invocation, not the NT51951 Common-map invocation; same three `0x4A...` words | Unregistered; no direct Golden; retirement is R10-04 | `profiles/built-in/nt51950-ab-merge/profiles/nt51951-ab-merge-desay.json:77,93,98,101,171,212,253,260`; `profiles/built-in/nt51950-ab-merge/families/nt51950-ab-merge.json:145,194,652` |

**Declared distinction:** both Common and recognized Desay format values currently select the Common-map
variants in `abFormatPolicy`; the two retained special Desay profiles are absent from runtime registrations.
Three special Desay maps and two profiles are to retire under R10-04, while format recognition/specialization
remains. Output length alone must not choose the B delta: G50O has a 1 MiB envelope but a `0x40000` delta.
[FQ, IDX, G50O, D213, A]

**Declared distinction:** Perfect profiles say `promotion.stage=supported`; all five Partial profiles say
`executable-candidate`. Current route policy separately publishes NT51950 single and NT51951 AB Merge
as supported, NT51950 cascade as candidate, and all ten bank Replace routes as candidate.
These are different declarations; a profile blocker or a Golden alone must not be substituted for route
publication authority. This evaluation changes none of them. [P19, P29, P32, P50, PC50, P51, PD50, PD51, POL]

## 3. CRC-only versus normalize -> postbuild -> restore

### 3.1 Exact differences in treatment

| Comparison dimension | R: relocation-only AB assembly | H: B Header finalization in AB assembly | C: proposed changed-B CRC-only | F: full bank-local postbuild / bank Replace |
| --- | --- | --- | --- | --- |
| Starting image | B's own immutable TP input, cloned locally | B's own immutable TP input, cloned locally; completed A/B banks staged | Must be B's own reference slice with explicit selected replacements; no accepted B-mode contract yet | Current compiler slices each selected bank's own immutable AB reference, not A as a B surrogate [BANK] |
| Address treatment | Add delta to `0x7164/68/6C` | Host adds delta at `0xA120`; tool relocates `0xA100/110` | Reads must map final addresses to declared local ranges, or normalize explicitly; CRC-only is not permission for out-of-buffer reads | B normalizes all three addresses before local processing. Perfect restores three afterward; Partial restores DIFF then uses H to finalize ILM/DLM and Header CRC [BANK] |
| Header CRC coverage | Recorded `[0x7104,0x7128)` excludes relocated fields [LC] | Recorded `[0xA100,0xA130)` includes all three fields; write `[0xA130,0xA134)` [LC, CFG] | Must be family-specific; a post-relocation Header refresh is needed for the Partial rule, but not implied for Perfect [LC] | Local CRC/header processing first; restoration follows family rule. Partial's stale pre-copy CRC seed is unresolved [STALE] |
| Header copy | Preserved from TP input; no copy command [P29] | Preserved from TP input; not one of three imports [P50] | By definition does not reproduce a full copy/Backup/marker sequence; whether that is sufficient is unknown [ROAD, D238] | Two catalog commands each carry Header-copy blocks: Perfect `[0x7000,0x7200)` -> `[0x27EF0,0x280F0)`; Partial `[0xA000,0xA200)` -> `[0x2D30C,0x2D50C)` [CAT, R31] |
| Processor write bounds | None | Exactly 12 bytes of declared transport authority, at the three words, not a 12-byte mandatory diff [INV, P50, P51] | Not declared; cannot borrow H's 12-byte bounds for DLM CRC or full postbuild | Selected content/Backup/marker bounds plus Header-copy and derived CRC bounds; topology adds different authority [WR, IR, CAT] |
| Copy back | Relocated TP section | Host TP overlay, then only three Header words from tool output | Would need a new explicit profile-bound import contract | Perfect publishes the processed selected local bank; Partial publishes its selected local slice, preserves remaining bank bytes and performs the bounded B finalizer [BANK] |
| Certified complete outputs | G29; G19/G32 aliases limited to declared facts | G50B/G50H/G50O; G51 aliases limited to workflow facts | None for a new B CRC-only mode | None in canonical bank Replace route evidence: all ten rows are contract-only [MAN] |

**Declared/source write bounds:** the current normal-mode host integrity authority is narrower than the
name "full postbuild" suggests. Perfect main words are `[0x7100,0x7104)` and `[0x7118,0x711C)`;
Partial main words are `[0xA11C,0xA120)` and `[0xA130,0xA134)`. Header-copy block destinations are also
writable. Cascade adds Perfect `[0x7128,0x7144)` or Partial `[0xA134,0xA180)` to main integrity authority.
The allowed selected-source intersections and copy destinations are assembled separately. These are
host write bounds, not a statement that every covered byte must change or that every CRC reads that range.
The NT51950 root-cause experiment's ILM CRC field `0xA10C` is not automatically added to host authority
by that experiment. Resetting it would require an approved change. [IR, WR, HIST]

**Declared/source content bounds:** the first local normal-mode command carries the following blocks;
the second retains only the Header-copy block. Each selected staged source is read from offset zero
for the target length, subject to the selected-source intersection and preservation rules. All target
intervals in this table are bank-local processor file offsets, not final AB addresses. [CAT, WR]

| Family | First-command selected content targets | Header copy in both commands | Other postbuild evidence / limits |
| --- | --- | --- | --- |
| Perfect | NF `[0x1FC00,0x21B90)`; Normal `[0x21B90,0x26590)`; VN `[0x26590,0x27EF0)`; cascade DiffDLM block `[0x2D100,0x35D00)` before active-record masking | Firmware source `[0x7000,0x7200)` -> `[0x27EF0,0x280F0)` | Single Backup map authority `[0x2E000,0x2F000)`; historical C copy reads `[0x1F200,0x20200)` and writes the Backup then marker. Cascade policy has Backup authority `[0x2F000,0x37000)` with 4096-byte copy length; that authority is not permission to overwrite its entire span [CAT, LOCAL29, LC] |
| Partial | NF `[0x22C00,0x25610)`; Normal `[0x25610,0x2B210)`; VN `[0x2B210,0x2D30C)`; cascade DiffDLM block `[0x33200,0x34600)` before masking | Firmware source `[0xA000,0xA200)` -> `[0x2D30C,0x2D50C)` | The bank compiler requires FWConfig source `[0x22200,0x22980)` and Backup `[0x36000,0x36780)`; copy length is `0x780`. Exact marker writes remain tied to the selected local profile/map; a CRC-only invocation has no declared authority to reproduce them [CAT, BANK] |

**Declared/source preservation:** cascade uses separate DiffDLM policies. Perfect accepts 2-8 ICs,
with per-record write `[0,0xB90)` and preserve `[0xB90,0x1400)`; Partial is exact 2 ICs, with write
`[0,0x910)` and preserve `[0x910,0x1400)`. These are offsets within a staged DiffDLM record, not AB
output offsets. A new mode must preserve these masks and each bank's own original unselected content.
The policy definitions do not prove a new Python implementation observes them. [CAT]

### 3.2 The 929 Backup failure is before CRC

**Historical observation:** the synthetic single-IC B control had length `0x40000`, stored DIFF address
`0x6D100` and a computed Backup destination `0x6E000`. The recorded C routine attempted a 4096-byte
Backup copy outside its local buffer before CRC calculation; the first command exited `0xC0000005`.
Normalizing the three addresses made the same Normal replacement succeed and equal the A control.
Restoration changed only three actual bytes within the three permitted address words. [LC]

**Historical limit:** that control used a synthetic B cloned from A, not the actual Golden B half.
It did not cover differing A/B content/version, unselected-bank preservation, cascade or 950/951.
Its initial NF write-authority refusal was later addressed by the recorded Single-family Backup
declaration correction; the historical 124-byte limit must not be described as the current authority.
The successful Normal control and that later correction still do not certify a new B mode. [LC]

**Evaluation:** CRC-only cannot be justified merely by the fact that the three relocated words are
outside Header CRC. It does not reproduce the Backup copy that caused the failure. Omitting that copy
could avoid this particular unsafe access while still failing required postbuild semantics. Full postbuild
needs correct local addressing before the copy; CRC correctness and consumer acceptance are separate gates.
This supports F as the comparison baseline for content replacement, not unconditional authorization of F.
[LC, ROAD, D238]

### 3.3 The 950/951 copy and CRC order matters

**Historical observation, NT51950 sample scope:** the investigated build runs `InsertSID.py`, then
`NT51950BASED_NORMAL_MODE` twice. Each pass copies the on-disk Header first, then calculates CRCs.
The Header copy lies inside bank-local DLM CRC coverage `[0x22200,0x33200)`. A fresh build starts with
zero ILM/DLM/Header CRC fields; replaying on a filled, already post-built TP gives a different copy seed.
Resetting those 12 bytes before the first pass reproduced the three recorded owner samples. This is
non-idempotence evidence, not an accepted reset policy or proof for every IC/topology. [HIST, D238]

The literal `CRC8` is a legacy algorithm selector, not a declaration that the stored CRC is eight bits.
The recorded Header CRC fields occupy four bytes; the uploaded reference computes CRC-32/MPEG-2.
[R31, ABREF]

**Historical observation:** Partial B normalization changes `0xA100/0xA110/0xA120` but retains a Header
CRC calculated for relocated addresses. The first pass can copy that stale value into the DLM-covered
copy window. Refreshing after normalization matched the A-local result in the investigation, but still
did not match owner expected output. R31-02 must choose initial state and copy timing; an extra pass,
refresh alone or final Header CRC validity does not settle those choices. [STALE, HIST, T]

**Evaluation:** a final B Header CRC refresh can correct H's final Header input while leaving a different
copy and DLM history from F. A valid main Header and valid copy Header do not prove the copy's stored
DLM CRC describes the final DLM, or that readers accept its address convention. [HIST, R31, D238]

### 3.4 Which differences are visible in output bytes?

| Difference | Observable byte consequence | What pins it / what remains unproved |
| --- | --- | --- |
| R relocation | Writes three four-byte address words; actual changed-byte count depends on the input values | G29 pins the complete assembly; same-input characterization recorded only three actual changed bytes after restoration, not a universal three-byte bound [G29, LC] |
| H relocation plus Header refresh | Host changes DIFF; tool can change ILM/DLM addresses and Header CRC; surrounding TP/copy/DP bytes are retained by the plan | G50B/G50H/G50O pin complete outputs; the 12-byte tool authority is not an output-comparison allowance [P50, G50B, G50H, G50O] |
| C instead of F after content replacement | Can omit Header-copy, FWConfig Backup/end-marker updates; changed data CRCs may differ even if final main Header passes | No independently certified C output; equivalence depends on the missing owner facts and complete-output evidence [LC, CAT, D238] |
| Different initial CRC/copy history in F | Recorded NT51950 AB differences: eight four-byte CRC fields, 32 bytes total across both banks | Private historical Replace investigation, not a permitted AB Merge delta; reset/allowance remain R31 decisions [HIST, T, D238] |
| Same TP in A/B | Perfect relocation is outside Header CRC; Partial relocation is inside it; normalized intermediate equality and final AB equality are different assertions | G50O certifies one same-physical-TP assembly; it does not certify same-TP bank Replace equality or consumer rules [LC, G50O, D238] |

## 4. What the two Combiner implementations establish

### 4.1 Legacy C 1.13.0 versus published Python

| Mode / family | Legacy C evidence checked here | Published `nvt_combiner` evidence checked here | Missing evidence |
| --- | --- | --- | --- |
| R / Perfect AB assembly | Dated recovered-source reading names `NT51932BasedMergeABMode`: three address additions, no CRC/copy call [LC] | Intake records AB Merge and range-safety differential evidence read previously; source unavailable in this checkout [ROAD, PY] | Exact published mode implementation, argv, per-family read/write ranges and complete-output comparison at `6490edf` |
| H / Partial AB assembly | Closed host catalog runs `NT51950BASED_MERGE_AB_MODE CRC8` with two bank artifacts, output and explicit `0x40000` or `0x80000`; recovered-source reading relocates ILM/DLM then calculates Header CRC [INV, LC] | Same record-only AB Merge intake scope; no checked callable implementation or deployment binary [ROAD, PY] | Cannot claim drop-in support for either invocation or full parity from the intake text |
| F / Perfect local postbuild | Catalog uses `NT51932BASED_NORMAL_MODE CRC8 <fw> <fw> <blocks...>` twice; Backup-before-CRC behavior explains failed unnormalized B control [CAT, LC, R07] | Records explicitly do not establish complete AB CtrlRAM Replace validation [ROAD] | Published handling of normalization, Backup/marker, Header-copy generation, DiffDLM masks and all selected-bank cases |
| F / Partial local postbuild + H finalizer | Catalog uses `NT51950BASED_NORMAL_MODE CRC8 <fw> <fw> <blocks...>` twice; current bank compiler normalizes then reuses local postbuild and H [CAT, BANK]; initial-state effect recorded for NT51950 [HIST] | No full bank Replace evidence in inspected records [ROAD, PY] | Initial-state/copy contract, reset scope, and full outputs at the exact candidate pin |
| C / dedicated B CRC-only | No B-specific invocation in the two-entry AB catalog. Existing `NT51927BASED_GEN_CRC_MODE CRC32 <fw> <fw>` belongs to NT51917/27/28 local processing, not these AB families [INV, R07, CAT] | Dedicated B CRC/postbuild mode is a pending assessment item, not an admitted implementation [ROAD] | Mode name/arguments, semantic owner, geometry, exact CRC read/write ranges, preservation and certification |

**Historical provenance limit:** the recovered 1.13 C entry is recorded with SHA-256
`7fb6551894d5a71f7df42b6b7c2bda99f35cbcc13f5c01713dc0ae596ebb5ea8`.
The record distinguishes it from the older tracked C source and does not prove reproducible binary-build
equivalence. This evaluation reads that record rather than recovering or executing the private archive.
The registered legacy binary is the fixed comparison baseline for R07-02. [LC, R07]

### 4.2 The immutable uploaded Python AB reference is separate evidence

**Observed source:** `refcode/ab_code_combiner/combine.py` copies DP, checks A CRC if configured,
copies A TP, relocates all three B addresses, refreshes B CRC if configured, then copies B TP.
Its CRC implementation is CRC-32/MPEG-2, stored little-endian. Perfect configurations 51929/51932
have no CRC configuration; 51950/51951 configure Header read `[0xA100,0xA130)` and write `0xA130`.
It has range/U32/overflow checks but no full Header-copy or FWConfig Backup postbuild flow. [ABREF, CFG]

**Historical observation:** G29, G50B and G50H record parity to this uploaded reference; G50B/G50H
also record legacy 1.13.0 parity. Its README still says 51950/51951 "Not yet", whereas the later case
provenance records those exact successful comparisons. The dated case records establish only their
named outputs, not family-wide verification. Neither reference parity nor its range checks certify the
different published `nvt_combiner` source pin. [G29, G50B, G50H, ABREADME, PY]

**Contract boundary:** the pure Python CRC worker protocol grants calculation only; the staged-transform
protocol is reserved/draft. A new B mode cannot treat either document as accepted mutation authority.
Existing Combiner invocations stay closed, profile-selected, hash-bound and host-audited. [WORKER, K, V2]

## 5. Certified outputs and approved difference bounds

**Recomputed inventory:** reading root `cases` and each referenced AB Merge `provenance/case.json`
gives eight rows: four `direct-full-output`, four `fact-scoped-alias`, zero AB Merge
`allowed-byte-difference` and zero AB Merge `input-only-evidence`. The four direct artifacts pin complete
outputs with **zero permitted byte differences**. None pins C or F on a changed B reference. [MAN, G29, G19,
G32, G50B, G50H, G50O, G51B, G51H]

| Case | Mode / complete-output requirement | Approved scope and exclusions |
| --- | --- | --- |
| `nt51929-ab-t05-d06` | R; complete 512 KiB equality, no AB Merge allowance | NT51929 direct; NT51919/32 fact-scoped applicability. Its separate first-half CtrlRAM observation records 15 changed bytes within classified CRC words and explicitly says `fullByteParity=false`; this does not relax AB assembly equality [G29, TEST] |
| `nt51919-ab-t05-d06-alias` | R facts; resolves G29 | Full-DP initializer, TP roles, referenced direct-case bytes; not an independent NT51919 product Golden [G19] |
| `nt51932-ab-t05-d06-alias` | R facts; resolves G29 | Referenced direct-case bytes; named NT51932 configuration parity remains separate [G32] |
| `nt51950-ab-boe-d82t80` | H; complete 512 KiB equality, no allowance | Exact NT51950 case; uploaded-reference/legacy parity recorded; does not establish cascade or published-Python parity [G50B, TEST] |
| `nt51950-ab-hiway-d82t80` | H; complete 512 KiB equality, no allowance | Same boundary for its distinct product case [G50H, TEST] |
| `nt51950-ab-osd-d03t02-20260924` | H; complete 1 MiB envelope equality, no allowance | NT51950 single OSD only; 512 KiB layout template; A/B share one 225280-byte physical TP. No NT51951 alias, support promotion or redistribution [G50O, OSDTEST] |
| `nt51951-ab-boe-d82t80-workflow-alias` | H workflow facts; resolves G50B | Command family/no `map.txt`, Header CRC behavior and referenced NT51950 bytes; NT51951 `0x80000` placement remains synthetic [G51B] |
| `nt51951-ab-hiway-d82t80-workflow-alias` | H workflow facts; resolves G50H | Same restricted scope; no independent NT51951 product output [G51H] |

**Declared distinction:** all six current AB Merge `routeEvidence` rows are **contract-only**, as are
all ten AB bank Replace rows. The retained four certified output cases and four fact-scoped case aliases
are a separate inventory. They pin their declared assembly cases, but do not turn current dynamic route
evidence into `direct-golden` or `approved-alias`. For example, NT51929 policy explicitly records
`dynamic-selection-pending-full-evidence`. No fresh exact-source execution or evidence promotion was
performed here; a new mode cannot inherit a complete-output certification from those route rows.
[MAN, POL, GOLDENCONTRACT]

**Evaluation:** retain every existing allowance. R31-03 must decide the historical 16-byte and recorded
32-byte postbuild differences case by case, after R31-02 determines initial-state policy. They do not
form a blanket AB allowance. R07-02 compares Python with fixed legacy on identical starting bytes and
argv with exact complete-output equality, separately from each owner-Golden comparison contract.
An in-range wrong CRC or omitted write can pass a bounds check; independent output checks remain necessary.
[T, D238, R07, K]

## 6. R08-02 options, route consequences and recommendation

### 6.1 Counts and re-registration method

**Recomputed, not copied from the earlier reports:** JSON was read with Python, without loading C#,
building or executing firmware tools. Profiles were selected by `experience.experienceId == "ab-merge"`;
their `mapBinding.mapIds` were joined to the two families' `imageMaps`, and profile id/version to
`package-trust-index.json.runtimeRegistrations`. Policy rows were selected by `workflowId == "ab-merge"`
or `workflowId == "ctrlram-replace"` with a declared AB `mapVariant`, then joined by exact `routeId`
to root `routeEvidence`. Each decision pin was counted once for authoring, publication and evidence.
No speculative topology expansion or fresh runtime fingerprint was counted. [IDX, POL, MAN, FP, FQ]

| Counted surface | Perfect family | Partial family | Total |
| --- | ---: | ---: | ---: |
| AB Merge profiles on disk | 3 | 5 | 8 |
| Runtime AB Merge registrations | 3 | 3 | 6 |
| Unregistered special profiles | 0 | 2 | 2 |
| AB maps on disk / retained active geometry maps | 3 / 3 | 6 / 3 | 9 / 6 |
| AB Merge policy routes | 3 | 3 | 6 |
| AB bank Replace policy routes | 6: three ICs x `1-ic` / `2-8-ic` | 4: two ICs x `1-ic` / `2-ic` | 10 |
| Union affected by changing the respective AB bundle hash | 9 | 7 | 16 |
| Policy decision objects for that union | 27 | 21 | 48 |
| Exact `routeEvidence` rows for that union | 9 | 7 | 16 |
| Physical AB direct outputs / aliases | 1 / 2 | 3 / 2 | 4 / 4 |

**Observed source / evaluation:** AB route identity uses the bundle hash; bank definition identity
includes both layout and local bundle/entry/family hashes, and the route binds that composite identity.
Changing an AB layout bundle can therefore require re-registration even when a bank's local profile
file is untouched. Policy decisions and Golden evidence require the exact resulting fingerprint.
Counts above describe that known dependency union, not proof of new identity values or approval.
[IDENTITY, BANKHASH, ROUTEBANK, POLICYCONTRACT, GOLDENCONTRACT]

Changing only a bank-specific compiler contract is a different surface from changing a layout bundle.
Ten bank routes are the semantic review scope for an all-five-IC bank-mode decision; whether their
existing fingerprint actually changes depends on the approved semantic identity/invocation changes.
It cannot be decided by counting files or assuming any code edit automatically changes a hash.
If a shared local postbuild contract changes too, the inspected policy has another 14 local CtrlRAM
routes across these ICs: 2 each for NT51919/29/32, 4 each for NT51950/51. That union is 30 routes
(16 AB plus 14 local), 90 policy decision objects and 30 evidence rows if all those identities change.
This is a conditional expanded scope, not an implementation estimate. [POL, IDENTITY]

For the separate full Combiner replacement, Python counting also reconfirmed 11 local invocation
profiles and 161 declared commands across all branches, plus two AB invocation profiles; 31 JSON
profiles carry legacy binding (26 CtrlRAM and five AB Merge, including the two unregistered profiles).
These counts do not mean 161 commands execute per run, or 31 runtime routes. R07-02/03 must inventory
their full dependency closure rather than reuse this B-only count. [CAT, INV, IDX, R07]

### 6.2 Options

All options retain decision 238's unknowns and the current allowances. They are proposals for R08-02,
not permission to implement, switch runtime, retire declarations or regenerate expected outputs. [D238, T]

| Option | Routes / registration consequences | Golden and firmware consequences | Evaluation |
| --- | --- | --- | --- |
| A. Preserve R/H assembly and current F bank processing; defer a new dedicated mode | This evaluation changes zero routes, decisions, evidence rows or bindings. R10 migration/retirement has its own 9/7-route bundle unions and remains separately allocated | Retain four direct assembly outputs and four alias scopes; ten bank routes remain contract-only. Preserve known reset/copy uncertainty; no new safety claim | Safest immediate boundary while owner facts and exact published source are missing; leaves the B-mode decision unresolved |
| B. Introduce family-specific C for changed B content | New closed B invocation/semantic contract. For all five ICs, ten bank routes are the semantic scope; AB layout bundle edits imply the 16-route / 48-decision / 16-evidence union. Shared local changes can expand to the conditional 30-route union | Requires independent complete outputs for changed B, explicit CRC coverage and proof that omitting copy/Backup/marker is acceptable. Existing assembly Goldens do not establish this; no new CRC-only output is certified | Not recommended as the general replacement policy: known Backup behavior and unknown readers prevent an equivalence claim |
| C. Retain family-specific F as the changed-content baseline; package a dedicated normalize -> existing postbuild -> restore/finalize mode only after facts are accepted | Reuse the existing profile/compiler semantic owner and external adapter contract; do not add a second planner. Same ten-route semantic scope and conditional bundle/local unions as B. A dedicated tool-only mode would need its own closed invocation and identity review | Preserve B's own source, selected-content masks, Backup/marker and copy order; Perfect restores three addresses without automatic Header CRC refresh, Partial uses H after restoration. Reset/refresh/copy generation remain R31-02 decisions; independent bank outputs still required | Recommended direction for R08-02, conditional on Q1-Q5 and R31-02/03; current F is a baseline to validate, not certified safe behavior |
| D. Apply F to every B even during AB Merge | Extends semantics to all six AB Merge routes and ten bank routes if both workflows adopt it; changes to both AB bundles give the 16-route identity union, with local closure assessed separately | Reprocesses already built TP, exposing the recorded non-idempotent copy/CRC history. All four direct assembly outputs must still pass or receive explicit owner-approved replacement evidence; aliases cannot fill missing products | Not recommended: a uniform flow is not supported by present byte evidence or consumer facts |

**Recommendation:** choose C as the conditional changed-content design direction, with A as the
current operational boundary until firmware facts and independent outputs arrive. Preserve R/H for
AB assembly; reject an unconditional CRC-only rule and an unconditional full-postbuild assembly rule.
If the firmware owner later establishes that specific copy/Backup actions may be omitted for named
combinations, evaluate B only within that boundary. Unknown answers keep that promotion pending.
[LC, HIST, D238, ROAD]

**Evaluation, coordination consequence:** freeze mode scope, R31 policy, shared-definition changes and
Desay retirement before a final identity refresh where their changes affect the same routes.
Decision 235 already includes the six Perfect bank re-pins and complete before/after output/write-range
comparison, with the parallel-loading dependency-graph precondition. Repeating a Perfect bundle identity
refresh would require its nine-route union again; Partial changes repeat its seven-route union.
A batched union can avoid duplicate re-registration, but batching is not a separate owner decision and
does not waive any Golden, firmware review or dependency-graph requirement. [D235, D213, A, IDENTITY]

## 7. Firmware-owner questions and limits

These questions carry forward R31-01 Q1-Q3b/Q5 and decision 238; they are not answers inferred from
tool source. Each response should name IC, IC Count, firmware version, selected bank(s), format and
consumer path. Options may differ per reader or product. Recommendations below are evaluation choices.
[R31, D238]

| Question | Options for the firmware owner | Recommendation / consequence |
| --- | --- | --- |
| Q1. Who reads `FLASHMAP_HEADER_COPY`, and when? | Boot ROM/bootloader; runtime firmware; programming/update/offline tools; nobody; `unknown`. For each reader: normal boot, main-Header failure/recovery, bank switch or build/update only | Identify reader functions/spec sections and trigger conditions; keep `unknown` where unconfirmed. Do not choose "nobody" from successful normal operation. This defines the consumer/negative-test matrix [R31, D238] |
| Q2. Which CRCs are checked, when, and how are mismatches handled? | Copy Header CRC; copy ILM/DLM CRC against content; main DLM including copy; data only; `unknown`. Handling: stop, fallback, recovery, ignore/continue or `unknown` | Reconfirm the tentative "ignore and continue" per named reader and CRC, with firmware evidence. It cannot authorize blanket allowances or declare copy safe [D238, R31] |
| Q3. Which Header generation should the copy contain, and which B address space should it use? | Previous-pass snapshot; final-main synchronized; only specified fields valid; `unknown`. B addresses: bank-local, full AB, conditional or `unknown` | Have firmware specify generation and valid fields independently of address convention; record SVN/copy timing. Preserve current behavior until accepted. The legacy tool sequence is evidence of what is generated, not what readers require [HIST, R31] |
| Q3b. For one shared TP, which CRCs must match between A/B, and what is the initial state before copying? | For main Header/main DLM/copy Header/copy DLM: equal, no equality requirement, conditional or `unknown`, at normalized-local or final-AB stage. Initial fields: zero, recalculate, preserve or `unknown`; pre-copy Header refresh: yes/no/conditional/unknown | Require an explicit stage and per-field answer. Compare zero/reset and preserve/refresh against independent expected outputs; do not adopt reset only because three historical samples matched. This is R31-02 authority [STALE, HIST, R31] |
| Q4. Which exact combinations may inherit decision 161's operation observation? | Only investigated NT51950 Normal/Both; other named NT51950 combinations; other named ICs/topologies/banks/formats; `unknown` | Limit to individually confirmed combinations with evidence; do not extend to boot/recovery by inference. This follows decision 238 [D238, R31] |
| Q5. What evidence and independent expected outputs exist? | Firmware source/spec; boot/switch/recovery records; normal-operation observations only; none; `unknown`. For 32-byte AB and historical 16-byte cases separately: all independent expected outputs, some named cases, none or unknown | Obtain versioned evidence IDs and immutable output hashes without adding firmware bytes here. Keep allowances until R31-03 decides each case; obtain new expected outputs for changed-bank modes independently of the candidate [T, D238, R31, R07] |
| Q6. What should R08-02 authorize after those facts arrive? | A: preserve/defer; B: bounded C; C: family-specific F baseline with a dedicated mode later; D: full postbuild for every assembly B | C conditionally, A until its facts/evidence are accepted. Name mode, argv/geometry, semantic owner, selected-content preservation and exact per-stage write/import contract; then follow R31-02/03 before R07-02/03 [T, A, ROAD] |

**Evaluation acceptance boundary for later work:** compare A-only, B-only and Both separately for each
named route/topology/format, including different A/B content and versions, shared TP, filled/zero initial
CRC states and the accepted copy generation. Compare the entire unselected bank byte for byte; preserve
unselected content/version within a selected bank except exact approved postbuild effects. Retain exact
per-stage read/write/import evidence, complete outputs and negative cases for invalid geometry, stale
normalization and out-of-range writes. R07-02 must separately report legacy equivalence, owner-Golden
comparison and consumer evidence. This is a proposed evidence matrix, not new accepted tests. [ROAD, R07, D238]

**Limits:** no .NET build, verifier, product test, Combiner execution, CRC experiment, Golden execution,
binary dump, device test, network, commit or publication was performed. The inspection establishes current
declarations, checked static behavior, historical evidence boundaries and recomputed counts only.
It does not establish published-Python mode support at `6490edf`, a deployed candidate hash,
independent changed-bank Golden outputs, firmware consumers, safe Header copy, a reset policy or
cross-family CRC equality. R08-01 stays preliminary (see Status); these remain R08-02/R31/R07 owner and evidence gates.

## 8. Checked citation register

Line numbers refer to the pinned source above, before adding this evaluation. Comma-separated numbers
identify separate checked locations, not firmware data. Inspection/count results are labelled in the text.

| ID | Checked repository source (`file:line`) | Evidence used |
| --- | --- | --- |
| T | `docs/handoff/1.1.14/1.2.x-inventory.md:186,188,189,343,344` | R07-02, R08-01/02, R31-02/03 scope and acceptance |
| A | `docs/handoff/1.1.14/1.2.x-allocation.md:212,214,215,218,222` | 1.2.5 sequencing, migration and retirement boundaries |
| ROAD | `docs/architecture/nfc_roadmap.md:430,1170,1173,1179,1181,1184,1185,1187,1192` | Version row; Python intake; B mode assessment; preservation; evidence gaps |
| D187 | `docs/handoff/1.1.12.md:1207,1215`; `docs/handoff/1.2.x.md` decision 187 (lines 907 to 923 at 8479dee8e) | No switch; public submodule follow-up |
| D213 | `docs/handoff/1.2.x.md:157,161,165,168` | Firmware route gates, Desay-map retirement and scope |
| D235 | `docs/handoff/1.2.x.md:385,393,396,400,404` | Shared-definition migration, six bank re-pins, graph check and output evidence |
| D238 | `docs/handoff/1.2.x.md:425,427,429,430,432,434` | Unknown Header-copy facts and mandatory limits |
| R07 | `docs/handoff/1.2.1/R07-01.md:11,15,24,26,27,28,29,35,75,79,88,93,95,97` | Legacy/candidate identities, six argv shapes, replacement and evidence boundaries |
| R31 | `docs/handoff/1.2.1/R31-01.md:20,44,52,53,64,65,66,67,76,77,78,82,126,133,141,149,157,169` | Header fields, copy windows, CRC coverage and owner questions |
| P19 | `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51919-ab-merge.json:4,6,99,116,266,268,271,274` | NT51919 profile and R operations |
| P29 | `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51929-ab-merge.json:4,6,128,185,393,411,429,447,465,475` | NT51929 clone, fields, order and no processor |
| P32 | `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/profiles/nt51932-ab-merge.json:4,6,90,98,248,250,253,256` | NT51932 profile and R operations |
| FP | `profiles/built-in/nt51919-nt51929-nt51932-ab-merge/families/nt51919-nt51929-nt51932-ab-merge.json:39,90,134,170,256,283,310` | Perfect bank template, placement, metadata and three maps |
| P50 | `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge.json:6,25,110,132,137,140,143,189,203,206,207,217,223,224,226` | NT51950 clone, transport, H invocation, authority and imports |
| PC50 | `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge-cascade.json:6,85,101,106,109,179,211,220,228,261,267,268,270` | NT51950 cascade geometry and H declarations |
| P51 | `profiles/built-in/nt51950-ab-merge/profiles/nt51951-ab-merge.json:6,46,54,59,62,111,114,115,116,126,132,133,135` | NT51951 geometry and H declarations |
| PD50 | `profiles/built-in/nt51950-ab-merge/profiles/nt51950-ab-merge-desay.json:6,77,93,98,101,171,212,220,248,253,260` | Retained NT51950 special declaration |
| PD51 | `profiles/built-in/nt51950-ab-merge/profiles/nt51951-ab-merge-desay.json:6,77,93,98,101,171,212,220,253,259,260,262` | Retained NT51951 special declaration |
| FQ | `profiles/built-in/nt51950-ab-merge/families/nt51950-ab-merge.json:5,19,21,23,145,169,194,320,442,467,597,624,652,679,696,723` | Format recognition, bank geometry and six maps |
| INV | `src/NvtFwCombiner.Application/ExternalTools/ExternalCombinerInvocationCatalog.cs:6,10,14,19,24,28,33,37` | Two exact closed AB invocations |
| CAT | `profiles/built-in/ctrlram-postbuild-v2/catalog.json:5,8,9,11,15,18,19,22,23,24,26,31,35,37,41,44,45,49,50,51,1354,4705,4714,4723,4724,4733,4734,4743,4744,4748,4752,4753,4754,4759,4776,4870,5035,5044,5078,5089,5198` | Full parsed catalog; DiffDLM policies; local normal-mode command sequences |
| LOCAL29 | `profiles/built-in/nt51929-ctrlram-replace-candidate/families/nt51929-ctrlram-replace.json:93,102,111,120,138,142` | Current Single content/copy/Backup authority, not the historical 124-byte bound |
| IR | `src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.IntegrityRanges.cs:13,15,16,24,38,42,45,46` | Exact main integrity and topology write authority |
| WR | `src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.WriteRanges.cs:28,49,72,88,98,103,120` | Selected-source intersections, copy destination and integrity authority |
| BANK | `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.Banks.cs:47,48,95,107,118,123,194,196,228,245,249,259,265,271,274,278,380,386,387,388,398,399,400,401` | Current bank-local preservation, normalization/postbuild/restoration and Partial finalizer |
| LC | `docs/architecture/ctrlram-postbuild-original-pasteback.md:362,366,391,394,395,396,402,406,426,434,438,440,447,448,449,450,451,460,467` | Legacy C source provenance; recorded B experiments, limits and later Backup correction |
| HIST | `docs/handoff/1.1.13.md:100,101,103,104,105,110,112,116,122` | NT51950 32-byte/reset/copy-history investigation and limitations |
| STALE | `docs/handoff/bugs/BUG-20260926-b-normalization-stale-header-crc.md:3,9,10,12,14,17,18` | Unfixed pre-copy stale Header CRC and deferred reset policy |
| PY | `third-party/nvt_combiner.SOURCE.md:4,5,8,10,18,20` | Published pin, optional checkout and no runtime/parity promotion |
| REF | `refcode/REFERENCE_MANIFEST.json:28`; `refcode/README.md:3,8,20` | Uploaded Python reference provenance; immutable evidence only |
| ABREF | `refcode/ab_code_combiner/combine.py:53,71,77,82,89,104,127,137,170,193,244,266,272,281,287,288,294` | Checked reference relocation, CRC, bounds and assembly flow |
| CFG | `refcode/ab_code_combiner/ic_config.py:123,142,164,170,180,184,185,188,194,204,208,209` | Family-specific reference offsets and CRC configuration |
| ABREADME | `refcode/ab_code_combiner/README.txt:28,30,31,32,33` | Older reference verification-status claims |
| K | `docs/contracts/external-combiner-tool-manifest-v1.md:7,37,40,42,52,56,63` | Profile versus tool authority; immutable staging; diff and failure rules |
| V2 | `docs/contracts/composition-profile-v2.md:226,238,245,254,294,302` | Shared execution and controlled processor/work-buffer contracts |
| WORKER | `docs/contracts/crc-worker-v1.md:5`; `docs/contracts/crc-worker-transform-v2-draft.md:3,7,18,24` | Pure calculation versus reserved mutation protocol |
| IDX | `profiles/built-in/package-trust-index.json:641,650,674,683,697` | Two AB bundle hashes, six exact registrations; full JSON used for counts |
| POL | `docs/contracts/canonical-capability-policy-v1.json:241,1343,1366,1367,1372,1517,1662,1691,1952,2213,2242,2271,2300,2329,2358,2387,2416,2445` | Six AB Merge and ten bank Replace rows; full JSON used for local-route counts |
| POLICYCONTRACT | `docs/contracts/canonical-capability-policy-v1.md:98,104,106,111` | Three decisions per route and exact evidence pins |
| GOLDENCONTRACT | `docs/contracts/canonical-golden-manifest-v1.md:5,29,40,49,57,65,76,95` | Golden inventory, exact-route evidence, full-output and alias limits |
| IDENTITY | `docs/handoff/1.2.5/R61-bundle-coupling-evaluation.md:489,498,499,502,503,509,515,522` | Checked current identity dependency explanation, not reused count totals |
| BANKHASH | `src/NvtFwCombiner.Domain/Composition/RuntimeReferenceBankReplaceV2CompilationContext.cs:87,95,99,103,104,105` | Composite layout/local source identity inputs |
| ROUTEBANK | `src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.Banks.cs:53,58,75,77,78` | Exact bank admission and route fingerprint bindings |
| MAN | `testdata/golden/canonical/manifest.json:29,45,223,224,564,565,601,602,638,639,645,646,734,735,805,812,819,826,833,840,847,854,861,868,916,920,924,928,932,936,940,944` | Root case inventory, AB scope and exact route evidence; full JSON counted |
| G19 | `testdata/golden/canonical/NT51919/ab-merge/t05-d06/topology-unscoped/nt51919-ab-t05-d06-alias/provenance/case.json:8,9,17,19` | Perfect alias scope |
| G29 | `testdata/golden/canonical/NT51929/ab-merge/t05-d06/topology-unscoped/nt51929-ab-t05-d06/provenance/case.json:8,9,58,68,70,86,118,143,145,150,153` | Direct output; separate first-half CRC observation |
| G32 | `testdata/golden/canonical/NT51932/ab-merge/t05-d06/topology-unscoped/nt51932-ab-t05-d06-alias/provenance/case.json:8,9,17,19` | Perfect alias limits |
| G50B | `testdata/golden/canonical/NT51950/ab-merge/boe-d82t80/topology-unscoped/nt51950-ab-boe-d82t80/provenance/case.json:8,9,58,68,70,85` | Exact BOE full output and recorded parity |
| G50H | `testdata/golden/canonical/NT51950/ab-merge/hiway-d82t80/topology-unscoped/nt51950-ab-hiway-d82t80/provenance/case.json:8,9,58,68,70,85` | Exact Hiway full output and recorded parity |
| G50O | `testdata/golden/canonical/NT51950/ab-merge/osd-d03t02/single/nt51950-ab-osd-d03t02-20260924/provenance/case.json:8,9,24,54,63,74` | OSD single same-TP 1 MiB complete-output scope |
| G51B | `testdata/golden/canonical/NT51951/ab-merge/boe-d82t80/topology-unscoped/nt51951-ab-boe-d82t80-workflow-alias/provenance/case.json:8,9,17,19` | Partial workflow alias and geometry limitation |
| G51H | `testdata/golden/canonical/NT51951/ab-merge/hiway-d82t80/topology-unscoped/nt51951-ab-hiway-d82t80-workflow-alias/provenance/case.json:8,9,17,19` | Partial workflow alias and geometry limitation |
| TEST | `tests/NvtFwCombiner.Bootstrap.Tests/AbMergeGoldenRegressionTests.cs:24,41,59,180,214,263,264,266` | Inspected complete-output assertions, not fresh test execution |
| OSDTEST | `tests/NvtFwCombiner.Bootstrap.Tests/AbMergeGoldenRegressionTests.PublicHost.cs:47,77,111,113,114,247,254,261` | Inspected OSD output/input preservation and processor assertions |
