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

### 2026-09-29 宣告與範圍盤點

State: local

下表所有區間的 address space 均為 `flash`，採 `[start, endExclusive)`；每一列 alignment 前後皆為 `1`。原有 region 的範圍與 writeConstraint 全部保留。新增 DP image 只將既有 CMI 與相鄰 protected leaves 分組；其 `explicit-range` 延續原 bank 的容器限制，children 仍逐一限制寫入。

三張 Desay map 共用原 `opaque-container-tail`；依 decision 192 改為 `dp-container-tail`，`flash [0x80000,0x100000)` 仍為 `forbidden`。950 merge 1024k 與 951 merge 1024k 則是各 `0x80000` 的 A/B banks，不使用該 tail 宣告。

#### `nt51950-ab-desay-single-1024k`

| Region：前 → 後 | flash range | owner/kind：前 → 後 | parentRegionId：前 → 後 | writeConstraint：前 → 後 |
| --- | --- | --- | --- | --- |
| ab-image | `[0x0,0x100000)` | system/image | — | explicit-range |
| a-bank | `[0x0,0x40000)` | system/image | ab-image | explicit-range |
| 新增 → a-dp-before-tp | `[0x0,0xA000)` | — → dp/image | — → a-bank | — → explicit-range |
| a-opaque-before-cmi → a-dp-before-cmi | `[0x0,0x5016)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-cmi-dp-version | `[0x5016,0x5019)` | dp/command | a-bank → a-dp-before-tp | explicit-range |
| a-opaque-after-cmi-before-tp → a-dp-after-cmi-before-tp | `[0x5019,0xA000)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-tp-code | `[0xA000,0x37000)` | tp/code | a-bank | explicit-range |
| a-opaque-after-tp → a-dp-after-tp | `[0x37000,0x40000)` | unknown/unmapped → dp/image | a-bank | forbidden |
| b-bank | `[0x40000,0x80000)` | system/image | ab-image | explicit-range |
| 新增 → b-dp-before-tp | `[0x40000,0x4A000)` | — → dp/image | — → b-bank | — → explicit-range |
| b-opaque-before-cmi → b-dp-before-cmi | `[0x40000,0x45016)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-cmi-dp-version | `[0x45016,0x45019)` | dp/command | b-bank → b-dp-before-tp | explicit-range |
| b-opaque-after-cmi-before-tp → b-dp-after-cmi-before-tp | `[0x45019,0x4A000)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-tp-code | `[0x4A000,0x77000)` | tp/code | b-bank | explicit-range |
| b-opaque-after-tp → b-dp-after-tp | `[0x77000,0x80000)` | unknown/unmapped → dp/image | b-bank | forbidden |
| opaque-container-tail → dp-container-tail | `[0x80000,0x100000)` | unknown/unmapped → dp/image | ab-image | forbidden |

#### `nt51950-ab-desay-cascade-1024k`

| Region：前 → 後 | flash range | owner/kind：前 → 後 | parentRegionId：前 → 後 | writeConstraint：前 → 後 |
| --- | --- | --- | --- | --- |
| ab-image | `[0x0,0x100000)` | system/image | — | explicit-range |
| a-bank | `[0x0,0x40000)` | system/image | ab-image | explicit-range |
| 新增 → a-dp-before-tp | `[0x0,0xA000)` | — → dp/image | — → a-bank | — → explicit-range |
| a-opaque-before-cmi → a-dp-before-cmi | `[0x0,0x5016)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-cmi-dp-version | `[0x5016,0x5019)` | dp/command | a-bank → a-dp-before-tp | explicit-range |
| a-opaque-after-cmi-before-tp → a-dp-after-cmi-before-tp | `[0x5019,0xA000)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-tp-code | `[0xA000,0x37000)` | tp/code | a-bank | explicit-range |
| a-opaque-after-tp → a-dp-after-tp | `[0x37000,0x40000)` | unknown/unmapped → dp/image | a-bank | forbidden |
| b-bank | `[0x40000,0x80000)` | system/image | ab-image | explicit-range |
| 新增 → b-dp-before-tp | `[0x40000,0x4A000)` | — → dp/image | — → b-bank | — → explicit-range |
| b-opaque-before-cmi → b-dp-before-cmi | `[0x40000,0x45016)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-cmi-dp-version | `[0x45016,0x45019)` | dp/command | b-bank → b-dp-before-tp | explicit-range |
| b-opaque-after-cmi-before-tp → b-dp-after-cmi-before-tp | `[0x45019,0x4A000)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-tp-code | `[0x4A000,0x77000)` | tp/code | b-bank | explicit-range |
| b-opaque-after-tp → b-dp-after-tp | `[0x77000,0x80000)` | unknown/unmapped → dp/image | b-bank | forbidden |
| opaque-container-tail → dp-container-tail | `[0x80000,0x100000)` | unknown/unmapped → dp/image | ab-image | forbidden |

