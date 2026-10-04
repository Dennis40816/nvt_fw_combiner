# C04-1：capability catalog Step0 評估

## Dispatch envelope

- **Outcome**: Step 0 evaluation of the capability catalog load path (`BuiltInCanonicalCapabilityPolicy.Load` to `PinnedJsonCatalogLoader.LoadExact`): which cost is avoidable, which trust work must stay, with their uncertainty (decision 296, ADR 0077).
- **Non-goals**: No change to the loader, the JSON, the hash pin, the pack format, the trust authority, an ADR or production code; no C04-2; a cost estimate is not an owner go and is not borrowed from the profile pack.
- **Authority**: the worker may edit this file and record evidence outside Git, and make a local commit on the task branch; push, pull request and integration belong to the commander. Risk class: R0 (documentation under `docs/handoff/**`; no production, contract or test change).
- **Branch and worktree**: local task branch `feature/queue/c04-1-capability-catalog-step0` (never published, so its commits have no public SHA), base `b5d996c5f` on the `1.2.x` trunk; a local task worktree whose path is not kept here.
- **Write lock**: `docs/handoff/1.2.x/C04-1-capability-catalog-step0.md`; measurement evidence stays under the test-area evidence folder, not in Git. Everything else is read-only for the worker.
- **Read first**: Inventory row C04-1, decision 296, ADR 0077. Owner-search disposition: `reuse` (the existing loaders are read, not changed).
- **Model reason**: Codex implementation worker run with its configured default model and effort of that day (not recorded per run); a bounded R0 documentation or measurement task.
- **Acceptance**: the brief's document-shape check on this file and `python scripts/verify.py --structure-only`, both exit 0 outside the worker's sandbox.
- **Stop and ask**: any need to edit outside the write lock, to change production code, a contract, an ADR or a test, or a result that would change an owner decision: stop and record it under Open.

日期：2026-10-04。性質：R0 現況盤點與原語成本量測，僅交付 C04-1 的 capability 分件。
來源：`b5d996c5f444e0c8ed5a4a82d84753f3ca03c11c`，分支
`feature/queue/c04-1-capability-catalog-step0`；量測期間 production source 未修改。

**結論：目前可證明並計入的淨可避免成本為 0 ms。** 同一 payload 的第二次
SHA-256 是可研究的重算成本，兩輪每次成本中位數為 0.770586／0.575847 ms；
這是原語成本候選值，並非已交付的節省。完整 loader、generated DTO 解碼、semantic
validation、替代載入路徑及其 acceptance 成本未量測，不能把未知成本當作零，
也不能據此宣告 startup gain、owner go 或開始 C04-2。

## 依據與範圍

- [inventory C04-1／C04-2](../1.1.14/1.2.x-inventory.md)：capability 與 postbuild
  各自提出 avoidable 成本、現行 JSON loader authority 與等價要求；不得從 profile pack 推定效益。
- [決策 296](../1.2.x.md)：C02–C06 現在立項，版本與第一步由 commander 提案、owner 核准；
  該決策沒有核准 catalog 的新格式、trust policy 或實作。
- [ADR 0077 §10](../../adr/0077-prebuilt-profile-catalog.md)：capability policy 與
  `ctrlram-postbuild-v2` 保持各自 pinned JSON loader，納入 pack 需要 Step0 證據及新決策。
  ADR 的 19-bundle Step0、100 ms gate 及既有 profile pack 收益有其限定範圍，
  本文件不將它們轉作 capability 的量測結果或准入門檻。

本文件不評估 postbuild，不關閉整體 C04-1，不改 loader、JSON、ExpectedSha256、
pack format、trust authority、ADR 或 production code；未實作任何候選最佳化。

## 現行 authority、pin 與呼叫鏈

