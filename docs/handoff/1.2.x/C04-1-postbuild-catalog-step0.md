# C04-1：ctrlram-postbuild-v2 catalog Step0

## Dispatch envelope

- **Outcome**: Step 0 evaluation of the `ctrlram-postbuild-v2` catalog load path (`BuiltInPostbuildProfileCatalog.Load` to `PinnedJsonCatalogLoader.Load`): avoidable cost, the current pin and authority, and the equivalence requirements (decision 296, ADR 0077).
- **Non-goals**: No change to `src/NvtFwCombiner.Infrastructure/ExternalTools/`, the catalog, pins, processor declarations or firmware; no processor execution; no pack extension; no C04-2; no extrapolation from the capability or profile pack results.
- **Authority**: the worker may edit this file and record evidence outside Git, and make a local commit on the task branch; push, pull request and integration belong to the commander. Risk class: R0 (documentation under `docs/handoff/**`; no production, contract or test change).
- **Branch and worktree**: local task branch `feature/queue/c04-1-postbuild-catalog-step0` (never published, so its commits have no public SHA), base `b5d996c5f` on the `1.2.x` trunk; a local task worktree whose path is not kept here.
- **Write lock**: `docs/handoff/1.2.x/C04-1-postbuild-catalog-step0.md`; measurement evidence stays under the test-area evidence folder, not in Git. Everything else is read-only for the worker.
- **Read first**: Inventory rows C04-1 and C04-2, decision 296, ADR 0077. Owner-search disposition: `reuse` (the existing loader and catalog are read, not changed).
- **Model reason**: Codex implementation worker run with its configured default model and effort of that day (not recorded per run); a bounded R0 documentation or measurement task.
- **Acceptance**: the brief's document-shape check on this file and `python scripts/verify.py --structure-only`, both exit 0 outside the worker's sandbox.
- **Stop and ask**: any need to edit outside the write lock, to change production code, a contract, an ADR or a test, or a result that would change an owner decision: stop and record it under Open.

日期：2026-10-04。狀態：獨立 R0 評估；不是 C04-2 的准入或 pack 擴充決策。
盤點／量測 source head：`b5d996c5f444e0c8ed5a4a82d84753f3ca03c11c`，
branch：`feature/queue/c04-1-postbuild-catalog-step0`。
brief 引用的 inventory head `f6c828159e1259165d72b758fa59b4a3087757d0`
是先前盤點來源；本文件的成本及程式觀察只適用於上述實際 checkout。

依據：[inventory C04-1／C04-2](../1.1.14/1.2.x-inventory.md) 第 392–393 行、
[決策 296](../1.2.x.md) 第 979–987 行，以及
[ADR 0077](../../adr/0077-prebuilt-profile-catalog.md) 第 546–548 行。
決策 296 授權立項，沒有核定擴充格式或特定版本；ADR 明定 postbuild catalog
保持既有 pinned JSON loader，納入 pack 須先有自身 Step0 證據及新決策。
本次只評估 `ctrlram-postbuild-v2/catalog.json`；不評估同目錄的 `flash-map.json`，
不使用 capability catalog 或 profile pack 的量測推定其效益。

## 結果與可避免成本的意義

現行路徑有可盤點的 JSON decoding 工作，但尚未證明移除它可改善實際啟動 wall time。
本次的 `avoidableCostMs = 0.799278` 是**假設整段暖態 DTO decoding 可移除時的
gross 成本點估計**，每個正常程序首次成功載入最多計一次；不是新實作的淨收益，
也不是整個 loader 的成本上限。此階段含 DTO 配置；替代表示仍須建立等價物件，
不能把這些配置全數算成已省下。已證實、可計入的淨啟動收益為 **0 ms**。
未實測的 policy/profile 建立成本另列為未知，不把未知填成零。

五個程序的暖態 decoding 中位數範圍為 **0.594175–1.148705 ms**；
每 20 次呼叫的批次平均範圍為 **0.533700–4.973950 ms**。
首次 pinned DTO load 中位數為 **50.530400 ms**，範圍 **43.424500–57.987300 ms**，
含 hash、首次型別 metadata、JIT 與 decoding，不能全部歸為 JSON 文字解析，
也不能直接算成可以移到 build 的成本。尚無替代 acceptance 成本或 packaged startup
的配對區間，因此本評估不支持自動進入 C04-2；現行 loader 維持原狀。

## 現行 authority、pin 與載入路徑