#### `nt51951-ab-desay-1024k`

| Region：前 → 後 | flash range | owner/kind：前 → 後 | parentRegionId：前 → 後 | writeConstraint：前 → 後 |
| --- | --- | --- | --- | --- |
| ab-image | `[0x0,0x100000)` | system/image | — | explicit-range |
| a-bank | `[0x0,0x40000)` | system/image | ab-image | explicit-range |
| 新增 → a-dp-before-tp | `[0x0,0xA000)` | — → dp/image | — → a-bank | — → explicit-range |
| a-opaque-before-cmi → a-dp-before-cmi | `[0x0,0x5016)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-cmi-dp-version | `[0x5016,0x5019)` | dp/command | a-bank → a-dp-before-tp | explicit-range |
| a-opaque-after-cmi-before-tp → a-dp-after-cmi-before-tp | `[0x5019,0xA000)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-tp-code | `[0xA000,0x37000)` | tp/code | a-bank | explicit-range |
| a-opaque-after-tp → a-dp-after-tp | `[0x37000,0x40000)` | unknown/unmapped → dp/image | a-bank | forbidden |
| b-bank | `[0x40000,0x80000)` | system/image | ab-image | explicit-range |
| 新增 → b-dp-before-tp | `[0x40000,0x4A000)` | — → dp/image | — → b-bank | — → explicit-range |
| b-opaque-before-cmi → b-dp-before-cmi | `[0x40000,0x45016)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-cmi-dp-version | `[0x45016,0x45019)` | dp/command | b-bank → b-dp-before-tp | explicit-range |
| b-opaque-after-cmi-before-tp → b-dp-after-cmi-before-tp | `[0x45019,0x4A000)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-tp-code | `[0x4A000,0x77000)` | tp/code | b-bank | explicit-range |
| b-opaque-after-tp → b-dp-after-tp | `[0x77000,0x80000)` | unknown/unmapped → dp/image | b-bank | forbidden |
| opaque-container-tail → dp-container-tail | `[0x80000,0x100000)` | unknown/unmapped → dp/image | ab-image | forbidden |

#### `nt51950-ab-merge-512k`