| 層次 | 現行責任與依據 |
| --- | --- |
| Reviewed policy | [canonical-capability-policy-v1.json](../../contracts/canonical-capability-policy-v1.json) 宣告每條 exact route 的 authoring／publication／evidence；[prose](../../contracts/canonical-capability-policy-v1.md) 與 [schema](../../contracts/canonical-capability-policy-v1.schema.json) 是契約 authority。policy label 不授予 Golden、release 或執行准入。 |
| Infrastructure admission | [BuiltInCanonicalCapabilityPolicy.cs](../../../src/NvtFwCombiner.Infrastructure/Capabilities/BuiltInCanonicalCapabilityPolicy.cs) 的 `RelativePath` 與 `ExpectedSha256` 是本 loader 的 source pin；不是 profile package trust index，也不是 pack 內自報的 hash。 |
| Exact-byte decoder | [PinnedJsonCatalogLoader.cs](../../../src/NvtFwCombiner.Infrastructure/PinnedJsonCatalogLoader.cs) 的 `LoadExact` 先驗 exact hash，再以傳入的 generated `JsonTypeInfo` 解碼；它不決定 capability publication。 |
| Application semantic types | [CapabilityRouteIdentity](../../../src/NvtFwCombiner.Application/Capabilities/CapabilityRouteIdentity.cs) 產生 framed routeId；[PinnedCapabilityDecision](../../../src/NvtFwCombiner.Application/Capabilities/CapabilityDecisions.cs) 保留 typed value、decisionId、fingerprint 與 sourceReference 的檢查。 |
| Compiler join／publication | [CanonicalCapabilityCatalogSource](../../../src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalogSource.cs) 在 policy 之外比對 static map 與 static／dynamic compiled fingerprint；[CanonicalCapabilityCatalog](../../../src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalog.cs) 擁有 atomic publication 與 last-known-good。此處成本不計入本 loader。 |

當次 payload：149,879 bytes，`schemaVersion = 1.1`、`catalogVersion = 1.23.0`、
`issuedOn = 2026-09-24`，85 routes、255 decisions。workflow 計數：Standard Merge 14、
AB Merge 6、CtrlRAM Replace 54、General Merge 10、General Replace 1。
publication：supported 63、candidate 11、internal 10、test-only 1；evidence：
direct-golden 26、approved-alias 7、synthetic-oracle 4、contract-only 48。
這些是當次 JSON 的宣告盤點，不是新增 support／evidence 認證。

`ExpectedSha256` 與本次 exact-byte 實測 hash 同為：

```text
a3ad08440076fb6b8b840ba64a6fbe0ccadb4345530ba1db09ab0b4f0fa671d4
```

呼叫順序及成本邊界：

1. `CompositionHostServices` 注入 `BuiltInCanonicalCapabilityPolicy.Load`；
   `CanonicalCapabilityCatalogSource.Load` 每次載入呼叫它一次，沒有在此 loader 快取 snapshot。
   policy 載入先於 dependency preload；不能按 bundle worker 數把它的成本除以四。
2. `BuiltInCanonicalCapabilityPolicy.Load()`（source lines 15–21）以
   `AppContext.BaseDirectory` 加固定 `RelativePath` 做一次 `File.ReadAllBytes`。
   `LoadExact` 接受 bytes，並不負責檔案讀取。此讀取沒有該 loader 自訂的 size limit；
   不把 profile pack 的 4 MiB guard 描述為現行 capability guard。
3. `Load(bytes, ExpectedSha256)` → `PinnedJsonCatalogLoader.LoadExact`
   （policy lines 24–35；shared loader lines 35–54）→ `VerifyExactHash` →
   `ComputeSha256`：一次 `SHA256.HashData`、lowercase hex 與 ordinal comparison，成功後才 decode。
   走的是 exact-byte 路徑，沒有呼叫 `ComputeCanonicalSha256`。
   契約中的 LF-normalized pin 對應 Git clean-export 的 LF bytes，runtime 不容許 CRLF 改寫後繼續載入。
4. `JsonSerializer.Deserialize(bytes, typeInfo)` 用
   `CanonicalCapabilityPolicyJsonContext` 的 generated metadata（Metadata mode）：
   camelCase、case-sensitive、unknown members disallowed；JSON／null document failure 拒絕載入。
   這不是 runtime JSON Schema meta-validation、schema build 或 schema evaluation；
   normative schema 的檢查仍由 repository verifier 負責。