| 觀察 | 現行 owner／證據 |
| --- | --- |
| 宣告 authority | [`catalog.json`](../../../profiles/built-in/ctrlram-postbuild-v2/catalog.json)：schema `2.3`，approved postbuild commands、selectors、DiffDLM policies、Common FW effective intervals、FWConfig write routes、assembly kind 及 evidence。不是 profile-bundle manifest／trust index 的成員准入結果。 |
| runtime adapter 與 pin | [`BuiltInPostbuildProfileCatalog.cs`](../../../src/NvtFwCombiner.Infrastructure/ExternalTools/BuiltInPostbuildProfileCatalog.cs) 第 8–26 行：`RelativePath = profiles/built-in/ctrlram-postbuild-v2/catalog.json`；`ExpectedSha256 = 417adb68d222dfe3bd02e9fbaf274b90f68e3fc99a01c6679d3e900a710313fc`。正式載入固定使用這個 pin；測試用 `expectedSha256` 參數不是 runtime 更新入口。 |
| 共用來源驗證／decoder | [`PinnedJsonCatalogLoader.cs`](../../../src/NvtFwCombiner.Infrastructure/PinnedJsonCatalogLoader.cs) 第 11–31、56–66、87–103 行：`Load<T>` 先 `VerifyHash`，成功才 `JsonSerializer.Deserialize<T>`；camelCase，`UnmappedMemberHandling.Disallow`，JSON 無效／空值沿現行 exception 路徑拒絕。 |
| canonical hash | `ComputeCanonicalSha256` 的 ASCII 且無 CR／FF fast path 直接 hash；其他輸入 UTF-8 decode → `ReplaceLineEndings("\n")` → UTF-8 encode → hash。因此接受 canonical LF pin 的 CRLF checkout；不是 `LoadExact<T>`，也不是 JSON semantic canonicalization。空白／欄位順序改動不會自動取得信任。 |
| typed semantic owner | [`LegacyCombinerPostbuildProfile.cs`](../../../src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildProfile.cs) 第 26–94、216–299 行及 [`LegacyCombinerDiffDlmPolicy.cs`](../../../src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerDiffDlmPolicy.cs)：既有建構／compiler 檢查及 typed plan；Infrastructure 只將宣告轉入該 owner，並驗證 catalog 層級條件。 |
| 包裝來源位置 | [`Infrastructure.csproj`](../../../src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj) 的 Content 項目：catalog 複製到 output／publish。無自訂 runtime catalog path，也無新 cache／pack owner。 |

本 head 的原始及 canonical SHA-256 都等於 `ExpectedSha256`，實際檔案為
181,905 bytes。盤點得到 2 policies、11 declared profiles，全部 11 個均為 runtime，
共 10 個 IC、161 commands、407 blocks、25 selectors。
commands／blocks 合計四種宣告 branch；不是任何一次 processor 的執行數。
這些是 pinned 宣告的規模，不據此新增 support 或 Golden parity 宣稱。

載入順序如下，各階段不混為同一項成本：

1. `All → Profiles.Value → BuiltInPostbuildProfileCatalog.Load()`：
   `Path.Combine(AppContext.BaseDirectory, RelativePath)`，`File.ReadAllBytes`。
   讀檔失敗直接失敗，沒有 fallback source。
2. `Load(bytes, ExpectedSha256) → PinnedJsonCatalogLoader.Load<CatalogDocument>`：
   先 canonical source hash 及 ordinal pin 比對，再嚴格 JSON decoding。
   source pin 不符時不進入 decoding／建構。
3. schema `2.3` 與必要頂層 collection 檢查 → `CreateDiffDlmPolicy`／ordinal dictionary →
   **所有 declared profiles** 的 `CreateProfile`／`CreateValidatedProfile`。
   逐一建立 typed commands、blocks、selectors、checked `ByteRange`，解析版本及閉合 enum／
   mode／CRC vocabulary，resolve `diffDlmPolicyId`，使用既有建構檢查。
4. profile 建構時建立 `_compiledPlans`：每個 selector 建立 `CompiledPlanTemplate`；
   有 cascade DiffDLM policy 時還依支援 count 建立多個 command shapes／protocol plans。
   [`LegacyCombinerPostbuildPlanCompiler.Resolve.cs`](../../../src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.Resolve.cs)
   的 `ResolveCommands`／`ExpandDiffDlmPolicy` 和
   [`LegacyCombinerPostbuildPlanCompiler.cs`](../../../src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanCompiler.cs)
   的 `CompileProtocol` 在此做純記憶體編譯；不是執行外部 processor。