| Region：前 → 後 | flash range | owner/kind：前 → 後 | parentRegionId：前 → 後 | writeConstraint：前 → 後 |
| --- | --- | --- | --- | --- |
| ab-image | `[0x0,0x80000)` | system/image | — | explicit-range |
| a-bank | `[0x0,0x40000)` | system/image | ab-image | explicit-range |
| a-opaque-before-tp → a-dp-before-tp | `[0x0,0xA000)` | unknown/unmapped → dp/image | a-bank | forbidden |
| a-tp-code | `[0xA000,0x37000)` | tp/code | a-bank | explicit-range |
| 新增 → a-dp-after-tp | `[0x37000,0x40000)` | — → dp/image | — → a-bank | — → explicit-range |
| a-opaque-after-tp-before-cmi → a-dp-after-tp-before-cmi | `[0x37000,0x3B016)` | unknown/unmapped → dp/data | a-bank → a-dp-after-tp | forbidden |
| a-cmi-dp-version | `[0x3B016,0x3B019)` | dp/command | a-bank → a-dp-after-tp | explicit-range |
| a-opaque-after-cmi → a-dp-after-cmi | `[0x3B019,0x40000)` | unknown/unmapped → dp/data | a-bank → a-dp-after-tp | forbidden |
| b-bank | `[0x40000,0x80000)` | system/image | ab-image | explicit-range |
| b-opaque-before-tp → b-dp-before-tp | `[0x40000,0x4A000)` | unknown/unmapped → dp/image | b-bank | forbidden |
| b-tp-code | `[0x4A000,0x77000)` | tp/code | b-bank | explicit-range |
| 新增 → b-dp-after-tp | `[0x77000,0x80000)` | — → dp/image | — → b-bank | — → explicit-range |
| b-opaque-after-tp-before-cmi → b-dp-after-tp-before-cmi | `[0x77000,0x7B016)` | unknown/unmapped → dp/data | b-bank → b-dp-after-tp | forbidden |
| b-cmi-dp-version | `[0x7B016,0x7B019)` | dp/command | b-bank → b-dp-after-tp | explicit-range |
| b-opaque-after-cmi → b-dp-after-cmi | `[0x7B019,0x80000)` | unknown/unmapped → dp/data | b-bank → b-dp-after-tp | forbidden |

#### `nt51950-ab-merge-1024k`

| Region：前 → 後 | flash range | owner/kind：前 → 後 | parentRegionId：前 → 後 | writeConstraint：前 → 後 |
| --- | --- | --- | --- | --- |
| ab-image | `[0x0,0x100000)` | system/image | — | explicit-range |
| a-bank | `[0x0,0x80000)` | system/image | ab-image | explicit-range |
| 新增 → a-dp-before-tp | `[0x0,0xA000)` | — → dp/image | — → a-bank | — → explicit-range |
| a-opaque-before-cmi → a-dp-before-cmi | `[0x0,0x5016)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-cmi-dp-version | `[0x5016,0x5019)` | dp/command | a-bank → a-dp-before-tp | explicit-range |
| a-opaque-after-cmi-before-tp → a-dp-after-cmi-before-tp | `[0x5019,0xA000)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-tp-code | `[0xA000,0x37000)` | tp/code | a-bank | explicit-range |
| a-opaque-after-tp → a-dp-after-tp | `[0x37000,0x80000)` | unknown/unmapped → dp/image | a-bank | forbidden |
| b-bank | `[0x80000,0x100000)` | system/image | ab-image | explicit-range |
| 新增 → b-dp-before-tp | `[0x80000,0x8A000)` | — → dp/image | — → b-bank | — → explicit-range |
| b-opaque-before-cmi → b-dp-before-cmi | `[0x80000,0x85016)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-cmi-dp-version | `[0x85016,0x85019)` | dp/command | b-bank → b-dp-before-tp | explicit-range |
| b-opaque-after-cmi-before-tp → b-dp-after-cmi-before-tp | `[0x85019,0x8A000)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-tp-code | `[0x8A000,0xB7000)` | tp/code | b-bank | explicit-range |
| b-opaque-after-tp → b-dp-after-tp | `[0xB7000,0x100000)` | unknown/unmapped → dp/image | b-bank | forbidden |

#### `nt51951-ab-merge-1024k`

