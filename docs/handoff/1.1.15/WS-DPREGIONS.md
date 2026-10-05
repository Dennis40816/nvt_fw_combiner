# WS-DPREGIONS — NT51950 / NT51951 AB DP declarations

## Dispatch and admission

- Outcome: implement decision 192 by declaring every non-TP range as DP in all six AB maps and nesting each CMI field under its DP section.
- Authority: local edits, proportionate tests and local commit only; R3 firmware-owner authority. No push, PR or GitHub mutation.
- Branch: `feature/1.1.15/nt51950-dp-regions`; worktree `<worktrees>/f115-dp`; base `020c035e691c237a86dc80f78f4a3e887dfbb91b`.
- Single writer: Codex `gpt-6-astra`, requested `xhigh`; selected by the owner for this firmware declaration change.
- Owned surfaces: `profiles/built-in/nt51950-ab-merge/`, its package trust-index entry and mechanically dependent pins, scoped regression tests, this checkpoint and requested external report. `docs/handoff/1.1.12.md` is read-only.
- Non-goals: planner, executor, validator, schema, operations, bytes, ordering, CRC/header, padding, naming, support promotion, release or integration.
- Acceptance: six maps cover every non-TP byte with declared DP; existing write constraints and alignment remain effective; three direct certified Goldens reproduce complete outputs; all compiled write ranges match the baseline; ProfileContract/profile narrow tests and structure-only verifier pass.
- Stop: changed Golden bytes or planned write ranges; required planner/executor/validator/schema semantic change; reference/owner range contradiction; three-hour budget exhausted. Record the condition in Open and end the task.

### Capability reuse / owner search

- Semantic owner: `families/nt51950-ab-merge.json` owns the physical region graph; its five composition profiles retain operation and input authority. Disposition: `extend-owner` for DP declarations, `reuse` for all compilation/execution and hash machinery; no second semantic path.
- Contract: `FirmwareRegion`, `FirmwareImageMap` and strict firmware-family 1.2 schema own classification, hierarchy, complete coverage, constraints and alignment. `unmapped` requires `unknown` and `forbidden`; `opaque` is an identifier description, not a kind. `image` is a nested container; `data` admits structured or opaque data.
- Callers: family normalizer feeds `V2CompositionPlanCompiler`; its governing-region chain checks every physical constraint. `CompositionEngine` executes only the compiled operations. Reports project planned operations/mutations; Memory Layout projects typed owner/kind/parent facts.
- Reuse evidence: standard DP Perspective declares `dp-container` as `dp/image`; AB 929 uses `parentRegionId`, but at this base its CMI parent is the system bank, not a DP region. No unrelated 929 correction is included.
- Chosen structure: keep forbidden leaf ranges and CMI explicit-range/alignment exactly; put the CMI and its adjacent protected leaves beneath one DP image container. Other non-TP sections remain forbidden DP images. This preserves the original physical permissions while exposing a real DP parent.
- Provenance: decision 192 in the read-only version board; workbook `docs/references/ic-flashmap/IC_FlashMap_20260922.xlsx`, SHA-256 `1bb37e8cf0c5826b5ea5e08d95b323cc740862f1a17b5d7ff4b37e18bf806350`, sheets `51950 DP Perspective` and `51950&51951 TP Flashmap`.
- Workbook interpretation: DP Perspective row 15 includes the customer page in its broad TP allocation; TP Flashmap rows 18 and 22 explicitly separate TP ending at `flash 0x37000` from customer information `flash [0x37000,0x38000)`. Decision 192 explicitly assigns this preserved page to DP for AB. Variant-specific bank placement remains the existing profile fact; workbook ordinary backup placement does not redefine the retained Desay maps.
- Evidence references are nonempty identifiers in family/map/region-set declarations; existing owner-decision references do not have a separate registry entry.
- Hash owner: `ProfileBundleEntryArrayHasher` through existing `AbBundleSourceHashTests`; package-index dependent release pin through `scripts/sync_derived.py --write --only reviewed-source-pins`.
- Residual integration gates: independent fixed-head review and firmware-owner approval of any future last push; no publication authority is supplied by this local task.

## Checkpoints

### 2026-09-29 baseline inspection

State: planned

Evidence: branch and base match; working tree initially clean. Canonical inventory has three NT51950 direct-full-output AB cases, all inputs and expected hashes available, plus two NT51951 fact-scoped aliases (not direct NT51951 Goldens).

Open: baseline execution, DP edit, compiled write-range comparison and final checks pending. No stop condition established.

Next: capture baseline compiled operations and execute existing certified AB Golden tests, then apply the declaration-only change.

<a id="2026-09-29-宣告與範圍盤點"></a>

### 2026-09-29 declaration and range inventory

State: local

All ranges in the tables below use the `flash` address space and `[start, endExclusive)` notation; alignment remains `1` in every row, before and after. All existing region ranges and writeConstraint values are preserved. The added DP images only group existing CMI fields and adjacent protected leaves; their `explicit-range` retains the original bank container constraint, and each child still constrains writes individually.

The three Desay maps share the original `opaque-container-tail`; decision 192 changes it to `dp-container-tail`, while `flash [0x80000,0x100000)` remains `forbidden`. The 950 merge 1024k and 951 merge 1024k maps instead have A/B banks of `0x80000` each and do not use that tail declaration.

#### `nt51950-ab-desay-single-1024k`

| Region: before → after | flash range | owner/kind: before → after | parentRegionId: before → after | writeConstraint: before → after |
| --- | --- | --- | --- | --- |
| ab-image | `[0x0,0x100000)` | system/image | — | explicit-range |
| a-bank | `[0x0,0x40000)` | system/image | ab-image | explicit-range |
| Added → a-dp-before-tp | `[0x0,0xA000)` | — → dp/image | — → a-bank | — → explicit-range |
| a-opaque-before-cmi → a-dp-before-cmi | `[0x0,0x5016)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-cmi-dp-version | `[0x5016,0x5019)` | dp/command | a-bank → a-dp-before-tp | explicit-range |
| a-opaque-after-cmi-before-tp → a-dp-after-cmi-before-tp | `[0x5019,0xA000)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-tp-code | `[0xA000,0x37000)` | tp/code | a-bank | explicit-range |
| a-opaque-after-tp → a-dp-after-tp | `[0x37000,0x40000)` | unknown/unmapped → dp/image | a-bank | forbidden |
| b-bank | `[0x40000,0x80000)` | system/image | ab-image | explicit-range |
| Added → b-dp-before-tp | `[0x40000,0x4A000)` | — → dp/image | — → b-bank | — → explicit-range |
| b-opaque-before-cmi → b-dp-before-cmi | `[0x40000,0x45016)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-cmi-dp-version | `[0x45016,0x45019)` | dp/command | b-bank → b-dp-before-tp | explicit-range |
| b-opaque-after-cmi-before-tp → b-dp-after-cmi-before-tp | `[0x45019,0x4A000)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-tp-code | `[0x4A000,0x77000)` | tp/code | b-bank | explicit-range |
| b-opaque-after-tp → b-dp-after-tp | `[0x77000,0x80000)` | unknown/unmapped → dp/image | b-bank | forbidden |
| opaque-container-tail → dp-container-tail | `[0x80000,0x100000)` | unknown/unmapped → dp/image | ab-image | forbidden |

