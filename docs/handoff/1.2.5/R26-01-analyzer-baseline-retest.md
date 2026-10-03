# R26-01 Analyzer baseline 重測

日期：2026-10-03。性質：R0 唯讀盤點；只列清單，不授權刪除或 mechanical cleanup。

證據狀態：本文件是量測報告，不是可重現的證據包。原始 SARIF、log、`outputs.props` 與 `unused.globalconfig` 留在作者的 test area，沒有進 Git；首輪報告檔也未提交。因此「15/15、指定 unused 規則 0 筆」尚未由第二方重現；R26-02／R26-03 在刪除或清理前必須先用本文的命令重測。

## 結果與來源

固定 source：`8479dee8ee95dbcee9f38ecf4793e70858c9ff41`。
補測 source：同一個固定 source 加上首輪報告檔（當時的暫存提交不在 repository 中，因此不列 SHA）；與固定 source 的產品檔案沒有差異。原始 SARIF 與 log 留在 test area，不入 Git；文末列出各檔 SHA-256。
分支：`feature/1.2.5/analyzer-baseline-retest`；兩輪量測前與建置後工作樹皆乾淨。

15 個產品專案已量測 15/15：首輪 11 個，補測 4 個，全部成功編譯並產生 SARIF。
指定 unused 規則與補充 compiler 規則各為 0 筆；style 共 45 筆，最安全候選為 0 項。
此結果完成全產品專案的指定規則重測，不代表沒有 unused code，也不是預設 warnings-as-errors gate 通過。

來源：

- [R26-01 定義](../1.1.14/1.2.x-inventory.md)：第 321 行；O18：第 548 行。
- [歷史基線](../../architecture/nfc_roadmap.md)：第 852–857 行。
- [目前版本分配](../1.1.14/1.2.x-allocation.md)：第 321 行列於 1.2.12。
- 本次 owner 指定 1.2.5 報告路徑，沒有修改既有排程或 R26-02/03 的實作範圍。

歷史基線只有規則總數；本次依來源追查未找到其逐筆清單、原始建置 log 或固定 commit。
因此只能比較數字，不能宣稱哪些歷史 finding 已解決。

## Repository analyzer 設定

根目錄只有 `Directory.Build.props` 與 `.editorconfig`；未找到 repository 或 src 的 `.globalconfig`、
巢狀 `.editorconfig`。實際載入的既存設定為根 `.editorconfig` 與 SDK recommended config。

`Directory.Build.props` 設定：

- `TargetFramework=net10.0`、`AnalysisLevel=latest-recommended`。
- `EnforceCodeStyleInBuild=true`、`TreatWarningsAsErrors=true`；沒有 unused 個別規則設定。
- `.editorconfig` 的 `[*.cs]` 使用 `dotnet_analyzer_diagnostic.severity = warning`。
- CA2000、CA1062 個別為 warning，CA2016 為 error；均不是 unused 規則。
- var 偏好與部分 qualification 偏好為 suggestion，不能把歷史 style 清單當 dead-code 清單。

| 規則 | 用途 | 原設定／有效範圍 | 本次 |
| --- | --- | --- | --- |
| IDE0005 | unnecessary using | 已啟用；bulk warning | warning |
| IDE0051 | unused private member | 已啟用；bulk warning | warning |
| IDE0052 | unread private member | 已啟用；bulk warning | warning |
| IDE0059 | unnecessary value assignment | 已啟用；bulk warning | warning |
| IDE0060 | unused parameter | 已啟用；bulk warning；repository 未指定可見性選項 | warning，all |
| CA1812 | uninstantiated internal class | recommended 未啟用；bulk severity 不會開啟 disabled 規則 | warning，明確啟用 |
| CA1823 | unused private field | recommended 未啟用；bulk severity 不會開啟 disabled 規則 | warning，明確啟用 |
| CS0162/CS0168/CS0219/CS0414/CS0649 | unreachable／unused／never-assigned compiler 診斷 | compiler warnings | 原 warning |

SDK IDE0005/51/52/59/60 descriptors 為 enabled-by-default、DefaultSeverity=Hidden，
具有 `EnforceOnBuild_HighlyRecommended`；bulk warning 與本次個別 warning 的來源不同。
原設定中的 warning 通常會被 `TreatWarningsAsErrors=true` 升成 error。
本次只在 command line 設為 false，以完整列出診斷；沒有新增 suppression 或改 repository 設定。