5. policy lines 36–60 檢查 root identity／version、有效 ISO date、非空 routes；
   `CreateRoute` 限制五種 active workflows，產生並比對 routeId；`CreateDecision` 對三個
   decision 各比對 routeId／fingerprint、解析封閉值並呼叫 Application constructor。
   85 次 route 建立、255 次 decision 建立、510 次 decision pin 字串比較，另有 token、
   lowercase SHA-256、nonblank、`Enum.IsDefined` 檢查及全體 routeId 唯一性檢查。
6. 成功後 policy line 59 再次 `ComputeSha256(bytes)`，放入 snapshot `SourceSha256`，
   routes 保留原 JSON 順序並以 `Array.AsReadOnly` 暴露。每次成功載入共 hash 299,758 bytes；
   第二次 hash 屬 provenance 建立成本，不能藏在 semantic validation 欄或漏算。

## 成本證據與不確定性

在本 worktree sandbox 的 PowerShell 7.6.6／.NET 10.0.12、SDK 10.0.303、
Windows 10.0.26300.0、20 logical processors 下，用 `Add-Type` 建立僅存於 process
記憶體的原語 probe；未編譯替代 production loader、未改 source、未建立 benchmark 專案。
`Stopwatch` frequency 為 10,000,000 ticks/s。probe 原語為：

```csharp
// bytes 為本次讀取的 exact payload，expected 為上述 ExpectedSha256。
// read: File.ReadAllBytes(path)
// exactHash: Hash(bytes) 後 StringComparer.Ordinal.Equals(actual, expected)
// sourceHash: Hash(bytes)
// jsonDomProxy: using var d = JsonDocument.Parse(bytes);
//               _ = d.RootElement.GetProperty("routes").GetArrayLength();
static string Hash(byte[] bytes)
{
    Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
    _ = SHA256.HashData(bytes, hash);
    return Convert.ToHexStringLower(hash);
}
// 每 batch：取 Stopwatch.GetTimestamp()，在 C# loop 執行 500 次指定原語，
// Stopwatch.GetElapsedTime(start).TotalMilliseconds / 500 得每次平均 ms。
```

每階段先 warmup 100 次，再跑兩輪；每輪每階段 11 batches，每 batch 500 次。
第一輪順序 read → exactHash → sourceHash → jsonDomProxy，第二輪反序。
共 88 scored batches／44,000 scored operations，另有 400 warmup operations。
以下 range 是 11 個 batch「每次平均值」的 min–max，並非單次延遲 percentile 或信賴區間。

| 階段 | 第一輪 median [min, max] ms／次 | 第二輪 median [min, max] ms／次 | 證據可支持的範圍 |
| --- | --- | --- | --- |
| Read | 0.082836 [0.077736, 0.125479] | 0.093783 [0.089258, 0.107262] | 相同 worktree file 的 warm-cache 讀取原語；不是 deployed package 的 cold I/O。 |
| Exact hash＋comparison | 0.638876 [0.449985, 2.103576] | 0.459464 [0.370840, 0.639820] | 與 `VerifyExactHash` 相同算法及 bytes／pin；必須保留的 trust work。 |
| JSON decoding | 未測 generated DTO decoder | 未測 generated DTO decoder | DOM proxy 分別為 0.290175 [0.205451, 1.491969] 與 0.260123 [0.230964, 0.370712]；不是 `LoadExact`，不具有與 generated decoding 的上下界關係。 |
| Semantic validation＋typed projection | 未量測 | 未量測 | 上述 85 routes／255 decisions 的 source-backed 工作量；不以 DOM parse、測試時間或其他 catalog 推估毫秒。 |
| SourceSha256 再計算 | 0.770586 [0.598675, 1.361862] | 0.575847 [0.392637, 1.436611] | 同 payload 的第二次 hash 原語候選；未量測候選實作的端到端節省。 |

令 `R` 為 read、`H1` 為 exact hash、`D` 為 generated decode、`S` 為 semantic
validation／projection、`H2` 為 SourceSha256 重算：目前單次成功載入成本為
`R + H1 + D + S + H2` 加呼叫／首次初始化成本。`D`、`S` 未測，不能把表格中位數
相加後稱為完整 loader time。JSON DOM proxy 只證明此 payload 的另一種 parse 工作量；
不計入 avoidableCostMs。