#### `nt51950-ab-desay-cascade-1024k`

| Region: before → after | flash range | owner/kind: before → after | parentRegionId: before → after | writeConstraint: before → after |
| --- | --- | --- | --- | --- |
| ab-image | `[0x0,0x100000)` | system/image | — | explicit-range |
| a-bank | `[0x0,0x40000)` | system/image | ab-image | explicit-range |
| Added → a-dp-before-tp | `[0x0,0xA000)` | — → dp/image | — → a-bank | — → explicit-range |
| a-opaque-before-cmi → a-dp-before-cmi | `[0x0,0x5016)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-cmi-dp-version | `[0x5016,0x5019)` | dp/command | a-bank → a-dp-before-tp | explicit-range |
| a-opaque-after-cmi-before-tp → a-dp-after-cmi-before-tp | `[0x5019,0xA000)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-tp-code | `[0xA000,0x37000)` | tp/code | a-bank | explicit-range |
| a-opaque-after-tp → a-dp-after-tp | `[0x37000,0x40000)` | unknown/unmapped → dp/image | a-bank | forbidden |
| b-bank | `[0x40000,0x80000)` | system/image | ab-image | explicit-range |
| Added → b-dp-before-tp | `[0x40000,0x4A000)` | — → dp/image | — → b-bank | — → explicit-range |
| b-opaque-before-cmi → b-dp-before-cmi | `[0x40000,0x45016)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-cmi-dp-version | `[0x45016,0x45019)` | dp/command | b-bank → b-dp-before-tp | explicit-range |
| b-opaque-after-cmi-before-tp → b-dp-after-cmi-before-tp | `[0x45019,0x4A000)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-tp-code | `[0x4A000,0x77000)` | tp/code | b-bank | explicit-range |
| b-opaque-after-tp → b-dp-after-tp | `[0x77000,0x80000)` | unknown/unmapped → dp/image | b-bank | forbidden |
| opaque-container-tail → dp-container-tail | `[0x80000,0x100000)` | unknown/unmapped → dp/image | ab-image | forbidden |

#### `nt51951-ab-desay-1024k`

| Region: before → after | flash range | owner/kind: before → after | parentRegionId: before → after | writeConstraint: before → after |
| --- | --- | --- | --- | --- |
| ab-image | `[0x0,0x100000)` | system/image | — | explicit-range |
| a-bank | `[0x0,0x40000)` | system/image | ab-image | explicit-range |
| Added → a-dp-before-tp | `[0x0,0xA000)` | — → dp/image | — → a-bank | — → explicit-range |
| a-opaque-before-cmi → a-dp-before-cmi | `[0x0,0x5016)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-cmi-dp-version | `[0x5016,0x5019)` | dp/command | a-bank → a-dp-before-tp | explicit-range |
| a-opaque-after-cmi-before-tp → a-dp-after-cmi-before-tp | `[0x5019,0xA000)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-tp-code | `[0xA000,0x37000)` | tp/code | a-bank | explicit-range |
| a-opaque-after-tp → a-dp-after-tp | `[0x37000,0x40000)` | unknown/unmapped → dp/image | a-bank | forbidden |
| b-bank | `[0x40000,0x80000)` | system/image | ab-image | explicit-range |
| Added → b-dp-before-tp | `[0x40000,0x4A000)` | — → dp/image | — → b-bank | — → explicit-range |
| b-opaque-before-cmi → b-dp-before-cmi | `[0x40000,0x45016)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-cmi-dp-version | `[0x45016,0x45019)` | dp/command | b-bank → b-dp-before-tp | explicit-range |
| b-opaque-after-cmi-before-tp → b-dp-after-cmi-before-tp | `[0x45019,0x4A000)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-tp-code | `[0x4A000,0x77000)` | tp/code | b-bank | explicit-range |
| b-opaque-after-tp → b-dp-after-tp | `[0x77000,0x80000)` | unknown/unmapped → dp/image | b-bank | forbidden |
| opaque-container-tail → dp-container-tail | `[0x80000,0x100000)` | unknown/unmapped → dp/image | ab-image | forbidden |

#### `nt51950-ab-merge-512k`

| Region: before → after | flash range | owner/kind: before → after | parentRegionId: before → after | writeConstraint: before → after |
| --- | --- | --- | --- | --- |
| ab-image | `[0x0,0x80000)` | system/image | — | explicit-range |
| a-bank | `[0x0,0x40000)` | system/image | ab-image | explicit-range |
| a-opaque-before-tp → a-dp-before-tp | `[0x0,0xA000)` | unknown/unmapped → dp/image | a-bank | forbidden |
| a-tp-code | `[0xA000,0x37000)` | tp/code | a-bank | explicit-range |
| Added → a-dp-after-tp | `[0x37000,0x40000)` | — → dp/image | — → a-bank | — → explicit-range |
| a-opaque-after-tp-before-cmi → a-dp-after-tp-before-cmi | `[0x37000,0x3B016)` | unknown/unmapped → dp/data | a-bank → a-dp-after-tp | forbidden |
| a-cmi-dp-version | `[0x3B016,0x3B019)` | dp/command | a-bank → a-dp-after-tp | explicit-range |
| a-opaque-after-cmi → a-dp-after-cmi | `[0x3B019,0x40000)` | unknown/unmapped → dp/data | a-bank → a-dp-after-tp | forbidden |
| b-bank | `[0x40000,0x80000)` | system/image | ab-image | explicit-range |
| b-opaque-before-tp → b-dp-before-tp | `[0x40000,0x4A000)` | unknown/unmapped → dp/image | b-bank | forbidden |
| b-tp-code | `[0x4A000,0x77000)` | tp/code | b-bank | explicit-range |
| Added → b-dp-after-tp | `[0x77000,0x80000)` | — → dp/image | — → b-bank | — → explicit-range |
| b-opaque-after-tp-before-cmi → b-dp-after-tp-before-cmi | `[0x77000,0x7B016)` | unknown/unmapped → dp/data | b-bank → b-dp-after-tp | forbidden |
| b-cmi-dp-version | `[0x7B016,0x7B019)` | dp/command | b-bank → b-dp-after-tp | explicit-range |
| b-opaque-after-cmi → b-dp-after-cmi | `[0x7B019,0x80000)` | unknown/unmapped → dp/data | b-bank → b-dp-after-tp | forbidden |

#### `nt51950-ab-merge-1024k`