## 執行方法

SDK pin 為 10.0.301，允許 latestPatch；實際使用 10.0.303、C# compiler 5.6.0。
Configuration 為 Debug、TFM 為 net10.0，未指定 RID；未建置或執行 tests。

暫存資料全部位於以下目錄；首輪使用第一個，四專案補測使用第二個：

```text
<test-area>/temp/R26-01-20261003-080549
<test-area>/temp/R26-01-20261003-110113
```

下文以 `$scratch` 表示各輪對應目錄；只在該 temp 區寫入 props、globalconfig、建置輸出與量測 evidence。
沒有複製或修改產品 source；逐專案複製既有輸出的 restore metadata：

- `project.assets.json`、`*.nuget.g.props`、`*.nuget.g.targets`、`project.nuget.cache`。
- 不複製 compiled DLL／incremental compile caches，使本次 compiler 與 analyzer 實際執行。
- 不執行 Clean/Rebuild，以免舊 FileListAbsolute 指向原工作樹。
- 首輪缺少 assets 的專案保留缺少狀態；補測使用這四個專案各自還原後的 metadata，未合成 assets。
- 補測相依 graph 沿用首輪 restore metadata；只替換 temp 路徑，`unused.globalconfig` 完全相同。

`outputs.props` 經 `-p:DirectoryBuildPropsPath=` 載入，再 import 原 `Directory.Build.props`。
它依 `$(MSBuildProjectName)` 將下列輸出隔離：

```text
BaseIntermediateOutputPath = $scratch/obj/$(MSBuildProjectName)/
MSBuildProjectExtensionsPath = $(BaseIntermediateOutputPath)
BaseOutputPath = $scratch/bin/$(MSBuildProjectName)/
ErrorLog = $scratch/logs/$(MSBuildProjectName).sarif
```

排除原專案的 obj/bin source glob；SDK 的 Configuration/TFM 層級仍保留。
透過 `GlobalAnalyzerConfigFiles` 加入 `unused.globalconfig`；
其 `is_global=true`、`global_level=999`，逐條設定下列規則為 warning：

```text
IDE0005 IDE0051 IDE0052 IDE0059 IDE0060 CA1812 CA1823
IDE0007 IDE0002 IDE0001 JSON002 IDE0008 IDE0003
dotnet_code_quality_unused_parameters = all
```

每個 src 專案執行的命令形狀如下，所有退出碼另存 `results.json`：

```powershell
dotnet build $project --no-restore --nologo `
  -p:DirectoryBuildPropsPath=$scratch/outputs.props `
  -p:TreatWarningsAsErrors=false -p:UseSharedCompilation=false `
  -p:RunAnalyzers=true -p:RunAnalyzersDuringBuild=true -v:minimal
