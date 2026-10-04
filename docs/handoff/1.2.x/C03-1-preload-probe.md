# C03-1：prebuilt path 有／無 preload 初次量測

## Dispatch envelope

- **Outcome**: Compare the prebuilt-profile path with and without the host preload callback on one frozen source (decision 296, ADR 0077 Measurement plan), as the basis for the B5 decision. A first result with no discernible gain is an acceptable result.
- **Non-goals**: No change to the working source, the preload policy, an ADR, package or signing paths or a firmware-owner path; the temporary patch exists only in an isolated measurement copy and is not committed; no retirement of preload; no C03-2.
- **Authority**: the worker may edit this file and record evidence outside Git, and make a local commit on the task branch; push, pull request and integration belong to the commander. Risk class: R0 (documentation under `docs/handoff/**`; no production, contract or test change).
- **Branch and worktree**: local task branch `feature/queue/c03-1-preload-probe` (never published, so its commits have no public SHA), base `b5d996c5f` on the `1.2.x` trunk; a local task worktree whose path is not kept here.
- **Write lock**: `docs/handoff/1.2.x/C03-1-preload-probe.md`; measurement copy, build and raw outputs live under the test-area evidence folder and are not committed. Everything else is read-only for the worker.
- **Read first**: Inventory row C03-1, decision 296, ADR 0077 (Measurement plan, B5). Owner-search disposition: `reuse` (the existing startup measurement script and prebuilt path are read, not changed).
- **Model reason**: Codex implementation worker run with its configured default model and effort of that day (not recorded per run); a bounded R0 documentation or measurement task.
- **Acceptance**: the brief's document-shape check on this file and `python scripts/verify.py --structure-only`, both exit 0 outside the worker's sandbox.
- **Stop and ask**: any need to edit outside the write lock, to change production code, a contract, an ADR or a test, or a result that would change an owner decision: stop and record it under Open.