| Region: before → after | flash range | owner/kind: before → after | parentRegionId: before → after | writeConstraint: before → after |
| --- | --- | --- | --- | --- |
| ab-image | `[0x0,0x100000)` | system/image | — | explicit-range |
| a-bank | `[0x0,0x80000)` | system/image | ab-image | explicit-range |
| Added → a-dp-before-tp | `[0x0,0xA000)` | — → dp/image | — → a-bank | — → explicit-range |
| a-opaque-before-cmi → a-dp-before-cmi | `[0x0,0x5016)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-cmi-dp-version | `[0x5016,0x5019)` | dp/command | a-bank → a-dp-before-tp | explicit-range |
| a-opaque-after-cmi-before-tp → a-dp-after-cmi-before-tp | `[0x5019,0xA000)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-tp-code | `[0xA000,0x37000)` | tp/code | a-bank | explicit-range |
| a-opaque-after-tp → a-dp-after-tp | `[0x37000,0x80000)` | unknown/unmapped → dp/image | a-bank | forbidden |
| b-bank | `[0x80000,0x100000)` | system/image | ab-image | explicit-range |
| Added → b-dp-before-tp | `[0x80000,0x8A000)` | — → dp/image | — → b-bank | — → explicit-range |
| b-opaque-before-cmi → b-dp-before-cmi | `[0x80000,0x85016)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-cmi-dp-version | `[0x85016,0x85019)` | dp/command | b-bank → b-dp-before-tp | explicit-range |
| b-opaque-after-cmi-before-tp → b-dp-after-cmi-before-tp | `[0x85019,0x8A000)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-tp-code | `[0x8A000,0xB7000)` | tp/code | b-bank | explicit-range |
| b-opaque-after-tp → b-dp-after-tp | `[0xB7000,0x100000)` | unknown/unmapped → dp/image | b-bank | forbidden |

#### `nt51951-ab-merge-1024k`

| Region: before → after | flash range | owner/kind: before → after | parentRegionId: before → after | writeConstraint: before → after |
| --- | --- | --- | --- | --- |
| ab-image | `[0x0,0x100000)` | system/image | — | explicit-range |
| a-bank | `[0x0,0x80000)` | system/image | ab-image | explicit-range |
| Added → a-dp-before-tp | `[0x0,0xA000)` | — → dp/image | — → a-bank | — → explicit-range |
| a-opaque-before-cmi → a-dp-before-cmi | `[0x0,0x5016)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-cmi-dp-version | `[0x5016,0x5019)` | dp/command | a-bank → a-dp-before-tp | explicit-range |
| a-opaque-after-cmi-before-tp → a-dp-after-cmi-before-tp | `[0x5019,0xA000)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-tp-code | `[0xA000,0x37000)` | tp/code | a-bank | explicit-range |
| a-opaque-after-tp → a-dp-after-tp | `[0x37000,0x80000)` | unknown/unmapped → dp/image | a-bank | forbidden |
| b-bank | `[0x80000,0x100000)` | system/image | ab-image | explicit-range |
| Added → b-dp-before-tp | `[0x80000,0x8A000)` | — → dp/image | — → b-bank | — → explicit-range |
| b-opaque-before-cmi → b-dp-before-cmi | `[0x80000,0x85016)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-cmi-dp-version | `[0x85016,0x85019)` | dp/command | b-bank → b-dp-before-tp | explicit-range |
| b-opaque-after-cmi-before-tp → b-dp-after-cmi-before-tp | `[0x85019,0x8A000)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-tp-code | `[0x8A000,0xB7000)` | tp/code | b-bank | explicit-range |
| b-opaque-after-tp → b-dp-after-tp | `[0xB7000,0x100000)` | unknown/unmapped → dp/image | b-bank | forbidden |

Evidence: The 12 sets of compiled initializers, ordered operations, source/target ranges, DeclaredWriteRanges, overlap, fill/patch and external processor invocations in `before-write-audit.json` and the first `after-write-audit.json` are identical item by item. This comparison is not limited to the TP constants in the family.

Open: the capability definition hash chain is still being synchronized; failures in this initial narrow run are due to old identity pins, not output or range differences. Final Golden, ProfileContract, structure-only and fixed-head review remain pending.

Next: finish the necessary pin synchronization and rerun the affected narrow tests.

<a id="2026-09-29-執行與寫入證據"></a>

### 2026-09-29 execution and write evidence

State: verified (the listed narrow tests and audit; not yet integrated or published)

Evidence: The added declaration checks first failed 6/6 on the baseline (still unknown/unmapped), then passed 6/6 after the change. The 32 existing AB GoldenRegression baseline tests passed; with 6 write-audit cases added, the baseline had 38 passes, plus the 6 expected red results above. The final AB/profile/catalog filter had 169/169 passes and 0 skipped.

#### Owner-certified Golden

| Canonical case | Bytes | Complete output SHA-256 (before = after = expected) | Result |
| --- | --- | --- | --- |
| `nt51950-ab-boe-d82t80` | 524288 | `d18db8dc02ab4ff52cb17b4b3b3b90f99047c9d1acd2a5c23627197cf32f8650` | Complete bytes identical; no missing inputs |
| `nt51950-ab-hiway-d82t80` | 524288 | `4a292cd9615c58079b8994af8060af92562eaa92a55bc24bacc5ec5234e23b30` | Complete bytes identical; no missing inputs |
| `nt51950-ab-osd-d03t02-20260924` | 1048576 | `71de58a5f9a2ca2cb0d51789794af64586435b09e4ca61d176b6cd41b4990136` | Complete bytes identical; no missing inputs |

The three direct cases are executed by the existing `Nt51950CandidateMatchesOwnerApprovedAbGoldenWithCombinerAsync` (BOE, Hiway) and `Nt51950OsdPublicHostMatchesOwnerCertifiedGoldenAsync` (OSD); both sides execute the actual packaged Combiner. The complete 1 MiB OSD output includes the preserved source envelope beyond the 512 KiB layout template, without narrowing the comparison range. The two NT51951 BOE/Hiway records are fact-scoped workflow aliases, not two independent direct Goldens; the existing alias-resolution checks pass. No case manifest, BIN, expected hash, difference contract, owner approval or evidence rank is changed.

#### Planned write-range audit

The same `CompiledWritesRemainTpOnlyAfterDpInitialization` test compiles all six maps against both the baseline production source and the modified source, exercising normal/dummy DP for each map. It compares the JSON projection of the complete initializer and ordered operations: sequence, kind, source/target address spaces/ranges, DeclaredWriteRanges, overlap, fill/patch and external invocation (including processor allowed read/write ranges and staged artifact ranges); all 12/12 are identical. A separate structural diff proves that the profile operation/processor/input definitions are unchanged; the existing candidate tests check the scalar relocation addend. This is not merely a transcription of TP constants from family JSON.

Normal mode preserves initialization by copying the entire DP seed into `output-image`; dummy mode preserves the original blank initializer and omits that copy. After initialization, all writes to `output-image` are within the original TP sections. `ab-combiner-work` is a host staging address space; the entire staged container must not be mistaken for the processor's allowed write range.

The before/after audit artifacts both have SHA-256 `dd4dd547444f821b6f657105e52487b5bf582fc7e48a5fe8da634e92cbf384d3`.

| Map | A TP: output-image | B TP: output-image | Processor allowed writes: ab-combiner-work (each identical) | Normal / dummy operations |
| --- | --- | --- | --- | --- |
| `nt51950-ab-desay-cascade-1024k` | `[0xA000,0x37000)` | `[0x4A000,0x77000)` | `[0x4A100,0x4A104); [0x4A110,0x4A114); [0x4A130,0x4A134)` | 10 / 9 |
| `nt51950-ab-desay-single-1024k` | `[0xA000,0x37000)` | `[0x4A000,0x77000)` | `[0x4A100,0x4A104); [0x4A110,0x4A114); [0x4A130,0x4A134)` | 10 / 9 |
| `nt51950-ab-merge-1024k` | `[0xA000,0x37000)` | `[0x8A000,0xB7000)` | `[0x8A100,0x8A104); [0x8A110,0x8A114); [0x8A130,0x8A134)` | 10 / 9 |
| `nt51950-ab-merge-512k` | `[0xA000,0x37000)` | `[0x4A000,0x77000)` | `[0x4A100,0x4A104); [0x4A110,0x4A114); [0x4A130,0x4A134)` | 10 / 9 |
| `nt51951-ab-desay-1024k` | `[0xA000,0x37000)` | `[0x4A000,0x77000)` | `[0x4A100,0x4A104); [0x4A110,0x4A114); [0x4A130,0x4A134)` | 10 / 9 |
| `nt51951-ab-merge-1024k` | `[0xA000,0x37000)` | `[0x8A000,0xB7000)` | `[0x8A100,0x8A104); [0x8A110,0x8A114); [0x8A130,0x8A134)` | 10 / 9 |