```

每個建置程序明確設定：

```text
TEMP = TMP = TMPDIR = <test-area>/temp
DOTNET_CLI_TELEMETRY_OPTOUT = 1
DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 1
DOTNET_NOLOGO = 1
```

補測每個程序另設 `DOTNET_CLI_USE_MSBUILD_SERVER=0`、`MSBUILDDISABLENODEREUSE=1`。
User-level `NFC_TEST_AREA_ROOT` 未設定；process-level 值為 `<test-area>`。
本次未執行 repository verifier 或測試，未初始化或修改 user-level 環境變數。
所有 build 都使用 `--no-restore`。

確認方法包括 MSBuild properties/items、實際載入的 analyzer DLL 與 globalconfig、
每個成功產品專案的 SARIF，以及首輪四個失敗專案的 NETSDK1004 log。
Bootstrap graph 另建置既有 `eng/prebuilt-profile-catalog` generator；其 SARIF 無診斷，
不混入 15 個產品專案的統計。
補測 compiler 同為 5.6.0；graph 中重新編譯的相依專案，其指定規則逐筆定位與首輪相同，未重複計入。

SARIF 以專案／規則／URI／line／column／message 定位 finding，排除 `suppressionStates`。
不把建置摘要重印的 warning 算第二次；source 與 temp generated URI 分開辨識。
指定 unused 規則在 raw SARIF（包含 suppressed records）中同樣為 0 筆。

## Unused 分組清單

專案名稱省略共同前綴 `NvtFwCombiner.`。數字涵蓋成功編譯的全部 15 個產品專案。

表頭縮寫：U=IDE0005、P=IDE0051、R=IDE0052、V=IDE0059、A=IDE0060、
C=CA1812、F=CA1823。

| 專案 | exit | U | P | R | V | A | C | F |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Domain | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Contracts | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Application | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| VersionManagement.Application | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Profiles | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Platform | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Infrastructure | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| VersionManagement.Infrastructure | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Presentation.Avalonia | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Bootstrap | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Desktop | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Cli | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| DistributionLauncher | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Launcher | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| LauncherBootstrap | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| 已量測合計 | — | 0 | 0 | 0 | 0 | 0 | 0 | 0 |

補充 compiler 規則 CS0162、CS0168、CS0219、CS0414、CS0649：
全部 15 個專案每條均為 0。

所有 unused 分組皆空，因此沒有可列的 file:line，也沒有可判定的 reflection/test-only、
public API 或 generated artifact finding。
最安全候選清單：**0 項**；沒有為湊足 15 項而加入 analyzer 未回報的符號，也不提出刪除批次。

沒有候選符號可進行 caller triangulation；未把沒有搜尋命中當成 unused 的證明。
已用 `rg` 在 src/tests/docs 搜尋上述 unused 診斷 ID 與 unused 類 SuppressMessage，
未命中；這只檢查可見的 suppression 線索，不證明不存在其他 suppression 或動態使用。

## 歷史 style 基線比較

只統計 owner/item 點名的六條 style 規則；這些 finding 都不是 unused 成員證據。

| 規則 | 歷史總數 | 本次已量測 source | 說明 |
| --- | --- | --- | --- |
| IDE0007 | 142 | 42 | var preference |
| IDE0002 | 10 | 0 | simplification |
| IDE0001 | 7 | 0 | simplification |
| JSON002 | 5 | 0 | JSON diagnostic |
| IDE0008 | 4 | 3 | explicit type preference |
| IDE0003 | 1 | 0 | qualification preference |
| 合計 | 169 | 45 | 本次 15/15；歷史範圍與 SDK 不明 |

JSON002 個別 severity 已指定，但不保證 CLI compiler 載入 IDE 的 JSON language service；
其 0 筆不能認定原 5 筆已消失。其餘規則也不能以 169−45 宣稱 cleanup 成果。

以下列出 45 筆的完整定位；每個小節先指定 `src/NvtFwCombiner.<project>/`，
清單路徑相對該目錄，格式為 `file:line:column`。全部為人工 source 的 style finding，
沒有將這些位置加入 unused 安全候選。
補測 Cli、DistributionLauncher、LauncherBootstrap 的 IDE0007 分別為 1、2、1 筆，Launcher 為 0 筆；
四專案的其餘五條 style 規則各為 0。


### Application — IDE0007（9 筆）

來源目錄：`src/NvtFwCombiner.Application/`。

- `Authoring/AbMergeAuthoringExperience.Readiness.cs:98:9`
- `Authoring/AbMergeAuthoringExperience.Readiness.cs:102:9`
- `Authoring/AuthoringSessionState.SelectedFileInspection.Adoption.cs:121:13`
- `Composition/AbMergeTopologyAdmission.cs:69:9`
- `Composition/CompositionExecutionBundleDelivery.cs:240:9`
- `Composition/CompositionExecutionExperience.cs:83:9`
- `InputInspection/FirmwareArtifactClassificationResolver.CtrlRam.cs:27:9`
- `MemoryLayout/MemoryLayoutProjector.Sections.cs:121:9`
- `MemoryLayout/MemoryLayoutProjector.Sections.cs:126:9`

### Bootstrap — IDE0007（2 筆）

來源目錄：`src/NvtFwCombiner.Bootstrap/`。

- `CompositionHostServices.cs:337:9`
- `ManagedDistributionLauncherHostServices.cs:226:9`

### Cli — IDE0007（1 筆）

來源目錄：`src/NvtFwCombiner.Cli/`。

- `ReplaceCliCommandHandler.CtrlRam.Choices.cs:141:13`

### Desktop — IDE0007（1 筆）

來源目錄：`src/NvtFwCombiner.Desktop/`。

- `Program.cs:77:9`

### DistributionLauncher — IDE0007（2 筆）

來源目錄：`src/NvtFwCombiner.DistributionLauncher/`。

- `Program.cs:33:19`
- `ReleasePayloadExtraction.cs:17:9`

### Domain — IDE0007（1 筆）

來源目錄：`src/NvtFwCombiner.Domain/`。

- `Firmware/FirmwareAbFormatPolicy.cs:116:9`

### Infrastructure — IDE0007（10 筆）

來源目錄：`src/NvtFwCombiner.Infrastructure/`。

- `Bundles/AcceptedPrebuiltProfileCatalog.cs:36:9`
- `Bundles/AcceptedPrebuiltProfileCatalog.cs:59:9`
- `Composition/BuiltInFirmwareInspection.cs:336:9`
- `Composition/BuiltInV2Bundle.cs:41:9`
- `Composition/BuiltInV2BundlePreload.cs:117:9`
- `Composition/BuiltInV2BundlePreload.cs:119:9`
- `ExternalTools/ExternalCombinerProcessor.cs:87:19`
- `ExternalTools/LegacyCombinerPostbuildProcessor.cs:114:19`
- `ExternalTools/ToolchainRuntimeCandidateInspector.cs:72:23`
- `Files/RawBinaryEditorFileSession.cs:104:17`

### LauncherBootstrap — IDE0007（1 筆）

來源目錄：`src/NvtFwCombiner.LauncherBootstrap/`。

- `Program.cs:13:13`

### Presentation.Avalonia — IDE0007（6 筆）

來源目錄：`src/NvtFwCombiner.Presentation.Avalonia/`。

- `Behaviors/ReportCopyAction.cs:57:9`
- `DesktopApplication.cs:28:9`
- `MainWindow.Lifetime.cs:140:13`
- `ViewModels/ReportReviewViewModel.Summary.cs:22:9`
- `Views/HexEditorPanel.axaml.cs:186:9`
- `Views/RunReportsTable.axaml.cs:69:9`

### Presentation.Avalonia — IDE0008（1 筆）

來源目錄：`src/NvtFwCombiner.Presentation.Avalonia/`。

- `Views/MemoryCoverageBar.Legend.cs:124:13`

### VersionManagement.Infrastructure — IDE0007（9 筆）

來源目錄：`src/NvtFwCombiner.VersionManagement.Infrastructure/`。

- `VersionManagement/FileSystemInstalledLauncherRepository.cs:154:13`
- `VersionManagement/FileSystemManagedFirstInstallationRootMaterializer.Helpers.cs:175:17`
- `VersionManagement/FileSystemManagedSetupRecoveryExecution.cs:510:9`
- `VersionManagement/FileSystemVersionManagerWriteLease.cs:55:17`
- `VersionManagement/JsonLauncherMutationFence.cs:75:9`
- `VersionManagement/JsonVersionManagerStateStore.cs:224:9`
- `VersionManagement/LauncherBootstrapRuntime.cs:186:9`
- `VersionManagement/LauncherBootstrapRuntime.cs:187:9`
- `VersionManagement/ManagedDistributionLauncherRuntime.cs:369:23`

### VersionManagement.Infrastructure — IDE0008（2 筆）

來源目錄：`src/NvtFwCombiner.VersionManagement.Infrastructure/`。

- `VersionManagement/ManagedDistributionLauncherRuntime.cs:231:17`
- `VersionManagement/ManagedDistributionLauncherRuntime.cs:390:17`

## 首輪未能建置的四個專案與補測

首輪四個專案在 `$scratch/obj/NvtFwCombiner.<project>/project.assets.json` 缺少 assets；
各自 build exit=1，錯誤為 NETSDK1004，因此首輪未量測。這四個專案已還原後，
補測沿用本報告的 Debug/net10.0 命令，四個 exit 均為 0。

補測專案：

- `src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj`
- `src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj`
- `src/NvtFwCombiner.Launcher/NvtFwCombiner.Launcher.csproj`
- `src/NvtFwCombiner.LauncherBootstrap/NvtFwCombiner.LauncherBootstrap.csproj`

補測 source 與 SDK 均與首輪相同；warning override 仍只為列出診斷，不代表預設 gate 通過。

## 判讀與限制

- IDE0051/52 與 CA1823 主要覆蓋 private 成員；不會完整列出未被外部 consumer 使用的 public API。
- CA1812 的 internal-class 判定受 friend assembly／InternalsVisibleTo 等條件影響；
  沒有警告不證明某個 internal type 在產品 runtime 被建立。
- Reflection、序列化、resource 名稱、DI 與動態載入可能缺少靜態 caller，不能只憑 analyzer 刪除。
- XAML binding、事件處理器與 generated command/property 可能是有效 consumer；
  MVVM source generator 或 Avalonia compiler 也可能使 compiler 看到原 source 沒寫出的使用。
- Generated artifact 不是直接刪除候選；若未來回報，需追查產生器與對應人工 source。
- 本次編譯產品專案，沒有編譯或執行 tests；test-only 使用須另查 tests 的引用與 friend 關係。
- 若未來量測產生候選，應用 `rg` 搜尋精確符號於 src/tests/docs，再讀 caller、反射字串與 XAML/resource。
  `rg` 零命中只是線索；同名符號、別名、動態名稱與外部使用可能使結論不完整。
- 沒有診斷 ID 的可見 suppression 搜尋命中，不代表沒有 category suppression、pragma 或規則內建排除。
- 本次未移除既存 suppression；raw SARIF 與排除 suppressed records 後的 unused 數量均為 0。
- 本次已完成 15/15 產品專案的指定規則重測；JSON language-service 與未固定的歷史基線仍限制歷史比較。
- 未做 Golden、測試或 runtime 行為驗證；只完成這份 R0 盤點文件與全產品專案的 analyzer 建置。

## Evidence 與文件檢查

以下首輪文件保留在第一個 temp 目錄，不加入 Git；SHA-256：

```text
outputs.props
a4e4c4da35a8b2ae9c8370b532487c09e19609131a9f4a5797023467f1620557
unused.globalconfig
52d534d721d5bb4f66e44baf4a88a63248a20cd39bb573ff58238c42ba03dc3f
diagnostics.json
e86d717e8c6943f14c9fb6f14be7f1d9e2b668d536e04843b3bff494b9105924
results.json
15c846c9625b429e27d22d741c7ebc349721a548c3130492ed47e569759b7c46
rule-descriptors.json
da6e292b14c4fb507c2ad1999cc73ff112f95f5526962821e8158c19e2d23ad2
evidence.sha256
5c3430547d69fad89f9f9b46ddd97d72f090b97d8b49f55596b018fce7c3a166
```

補測文件保留在第二個 temp 目錄；SHA-256：

```text
outputs.props
5d005934be902411d2ec2e8f53d5c8c7ba0b51b3c65ddfd0e78dccacf1cda235
unused.globalconfig
52d534d721d5bb4f66e44baf4a88a63248a20cd39bb573ff58238c42ba03dc3f
diagnostics.json
2cbe6380214bdfc1591e5101b2ef92fdff2378087bf6d21ba9f454ac947bf4ae
results.json
7e243dc8963d2cb910de371acc14f09ee7a2a49f89ba8fbfce082fdba98c30e0
evidence.sha256
a63edb374ed75f72abeca28370fac30f25634e158aed15fb4dbee9b4c79794d7
```

`evidence.sha256` 記錄各 build log 與 SARIF 的 hash；
`diagnostics.json` 保留原 URI、位置、message 與 Suppressed 狀態。
首輪 `results.json` 記錄 15 個產品專案的退出碼，補測同名檔記錄四專案的成功退出碼。
首輪 `effective-analyzers.json` 記錄實際 analyzer/config items；
首輪 `rule-descriptors.json` 記錄五條 IDE unused 規則的 SDK metadata。

文件交付檢查：UTF-8、LF、最長行不超過 120 字元，並檢查所引用的文件與 source 位置。
補測最終 worktree 只修改本檔，沒有產品變更、commit、integration 或 publication。

Open questions：None；四個 NETSDK1004 專案的前置建置與補測已完成。
歷史基線缺原始 evidence，JSON002 缺 CLI language-service 涵蓋，已明示為比較限制。