**可避免成本處置：** 原 JSON 的 read 原語約 0.083–0.094 ms 中位數，但任何替代
路徑仍有讀取與 acceptance，不能全數計為節省。載入 exact reviewed bytes 仍須 hash；
若只把 raw JSON 搬入 container，generated decoding 與 semantic validation 仍須執行。
不能直接免除這些檢查或持久化另一套 capability semantic model。
將已驗證 bytes 的 digest 重用於 `SourceSha256` 可列為條件式候選，gross 成本估計
0.576–0.771 ms／load（兩輪中位數），觀測 batch range 0.393–1.437 ms；
它不是納入 pack 才能取得的效益，本次也未實作或核准。
替代路徑及其 acceptance 未存在／未測，因此 **已證明的淨改善仍記 0 ms**，
不是聲稱現行所有工作都不可最佳化，也不是聲稱完整 loader 為 0 ms。

量測不是宣告過的 quiet window，未排除背景排程、CPU／安全軟體、tiered JIT 或 GC；
第二次 hash 的兩輪中位數相差 0.195 ms，輪內 spread 為 0.763／1.044 ms，
不小於候選節省。首次 context 初始化、assembly loading、cold disk／deployed path、
完整 source join／compilation、reload 與新 container admission 都未涵蓋。
觀測 range 不是未知 cold-run 上界；本結果不投影 first-window 或 all-loading-done 時間。

## 需保留的 trust work 與等價要求

1. Reviewed JSON、固定 path 與 code-owned `ExpectedSha256` 維持現行 authority；
   exact source bytes hash 必須在 deserialize 之前驗證。替代資料自行攜帶的 checksum
   不能取代此 pin，亦不能把 profile trust index 當作 capability policy 的新 authority。
2. 保留 generated DTO decoding 的現行設定、root identity／version／date／nonempty 檢查、
   active workflow vocabulary、routeId 推導及唯一性、三個獨立 decision 的 bindings、
   enum／fingerprint／decisionId／sourceReference 檢查；不因 parse 成功或 hash 相符提升 support。
3. 同一 admitted payload 產生相同 `CatalogId`、`CatalogVersion`、exact `SourceSha256`、
   route 原順序與 readonly collection；逐欄比對 identity axes、routeId、fingerprint、
   authoring／publication／evidence 的 decisionId、value、routeId、fingerprint、sourceReference。
   `issuedOn` 雖未出現在 snapshot，原有驗證仍須保留。
4. 使用現行 semantic owners；對 compiled static map／static 與 dynamic fingerprints 的
   Application join、atomic publication、reload failure 的 last-known-good／cold-start
   無有效 catalog 時阻擋 Build、cancellation 保留現行結果。loader 不得代替 compiler 證明可執行性。
5. failure 等價至少涵蓋 missing／unreadable source、byte／line-ending mutation 的 hash
   mismatch、invalid JSON／null／unknown members、root mismatch／invalid date／empty routes、
   duplicate routeId／route drift、missing 或 mismatched decision pin、closed-value violation、
   self-consistent retired／unknown workflow。保留現行拒絕路徑、diagnostic 與 Application
   `capability.catalog.source-unavailable`／`capability.catalog.source-invalid` 分類；
   不新增 silent fallback。原始 IO failure 與 `InvalidDataException` 在現行分類上的觀測行為
   也要比較，不只測 happy path。
6. Repository schema 驗證、release package 的固定 policy path／pin 與 Golden routeEvidence
   cross-link 仍是各自的 gate；本次 schema 未執行、Golden 未執行，不宣稱 parity。
   本文件列的是 C04-2 若獲另行授權時須證明的等價要求，不制定新 pack 格式或 trust policy。

## 本地驗證與交付邊界

- `dotnet build tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj -c Release --no-restore -v q -nologo -p:UseSharedCompilation=false -nodeReuse:false`：
  失敗，0 warnings／1 error，NETSDK1004（此 checkout 缺 `obj/project.assets.json`）。
  未 restore，未繞過 missing assets。