5. duplicate processor id 檢查 → 按 availability 篩選 runtime →
   `ValidateRuntimeIntervals` → `Array.AsReadOnly` 發布。
   evidence-only row 也先建構／驗證才篩掉，不能把提前丟棄當成等價節省。

## Lazy lifecycle 與成本所處區間

`Profiles = new(Load)` 是 static `Lazy<IReadOnlyList<LegacyCombinerPostbuildProfile>>`，
使用預設 ExecutionAndPublication：首次讀取執行一次 factory，並行首次呼叫等待同一結果；
成功後快取相同 list，factory exception 也會快取。這是依程式及 .NET Lazy 契約的靜態判讀，
本次沒有跑產品並行／失敗生命週期測試。直接呼叫測試 overload `Load(bytes, pin)`
會每次重建，不能用其重複量測表示正式每次查詢都付相同成本。

正常 catalog load 可透過以下既有 consumer 觸發首次讀取：
[`CompositionHostServices.cs`](../../../src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs) 第 144–155 行
將 dynamic resolver／disclosure adapters 交給
[`CanonicalCapabilityCatalogSource.cs`](../../../src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalogSource.cs)
第 93–104 行；
[`CanonicalDynamicRouteInventory.cs`](../../../src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.cs)
第 397–407 行和
[`CanonicalCapabilityDisclosureInventory.cs`](../../../src/NvtFwCombiner.Infrastructure/Composition/CanonicalCapabilityDisclosureInventory.cs)
第 55–64 行均呼叫 `GetProfiles → All`。
它也有 inspection／authoring consumers，但同一正常程序成功初始化後不會重新讀檔、hash 或 decode。
catalog reload 建立新的 dynamic resolver Lazy，不會重設這個 static postbuild Lazy。
本次沒有 trace 證明哪個 consumer 最先觸發或該時刻的 serializer 是否已暖身。

`GetProfiles` 仍每次 filter、排序及配置結果陣列；`TrySelectProfileForCommonFwVersion`
仍解析／比較版本。這是查詢成本，不是首次 JSON admission；並行首次等待與
Lazy.Value 本身亦未獨立量測，不能再加一份 loader 成本。
postbuild 沒有一組 entry JSON schemas 的 meta-validation／evaluation，不能套用
ADR 0077 profile-bundle 的 schema 成本比例。bundle preload 表也不是這份 catalog 的專屬量測。

## 量測方法、分項成本與限制

量測用獨立、只讀 .NET microprobe，沒有重建或執行產品 assembly：
以 `Add-Type /optimize+ /nullable:enable /warnaserror+` 編譯**未改動的
`PinnedJsonCatalogLoader.cs`**，逐字擷取 `BuiltInPostbuildProfileCatalog.cs`
第 377–445 行的八個 private DTO record 宣告，放入 probe 類別。
probe 以這些相同形狀的 DTO 執行實際 `PinnedJsonCatalogLoader.Load<T>`，並取出其
`JsonOptions` 分別量 hash 與 `JsonSerializer.Deserialize<T>`。
DTO 的 enclosing type 是 probe；此結果代表該來源、DTO 形狀及 options 的 microbenchmark，
不冒充整個 `BuiltInPostbuildProfileCatalog.Load` 或 packaged binary 的量測。
沒有呼叫 `CreateDiffDlmPolicy`、`CreateProfile`、plan compiler 或 processor。

環境：Windows `10.0.26300` x64、PowerShell `7.6.6`、`.NET 10.0.12`。
在同一 session 依序啟動 5 個新的 `pwsh -NoProfile` 程序；每程序先記錄首次 read、
首次 hash／loader type init、首次 pinned DTO load，再各暖身 100 次。
每階段量 50 批、每批 20 次，交替正反階段順序；合計每階段 5,000 次 scored calls。
以各程序的 50 個批次平均先取中位數，再以五個程序中位數取中位數與範圍。
下表的範圍是程序中位數範圍，不是信賴區間。
沒有清除 OS file cache、保證 quiet machine、publish、ReadyToRun、產品 startup 或 A/B candidate。
`Add-Type` 編譯時間不在量測中；首次數字仍受 PowerShell 已載入的 runtime 與 JIT／metadata 狀態影響。