All TP source ranges are `tp-a-input [0xA000,0x37000)` and `tp-b-work [0xA000,0x37000)`. B relocation is at `tp-b-work [0xA120,0xA124)`; the three four-byte postbuild imports write back into `output-image` at the same respective ranges as the processor allowed writes in the table above. The normal DP copy writes to `output-image [0,capacity)`; staging copies write to the `ab-combiner-work` ranges corresponding to the A/B banks. These ranges, order and sources are all identical before and after.

<a id="hash-與附帶-identity-pins"></a>

#### Hash and associated identity pins

- Family `0.7.2` → `0.7.3`; bundle `1.1.13-tp-svn.1` → `1.1.15-dp-regions.1`; the five composition profiles update only the family version/hash, leaving profileVersion and all operational declarations unchanged.
- Family SHA-256 `059947a9ef4f536feab0be71769a3a5578e6dfac459ea876e1619d078c95ff8d`; bundle entry-array hash `74c20ce3f1d53ca343995e1b757e0785cb842fe926e0799140d6d8251f137c49` is recalculated by the existing `AbBundleSourceHashTests`/`ProfileBundleEntryArrayHasher`, without a manually written hash algorithm.
- The catalog identity owner is `CanonicalDynamicRouteInventory`; its existing producer recalculates 3 AB Merge and 4 AB CtrlRAM fingerprints. The policy and the canonical manifest's `routeEvidence` synchronize only these seven sets of pins; all other JSON fields are identical. This is a necessary mechanical update to the existing hash chain, with no support promotion.
- `scripts/sync_derived.py --write --only reviewed-source-pins` synchronizes loader, package, smoke and Python fixture pins; a subsequent check reports 0 files changed. The catalog/dynamic-routes digests in `CanonicalCatalogSnapshotDigest` are synchronized; all section lengths and other sections remain unchanged.
- Derived pin surfaces extend the admission: `docs/contracts/canonical-capability-policy-v1.json`, `testdata/golden/canonical/manifest.json`, the existing loader/package/smoke pins and their related tests. All dispositions are `reuse`, with no new policy/validator semantics; under the existing path authority classification, future integration retains an additional `release-owner` gate.

Open: the structure-only verifier, Python pin tests and independent fixed-head review still need to be completed. Any Golden bytes/write ranges difference, need to change planner/executor/validator/schema semantics, reference/owner range contradiction or exhausted budget requires an immediate stop; none has been triggered so far.

Next: complete the pending checks, freeze the commit, then have a fresh-session read-only reviewer inspect that exact head.

<a id="2026-09-29-r3-p3-4p3-5-測試-checkpoint"></a>

### 2026-09-29 R3 P3-4/P3-5 test checkpoint

State: verified (local test strengthening; not integrated or published). The write audit in `Nt51950AbDpRegionTests` now includes `ScalarTransform.Addend` as an invariant-culture decimal string and pins 12 UTF-8 JSON SHA-256 values for normal/dummy modes across six maps. Every cell below is **before `6429bfb2c` = after `84ad3fdd9`**; comparison by map and mode key found 12/12 identical, and assertions were added only after confirming no differences. The base did not yet have this test file, so the same projection test was temporarily placed in a one-off detached worktree, changing only the bundle trust hash to the value committed in the base; that temporary file was not committed.

| Map | Normal: before = after SHA-256 | Dummy: before = after SHA-256 |
| --- | --- | --- |
| `nt51950-ab-desay-single-1024k` | `d144b4266f2f9ef71c37f6b7d0062cbfa06a4659264e3cbf546d06450683915d` | `ebf76760ba08a89df319353e299778275a3de1c721349d3f1b32d0c561870bcd` |
| `nt51950-ab-desay-cascade-1024k` | `fc0a3197028ba1f97968e28989997fd2e24d4fd8b42372344cd5024c5a10e60c` | `4239e04831afeb17f3690665baade0e98e74103295b736193bbf01a6596ec9ab` |
| `nt51951-ab-desay-1024k` | `c2ceff2b12858c7856829a2756457f2108e36edbcbba425891eaadca401ae043` | `56adba9615d976c8029d1f2845a6dd7b88d2547c60c2eb7168e796805e0fcc07` |
| `nt51950-ab-merge-512k` | `046c6c36a3c4a7cc8739aa6d2cf9f5deb37586f3e107e75d5ff9e4a02c3ce3e4` | `e1def5e48b017ccb578f83f81710c47b2fbfbbc7f7644704bf45233e8c0e7911` |
| `nt51950-ab-merge-1024k` | `0ea9b1bf4de754b9b8a8ad25eb462681e5347ba0a9ce9828142f0319bb5ab020` | `38977c70a3804b1f5536537957ee4984310080893975bad42f8dd9fc847ad087` |
| `nt51951-ab-merge-1024k` | `abe450e52fb6f2f49822f227ff084c68c39d679e383f38450d9c01d81cd762a5` | `12b5fb9250b2dd2b9ca382d562e8c0d95c12e26e9fdb571837732d5b8d249c58` |

P3-4 locks `Owner=Dp` and `Kind=Image` for `dp-container-tail` at the region level in the three Desay maps. The existing tests compile profile candidates directly, whereas `MemoryLayoutProjector.Project` requires a separately created accepted authoring session with the exact capability; the region-level assertions allowed by the review task are therefore used. The projector's `ClassifyContent` projects it as `ContentRole=Dp` based on `Owner=Dp`; the initial disposition in dummy mode consequently changes from `Resolved` to `Blank`, while UI display actions and labels remain the same.

Commands: both sides first load user-level `NFC_TEST_AREA_ROOT`, confirm the existing `temp`, set `TEMP`, `TMP`, `TMPDIR` to that location and set `DOTNET_CLI_UI_LANGUAGE=en`. The base runs `dotnet test tests/NvtFwCombiner.Bootstrap.Tests --filter FullyQualifiedName~Nt51950AbDpRegionTests.CompiledWritesRemainTpOnlyAfterDpInitialization --logger 'console;verbosity=detailed'` (6/6 pass); the after source runs `dotnet test tests/NvtFwCombiner.Bootstrap.Tests --no-restore --filter FullyQualifiedName~Nt51950AbDpRegionTests --logger 'console;verbosity=detailed'` when producing the SHA values (13/13 pass). After adding the pins, `dotnet test tests/NvtFwCombiner.Bootstrap.Tests --no-restore --filter FullyQualifiedName~Nt51950AbDpRegionTests` runs: 13/13 pass, 0 failed/skipped.

<a id="2026-09-29-本機凍結-checkpoint"></a>