| Region：前 → 後 | flash range | owner/kind：前 → 後 | parentRegionId：前 → 後 | writeConstraint：前 → 後 |
| --- | --- | --- | --- | --- |
| ab-image | `[0x0,0x100000)` | system/image | — | explicit-range |
| a-bank | `[0x0,0x80000)` | system/image | ab-image | explicit-range |
| 新增 → a-dp-before-tp | `[0x0,0xA000)` | — → dp/image | — → a-bank | — → explicit-range |
| a-opaque-before-cmi → a-dp-before-cmi | `[0x0,0x5016)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-cmi-dp-version | `[0x5016,0x5019)` | dp/command | a-bank → a-dp-before-tp | explicit-range |
| a-opaque-after-cmi-before-tp → a-dp-after-cmi-before-tp | `[0x5019,0xA000)` | unknown/unmapped → dp/data | a-bank → a-dp-before-tp | forbidden |
| a-tp-code | `[0xA000,0x37000)` | tp/code | a-bank | explicit-range |
| a-opaque-after-tp → a-dp-after-tp | `[0x37000,0x80000)` | unknown/unmapped → dp/image | a-bank | forbidden |
| b-bank | `[0x80000,0x100000)` | system/image | ab-image | explicit-range |
| 新增 → b-dp-before-tp | `[0x80000,0x8A000)` | — → dp/image | — → b-bank | — → explicit-range |
| b-opaque-before-cmi → b-dp-before-cmi | `[0x80000,0x85016)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-cmi-dp-version | `[0x85016,0x85019)` | dp/command | b-bank → b-dp-before-tp | explicit-range |
| b-opaque-after-cmi-before-tp → b-dp-after-cmi-before-tp | `[0x85019,0x8A000)` | unknown/unmapped → dp/data | b-bank → b-dp-before-tp | forbidden |
| b-tp-code | `[0x8A000,0xB7000)` | tp/code | b-bank | explicit-range |
| b-opaque-after-tp → b-dp-after-tp | `[0xB7000,0x100000)` | unknown/unmapped → dp/image | b-bank | forbidden |

Evidence: `before-write-audit.json` 與第一輪 `after-write-audit.json` 的 12 組 compiled initializer、ordered operation、source/target range、DeclaredWriteRanges、overlap、fill/patch 及 external processor invocation 逐項相同。不是僅比較 family 中的 TP 常數。

Open: capability definition 的 hash 鏈尚在同步；本次初步 narrow run 失敗為舊 identity pins，不是輸出或範圍差異。最終 Golden、ProfileContract、structure-only 與 fixed-head review 待完成。

Next: 完成必要 pin 同步並重跑受影響窄測試。

### 2026-09-29 執行與寫入證據

State: verified（列明的窄測試與 audit；尚未整合／發布）

Evidence: 新增宣告檢查先在基準失敗 6/6（仍是 unknown/unmapped），修改後通過 6/6。原有 AB GoldenRegression 的 32 項 baseline 測試通過；加上 6 個 write-audit cases，baseline 共 38 pass，另有上述 6 個預期 red。最終 AB/profile/catalog filter 為 169/169 pass、0 skipped。

#### Owner-certified Golden

| Canonical case | Bytes | 完整輸出 SHA-256（before = after = expected） | 結果 |
| --- | --- | --- | --- |
| `nt51950-ab-boe-d82t80` | 524288 | `d18db8dc02ab4ff52cb17b4b3b3b90f99047c9d1acd2a5c23627197cf32f8650` | 完整 bytes 相同；輸入無缺件 |
| `nt51950-ab-hiway-d82t80` | 524288 | `4a292cd9615c58079b8994af8060af92562eaa92a55bc24bacc5ec5234e23b30` | 完整 bytes 相同；輸入無缺件 |
| `nt51950-ab-osd-d03t02-20260924` | 1048576 | `71de58a5f9a2ca2cb0d51789794af64586435b09e4ca61d176b6cd41b4990136` | 完整 bytes 相同；輸入無缺件 |

三個 direct cases 分別由既有 `Nt51950CandidateMatchesOwnerApprovedAbGoldenWithCombinerAsync`（BOE、Hiway）及 `Nt51950OsdPublicHostMatchesOwnerCertifiedGoldenAsync`（OSD）執行；兩側都執行實際 packaged Combiner。OSD 的完整 1 MiB 輸出包含超過 512 KiB layout template 的 preserved source envelope，沒有縮短比較範圍。NT51951 的 BOE／Hiway 兩筆為 fact-scoped workflow aliases，並非兩個獨立 direct Goldens；現有 alias-resolution 檢查通過。不變更任何 case manifest、BIN、expected hash、difference contract、owner approval 或 evidence rank。