| 階段 | 暖態 ms：中位數 [程序中位數範圍] | 每次 managed allocation 中位數 | 可避免性／保留工作 |
| --- | --- | --- | --- |
| cached `File.ReadAllBytes` | 0.137918 [0.107710, 0.178590] | 182,019.2 bytes | source 讀取成本；現行 authority 下保留，不能因另一份 pack 已准入就刪除。 |
| canonical hash | 0.566098 [0.441982, 0.787413] | 152 bytes | 信任驗證必須保留。本次來源命中 ASCII LF fast path；不代表 CRLF／Unicode normalization 同成本。 |
| DTO decoding | 0.799278 [0.594175, 1.148705] | 274,552 bytes | 可避免候選 gross envelope；假設整段移除才取此估計，替代 decoding／rehydration 及 acceptance 成本未扣。 |
| pinned DTO load | 1.314188 [0.990203, 1.973310] | 274,704 bytes | 含 hash＋DTO；不是正式完整 loader，不能把此數與上述兩項重複相加。 |
| policy/profile／plan 建立、catalog checks | 未量測 | 未量測 | 2 policies、11 profiles、25 selector templates，另有 count-dependent shapes。沿原 constructor／compiler 載入資料仍須做；搬移或省略檢查需新 authority 決策及等價證據。 |
| Lazy lifecycle／首次 contention、後續 query | 未量測 | 未量測 | 一次初始化與等待／快取不會因換容器自動消失；後續 query 不是 decoding 成本。 |

首次 read：0.226800 ms [0.175500, 6.851800]；首次 hash＋loader type init：
0.751100 ms [0.665200, 1.281100]；首次 pinned DTO load：
50.530400 ms [43.424500, 57.987300]。這三段依序觀測，不是冷磁碟或產品啟動區間。
首次 pinned DTO load 與暖態 DTO decoding 的差不能視為可避免 metadata／JIT 的精確歸因。

現行完整一次成本可表為 `read + trust + decode + model/compile/checks + lazy overhead`。
若保持 source authority，read／trust 留在現行路徑；理想移除 decoding 的 gross 暖態估計為
0.799278 ms，實際淨差還要扣替代表示的 decode／acceptance／物件建立成本，甚至可能為負。
model/compile/checks 的額外可避免部分尚無數值依據；總成本及啟動淨差目前不能給有限數值上限。
啟動收益另須驗證所在 critical path 及共享 metadata 暖身，不能把各方法時間直接相加當 wall time。
ADR 0077 的 100 ms gate 屬原 19-bundle B1 配對實驗；本次既沒有證明達到它，
也沒有擅自把它指定為 postbuild 的新 gate。後續若要做 C04-2，由 owner 另定範圍及准入。

證據暫存於 sandbox 核准可寫的 `<test-area>\temp`：
`c04-1-postbuild-probe.ps1`、`c04-1-postbuild-sample-1.json` 至 `-5.json`、
`c04-1-postbuild-summary.json`；probe SHA-256：
`61ed19ab9bcb66733dd6f7bd35125468cb72cf1c895ad3cfd8da8d9efab8104a`。
摘要及原始樣本保留每批時間、allocation、source head 與兩個 loader source hashes。
正式保存目的地仍為 `NFC_TEST_AREA_ROOT/evidence/c04-1-postbuild-*`；該 evidence 目錄
不在 sandbox 可寫範圍，本次未寫入，由 commander 保存上述量測／盤點證據。
worktree 只新增本文件，沒有生成測試 report。

## 等價要求及 retained trust work

以下列出現行可觀察契約的保留要求，不批准改寫 trust／firmware authority：

1. 保留 canonical source SHA-256 與固定 `ExpectedSha256` 綁定、驗證先於 decode，
   LF／CRLF 及完整 normalization 契約；missing／read failure／pin mismatch／invalid JSON／
   empty／unknown fields 沿現有 refusal，不引入 fallback、忽略損壞來源或自訂執行路徑。
2. 保留 schema `2.3`、necessary collections、所有 declared row 驗證、ordinal identity／
   duplicate 拒絕、availability 篩選順序及 runtime intervals；不得只比 profile 數或 final hash。
   現行 NT51926 分界為 `[1.0.0, 2.0.0)`／`[2.0.0, infinity)`；多 profile 缺有效
   Common FW、低於最低版本及單 profile 的選取行為／issue 需相同。