### 2026-09-29 local freeze checkpoint

State: verified (local patch; this checkpoint is included in the commit, not integrated or published)

Evidence:

| Check | Actual result |
| --- | --- |
| `dotnet test tests/NvtFwCombiner.ProfileContract.Tests/NvtFwCombiner.ProfileContract.Tests.csproj` | 484 passed, 0 failed/skipped |
| Bootstrap narrow tests (exact filter below) | 169 passed, 0 failed/skipped |
| `dotnet test tests/NvtFwCombiner.GoldenRegression.Tests/NvtFwCombiner.GoldenRegression.Tests.csproj` | 15 passed, 0 failed/skipped; separate evidence of actual AB direct Golden execution is in Bootstrap |
| `dotnet test tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj --no-build --filter FullyQualifiedName~AbBundleSourceHashTests` | 2 passed; the existing hash owner reads the modified source bytes |
| `dotnet test tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj --filter FullyQualifiedName~BuiltInV2BundlePinsHaveOneOwner` | 1 passed |
| `python -m pytest tests/scripts/test_release_package_policy.py tests/scripts/test_release_smoke_policy.py tests/scripts/test_sync_derived.py -q` | First run: 161 passed/2 failed; the 2 RID lockfile environment failures passed the recheck below |
| `python -m pytest tests/scripts/test_release_package_policy.py -q -k 'distribution_launcher_committed_rid_locks_preserve_package_identities or distribution_launcher_rid_restore_keeps_invocation_locks_immutable'` | 2 passed, 87 deselected; a total of 163 distinct cases passed, not a second full-file rerun |
| `python scripts/verify.py --structure-only` | PASS; structure lane and Polytail fast checks passed |
| `python scripts/sync_derived.py` | PASS; 0 files changed |
| `git diff --check` | PASS |
| JSON semantic diff/privacy and ownership checks | The five profiles are identical except for family version/hash; policy/Golden inventory are identical except for seven sets of fingerprints; no changes to BINs, schema, planner, executor, validator semantics or the commander board |

Every tests/verifier shell first reads user-level `NFC_TEST_AREA_ROOT`, verifies the existing `temp`, and explicitly sets `TEMP`, `TMP`, `TMPDIR`. `--all` was not run.

Exact filter for the Bootstrap command:

```text
dotnet test tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj --no-restore --filter 'FullyQualifiedName~Nt51950AbDpRegionTests|FullyQualifiedName~AbMergeGoldenRegressionTests|FullyQualifiedName~Nt51950AbMergeCandidateProfileTests|FullyQualifiedName~Nt51951AbMergeCandidateProfileTests|FullyQualifiedName~AbMergeFormatVariantProfileTests|FullyQualifiedName~AbMergeAuthoringDefinitionTests|FullyQualifiedName~AbDummyDpCompilationTests|FullyQualifiedName~CanonicalSourceProjectionBuiltInBundleTests|FullyQualifiedName~CanonicalCatalogSnapshotDigestTests|FullyQualifiedName~CanonicalCapabilityCatalogMigrationTests'
```

Each .NET command also uses a TRX logger and a fixed test-area evidence directory; the actual records are archived with the SHA-256 values below. The baseline and final production sources are the specified base and the commit containing this checkpoint, respectively; adding characterization tests to the baseline did not change production source.

Environment failure classification: a local restore without a RID temporarily removed the `net10.0/win-x64` entries from 14 lockfiles; the first Python check therefore lacked that key, and the second check observed the writer restoring the lockfiles during its test. After confirming that the non-RID dependency graph was unchanged, only lockfile edits generated by this runtime were restored; once the environment was fixed, the two previously failing cases passed individually. No lockfiles were changed, assertions relaxed, failures skipped or complete suite rerun.

Scoped Polytail / requirement trace: decision 192 → family graph/existing compiler → DP coverage and CMI parents across six maps, and 12 sets of compiled write audits; the same expected bytes → three direct Golden baseline/final executions; hash closure → existing hasher, dynamic inventory, derived sync and pin tests. No new runtime semantic path, fallback, support declaration or additional infrastructure. The primary writer's local review is `PASS-WITH-HUMAN-GATE`; this record does not replace the fresh-session independent review after the commit.

Open:

- The local firmware implementation and specified verification have no remaining failures; no stop condition was triggered, and the 3-hour budget was not exhausted.
- There is no NT51951 standalone direct AB Golden; the two workflow aliases retain their original evidence scope. This is not a missing-input issue for this task and does not declare a new certified case.
- The independent fixed-head review after the local commit is recorded in the detailed report specified by the owner, bound to the exact commit SHA; R3 review closure is not claimed before it is completed.
- Any future integration/push still requires exact-head review and owner last-push approval naming `firmware-owner` and `release-owner` under the current path policy. This task has no push/PR/GitHub write authority and does not claim integration-ready or release-ready status.

Golden coverage (2026-09-29 review P2 correction):
- All three owner-certified direct Goldens (BOE, Hiway, OSD) use `nt51950-ab-merge-512k`.
- The other five maps have no direct Golden: Desay single, Desay cascade and 951 Desay 1024k, 950 merge 1024k, and 951 merge 1024k.
- Evidence for these five maps consists of: 12/12 identical compiled plans before and after (6 maps × normal/dummy), Python reference and dummy tests.
- Visible change: the container tail in the three Desay maps changes from Unmapped (Neutral) to DP in AB merge Memory Layout; the disposition in dummy mode changes from Resolved to Blank, with the same screen presentation.

Next: complete the local commit, have a fresh-session read-only reviewer inspect that exact head, record the review and commit SHA in the specified detailed report, then hand back to the commander; do not continue with other backlog work.

#### Evidence artifact identities

| Artifact | SHA-256 |
| --- | --- |
| `baseline-ab.trx` | `80dc4e74c7f215d691a3a68cd46897fe2eb7e5aada3d36676039f6089138bc66` |
| `before-write-audit.json` | `dd4dd547444f821b6f657105e52487b5bf582fc7e48a5fe8da634e92cbf384d3` |
| `after-write-audit.json` | `dd4dd547444f821b6f657105e52487b5bf582fc7e48a5fe8da634e92cbf384d3` |
| `final-ab.trx` | `2e999931a2e1700d1a506f2907d40ac74db9318f2a84adc3c6e4acefb0c6fe9d` |
| `profile-contract.trx` | `2b72cb8b9056dc5cd5e862750062777fe5918d2d04e5d701fc7d29db77a259db` |
| `golden-regression.trx` | `a9c722f9655d446f335cec083445cd91882b6c7eef78fe209bc9151f51f69f80` |
| `final-hash.trx` | `0671f717348aa79c90175643f3c74b274624636f5a2e4a14866cee7085a22763` |
| `architecture-pin.trx` | `5e90739fac1baf868f8ec38c32c34a81a0d2d5b55b2af4a10c5e5fbbf0b7dcfd` |
| `pin-python-tests.log` | `eef6501a37fd0ba8d7df5e52dc132748dcf58272997a35d39dc8a7333fd34c86` |
| `pin-python-rid-recheck.log` | `3e7274849ab85b73b930bc988b512493ecfe7d7e99e50d3980b2a65ea00751cc` |
| `structure.log` | `82eedabf1d6237a3e604060b6c9b2db920c962fecf362953cd9401b307615d91` |
| `route-pins.json` | `4c2f6737105957c3aaa54daf6a76d6fdfe1c0d26fa76ba696883261470320b7c` |