- `dotnet test tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj -c Release --no-build -nologo --filter "FullyQualifiedName~BuiltInCanonicalCapabilityPolicyTests"`：
  失敗，test source DLL 不存在；執行 0 tests，不能宣稱該 class 通過。
  已檢視該 class 的 inventory、deployment、exact LF pin、line-ending、unknown-member、
  identity／decision／workflow negative cases；這是 source evidence，不是測試執行結果。
- 所有 shell commands 設定 `TEMP`／`TMP`／`TMPDIR = <test-area>\temp`，
  telemetry opt-out、MSBuild no-reuse／no-server；先查 user-level `NFC_TEST_AREA_ROOT`，
  sandbox 中未取得值，依本任務給定的 process root `<test-area>` 執行，未修改 user 設定。
- `verify.py --structure-only` 所需的 verifier session 寫入在 root 的 `sessions` 下，
  超出本 sandbox 可寫範圍，保留由 commander 在外部執行；不改 root 或 verifier 規避限制。
  未執行此 gate，也未宣稱通過。
- 指定 `python -c` JSON acceptance：通過，exit 0，六個必要摘要欄位及五個必要
  identifiers 均滿足。`git diff --check`：exit 0；針對尚未 staged 新文件的
  `git diff --no-index --check -- NUL docs/handoff/1.2.x/C04-1-capability-catalog-step0.md`：
  exit 1（新檔與空檔有 diff），無 whitespace findings；另以逐行 trailing-whitespace
  檢查確認 0 findings。`packages.lock.json` 無變更。
  限定檢視的驗證 verdict：`FAIL`（build／class test 缺 assets／DLL，不能給全數通過）；
  文件與原語盤點已完成，外部結構驗收及可執行 test evidence 仍待 commander。
- 未另存報告或 raw evidence 檔：可寫的外部位置只有 root 的 `temp`，不包含允許的
  `evidence/c04-1-capability-*`；量測摘要及原始 batch readings 直接保留於本文件。
  不把此 local assessment 稱為 integration／release-ready。

## JSON 摘要與原始 batch readings

`avoidableCostMs` 表示已證明、可計入的淨改善；條件式重算候選及未測欄位另列。
`samplesMs` 每個數字是 500 次原語的平均 ms，保留兩輪全部 88 batches。