3. 完整比較每筆 policy/profile 與所有 branch 的有序 command／block／selector graph：
   IDs、tool binding、filename、evidence／display category、assembly kind、effective version、
   FWConfig write route、source kind／file／offset／staged identity／section、checked 半開 target range、
   mode／CRC arguments、DiffDLM full stride／preservation／count／Backup authority 及其引用。
   不以 capability 或 pack fingerprint 代替這份 catalog 的等價比較。
4. 沿既有 owner 比較各 selector／supported count 的 resolved command shape、compiled protocol
   arguments/order、required capacity、write/integrity ranges 及 fingerprints；拒絕不支援 count、
   foreign selector／plan 的行為也須相同。這是既有純編譯結果的要求，不准執行 processor，
   不新增 decoder／第二條語意路徑；firmware bytes／CRC／Header／staging 限制不因此變動。
5. 保留一次 static Lazy publication、失敗快取、並行首次等待及程序內 source 固定生命週期；
   catalog reload 不偷換 postbuild source。公開 query 的排序、selection／issue、typed plan ownership
   與現有 consumers 取得的結果要等價。載入資料仍使用現行 constructors 時，不能宣稱其 compile
   成本已經避免。

現有證據入口：
[`BuiltInPostbuildProfileCatalogTests`](../../../tests/NvtFwCombiner.Infrastructure.Tests/ExternalTools/BuiltInPostbuildProfileCatalogTests.cs)
有 16 個 Fact 方法，覆蓋 runtime rows、hash drift、CRLF、hash allocation／normalization、unknown field、
argument allowlist、availability、required version／selector／write route、selector overlap／count range、
duplicate／minimum effective intervals；
[`LegacyCombinerPostbuildCatalogTests.Version.cs`](../../../tests/NvtFwCombiner.Application.Tests/ExternalTools/LegacyCombinerPostbuildCatalogTests.Version.cs)
及同名 partial tests 保留 version、alias、command-line、DiffDLM、range／fingerprint 的既有案例。
本次僅讀取其 source，不宣稱這些產品測試 fresh pass、完整等價或 Golden byte parity。

## 本次檢查與待交接

- microprobe：5 個 fresh PowerShell 程序成功；每程序驗證 pin mismatch 拒絕、重算 pin 後
  unknown field 拒絕、CRLF 接受，共 15／15 probe checks。這三類只證明 source／DTO decoder，
  不是 profile semantic 測試。各階段 scored calls 共 5,000；沒有 processor execution。
- `dotnet build tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj -c Release --no-restore -v q -nologo -p:UseSharedCompilation=false -nodeReuse:false`：
  失敗，0 warnings／1 error，`NETSDK1004`：該 checkout 缺 `obj/project.assets.json`。
  未 restore、未繞過產品建置、未執行 class-filtered tests；產品測試執行數 0。
- user-level `NFC_TEST_AREA_ROOT` 在此 sandbox 查得空值，既有 process 值及 brief 均為
  `<test-area>`；執行時沿用該固定 root，TEMP／TMP／TMPDIR 顯式設為其 existing `temp`，
  亦設 brief 的 telemetry／MSBuild flags，沒有改寫 user 設定。
- `python scripts/verify.py --structure-only` 未執行：`verify.py` 的
  session 邏輯（第 2257、2388–2441、7370–7377 行）會在 fixed root 的 `sessions` 建立目錄，超出
  sandbox writable roots；按 brief 由 commander 執行。文件 JSON assertion／diff／affected-link
  檢查不代稱結構 verifier 已通過。
- brief 的 `python -c` JSON acceptance assertion：1／1 通過；Python 本地連結存在性檢查：
  17／17 通過。`git diff --check` 通過；另外因文件尚未 staged，以
  `git diff --no-index --check -- /dev/null docs/handoff/1.2.x/C04-1-postbuild-catalog-step0.md`
  檢查新增全文，無空白錯誤輸出（exit 1 表示檔案內容與空檔不同）。
  `git status --short --untracked-files=all` 僅列本文件；`packages.lock.json` 無變更。

scope review：現行 pin／authority 未變，本文件只記錄 R0 觀察，沒有 production diff、
pack 格式提案、processor declaration、firmware、C04-2 或其他 inventory row 工作。
以本 head 的具名 loader／DTO／constructor／consumer／測試 source 核對文件，未發現
本文件新增的語意 owner、未授權變更或需另記錄的 bug；產品測試及正式結構 gate 的限制如上。
沒有 commit／push／Git config、網路或其他 worktree 操作。
Step0 文件已交付；產品建置、structure verifier 與 evidence 正式保存留給 commander。
新 pack／版本／gate 的 owner 決定屬於 C04-2，不是本次 Step0 的未回答 blocker。