日期：2026-10-04。狀態：R0 本機量測與證據摘要；僅提供 B5 判斷依據。
依據：[inventory C03-1](../1.1.14/1.2.x-inventory.md)、[決策 296](../1.2.x.md)、
[ADR 0077 Measurement plan／B5](../../adr/0077-prebuilt-profile-catalog.md#measurement-plan)。

本次兩個 pass 的 catalog gain 為 **117.028 ms、29.801 ms**，分別低於
same-session spread **1,708.599 ms、157.085 ms**，兩 variant 的 ranges 均重疊。
因此首次量測**沒有可辨識的 preload 效益**；這不是效益不存在的證明，也不是退役決定。
ADR 0077 的 B5 owner decision 與獨立 R2 batch 仍未執行；C03-2 未開始。

## Frozen source 與隔離範圍

- Source：`b5d996c5f444e0c8ed5a4a82d84753f3ca03c11c`。
  工作 branch：`feature/queue/c03-1-preload-probe`；起始工作樹乾淨。
- `git archive` 同一 commit，分別解壓至 measurement copy `p`（preload）、
  `n`（no-preload）；沒有建立另一個 Git worktree。兩份副本沿用工作 checkout
  已有的 `src/*/obj`、`eng/*/obj`，以便 `--no-restore` build。
- 實際證據根目錄（以下 `$probeRoot`）：
  `<test-area>\temp\c03-1-b5d996c5`。
  Sandbox 可寫範圍只有 test area 的 `temp`；未嘗試寫入其 `evidence` child。
  指揮官待在 sandbox 外保存至
  `NFC_TEST_AREA_ROOT/evidence/c03-1-b5d996c5`，保留原始證據與路徑對應。
  本文所有原始路徑指向已存在的 `temp` 證據，未聲稱已移存。
- SDK `10.0.303`、PowerShell `7.6.6`、Windows 11 build `26300`、AMD64。
  Build 為 Release `net10.0` framework-dependent apphost；兩份均非
  package-equivalent publish。這是同 source 的 preload probe，未執行 B4 的
  v1.1.12／candidate／JSON package 比較、GC counters 或 release gates。
- 兩份生成的 `prebuilt-profile-catalog.pack` SHA-256 相同：
  `51e7d2e9b6f22681d09a815c7bb2c366e982f17659219a3b2aef29b85871f499`。
  Archive SHA-256：
  `d057d31f4902e6bf95babc5cc257ef3109b252d5b3875a6862ec78f6d25d73f7`。

Temporary patch 只存在量測副本。兩份 `Program.cs` 均使用同一隔離 local-state
目錄，避免預設使用者資料寫入；差異 patch 僅在 `n` 停用既有 host callback：

```diff
--- frozen/src/NvtFwCombiner.Desktop/Program.cs
+++ p,n/src/NvtFwCombiner.Desktop/Program.cs
@@
-            string localStateDirectory = CompositionHostServices.ResolveCurrentUserLocalStateDirectory();
+            string localStateDirectory = @"<test-area>\temp\c03-1-b5d996c5\state\NvtFwCombiner";
--- p/src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs
+++ n/src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs
@@
-            BuiltInV2BundlePreload.Run);
+            null);
```

`CanonicalCapabilityCatalogSource` 已有 nullable `preloadDependencies` seam；
其後的 serial materialization 與終端判斷仍走原路徑。沒有移除 pack、修改
preload policy、layer table、ADR、package/signing 或 firmware-owner path。
Build 後逐一比較 archive 內所有 tracked files：`p` 只改 `Program.cs`；`n`
只改 `Program.cs` 與 `CompositionHostServices.cs`；`packages.lock.json` 均未改。

## 命令與量測方法

每個 PowerShell command 均先設定：

```powershell
$env:TEMP = $env:TMP = $env:TMPDIR = '<test-area>\temp'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$declaredTestArea = [Environment]::GetEnvironmentVariable('NFC_TEST_AREA_ROOT', 'User')
if ($declaredTestArea) { $env:NFC_TEST_AREA_ROOT = $declaredTestArea }
```

Sandbox 讀取 user-level 值為空；既有 process declaration 為
`<test-area>`，沿用該固定 root，未改使用者環境或另設 root。
Source 準備與 build 命令（`$probeRoot` 取上述實際路徑）：

```powershell
git archive --format=zip --output "$probeRoot\source.zip" b5d996c5f444e0c8ed5a4a82d84753f3ca03c11c
Expand-Archive -LiteralPath "$probeRoot\source.zip" -DestinationPath "$probeRoot\p"
Expand-Archive -LiteralPath "$probeRoot\source.zip" -DestinationPath "$probeRoot\n"
# 各副本複製已有 project.assets.json 的 src/eng project obj，再套用上述 temporary patch。
# 分別在 $probeRoot\p、$probeRoot\n 執行：
dotnet build src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj -c Release --no-restore -v q -nologo -p:UseSharedCompilation=false -nodeReuse:false
# 在工作 checkout 執行既有 runner 契約測試；無 bytecode 輸出：
python -B -m unittest discover -s tests/scripts -p test_measure_startup.py -v
# Builds 與 tests 結束後，在同一連續量測 session 執行：
& "$probeRoot\run-probe.ps1"
```

`run-probe.ps1` 是 test-area 的一次性執行紀錄；呼叫同 source、未修改的
`p/scripts/measure-startup.ps1`。使用既有 `LOCALAPPDATA` 環境變數將
version-manager state 指向 `$probeRoot/state`，與 common patch 共用 state。
既有 `NFC_UPDATE_SOURCE_REGISTRY_PATH` 指向不存在的本機
`$probeRoot/unconfigured-registry.json`，避免存取真實 registry；無網路操作。
另設 `DOTNET_CLI_TELEMETRY_OPTOUT=1`。實際 invocation 為：

```powershell
& $measurementScript -ApplicationPath $apps[$variant] -OutputPath (Join-Path $probeRoot "$stem.json") -Page home -WarmupRuns 1 -Runs 5 -TimeoutSeconds 30 -RequirePreloadLifecycle -RequireAdmissionSource prebuilt
```

`$apps` 分別是 `p`、`n` 下
`src/NvtFwCombiner.Desktop/bin/Release/net10.0/NvtFwCombiner.Desktop.exe`；
`$stem` 依序為 `pass1-preload`、`pass1-no-preload`、`pass2-no-preload`、
`pass2-preload`。每次 invocation 各 1 warm-up、5 scored launch，總共
**4 warm-up、20 scored、24 個不同 processId**；沒有重試、刪慢值或補選 samples。
Session：`2026-10-04T06:08:43Z` 至 `2026-10-04T06:10:28Z`（臺灣時間 14:08–14:10）。
本 runtime 沒有併行 build/test 負載；未量測其他 OS 負載，不能證明整台 owner
machine 完全安靜。Pass 1 波動明顯，未推定其原因；此限制保留在 B5 判斷中。

主指標為每個 scored trace 的 `main-window.opened` 至
`startup-warmup.catalog-state.applied`，即 runner 的
`catalogReadyAfterWindowMilliseconds`，包含首窗後的 catalog critical path。
以下全部單位為 ms，range 為 min–max，warm-up 不納入統計。
`gainMs = median(no-preload) - median(preload)`，正值表示 preload 較快。
每 pass 的 `sameSessionSpreadMs` 定義為兩 variant 各自 `max - min` 的較大值；
這是本次描述性波動尺度，並非統計信賴區間或新增 ADR gate。

| Pass／order | preload median [range] | no-preload median [range] | gain | same-session spread |
| --- | ---: | ---: | ---: | ---: |
| 1：preload → no-preload | 1544.867 [1104.905–2351.721] | 1661.895 [1324.383–3032.982] | 117.028 | 1708.599 |
| 2：no-preload → preload | 1140.425 [1084.036–1241.121] | 1170.226 [1136.936–1284.523] | 29.801 | 157.085 |

補充每 launch 的首窗與 startup 完成時間，以辨識整體變動。`processToTrace`
是 host launch 至完成 trace 可讀的時間，包含輪詢／trace I/O；managed-entry
至 `startup-warmup.completed` 是 trace 內部邊界，兩者不可互換。

| Pass／variant | process-to-window median [range] | process-to-trace median [range] | managed-entry-to-completed median [range] |
| --- | ---: | ---: | ---: |
| 1／preload | 1645.075 [1499.094–2869.642] | 3655.248 [2953.849–5837.389] | 3548.430 [2853.871–5708.112] |
| 1／no-preload | 1874.753 [1658.408–4414.924] | 3958.403 [3414.654–8284.115] | 3831.136 [3304.281–7997.383] |
| 2／no-preload | 1568.752 [1452.716–1628.807] | 3104.904 [2936.812–3271.505] | 3003.398 [2836.990–3188.217] |
| 2／preload | 1577.166 [1515.262–1636.561] | 3077.719 [2967.435–3232.245] | 2969.243 [2867.840–3145.255] |

## JSON 摘要

`samples` 保留每個 variant 實際 launch 順序，取上述主指標：

```json
{
  "source": "b5d996c5f444e0c8ed5a4a82d84753f3ca03c11c",
  "passes": [
    {
      "order": ["preload", "no-preload"],
      "samples": {
        "preload": [1128.226, 1104.905, 1544.867, 1756.923, 2351.721],
        "no-preload": [1324.383, 1570.432, 2554.593, 3032.982, 1661.895]
      }
    },
    {
      "order": ["no-preload", "preload"],
      "samples": {
        "no-preload": [1136.936, 1182.832, 1284.523, 1159.565, 1170.226],
        "preload": [1084.036, 1116.636, 1140.425, 1143.484, 1241.121]
      }
    }
  ],
  "gainMs": [117.028, 29.801],
  "sameSessionSpreadMs": [1708.599, 157.085]
}
```

## 原始證據與驗證

以下均位於實際 `$probeRoot`，不是工作 checkout 的額外 report files：

- `pass1-preload.json`、`pass1-no-preload.json`、`pass2-no-preload.json`、
  `pass2-preload.json` 與同名 `.log`：原始 runner 輸出，JSON 內含完整 warm-up／
  scored trace、PID、lifecycle、memory 與 timestamps。Runner 自行清除暫存
  trace files；原始 trace 仍完整內嵌在這四份 JSON。
- `source.zip`、`p/`、`n/`、`p-measurement.patch`、`n-measurement.patch`、
  `provenance.json`：frozen source、量測副本、完整 temporary patch 與環境身分。
- `build-preload.log`、`build-no-preload.log`、兩份 `.exit.txt`、
  `measure-runner-tests.log`、`run-probe.ps1`、`session.json`：build／test／命令證據。
- `summary.json`、`metrics.json`、`pack-sha256.txt`、`sha256-manifest.json`：
  由全部原始 samples 計算的摘要，以及原始輸出、archive、patch、實際執行
  apphost／Desktop／Bootstrap assemblies／pack 的 SHA-256。

兩份 Desktop build：**2/2 成功，0 warnings、0 errors**。
既有 `StartupMeasurementContractTests`：**11/11 PASS**（104.032 s）。
原始證據核對：**24/24** 有唯一 matching `prebuilt` admission marker、成功
lifecycle 與相符 trace PID；4 warm-up／20 scored 分組正確，主指標可由各 trace
重算。已核對全部 frozen tracked files 的差異與兩份 pack identity。
文件檢查：以 `python -B -` 執行 owner 提供的 JSON assertions，另逐項對照
原始 samples／摘要／spread：**PASS**；3 個受影響連結與 30 個 artifact
SHA-256：**PASS**（`document-check.log`）。`git diff --check` 通過；另以
`git diff --no-index --check` 檢查 untracked 新文件，沒有 whitespace findings
（exit 1 表示新文件與空檔的差異）。
Bootstrap／UiSmoke test projects 沒有既有 `obj/project.assets.json`，未 build、
未 restore、未執行完整 test project；本次沒有 firmware parity／Golden 宣稱。

剩餘交接：sandbox 外移存原始證據至上述 `evidence/c03-1-*`，並由指揮官複驗
owner 提供的文件 JSON acceptance command、執行
`python scripts/verify.py --structure-only`。Verifier 會在固定 root 建立 session，
超出本 sandbox 的可寫範圍，因此此處未執行、未聲稱通過。
工作 checkout 僅新增本文；沒有 commit、push、Git config 變更或整合／發布。

## Checkpoints

### 2026-10-04 Delivered and checked
State: verified (documentation and structure checks only)
Commits: this pull request's integration commit; the worker's local task commit on `feature/queue/c03-1-preload-probe` is not published and has no stable public SHA.
Evidence: the brief's document-shape check -> exit 0; `python scripts/verify.py --structure-only` -> exit 0, both run on the worker's local task commit outside its sandbox; they apply to the document text that this pull request integrates. The only later change to this file is the envelope and checkpoint themselves. The head of this pull request is checked by its own CI.
Open: The B5 owner decision and the independent R2 batch have not been executed; C03-2 has not started. No discernible gain is not proof of no gain and is not a retirement decision.
Next: The commander keeps the numbers as input for the B5 decision; no further step in this file.