#### Planned write-range audit

同一 `CompiledWritesRemainTpOnlyAfterDpInitialization` 測試，在基準 production source 與修改後 source 各編譯六張 map，逐張執行 normal／dummy DP。比對完整 initializer 及 ordered operation 的 sequence、kind、source/target address spaces/ranges、DeclaredWriteRanges、overlap、fill/patch、external invocation（含 processor allowed read/write ranges 與 staged artifact ranges）的 JSON projection；12/12 完全相同。Profile 的 operation／processor／input 定義另以結構 diff 證明未變；既有 candidate 測試檢查 scalar relocation addend。這不是僅從 family JSON 抄錄 TP 常數。

Normal 保留整張 DP seed 複製到 `output-image` 的初始化；dummy 保留原 blank initializer 並省去該 copy。初始化後，`output-image` 的寫入都位於原 TP 區段。`ab-combiner-work` 是 host staging address space，不能把整個 staged container 誤當成 processor 的允許寫入區。

Before/after audit artifact SHA-256 均為 `dd4dd547444f821b6f657105e52487b5bf582fc7e48a5fe8da634e92cbf384d3`。

| Map | A TP：output-image | B TP：output-image | Processor allowed writes：ab-combiner-work（逐一相同） | Normal / dummy operations |
| --- | --- | --- | --- | --- |
| `nt51950-ab-desay-cascade-1024k` | `[0xA000,0x37000)` | `[0x4A000,0x77000)` | `[0x4A100,0x4A104); [0x4A110,0x4A114); [0x4A130,0x4A134)` | 10 / 9 |
| `nt51950-ab-desay-single-1024k` | `[0xA000,0x37000)` | `[0x4A000,0x77000)` | `[0x4A100,0x4A104); [0x4A110,0x4A114); [0x4A130,0x4A134)` | 10 / 9 |
| `nt51950-ab-merge-1024k` | `[0xA000,0x37000)` | `[0x8A000,0xB7000)` | `[0x8A100,0x8A104); [0x8A110,0x8A114); [0x8A130,0x8A134)` | 10 / 9 |
| `nt51950-ab-merge-512k` | `[0xA000,0x37000)` | `[0x4A000,0x77000)` | `[0x4A100,0x4A104); [0x4A110,0x4A114); [0x4A130,0x4A134)` | 10 / 9 |
| `nt51951-ab-desay-1024k` | `[0xA000,0x37000)` | `[0x4A000,0x77000)` | `[0x4A100,0x4A104); [0x4A110,0x4A114); [0x4A130,0x4A134)` | 10 / 9 |
| `nt51951-ab-merge-1024k` | `[0xA000,0x37000)` | `[0x8A000,0xB7000)` | `[0x8A100,0x8A104); [0x8A110,0x8A114); [0x8A130,0x8A134)` | 10 / 9 |

TP source ranges 均為 `tp-a-input [0xA000,0x37000)` 與 `tp-b-work [0xA000,0x37000)`。B relocation 在 `tp-b-work [0xA120,0xA124)`；postbuild 的三次四-byte import 分別寫回 `output-image` 中與上表 processor allowed writes 相同的範圍。Normal DP copy 寫入 `output-image [0,capacity)`；staging copy 寫入 A/B banks 對應的 `ab-combiner-work` 範圍。這些範圍、順序與來源前後都相同。

#### Hash 與附帶 identity pins