### 2026-09-29 decision 195 admission

State: planned. Base: `cb321efeb` (commander commits `089702dd1`, `cb321efeb`).

- Outcome: only `nt51950-ab-merge-1024k` and `nt51951-ab-merge-1024k` move B CMI from `flash [0x85016,0x85019)` to `[0x84016,0x84019)` under decision 195; preserve complete DP partitions and TP writes.
- Risk / authority: R3; local edit, tests and commit authorized; no push, PR or GitHub mutation. Required future approval roles: `firmware-owner`, `release-owner` (dependent policy and release pins).
- Single writer: Codex `gpt-6-astra`, owner-requested `xhigh`; same branch and worktree as this dispatch. Owned surfaces: AB family/bundle/five profile bindings, trust index, mechanically dependent capability/Golden/release/test pins, affected Bootstrap tests and this log. Commander board remains read-only.
- Owner search: `rg` for `cmi-dp-version` identifies the shared `nt51951-ab-merge-1024k-seed-and-placement` as the sole region owner for these two maps. The 512k and Desay maps bind different region sets. `FirmwareRegion` carries the range through the existing compiler. Disposition: `extend-owner` for that region and its adjacent DP leaves; `reuse` for all consumers and hash producers.
- Direct readers: `CompiledInputArtifactObservationService.DecodeDpRegion`, `AbCodeOutputNameResolver.ReadDpToken`, and `FirmwareArtifactClassificationResolver.InspectAbCandidate`; compiler additional-delivery lowering refers to A CMI only. Follow the typed observations into firmware info/Details, report and naming; record each effect in the completion checkpoint.
- Hash producers: `AbBundleSourceHashTests` / `ProfileBundleEntryArrayHasher`, `CanonicalDynamicRouteInventory`, `CanonicalCatalogSnapshotDigest`, and `scripts/sync_derived.py --write --only reviewed-source-pins`. No hash algorithm, Golden expected bytes/hash or support decision changes.
- Acceptance / narrow gate: exact B CMI ranges for both 1024k maps, unchanged 512k/Desay ranges, complete nested partitions, and all 12 existing write-audit SHA pins unchanged. Final gate: requested Bootstrap filter, GoldenRegression, ProfileContract, affected hash/architecture/Python tests and structure-only verifier.
- Non-goals: planner/executor/validator/schema semantics, Desay retirement, public version changes, integration or publication. Output naming effects are disclosed for R3 owner confirmation, not independently accepted here.
- Stop and record in Open: any changed Golden expected bytes or TP write range; need for planner/executor/validator/schema semantic edits; the same test still failing after two fixes; two-hour budget exhausted.

Open: implementation and verification pending; real 1024k images have no direct Golden and retain decision 195's `1.2.0` owner verification gate.

### 2026-09-29 decision 195 B-bank CMI correction

State: verified (local gates). Source: base `cb321efeb` plus the decision 195 commit containing this checkpoint. Codex `gpt-6-astra`, owner-requested `xhigh`, remains the sole writer. Local commit only; no integration or publication.

#### Declarations and range evidence

Only the shared `nt51951-ab-merge-1024k-seed-and-placement` region set changes firmware ranges. It serves `nt51950-ab-merge-1024k` (the 2-plus-IC map, including the tested 2/3-IC selections) and `nt51951-ab-merge-1024k`:

| Region | Before, `flash` | After, `flash` |
| --- | --- | --- |
| `b-dp-before-cmi` | `[0x80000,0x85016)` | `[0x80000,0x84016)` |
| `b-cmi-dp-version` | `[0x85016,0x85019)` | `[0x84016,0x84019)` |
| `b-dp-after-cmi-before-tp` | `[0x85019,0x8A000)` | `[0x84019,0x8A000)` |

CMI remains `dp/command`, length 3, alignment 1, `explicit-range`, under `b-dp-before-tp` (`dp/image`, `flash [0x80000,0x8A000)`). The surrounding leaves remain `forbidden`; their boundary follows the relocated field. The three children tile the same parent without overlap or gaps. A CMI remains `[0x5016,0x5019)`. The 512k B CMI stays `[0x7B016,0x7B019)`, and all three Desay B CMIs stay `[0x45016,0x45019)`; those region sets are byte-for-byte unchanged.

`Nt51950AbDpRegionTests.DpSectionsPartitionTheTpComplementAndOwnCmi` now locks all six B CMI ranges and `flash` address space, DP parent membership, each DP parent's child partition and the complete TP complement. Before the profile edit, `red-ranges.trx` has 11 pass / 2 fail, exactly the two old 1024k ranges. After the edit, all assertions pass.

The 12 existing normal/dummy audit SHA pins are **unchanged**, including invariant decimal `ScalarAddend`; no expected SHA was replaced. Actual `WRITE-AUDIT` JSON from baseline `red-ranges.trx` and final `bootstrap.trx` compares equal for every map/mode. The projection contains initialization and ordered operations, source/target/declared write ranges, overlap, scalar transforms and processor permissions; it does not contain the CMI region graph or governing-chain identity. Thus relocating this read-only metadata does not change the audit. The two target maps still overlay TP at `flash [0xA000,0x37000)` and `[0x8A000,0xB7000)`; every subsequent output write remains inside those TP ranges. The 512k/Desay TP ranges also remain individually identical.

#### Consumer inventory and before/after effects

Search: `rg -n 'cmi-dp-version' src` plus callers of the typed observations and fingerprint producers. The following are the complete direct region-ID readers and their downstream consumers at this source; no new reader was introduced.