## JSON 摘要

```json
{
  "catalog": "ctrlram-postbuild-v2",
  "loader": "BuiltInPostbuildProfileCatalog.Load",
  "sourceHead": "b5d996c5f444e0c8ed5a4a82d84753f3ca03c11c",
  "costBasis": {
    "kind": "gross warm DTO-decoding cost estimate if the entire stage were removed; not measured net startup savings",
    "method": "5 fresh pwsh processes; exact PinnedJsonCatalogLoader source plus verbatim catalog DTO declarations; .NET 10.0.12; 100 warmups and 50 batches of 20 calls per stage per process; median of process medians",
    "sourceBytes": 181905,
    "policyCount": 2,
    "runtimeProfiles": 11,
    "runtimeIcCount": 10,
    "commandCount": 161,
    "blockCount": 407,
    "selectorCount": 25,
    "warmDtoDecodeProcessMedianRangeMs": [0.594175, 1.148705],
    "warmDtoDecodeBatchMeanRangeMs": [0.5337, 4.97395],
    "warmCachedReadMs": 0.137918,
    "warmCanonicalHashMs": 0.566098,
    "warmPinnedDtoLoadMs": 1.314188,
    "firstPinnedDtoLoadMs": 50.5304,
    "firstPinnedDtoLoadRangeMs": [43.4245, 57.9873],
    "policyProfileAndPlanConstructionMs": null,
    "lazyContentionAndQueryMs": null,
    "alternativeAcceptanceMs": null,
    "creditedNetStartupSavingsMs": 0,
    "uncertainty": "DTO enclosing type is the probe; no product startup or candidate A/B, no disk-cache purge or quiet-machine guarantee; first-use includes hash/JIT/metadata; unknown model and acceptance costs prevent a total or net upper bound",
    "evidenceStaging": "<test-area>/temp/c04-1-postbuild-*",
    "evidenceDestination": "NFC_TEST_AREA_ROOT/evidence/c04-1-postbuild-* (commander copies; not written here)"
  },
  "avoidableCostMs": 0.799278,
  "retainedTrustWork": [
    "Read the fixed AppContext.BaseDirectory catalog source and verify canonical SHA-256 before JSON decoding",
    "ExpectedSha256=417adb68d222dfe3bd02e9fbaf274b90f68e3fc99a01c6679d3e900a710313fc; preserve complete LF/CRLF and Unicode line-ending normalization behavior",
    "Preserve strict JSON/schema/identity/availability/runtime-interval refusal and existing typed constructor/compiler checks unless a separate authority decision approves moving them",
    "Do not infer postbuild admission or support from capability catalog, profile pack, filenames or observed firmware output"
  ],
  "equivalenceRequirements": [
    "Same pinned-source acceptance and failures, schema 2.3, all declared rows checked before runtime filtering, and Common FW interval selection",
    "Same ordered policy/profile/selector/command/block graph, IDs, filenames, evidence, versions, assembly/write routes, source and target ranges, arguments and DiffDLM/Backup bindings",
    "Same existing-owner compiled shapes/protocols for every supported selector/count, capacities, write/integrity ranges, fingerprints and unsupported/foreign-plan refusals",
    "Same static Lazy once-only publication, concurrency and failure cache, process-fixed source across catalog reload, and query/consumer results",
    "Fresh exact-source semantic tests and separately attributed product startup/acceptance evidence before any later expansion decision; no processor execution or Golden parity claim in this Step0"
  ]
}
```

## Checkpoints

### 2026-10-04 Delivered and checked
State: verified (documentation and structure checks only)
Commits: this pull request's integration commit; the worker's local task commit on `feature/queue/c04-1-postbuild-catalog-step0` is not published and has no stable public SHA.
Evidence: the brief's document-shape check -> exit 0; `python scripts/verify.py --structure-only` -> exit 0, both run on the worker's local task commit outside its sandbox; they apply to the document text that this pull request integrates. The only later change to this file is the envelope and checkpoint themselves. The head of this pull request is checked by its own CI.
Open: This is an independent R0 evaluation, not an admission for C04-2 and not a decision to extend the pack.
Next: None in this file; C04-2 needs its own owner go.