- Family `0.7.2` → `0.7.3`；bundle `1.1.13-tp-svn.1` → `1.1.15-dp-regions.1`；五份 composition profile 只更新 family version/hash，profileVersion 與所有 operational declarations 不變。
- Family SHA-256 `059947a9ef4f536feab0be71769a3a5578e6dfac459ea876e1619d078c95ff8d`；bundle entry-array hash `74c20ce3f1d53ca343995e1b757e0785cb842fe926e0799140d6d8251f137c49` 由既有 `AbBundleSourceHashTests`／`ProfileBundleEntryArrayHasher` 重算，沒有手編 hash algorithm。
- Catalog identity 的 owner 是 `CanonicalDynamicRouteInventory`，其既有 producer 重算 3 個 AB Merge／4 個 AB CtrlRAM fingerprints；policy 與 canonical manifest 的 `routeEvidence` 只同步這七組 pins，所有其他 JSON fields 完全相同。這是現有 hash chain 的必要機械更新，沒有 support promotion。
- `scripts/sync_derived.py --write --only reviewed-source-pins` 同步 loader、package、smoke 與 Python fixture pins；再次 check 為 0 files changed。`CanonicalCatalogSnapshotDigest` 的 catalog/dynamic-routes digests 同步；所有 section lengths 與其他 sections 保持不變。
- 衍生 pin surfaces 擴充 admission：`docs/contracts/canonical-capability-policy-v1.json`、`testdata/golden/canonical/manifest.json`、既有 loader/package/smoke pins 與其相關測試。Disposition 全部為 `reuse`，無新 policy/validator 語意；因現有 path authority 分類，未來 integration 另保留 `release-owner` gate。

Open: structure-only verifier、Python pin tests 及獨立 fixed-head review 尚待收尾。任何 Golden bytes／write ranges 差異、需改 planner/executor/validator/schema 語意、reference/owner 範圍矛盾或預算用盡皆立即停止；目前未觸發。

Next: 完成尚待檢查、凍結 commit，再由 fresh-session read-only reviewer 檢查該 exact head。

### 2026-09-29 本機凍結 checkpoint

State: verified（本機 patch；本 checkpoint 隨同 commit，未整合、未發布）

Evidence:

| 檢查 | 實際結果 |
| --- | --- |
| `dotnet test tests/NvtFwCombiner.ProfileContract.Tests/NvtFwCombiner.ProfileContract.Tests.csproj` | 484 passed，0 failed/skipped |
| Bootstrap 窄測試（下列 exact filter） | 169 passed，0 failed/skipped |
| `dotnet test tests/NvtFwCombiner.GoldenRegression.Tests/NvtFwCombiner.GoldenRegression.Tests.csproj` | 15 passed，0 failed/skipped；AB direct Golden 在 Bootstrap 另有實際執行證據 |
| `dotnet test tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj --no-build --filter FullyQualifiedName~AbBundleSourceHashTests` | 2 passed；現有 hash owner 讀取修改後 source bytes |
| `dotnet test tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj --filter FullyQualifiedName~BuiltInV2BundlePinsHaveOneOwner` | 1 passed |
| `python -m pytest tests/scripts/test_release_package_policy.py tests/scripts/test_release_smoke_policy.py tests/scripts/test_sync_derived.py -q` | 第一輪 161 passed／2 failed；2 個 RID lockfile 環境失敗於下述重查通過 |
| `python -m pytest tests/scripts/test_release_package_policy.py -q -k 'distribution_launcher_committed_rid_locks_preserve_package_identities or distribution_launcher_rid_restore_keeps_invocation_locks_immutable'` | 2 passed，87 deselected；合計 163 個 distinct cases 已通過，非第二次全檔重跑 |
| `python scripts/verify.py --structure-only` | PASS；structure lane 與 Polytail fast checks 通過 |
| `python scripts/sync_derived.py` | PASS；0 files changed |
| `git diff --check` | PASS |
| JSON semantic diff／隱私與所有權檢查 | 五份 profile 除 family version/hash 外完全相同；policy/Golden inventory 除七組 fingerprints 外完全相同；無 BIN、schema、planner、executor、validator 語意或 commander board 修改 |