| Consumer / position | Effect on the two target maps |
| --- | --- |
| `src/NvtFwCombiner.Application/InputInspection/CompiledInputArtifactObservationService.cs:153`, `ObserveDp` → `DecodeDpRegion` at :174 | DP-B reads the accepted immutable DP snapshot at `0x84016` instead of `0x85016`. Major is byte `+1`; minor is the high nibble of byte `+2`; Jira is byte `+0` plus the low nibble of byte `+2`. DPA and unknown/bounds policies are unchanged. |
| `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareInspectionSession.cs:74`, `GetAbInputFacts` | Firmware info renders the preceding typed DPB Version/Jira values from the new location. Labels, priority and formatting are unchanged. The inspection regression puts different old/new values into the same synthetic input and verifies new major/minor/Jira for NT51950 2/3 IC and NT51951. |
| `src/NvtFwCombiner.Application/InputInspection/FirmwareArtifactClassificationResolver.CtrlRam.cs:151`, `InspectAbCandidate`, CMI call at :199 | AB CtrlRAM Reference inspection uses the same layout CMI reader, so DPB observation follows the new location when either target map is selected. TP/FWConfig validity, bank selection, structure checks and write plans do not derive from DP CMI and remain unchanged. |
| `src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.FirmwareFacts.cs:143` | Reference firmware Details renders `DPB Version` and `DPB Jira Index` from that new typed observation. DPA and bank ranges are unchanged. |
| `src/NvtFwCombiner.Application/Composition/AbCodeOutputNameResolver.cs:17`, :86, :141 | `dp-b` naming token now derives from the new CMI; its format remains `D{major:X2}{minor:X2}`. See the explicit naming disclosure below. Normal inputs may produce a different automatic filename; dummy DP still uses `Dummy`, and explicit filename override still controls the actual name. |
| `src/NvtFwCombiner.Application/Composition/CompositionRunService.Reports.cs:64`, `CompositionRunReport.cs:100,117`, `CompositionRunReportJson.cs:87` | Reports project existing typed results. `OutputNaming.Tokens[dp-b].Value` and `.ParserId`, automatic/actual filenames (unless overridden), and `Output.FileName` follow the corrected read. `CompilationFingerprint` changes with declaration identity. Reports do not independently decode CMI. Input/output byte hashes, operations and mutation ranges are unchanged for identical inputs; token source-snapshot SHA remains the same. CLI regression asserts the serialized DP-B value, parser registers and automatic output name. |
| `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.ContractLowering.cs:168`, `CreateAdditionalDeliveries` | This direct ID reader uses **A** CMI only, together with other required A region IDs; no B CMI read exists here. Additional-delivery rules and A tokens do not change on either map. |
| `src/NvtFwCombiner.Application/MemoryLayout/MemoryLayoutProjector.cs:223,275` | Generic region projection consumes map ranges; its outer bank/DP sections are unchanged. This branch has no special `*-cmi-dp-version` reader in Memory Layout. Decision 192's separate DP card/subfield presentation work stays with the memory-layout workstream. |
| `src/NvtFwCombiner.Domain/Firmware/FirmwareMapResolutionResult.Fingerprint.cs:16`; `src/NvtFwCombiner.Domain/Composition/CompiledComposition.Fingerprint.cs:29,118`; `src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.cs:109,467` and `.Banks.cs:71` | Family/bundle identity and map facts feed resolution/compilation and capability identities through the existing owners. Both target capabilities change; shared bundle identity also mechanically changes the 512k route and all four AB CtrlRAM route pins, without changing their support decisions or operation semantics. |
| `docs/contracts/canonical-capability-policy-v1.json`; `testdata/golden/canonical/manifest.json` | Seven routes re-pin the freshly produced fingerprints: policy has 28 changed scalar fields (route plus authoring/publication/evidence), Golden inventory has seven `routeEvidence[*].capabilityFingerprint` changes. No other policy/manifest semantic field changes; `cases`, evidence scope, approvals and every Golden expected output hash remain identical. |

Naming disclosure for owner confirmation: both profiles keep `NT{ic}_FlashCode_A_{dp-a}{tp-a}_B_{dp-b}{tp-b}_{date}.bin`. Before, `dp-b = D{input[0x85017]:X2}{(input[0x85018] >> 4):X2}`; after, `dp-b = D{input[0x84017]:X2}{(input[0x84018] >> 4):X2}`. Jira does not enter the filename. For the synthetic CLI fixture with old CMI major/minor `0x91/0x09` and new `0x83/0x01`, the rule changes `NT51950_FlashCode_A_D8200T8004_B_D9109T8102_{date}.bin` to `NT51950_FlashCode_A_D8200T8004_B_D8301T8102_{date}.bin`; NT51951 differs only in the IC number. The former name is derived from the inspected pre-change rule; the latter is exercised by the final CLI tests. Real filenames change only if the two locations decode different versions. This is an identified effect, **not** a declaration that the owner has accepted all real-image naming changes.

#### Derived identities

Family `0.7.3` → `0.7.4`; bundle `1.1.15-dp-regions.1` → `.2`. Five composition profile versions and all operational declarations stay unchanged; only each family version/hash binding moves. Source-file SHA-256, the existing `ProfileBundleEntryArrayHasher` via `AbBundleSourceHashTests`, `CanonicalDynamicRouteInventory` and `CanonicalCatalogSnapshotDigest` supplied the identities; `sync_derived.py --write --only reviewed-source-pins` synchronized loader/package/smoke/Python pins. No hash algorithm was reimplemented and no expected firmware hash was regenerated.

| Identity | Before → after |
| --- | --- |
| Family source SHA-256 | `059947a9ef4f536feab0be71769a3a5578e6dfac459ea876e1619d078c95ff8d` → `625151beda7c3beca729e04d767c3e43e895020e0ad5a2dab43a4640b9e6b5e6` |
| Bundle entry-array hash | `74c20ce3f1d53ca343995e1b757e0785cb842fe926e0799140d6d8251f137c49` → `68b3a4d6daa55ba9ac76dc4b1815f744f0f1ff82afa734b24b7a28afae5e677c` |
| Trust index SHA-256 | `5d01d55748fa0833d792e42a0be0c89ea0aa7b63b3f16a578a77da3af48bcb93` → `831a3968740801e14c1f2c6e362dbcef1248d6ba870a1c5d0c41f1124ca2982f` |
| Capability policy SHA-256 | `544433f640d66170e556f0f7285ec4a7b6907489cdc234d8cd24801ffb5c8d2f` → `a3ad08440076fb6b8b840ba64a6fbe0ccadb4345530ba1db09ab0b4f0fa671d4` |
| Golden inventory file SHA-256 | `19e6ee1d56441b03d3e0b2df2c9a33e35b6394bcc1cab57848ef3399ccf0c69b` → `5fa41fea1ce7d8b9278e2bdd7d38a4cc6759bd24e18d07781006b3e9b4416c59`; only seven route identity pins changed. |
| Catalog snapshot digest | `565eb0914427c8becb4ff3e4751bab7b0611a9ef770a6e98ccbd1cfca48804ab` → `adf144922d409b48b3ef76a263f57c957337c3c4b4ecefd3b65137e18bbca730`; only `catalog` and `dynamic-routes` section digests change; all section lengths remain identical. End to end from the `1.1.x` base, decision 192 moved it `2039f8287e34…` → `565eb0914427…` and decision 195 moved it on to `adf144922d40…`. Two test files pin the published snapshot and must move together: `CanonicalCatalogSnapshotDigestTests` and `PrebuiltProfileCatalogEquivalenceTests.AssertPinned`. The second was missed at first, so the `dotnet / test (bootstrap)` and `dotnet / build-test` checks failed on head `693a3ca37` (35/95 in the prebuilt-catalog tests); it was found in CI, fixed in the follow-up commit, and the full Bootstrap project then passed (2,158/2,158). |
| Candidate compilation pins | NT51950 512k: `0ef1f2cd…57e0ef` → `b474c4ba…851f78`; NT51951 1024k: `ace12208…d5fbdd` → `7626dd39…7e3517`. Both include the shared family/bundle identity. |

| Canonical route (IC / workflow / count / map) | Fingerprint before → after |
| --- | --- |
| 950 / AB Merge / 1 IC / merge maps | `81a69ebb…de22b5` → `850fa225…565c5a8` |
| 950 / AB Merge / 2-plus IC / cascade maps | `55995600…fc592a` → `d2e3caab…10e0baa` |
| 951 / AB Merge / selector-free / 1024k | `d0d38b5b…eb2904` → `13886377…1e0dc93` |
| 950 / AB CtrlRAM / 1 IC / 512k | `905b2c62…fa9593` → `9e985b75…86508b8` |
| 950 / AB CtrlRAM / 2 IC / 1024k | `efded7f6…1f99a75` → `389e0e7a…5f6e868e` |
| 951 / AB CtrlRAM / 1 IC / 1024k | `a830b727…ad802f` → `a4ad18a2…e8c8e8d` |
| 951 / AB CtrlRAM / 2 IC / 1024k | `fd56dff9…ad77218` → `c636736c…349dfef` |