```json
{
  "catalog": "canonical-capability-policy",
  "loader": "BuiltInCanonicalCapabilityPolicy.Load",
  "costBasis": {
    "sourceCommit": "b5d996c5f444e0c8ed5a4a82d84753f3ca03c11c",
    "method": "In-memory C# primitive probe via PowerShell Add-Type; no full loader or candidate timing; two reverse-order passes, 100 warmups per stage, 11 batches per pass and stage, 500 operations per batch; warm-cache file reads.",
    "runtime": ".NET 10.0.12 / PowerShell 7.6.6 / SDK 10.0.303 / Windows 10.0.26300.0",
    "payloadBytes": 149879,
    "expectedSha256": "a3ad08440076fb6b8b840ba64a6fbe0ccadb4345530ba1db09ab0b4f0fa671d4",
    "catalogVersion": "1.23.0",
    "routes": 85,
    "decisions": 255,
    "scoredOperations": 44000,
    "unmeasured": ["generated DTO decoding", "semantic validation and typed projection", "complete loader cold/warm time", "replacement path and acceptance cost", "startup critical-path gain"],
    "uncertainty": "Non-quiet sandbox, batched warm averages; GC/JIT/scheduling and cold package behavior unisolated. DOM proxy is neither a generated-decoder measurement nor a bound. Net gain not demonstrated.",
    "samplesMs": [
      {"pass": 1, "stage": "read", "values": [0.0791052, 0.0777362, 0.0813468, 0.0897214, 0.1254790, 0.0924806, 0.0804862, 0.0828364, 0.0817952, 0.1031996, 0.0945256]},
      {"pass": 1, "stage": "exactHash", "values": [0.4499854, 0.4780156, 0.5167078, 0.6538666, 0.5965806, 0.8385256, 0.6388762, 0.6073030, 0.9234748, 0.8886050, 2.1035762]},
      {"pass": 1, "stage": "sourceHash", "values": [1.3618622, 1.0041044, 0.7087066, 0.5986754, 0.8366802, 0.7722396, 0.6814850, 0.7342250, 0.7049472, 0.7717442, 0.7705864]},
      {"pass": 1, "stage": "jsonDomProxy", "values": [1.4919686, 0.3147198, 0.2558460, 0.2470080, 0.3196198, 0.3649766, 0.3385778, 0.2901752, 0.2054512, 0.2617908, 0.2724848]},
      {"pass": 2, "stage": "jsonDomProxy", "values": [0.2349690, 0.2945368, 0.2786002, 0.2309636, 0.2537310, 0.2490412, 0.2549056, 0.2601228, 0.3707124, 0.2893910, 0.2874136]},
      {"pass": 2, "stage": "sourceHash", "values": [1.4366112, 0.7017516, 0.6778782, 0.7234108, 0.6789282, 0.5758472, 0.4814480, 0.4255412, 0.4875600, 0.3926372, 0.4275030]},
      {"pass": 2, "stage": "exactHash", "values": [0.5019748, 0.3856844, 0.4594642, 0.4647334, 0.5157814, 0.6398200, 0.5015568, 0.3708402, 0.3847014, 0.4156860, 0.4509464]},
      {"pass": 2, "stage": "read", "values": [0.1072616, 0.0928966, 0.0937830, 0.0957522, 0.1064100, 0.0936912, 0.0968454, 0.0963238, 0.0896980, 0.0892576, 0.0905000]}
    ]
  },
  "avoidableCostMs": 0,
  "avoidableCostMeaning": "Conservative credited net gain: none demonstrated; not a zero baseline or proof that optimization is impossible.",
  "grossDuplicateHashCandidateMs": {"passMedians": [0.7705864, 0.5758472], "observedBatchRange": [0.3926372, 1.4366112], "implemented": false},
  "retainedTrustWork": [
    "Reviewed JSON authority and fixed path/code-owned ExpectedSha256; exact-byte admission before decoding, without newline normalization or a new trust anchor.",
    "Generated metadata decoding and existing root, workflow, identity, uniqueness, decision binding, typed-value and provenance validation.",
    "Exact source identity and readonly ordered snapshot; Application compiler join, fail-closed diagnostics, cancellation, atomic publication and last-known-good behavior.",
    "Independent repository schema, release-package and Golden evidence gates."
  ],
  "equivalenceRequirements": [
    "Same CatalogId, CatalogVersion, exact SourceSha256, route order, identity axes, fingerprints and all three decision fields including decisionId and sourceReference.",
    "Same rejection and diagnostics for source IO failures, exact hash/line-ending mismatch, invalid JSON/unknown members, root/date/routes, identity/duplicate routes, decision bindings/values and inactive workflows.",
    "Same static/dynamic compiler fingerprint and map checks, publication, reload retention, cold-start Build blocking and cancellation; no semantic duplication or implicit support promotion.",
    "Any future catalog/pack change needs separate C04-2 authority and measured acceptance cost; profile-pack savings are not capability evidence."
  ]
}
```

## Checkpoints

### 2026-10-04 Delivered and checked
State: verified (documentation and structure checks only)
Commits: this pull request's integration commit; the worker's local task commit on `feature/queue/c04-1-capability-catalog-step0` is not published and has no stable public SHA.
Evidence: the brief's document-shape check -> exit 0; `python scripts/verify.py --structure-only` -> exit 0, both run on the worker's local task commit outside its sandbox; they apply to the document text that this pull request integrates. The only later change to this file is the envelope and checkpoint themselves. The head of this pull request is checked by its own CI.
Open: The net avoidable cost that can be proven is 0 ms; the full loader, DTO decoding and semantic validation costs were not measured, so this file is not an admission for C04-2.
Next: None in this file; C04-2 needs its own owner go.