所有 tests/verifier shell 都先讀取 user-level `NFC_TEST_AREA_ROOT`，驗證既有 `temp`，並明確設定 `TEMP`、`TMP`、`TMPDIR`。沒有執行 `--all`。

Bootstrap command 的 exact filter：

```text
dotnet test tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj --no-restore --filter 'FullyQualifiedName~Nt51950AbDpRegionTests|FullyQualifiedName~AbMergeGoldenRegressionTests|FullyQualifiedName~Nt51950AbMergeCandidateProfileTests|FullyQualifiedName~Nt51951AbMergeCandidateProfileTests|FullyQualifiedName~AbMergeFormatVariantProfileTests|FullyQualifiedName~AbMergeAuthoringDefinitionTests|FullyQualifiedName~AbDummyDpCompilationTests|FullyQualifiedName~CanonicalSourceProjectionBuiltInBundleTests|FullyQualifiedName~CanonicalCatalogSnapshotDigestTests|FullyQualifiedName~CanonicalCapabilityCatalogMigrationTests'
```

各 .NET command 另使用 TRX logger 與固定 test-area evidence directory；實際紀錄以以下 SHA-256 封存。Baseline 與 final production source 分別是指定 base 與本 checkpoint 所在 commit；baseline 新增 characterization tests 沒有改 production source。

環境失敗分類：本機無 RID 的 restore 暫時移除了 14 份 lockfile 的 `net10.0/win-x64` entries；第一個 Python check 因此缺少該 key，第二個 check 的測試期間又觀察到 writer 將 lockfiles 恢復。確認非 RID dependency graph 不變後，只恢復本 runtime 產生的 lockfile edits；環境固定後，兩個原失敗 cases 單獨通過。沒有改 lockfiles、放寬 assertion、略過失敗或重跑完整 suite。

Scoped Polytail / requirement trace：decision 192 → family graph／existing compiler → 六張 DP coverage、CMI parent 與 12 組 compiled write audits；同一批 expected bytes → 三個直接 Golden baseline/final executions；hash closure → 既有 hasher、dynamic inventory、derived sync 與 pin tests。無新增 runtime semantic path、fallback、support declaration 或額外 infrastructure。主要 writer 的 local review 為 `PASS-WITH-HUMAN-GATE`；此紀錄不取代 commit 後的 fresh-session independent review。

Open:

- 本機 firmware 實作與指定驗證無剩餘失敗；未觸發任一停止條件，3 小時預算未用盡。
- 沒有 NT51951 standalone direct AB Golden；兩筆 workflow aliases 維持原證據範圍。這不是本次缺少輸入，也不宣告新的 certified case。
- 本機 commit 後的 independent fixed-head review 記錄於 owner 指定的詳細報告，綁定 exact commit SHA；在其完成前不宣稱 R3 review closure。
- 未來任何 integration／push 仍需 exact-head review 與 owner last-push approval，依目前 path policy 命名 `firmware-owner`、`release-owner`。本任務沒有 push／PR／GitHub write 權限，也不宣稱 integration-ready 或 release-ready。

Golden 覆蓋範圍（2026-09-29 審查 P2 補正）：
- 三個 owner 認證的 direct Golden（BOE、Hiway、OSD）全部走 `nt51950-ab-merge-512k`。
- 其餘五張 map 沒有 direct Golden：Desay single、Desay cascade 與 951 Desay 1024k、950 merge 1024k、951 merge 1024k。
- 這五張 map 的證據是：12/12 compiled-plan 前後相同（6 maps × normal／dummy）、Python reference 與 dummy 測試。
- 可見變化：三張 Desay map 的 container tail 在 AB merge 的 Memory Layout 由 Unmapped（Neutral）改為 DP；dummy 模式的 disposition 由 Resolved 改為 Blank，畫面呈現相同。

Next: 完成本機 commit，交由 fresh-session read-only reviewer 檢查該 exact head，將 review 與 commit SHA 寫入指定詳細報告後交回 commander；不繼續其他 backlog。

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