#### Verification and residual gates

Every test process loaded user-level `NFC_TEST_AREA_ROOT`, used its existing `temp` child for `TEMP`, `TMP`, `TMPDIR`, and set `DOTNET_CLI_UI_LANGUAGE=en`. All .NET commands used `dotnet test tests/<project> --no-restore`, a TRX logger and test-area evidence directory `evidence/f115-dp-cmi-195-20260929`.

| Check | Result |
| --- | --- |
| Bootstrap filter `FullyQualifiedName~Nt51950AbDpRegionTests\|FullyQualifiedName~AbMergeGoldenRegressionTests\|FullyQualifiedName~Nt5195` | 217 passed, 0 failed/skipped |
| Bootstrap consumer filter: `CanonicalCatalogSnapshotDigestTests`, `CanonicalCapabilityCatalogMigrationTests`, `CanonicalSourceProjectionBuiltInBundleTests`, `AbMergeAuthoringDefinitionTests`, `AbMergeFormatVariantProfileTests`, `AbDummyDpCompilationTests`, `AbCtrlRam*`, `CtrlRamMemorySection`, `AbMergeCanonicalReadinessTests` | 293 passed, 0 failed/skipped |
| `dotnet test tests/NvtFwCombiner.GoldenRegression.Tests --no-restore` | 15 passed |
| `dotnet test tests/NvtFwCombiner.ProfileContract.Tests --no-restore` | 484 passed |
| Infrastructure filter `FullyQualifiedName~AbBundleSourceHashTests` | 2 passed |
| Architecture filter `FullyQualifiedName~BuiltInV2BundlePinsHaveOneOwner` | 1 passed |
| `python -m pytest tests/scripts/test_release_package_policy.py tests/scripts/test_release_smoke_policy.py tests/scripts/test_sync_derived.py tests/scripts/test_ab_merge_fixture_validation.py tests/scripts/test_canonical_golden_validation.py tests/scripts/test_v0916_parity_1x_amendment.py -q` | 265 passed, 480.25s |
| `python scripts/sync_derived.py` | PASS, 0 files changed |
| `python scripts/verify.py --structure-only` | PASS, structure 26.8s |

Golden scope: BOE/Hiway `Nt51950CandidateMatchesOwnerApprovedAbGoldenWithCombinerAsync` and OSD `Nt51950OsdPublicHostMatchesOwnerCertifiedGoldenAsync` all actually ran and passed complete-output comparisons in `bootstrap.trx`. All three are **512k map** cases (OSD additionally preserves its complete larger source envelope). The other five maps still have no direct Golden. This change neither creates new certification nor treats NT51951 aliases as direct output evidence.

Development failures are retained: the first range-test build used the wrong address-space owner ([fixed test defect](../bugs/BUG-20260929-dp-cmi-test-address-space-owner.md)); scratch text writes initially used Windows newlines ([fixed pin-refresh defect](../bugs/BUG-20260929-dp-pin-refresh-newlines.md)). An early bundle build was blocked by the not-yet-synchronized trust entry, so the unchanged existing hasher was invoked with `--no-build` against source bytes. TRX repeats stdout at result/run scope; the scratch pin extractor initially rejected 14 records, then deduplicated the seven identical producer tuples. One premature broad run therefore still saw old policy/compilation pins and unavailable catalog publication; the successful producer synchronization removed those failures. A final catalog digest mismatch was the expected identity change above and was re-pinned from the existing digest output. No firmware output expectation or write-audit pin was relaxed.

Scoped local review traces decision 195 to the one existing region owner, its typed readers, the location-selective synthetic tests and the unchanged write audit. There is no alternate firmware path, support promotion or hidden generated payload. `git diff --check` passes; `packages.lock.json` and the commander board have no diff. The final candidate retains only the approved declaration, its derived identities, tests, this checkpoint and two fixed development-bug records. No full integration/release suite or publication is claimed by these local results.

Open:

- Owner must explicitly confirm the disclosed DP-B automatic filename/token effect in the R3 approval. Implementation does not wait on that future integration gate.
- Decision 195 keeps real-image verification of both 1024k B CMI locations/versions/names in `1.2.0`; no direct Golden exists for these maps. Available evidence is the exact synthetic reader/naming tests and unchanged compiled write plans, not hardware certification.
- Fresh independent fixed-head review and any future last-push approval naming **`firmware-owner` and `release-owner`** remain separate gates; the existing external R3 report covers earlier heads, not this new delta. This local task grants no push/PR/GitHub authority and does not claim integration/release readiness.
- No Golden expected bytes or TP write ranges changed; no planner/executor/validator/schema semantic edit was needed. No test remained failing after two corrective fixes, and the two-hour stop budget was not exhausted. Any occurrence of those stop conditions ends work and must be recorded here.

Next: commit this coherent patch, obtain a read-only independent review of that exact commit, and return the SHA plus remaining owner gates without pushing. The fresh reviewer uses `gpt-6-astra`, `xhigh`, without the writer's conversation, chosen for the R3 firmware/consumer evidence review; it owns no mutable path.

#### Decision 195 evidence identities

| Artifact | SHA-256 |
| --- | --- |
| `red-ranges.trx` | `8d1429c268f38e2e953d065e6cd2114f0e8041452c0abb835a8fe1a8e7f95354` |
| `bootstrap.trx` | `0ab12b37765aedd51f183435eecf6154eeb480d0ff5bd366422742843d3112ac` |
| `bootstrap-consumers.trx` | `e214ae87aceacd2514ecf6166500a24eb5e3bcee40a517ad558ac4e322825367` |
| `golden.trx` | `8ec40451564ea397cec01a8945b742e88881cc6b1b3fd8e679668423fb1a5607` |
| `profile-contract.trx` | `3cf6b2ed13e065d0ee82b0e390c6b5823f3b6b1d71e174375e985939ab7692ff` |
| `hash.trx` | `557312a81223695fa56ce4099ec116a1937a8352b407f52d9dbee56556a8e748` |
| `architecture.trx` | `7cbfc4ee8e988b0a58f764e84e9fed461c337d99d5d55264dd503a35341ca0b7` |
| `python.log` | `1251fc59f7add02c4c651f9a1e8ec39fb349acf07b6c395e0125ea45e1cc13de` |
| `audit-comparison.json` | `2447625a755fad2d77ededfe70a082c6c396fb65eac7851153226593eca6a117` |
| `route-pins.json` | `db09625b01c9db4969c240b11df361631bb7ba25016b309a7aeba92aa58a8b3e` |
| `structure.log` | `213d5b5d2aa74efde40d4b6cdde6e1c1a49edad6115967d70f33a7531eeea6d8` |

Merged into `1.1.x` through #490 (merge `329ae9f37`) on 2026-09-30 and shipped in v1.1.15 and v1.2.0. The owner's
real-image check of both 1024k B-bank CMI locations was carried by the owner's `1.2.0` release approval
(decisions 195, 199 and 206; see the [v1.2.0 closure](../1.2.x.md#released-v120--2026-09-30)).
