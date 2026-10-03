# CLI firmware-owner path inventory（decision 270 的 inventory，來源：#533 審查 P3）

日期：2026-10-03。狀態：read-only inventory 與 proposal；未套用 policy 或 production code 變更。

來源：8479dee8ee95dbcee9f38ecf4793e70858c9ff41（8479dee8e），branch feature/1.2.3/cli-firmware-path-inventory，起始 working
tree clean。

唯一交付檔為本文件；未建立其他 report。未使用網路、未 build/test、未 commit/push、未更改 Git config 或其他 worktree。

## Finding 與判讀基準

[P3] CLI output-deciding options 的 firmware-owner path map 不完整。board decision 270 說明 #533 的五個 CLI
entries，並留下盤點義務（[BOARD788]、[BOARD803]）。

本次盤點 21 個 CLI source files，5 個已覆蓋、16 個未覆蓋；建議新增 15 個精確路徑，Program.cs 留在外。另列出 14 個不進入 build 選項鏈的 CLI files 與 6 個
Bootstrap files 的排除理由。這些數字是檔案數，不是選項或 command 數。

現行 canonical owner 是 docs/governance/authority-policy.json 的 firmware entry（[P15]、[P18]）；CLI 清單只有
[P40]–[P44]，.github/CODEOWNERS 的投影為 [CODE25] 起的五個條目。其他本次 CLI/Bootstrap sources 僅匹配 code-and-documentation 的
src/**，floor R1、roles empty（[P239]）。no 表示沒有 firmware-owner match，不表示該檔的 firmware-semantic 變更獲免審。

依 ADR 0080，匹配 entries 的 floor 取最高、roles 取聯集，且 declared firmware semantics 可以提高 authority（[ADR466]）。checker 使用 full
path match 並合併結果（[MATCH255]、[MATCH278]）；本次以來源文字比對，沒有執行 classifier/test。精確 *.cs entry 不會自動覆蓋同名 partial sibling。

本文件屬 R0 盤點；root AGENTS.md 的 read-only 規則與風險表適用（[ROOT149]、[ROOT161]）。後續 path-list 變更是獨立 R3 governance
PR；firmware-semantic R3 的 firmware review、byte/golden 與 write-range 要求由 [ROOT186] 保留。

## Inventory table

Fxx 是檔案連結；各節列出完整 repository-relative path、Application type/method 與 source line。表內同一檔的續列只延續選項名稱。Application 欄列出
該鏈中的 type/method；完整直接或跨檔轉送鏈見同號證據。

選項欄包含同檔一併處理的 --report；它本身是 report destination，並非新增 firmware-owner 的理由。原始 argv 與 generic helpers 的選項名由已讀 caller 追溯，並非
helper 另行宣告。

| 檔案 | 選項名稱 | Application type/method | 已覆蓋／list entry | 建議 |
| --- | --- | --- | --- | --- |
| [F01] | `--profile`, `--dp`, `--tp`, `--ldc`, `--output` | `PrepareSession` | no [P239] | 新增 |
|  | `--bundle-parent`, `--bundle-name`, `--report` |  |  |  |
| [F02] | `--profile`, `--dp-ab`, `--tp-a`, `--tp-b` | `PrepareSessionAsync` | yes [P40] | 保留 |
|  | `--ab-topology`, `--dp-mode` |  |  |  |
|  | `--acknowledge-non-tp-ff` |  |  |  |
|  | `--include-a-flashcode`, `--output` |  |  |  |
|  | `--bundle-parent`, `--bundle-name`, `--report` |  |  |  |
| [F03] | `--dp-mode`, `--acknowledge-non-tp-ff` | `GetAuthoringSnapshot` | yes [P41] | 保留 |
|  | `--ab-topology`, `--dp-ab`, `--tp-a`, `--tp-b` |  |  |  |
| [F04] | `--profile`, `--size`, `--fill`, `--mapping` | `PrepareMergeSessionAsync` | no [P239] | 新增 |
|  | `--rule`, `--slot`, `--output` |  |  |  |
|  | `--bundle-parent`, `--bundle-name`, `--report` |  |  |  |
| [F05] | `--profile`, `--size`, `--fill`, `--mapping` | `PrepareMergeSessionAsync` | no [P239] | 新增 |
|  | `--rule`, `--slot`, `--output` |  |  |  |
|  | `--bundle-parent`, `--bundle-name`, `--report` |  |  |  |
| [F06] | `--profile`, `--size`, `--fill`, `--mapping` | `GeneralMergeDraftState` | no [P239] | 新增 |
|  | `--rule`, `--slot` |  |  |  |
| [F07] | `--rule`, `--slot`, `--profile` | `LoadGeneralMergeSavedRule` | no [P239] | 新增 |
| [F08] | `--profile`, `--ic-num`, `--base` | `PrepareSession / PrepareReplaceSessionAsync` | yes [P42] | 保留 |
|  | `--output`, `--bundle-parent`, `--bundle-name` |  |  |  |
|  | `--ctrlram`, `--bank`, `--firmware-version` |  |  |  |
|  | `--firmware-sub-version`, `--a-firmware-version` |  |  |  |
|  | `--a-firmware-sub-version` |  |  |  |
|  | `--b-firmware-version` |  |  |  |
|  | `--b-firmware-sub-version`, `--mapping` |  |  |  |
|  | `--patch`, `--fill`, `--rule`, `--slot` |  |  |  |
|  | `--report` |  |  |  |
| [F09] | `--profile`, `--ic-num`, `--base`, `--ctrlram` | `PrepareSession` | yes [P43] | 保留 |
|  | `--bank`, `--firmware-version` |  |  |  |
|  | `--firmware-sub-version`, `--a-firmware-version` |  |  |  |
|  | `--a-firmware-sub-version` |  |  |  |
|  | `--b-firmware-version` |  |  |  |
|  | `--b-firmware-sub-version`, `--output` |  |  |  |
|  | `--bundle-parent`, `--bundle-name`, `--report` |  |  |  |
| [F10] | `--bank`, `--firmware-version` | `TransitionFirmwareVersionCompilation` | yes [P44] | 保留 |
|  | `--firmware-sub-version`, `--a-firmware-version` |  |  |  |
|  | `--a-firmware-sub-version` |  |  |  |
|  | `--b-firmware-version` |  |  |  |
|  | `--b-firmware-sub-version` |  |  |  |
| [F11] | `--ctrlram`, `--base`, `--profile` | `GetDiscoveryDisplayFromAcceptedBase` | no [P239] | 新增 |
|  | `--ic-num` |  |  |  |
| [F12] | `--profile`, `--ic-num`, `--base`, `--mapping` | `PrepareReplaceSessionAsync` | no [P239] | 新增 |
|  | `--patch`, `--fill`, `--rule`, `--slot` |  |  |  |
|  | `--output`, `--bundle-parent`, `--bundle-name` |  |  |  |
|  | `--report` |  |  |  |
| [F13] | `--ic-num`, `--base` | `PrepareSession / PrepareReplaceSessionAsync` | no [P239] | 新增 |
| [F14] | `--profile`, `--base`, `--ctrlram`, `--mapping` | `ExecuteAsync` | no [P239] | 新增 |
|  | `--output`, `--bundle-parent`, `--bundle-name` |  |  |  |
|  | `--report` |  |  |  |
| [F15] | `--slot` | `LoadGeneralMergeSavedRule / LoadGeneralReplaceSavedRule` | no [P239] | 新增 |
| [F16] | `--profile`, `--dp`, `--tp` | `PrepareSession / PrepareReplaceSessionAsync` | no [P239] | 新增 |
|  | `--ldc`, `--dp-ab`, `--tp-a`, `--tp-b` |  |  |  |
|  | `--ab-topology`, `--dp-mode` |  |  |  |
|  | `--acknowledge-non-tp-ff` |  |  |  |
|  | `--include-a-flashcode`, `--ic-num`, `--base` |  |  |  |
|  | `--ctrlram`, `--bank`, `--firmware-version` |  |  |  |
|  | `--firmware-sub-version`, `--a-firmware-version` |  |  |  |
|  | `--a-firmware-sub-version` |  |  |  |
|  | `--b-firmware-version` |  |  |  |
|  | `--b-firmware-sub-version`, `--mapping` |  |  |  |
|  | `--patch`, `--fill`, `--rule`, `--slot` |  |  |  |
|  | `--output`, `--bundle-parent`, `--bundle-name` |  |  |  |
|  | `--report` |  |  |  |
| [F17] | `--dp`, `--tp`, `--ldc`, `--dp-ab`, `--tp-a` | `CompiledAuthoringSelectedInput` | no [P239] | 新增 |
|  | `--tp-b`, `--base`, `--ctrlram` |  |  |  |
| [F18] | `--output`, `--report` | `AcceptedCompositionExecutionRequest` | no [P239] | 新增 |
| [F19] | `--bundle-parent`, `--bundle-name`, `--output` | `CompositionOutputBundleIntent` | no [P239] | 新增 |
| [F20] | `--a-firmware-sub-version` | `PrepareSession / PrepareMergeSessionAsync` | no [P239] | 新增 |
|  | `--a-firmware-version`, `--ab-topology` |  |  |  |
|  | `--acknowledge-non-tp-ff` |  |  |  |
|  | `--b-firmware-sub-version` |  |  |  |
|  | `--b-firmware-version`, `--bank`, `--base` |  |  |  |
|  | `--bundle-name`, `--bundle-parent`, `--ctrlram` |  |  |  |
|  | `--dp`, `--dp-ab`, `--dp-mode`, `--fill` |  |  |  |
|  | `--firmware-sub-version`, `--firmware-version` |  |  |  |
|  | `--ic-num`, `--include-a-flashcode`, `--ldc` |  |  |  |
|  | `--mapping`, `--output`, `--patch`, `--profile` |  |  |  |
|  | `--report`, `--rule`, `--size`, `--slot`, `--tp` |  |  |  |
|  | `--tp-a`, `--tp-b` |  |  |  |
| [F21] | `--a-firmware-sub-version` | `PrepareSession（經 F20）` | no [P239] | 留外 |
|  | `--a-firmware-version`, `--ab-topology` |  |  |  |
|  | `--acknowledge-non-tp-ff` |  |  |  |
|  | `--b-firmware-sub-version` |  |  |  |
|  | `--b-firmware-version`, `--bank`, `--base` |  |  |  |
|  | `--bundle-name`, `--bundle-parent`, `--ctrlram` |  |  |  |
|  | `--dp`, `--dp-ab`, `--dp-mode`, `--fill` |  |  |  |
|  | `--firmware-sub-version`, `--firmware-version` |  |  |  |
|  | `--ic-num`, `--include-a-flashcode`, `--ldc` |  |  |  |
|  | `--mapping`, `--output`, `--patch`, `--profile` |  |  |  |
|  | `--report`, `--rule`, `--size`, `--slot`, `--tp` |  |  |  |
|  | `--tp-a`, `--tp-b` |  |  |  |

## 每檔傳遞證據與建議

### F01 — CliApplication.StandardMerge.cs

`src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs`

Application 邊界：IStandardMergeAuthoring.PrepareSession; AcceptedCompositionExecutionRequest →
ICompositionExecution.ExecuteAsync。

建議：新增。選定 profile、DP/TP/LDC inputs、explicit/automatic output 名稱與 bundle intent 都在此轉送。

宣告並解析選項：[F01:39]。

建立 address-space/path 綁定：[F01:98]。

PrepareSession 傳入 selected IC 與 captured inputs：[F01:226]。

explicit 與 automatic output naming 分流：[F01:275]。

建立並執行 typed request：[F01:314]。

### F02 — AbMergeCliCommandHandler.cs

`src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.cs`

Application 邊界：IAbMergeAuthoring.PrepareSessionAsync; AcceptedCompositionExecutionRequest →
ICompositionExecution.ExecuteAsync。

建議：保留既有 entry。DP mode、topology、inputs 及 A FlashCode additional delivery 在此轉送。

解析 value/flag options：[F02:47]。

傳入 topology、inputs 與 dpMode：[F02:183]。

轉送 additionalDeliveryKind：[F02:273]。

轉送 accepted session、slotPaths、output 與 bundle：[F02:289]。

既有 firmware pattern 正是上述完整路徑（[P40]）；不是 directory wildcard。

### F03 — AbMergeCliCommandHandler.Dummy.cs

`src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.Dummy.cs`

Application 邊界：AbMergeDpMode; IAbMergeAuthoring.GetAuthoringSnapshot; 經 F02 PrepareSessionAsync。

建議：保留既有 entry。normal/dummy 解析與 acknowledgement 決定是否採用 Dummy route；slot 綁定回到 F02。

mode/acknowledgement 選項名稱：[F03:7]。

以 topology 和 Dummy mode 查詢 Application snapshot：[F03:13]。

轉送 options 建立選定 inputs 的 slot paths：[F03:23]。

解析 mode 並檢查 DP/acknowledgement 組合：[F03:28]。

既有 firmware pattern 正是上述完整路徑（[P41]）；不是 directory wildcard。

### F04 — MergeCliCommandHandler.cs

`src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs`

Application 邊界：IGeneralAuthoring.PrepareMergeSessionAsync; AcceptedCompositionExecutionRequest →
ICompositionExecution.ExecuteAsync。

建議：新增。選擇 IC 與 manual/saved-rule draft，並轉送 output/bundle destinations 至 Application。

轉送 argv 至 F05 parser：[F04:31]。

讀取並解析 profile：[F04:41]。

選擇 rule 與 initializer 組合：[F04:55]。

轉送 draft options 至 F06：[F04:72]。

選定 output target：[F04:91]。

傳入 IC 與 draft：[F04:130]。

傳入 output destination 與 bundle：[F04:157]。

### F05 — MergeCliCommandHandler.Options.cs

`src/NvtFwCombiner.Cli/MergeCliCommandHandler.Options.cs`

Application 邊界：ParsedOptions → F06 GeneralMergeDraftState → F04 PrepareMergeSessionAsync。

建議：新增。此 parser 保存 singleton/repeated values；重複選項的保存策略是 build draft 的輸入邊界。

宣告 build/preview 選項與 repeatable 選項：[F05:16]。

保存值、重複 mapping/slot 及覆蓋 singleton 值：[F05:35]。

RequireOption 回傳選定值：[F05:54]。

### F06 — MergeCliCommandHandler.ManualMappings.cs

`src/NvtFwCombiner.Cli/MergeCliCommandHandler.ManualMappings.cs`

Application 邊界：GeneralMergeInitializerInput.TryResolve; GeneralMappingDraftRow; GeneralMergeDraftState。

建議：新增。initializer、source/target range、input path 與 saved-rule 分流在此轉換為 Application draft。

讀取 mapping/rule/slot 並分流：[F06:20]。

讀取 size/fill 並建立 initializer：[F06:52]。

以 shared codec 解析 source/target ranges：[F06:118]。

建立 CopyRange row 與 source path：[F06:133]。

解析 profile selector 至 IC：[F06:148]。

### F07 — MergeCliCommandHandler.SavedRules.cs

`src/NvtFwCombiner.Cli/MergeCliCommandHandler.SavedRules.cs`

Application 邊界：ISavedRuleAuthoring.LoadGeneralMergeSavedRule → GeneralMergeDraftState /
GeneralSavedRuleResourcePolicy。

建議：新增。build path 的 rule、IC 與 slot bindings 在此直接交給 Application loader；並非規則文字 renderer。

接收 rule path、slots 與 IC：[F07:8]。

轉送 slot values 至 F15：[F07:19]。

呼叫 Application loader 並取回 draft/resource policy：[F07:27]。

### F08 — ReplaceCliCommandHandler.cs

`src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs`

Application 邊界：ParsedCliOptions → F09 ICtrlRamAuthoring.PrepareSession / F12
IGeneralAuthoring.PrepareReplaceSessionAsync。

建議：保留既有 entry。此檔定義 Replace 選項集合與 CtrlRAM/General 轉送路徑。

共用 value options：[F08:41]。

CtrlRAM choice 與 General mapping 選項集合：[F08:54]。

呼叫 shared parser：[F08:69]。

解析 profile selector：[F08:91]。

轉送 parsed options 至兩個 Replace handlers：[F08:109]。

既有 firmware pattern 正是上述完整路徑（[P42]）；不是 directory wildcard。

### F09 — ReplaceCliCommandHandler.CtrlRam.cs

`src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs`

Application 邊界：ICtrlRamAuthoring.PrepareSession / TransitionFirmwareVersionCompilation;
AcceptedCompositionExecutionRequest。

建議：保留既有 entry。Base bytes、replacement slot bytes、IC count、bank/version 選項與 output/bundle 送至 Application。

version options 的 build 限制與 choice 解析：[F09:19]。

讀取 IC count/base：[F09:32]。

讀取 Base bytes：[F09:54]。

轉送至 slot resolver：[F09:80]。

PrepareSession 接收 paths/bytes：[F09:129]。

套用 bank/version choices：[F09:145]。

建立 bundle intent：[F09:183]。

建立 execution request：[F09:200]。

轉送 output options 至 F14：[F09:213]。

既有 firmware pattern 正是上述完整路徑（[P43]）；不是 directory wildcard。

### F10 — ReplaceCliCommandHandler.CtrlRam.Choices.cs

`src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Choices.cs`

Application 邊界：AbCtrlRamDraftState / CtrlRamFirmwareVersionDraftState →
ICtrlRamAuthoring.TransitionFirmwareVersionCompilation。

建議：保留既有 entry。bank enum、hex version bytes 與逐 bank transition 的轉送都在此。

完整 choice 名稱：[F10:12]。

解析 bank：[F10:24]。

解析三組 version/sub-version：[F10:40]。

建立 version draft：[F10:82]。

轉送 bank draft：[F10:116]。

轉送 Standard version：[F10:128]。

建立並轉送 AB version draft：[F10:142]。

既有 firmware pattern 正是上述完整路徑（[P44]）；不是 directory wildcard。

### F11 — ReplaceCliCommandHandler.CtrlRam.Slots.cs

`src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Slots.cs`

Application 邊界：ICtrlRamAuthoring.GetDiscoveryDisplayFromAcceptedBase; slotPaths → F09 PrepareSession。

建議：新增。解析 slot token/path、依 Base 與 IC/count 找 canonical slot、加入 Reference Base，直接影響 replacement input 綁定。

解析 ctrlram values：[F11:13]。

接收 IC/count/base 及 arguments：[F11:46]。

建立 ReplaceBase path：[F11:56]。

以 canonical slot ID 保存 replacement path：[F11:82]。

查詢 accepted Base 的 Application discovery：[F11:100]。

### F12 — ReplaceCliCommandHandler.General.cs

`src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs`

Application 邊界：IGeneralAuthoring.PrepareReplaceSessionAsync; ISavedRuleAuthoring.LoadGeneralReplaceSavedRule;
GeneralMappingDraftRow。

建議：新增。Reference Base、mapping ranges、hex overwrite/fill payload 與 saved-rule slots 均轉成 Application draft/request。

讀取 base/count 與 rule/manual 選項：[F12:20]。

建立 Reference Base path：[F12:68]。

傳入 IC/count/Base/mapping draft：[F12:84]。

建立 bundle 並轉送 output options 至 F14：[F12:135]。

建立 execution request：[F12:152]。

建立 slots 並保留 Base slot：[F12:228]。

載入 General Replace saved rule：[F12:245]。

讀取 mapping/patch/fill：[F12:266]。

建立 HexOverwrite/HexFill sources 與 ReplaceRange rows：[F12:351]。

建立 file mapping row：[F12:409]。

解析 target range：[F12:449]。

### F13 — ReplaceCliCommandHandler.Options.cs

`src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Options.cs`

Application 邊界：RequireOption → F09 PrepareSession / F12 PrepareReplaceSessionAsync。

建議：新增。共用 helper 從 ParsedCliOptions 回傳 count/Base 的值；納入與 shared parser 相同的輸入邊界。

接收 optionName 並從 Values 回傳值：[F13:7]。

### F14 — ReplaceCliCommandHandler.RunSupport.cs

`src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.RunSupport.cs`

Application 邊界：run delegate → F09/F12 ICompositionExecution.ExecuteAsync; InputArtifactBinding。

建議：新增。解析 IC、Base/input bindings、explicit/automatic output destination 與 bundle/build delegate arguments。

解析 profile selector：[F14:8]。

轉換 Base 與 slot paths 為 InputArtifactBinding：[F14:20]。

讀取 output、判定 build/bundle：[F14:46]。

選擇 explicit/automatic destinations：[F14:63]。

轉送 destinations、bundle 與 build 至 execution delegate：[F14:72]。

### F15 — SavedRuleCliSupport.cs

`src/NvtFwCombiner.Cli/SavedRuleCliSupport.cs`

Application 邊界：slot bindings → F07/F12 ISavedRuleAuthoring.LoadGeneralMergeSavedRule / LoadGeneralReplaceSavedRule。

建議：新增。此檔解析 build 用的 slot ID/path 並正規化 source path；不是 saved-rule 查詢 command 的 renderer。

接收 slot values：[F15:8]。

拆出 slot ID/path：[F15:16]。

儲存 normalized slot binding：[F15:31]。

### F16 — CliOptionParser.cs

`src/NvtFwCombiner.Cli/CliOptionParser.cs`

Application 邊界：ParsedCliOptions → F01/F02/F08 Application authoring/execution boundaries。

建議：新增。generic parser 仍保存會決定 build 的 flag、singleton 與 repeated values；變更解析即影響轉送結果。

接收 caller 指定的 value/repeatable/flag options：[F16:5]。

保存 flags：[F16:19]。

保存值與 repeated values：[F16:39]。

建立 ParsedCliOptions：[F16:59]。

定義 result 與 GetValues：[F16:71]。

### F17 — CliFixedWorkflowInputReader.cs

`src/NvtFwCombiner.Cli/CliFixedWorkflowInputReader.cs`

Application 邊界：ILocalFileStore.ReadAsync; CompiledAuthoringSelectedInput → F01/F02 PrepareSession; bytes → F09
PrepareSession。

建議：新增。此 adapter 把 option 選到的 path 讀成完整 bytes，固定 address-space/path/bytes 綁定；位於 build input 傳遞鏈。

接收選定 path map 與 compiled input contract：[F17:12]。

逐 address space 讀取 bytes：[F17:44]。

建立 CompiledAuthoringSelectedInput：[F17:64]。

透過 Application file port 讀取完整 stream：[F17:77]。

### F18 — CliCompositionRunSupport.cs

`src/NvtFwCombiner.Cli/CliCompositionRunSupport.cs`

Application 邊界：CliOutputTarget → F01/F02/F04/F14 AcceptedCompositionExecutionRequest output fields。

建議：新增。ResolveOutputTarget 實際選擇預設或 explicit path，並拆出 output directory/file name；文字/report helpers 同檔也被涵蓋。

解析 requested/default output path 並產生 directory/fileName：[F18:17]。

轉送 input alias guard：[F18:29]。

report alias guard：[F18:39]。

定義 output target/full path：[F18:355]。

### F19 — CliBundleOptions.cs

`src/NvtFwCombiner.Cli/CliBundleOptions.cs`

Application 邊界：ICompositionOutputNaming.ResolveAcceptedBundleProposal / ValidateBundleDestination;
CompositionOutputBundleIntent。

建議：新增。讀取 bundle parent/name、採用 default folder name、傳遞 additionalDeliveryKind 並產生 typed intent，涉及 naming/delivery。

定義 parent/name options：[F19:8]。

檢查 build、name/parent/output 組合：[F19:17]。

接收 accepted session 及 additional delivery：[F19:52]。

解析 proposal：[F19:71]。

讀取 parent 與 folderName：[F19:74]。

建立 intent：[F19:85]。

驗證 destination：[F19:105]。

### F20 — CliApplication.cs

`src/NvtFwCombiner.Cli/CliApplication.cs`

Application 邊界：F01/F02/F04/F08 handlers → 各 Application authoring/execution boundaries。

建議：新增。此檔按 command 選擇 firmware workflow handler，組合其 Application services 並轉送 argv；這是具 workflow 意義的 dispatch 邊界。

組合 CLI Application services：[F20:78]。

按 command 分流並轉送 args[1..]：[F20:95]。

### F21 — Program.cs

`src/NvtFwCombiner.Cli/Program.cs`

Application 邊界：CliApplication.RunAsync → F20 dispatch → 各 Application boundaries。

建議：留在外。只將完整 argv 原樣交給 CliApplication.RunAsync，未拆值、選擇 firmware workflow、映射 slot/range 或產生 execution request。

先轉送 runtime trust probe：[F21:7]。

原樣轉送 args 與 cancellation 至 CliApplication：[F21:20]。

跨檔呼叫補證：F05/F06/F07 的 draft 回到 [F04:130]；F13 的 RequireOption callers 是 [F09:32] 與 [F12:20]。F11 的 paths 回到
[F09:129]；F15 的 bindings 回到 [F07:27] 與 [F12:245]。F17 的 bytes/inputs 回到 [F01:226]、[F02:183] 與 [F09:129]。F18 的
directory/fileName 回到 [F01:314]、[F02:289]、[F04:157]、[F14:72]。F14 的 run delegates 建立 request 的位置是 [F09:200] 與
[F12:152]。

納入 F20 而排除 F21 的理由：F20 有 command → firmware workflow 的實際分流與 service wiring（[F20:78]、[F20:95]）；F21 只原樣交出
argv（[F21:20]）。新增 F20 是 conservative path classification 建議，不宣稱它擁有 firmware semantics。shared parser、input
reader、output/bundle helpers 也只是轉接者，Application/Domain 的語意所有權不因此搬移。

## 已讀但建議留外的其他 CLI files

以下均 no firmware-owner match，現行 list entry 為 src/** / code-and-documentation（[P239]）；不計入上表 21 個 build-option
轉送檔。推薦全部留外，理由及行號如下。

| 檔案 | 留外理由 |
| --- | --- |
| [X01] | usage text 與 helper 呼叫；沒有解析或 build request。 |
| [X02] | global using 與 service references record；沒有 options values。 |
| [X03] | 查詢 summaries 並印出 default-output；沒有選定 build session。 |
| [X04] | 渲染既有 CompositionRunResult 與 issues。 |
| [X05] | 印出 usage、bank/version/Dummy 說明文字。 |
| [X06] | --registry 送至 version environment self-test；不是 firmware version 選項。 |
| [X07] | --workflow/--profile/--ic-num 僅篩選 discovery 清單並印出結果。 |
| [X08] | 印出既有 result/report issues；沒有建構 build inputs。 |
| [X09] | 僅輸出 usage。 |
| [X10] | 印出既有 run result、diagnostic marker 與 mutations。 |
| [X11] | 僅印出 unknown-profile 錯誤。 |
| [X12] | 僅輸出各 Replace command 的 usage。 |
| [X13] | 僅接受 validate/mappings，將 mapping rows 格式化為文字 fragments。 |
| [X14] | InspectSavedRuleV2 後渲染 identity/lifecycle/mappings；未載入 build draft。 |

完整路徑（Xxx 連結含證據行號）：

- X01：`src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.Usage.cs`。
- X02：`src/NvtFwCombiner.Cli/ApplicationCompositionGlobalUsings.cs`。
- X03：`src/NvtFwCombiner.Cli/CliApplication.Profiles.cs`。
- X04：`src/NvtFwCombiner.Cli/CliApplication.Result.cs`。
- X05：`src/NvtFwCombiner.Cli/CliApplication.Usage.cs`。
- X06：`src/NvtFwCombiner.Cli/CliApplication.VersionSelfTest.cs`。
- X07：`src/NvtFwCombiner.Cli/CliApplication.Workflows.cs`。
- X08：`src/NvtFwCombiner.Cli/MergeCliCommandHandler.Result.cs`。
- X09：`src/NvtFwCombiner.Cli/MergeCliCommandHandler.Usage.cs`。
- X10：`src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Report.cs`。
- X11：`src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Result.cs`。
- X12：`src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Usage.cs`。
- X13：`src/NvtFwCombiner.Cli/SavedRuleCliCommandHandler.cs`。
- X14：`src/NvtFwCombiner.Cli/SavedRuleCliCommandHandler.V2.cs`。

## Bootstrap 邊界

讀取指定 Bootstrap source 的 host wiring 與入口；未發現 firmware build options 的 CLI parser。以下也僅在 src/** default（[P239]），建議不因本次
CLI options inventory 加入 firmware entry。這不是 Bootstrap 全部行為的 authority audit。

`src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs`：[B01]。建立共享 authoring/output/execution owners；第 110–112 行取得
external environment processor lease，第 185–187 行建立 persisted toolchain session；沒有 CLI processor 選項。

`src/NvtFwCombiner.Bootstrap/CompositionHostServices.Toolchain.cs`：[B02]。reload toolchain configuration；第 38 行只轉送
internal runtime trust probe，沒有 firmware build option value。

`src/NvtFwCombiner.Bootstrap/CompositionHostServices.CatalogProbe.cs`：[B03]。--profile-catalog-probe-v1 是 catalog
loaded/admission JSON probe；第 55 行輸出結果。

`src/NvtFwCombiner.Bootstrap/ManagedDistributionLauncherHostServices.cs`：[B04]。由 executable/assembly/environment 建立
launcher graph；第 284 行跑 installation entry，非 firmware build argv。

`src/NvtFwCombiner.Bootstrap/UpdateSourceRegistryLocator.cs`：[B05]。解析 update Registry locators；供 version
self-test/launcher 使用，非 firmware reference image。

`src/NvtFwCombiner.Bootstrap/ApplicationCompositionGlobalUsings.cs`：[B06]。只有 global using。

## Proposed diff（僅提案，未套用）

新增以下 15 個精確檔案至既有 firmware.patterns，保留 #533 五個 entries。精確清單避免把 text-only/query CLI sources 一併升級；新檔案或日後把 logic 移到
excluded 檔案，仍需重新評估分類。

```diff
--- a/docs/governance/authority-policy.json
+++ b/docs/governance/authority-policy.json
@@ -41,6 +41,21 @@
         "src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.Dummy.cs",
         "src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs",
         "src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs",
-        "src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Choices.cs"
+        "src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Choices.cs",
+        "src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs",
+        "src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs",
+        "src/NvtFwCombiner.Cli/MergeCliCommandHandler.Options.cs",
+        "src/NvtFwCombiner.Cli/MergeCliCommandHandler.ManualMappings.cs",
+        "src/NvtFwCombiner.Cli/MergeCliCommandHandler.SavedRules.cs",
+        "src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Slots.cs",
+        "src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs",
+        "src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Options.cs",
+        "src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.RunSupport.cs",
+        "src/NvtFwCombiner.Cli/SavedRuleCliSupport.cs",
+        "src/NvtFwCombiner.Cli/CliOptionParser.cs",
+        "src/NvtFwCombiner.Cli/CliFixedWorkflowInputReader.cs",
+        "src/NvtFwCombiner.Cli/CliCompositionRunSupport.cs",
+        "src/NvtFwCombiner.Cli/CliBundleOptions.cs",
+        "src/NvtFwCombiner.Cli/CliApplication.cs"
       ]
     },
--- a/.github/CODEOWNERS
+++ b/.github/CODEOWNERS
@@ -27,4 +27,19 @@
 /src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs @Dennis40816
 /src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs @Dennis40816
 /src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Choices.cs @Dennis40816
+/src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs @Dennis40816
+/src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs @Dennis40816
+/src/NvtFwCombiner.Cli/MergeCliCommandHandler.Options.cs @Dennis40816
+/src/NvtFwCombiner.Cli/MergeCliCommandHandler.ManualMappings.cs @Dennis40816
+/src/NvtFwCombiner.Cli/MergeCliCommandHandler.SavedRules.cs @Dennis40816
+/src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Slots.cs @Dennis40816
+/src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs @Dennis40816
+/src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Options.cs @Dennis40816
+/src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.RunSupport.cs @Dennis40816
+/src/NvtFwCombiner.Cli/SavedRuleCliSupport.cs @Dennis40816
+/src/NvtFwCombiner.Cli/CliOptionParser.cs @Dennis40816
+/src/NvtFwCombiner.Cli/CliFixedWorkflowInputReader.cs @Dennis40816
+/src/NvtFwCombiner.Cli/CliCompositionRunSupport.cs @Dennis40816
+/src/NvtFwCombiner.Cli/CliBundleOptions.cs @Dennis40816
+/src/NvtFwCombiner.Cli/CliApplication.cs @Dennis40816
 # contracts (R3)
```

## 後續獨立 PR 的治理與測試變更

Canonical path-list 變更檔：docs/governance/authority-policy.json 的 firmware entry；同步 .github/CODEOWNERS 投影。兩者與
authority tests 本來就屬 governance-owner R3（[P132]）。policy description 已明言 output-deciding CLI
handlers（[P15]）；新增時保留描述或補明 helpers/dispatch 都是 options forwarding surface。

治理文件：在 docs/adr/0080-governance-reset.md item 4 的 firmware row 附上這批 CLI option surfaces 的 amendment/owner
decision，而不要改寫 G1-A 初始清單的歷史（[ADR513]）。policy 仍是精確 path owner（[ADR466]）；branch-version-and-release-governance.md 的既有
branch/R3 approval 規則沒有因新增清單而需要改變（[BRANCH58]、[BRANCH90]）。

測試變更：tests/scripts/test_authority_policy.py 新增 scoped CLI classification test，逐一覆蓋五個既有加十五個新增 paths，要求 case-sensitive
floor R3 且 roles 為 firmware-owner；對 Program.cs 及上述 text/query exclusions 保留 R1/empty roles 的 negative checks。這測的是
path authority 邊界，並非 firmware byte output。

既有 test_codeowners_exactly_matches_r3_patterns 及 test_codeowners_mismatch_with_policy_fails 必須繼續通過；其實作以 policy 的 R3
set 比較 projection/principals（[TEST142]、[TEST280]、[TEST417]）。不能只加 CODEOWNERS 或只改 JSON；也不能把 consistency test 當成已具備逐檔
CLI 語意分類測試。

本文件只交付提案，不建立 test。獨立 PR 需依 [ROOT186] 與 ADR 0080 self-change procedure（[ADR816]）取得 governance-owner
review、base-checker 對 final head 的結果與明確 self-change statement；firmware-owner 應確認此 output-deciding surface 清單。若 PR
同時改 firmware 行為，必須另保留相應 byte/golden/write-range 證據，不能用此靜態盤點替代。

## Limits、狀態與 Open questions

實際檢查：root/docs AGENTS.md、指定 branch governance、ADR 0080 分類/self-change 相關段落、authority-policy.json、CODEOWNERS、checker
matching/classification 函式、authority-policy tests、board decision 270；讀取 CLI 的 35 個 *.cs 與 Bootstrap 六個 *.cs，從 CLI
handlers/選項 helpers 追至來源可見的 Application type/method。

CLI 未提供獨立 --overwrite、--processor、--padding、--truncate、--crc 或 --header build switches；上述結論限於已讀 parser
declarations（[F01:39]、[F02:47]、[F05:16]、[F08:41]、[F10:12]）。General Replace 的 --patch 對應
HexOverwrite（[F12:351]），不能誤寫成不存在的 --overwrite。processor/CRC/header 路由如何執行、padding/truncation 的 profile policy，未沿
Application/Domain 深查。

未檢查：其他 executable projects 的 handlers（Desktop/Launcher 等）、Application/Domain/Infrastructure/Profile/CRC-worker
的完整實作、firmware fixtures/BIN/Golden bytes、全 repository authority drift、GitHub 的 #533 原始
review/approval/rulesets/CI、release candidate、其他 branch/worktree。讀取 Bootstrap wiring 不等於驗證 persisted toolchain/Event
Buffer configuration 的 firmware effect。

未執行 build、test、verify、classifier 或 firmware command；未主張 Golden parity、fresh CI pass、整合完成或
publication-ready。Application 邊界是靜態呼叫證據，沒有 execution trace。

Polytail：PASS（僅本次 R0 read-only inventory / 未套用提案）；已知 P3 用本文件交付盤點，path-list 缺口仍待 separate R3 PR 修正。此 verdict 不核准或證明
proposed policy change 已通過測試（[POLY]）。

Open questions：None。新增路徑是供後續 owner review 的具體建議，沒有在本工作樹改變 approval authority。

## Source references

[F01]: ../../../src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs#L39
[F01:39]: ../../../src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs#L39
[F01:98]: ../../../src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs#L98
[F01:226]: ../../../src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs#L226
[F01:275]: ../../../src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs#L275
[F01:314]: ../../../src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs#L314
[F02]: ../../../src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.cs#L47
[F02:47]: ../../../src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.cs#L47
[F02:183]: ../../../src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.cs#L183
[F02:273]: ../../../src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.cs#L273
[F02:289]: ../../../src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.cs#L289
[F03]: ../../../src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.Dummy.cs#L7
[F03:7]: ../../../src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.Dummy.cs#L7
[F03:13]: ../../../src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.Dummy.cs#L13
[F03:23]: ../../../src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.Dummy.cs#L23
[F03:28]: ../../../src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.Dummy.cs#L28
[F04]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs#L31
[F04:31]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs#L31
[F04:41]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs#L41
[F04:55]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs#L55
[F04:72]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs#L72
[F04:91]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs#L91
[F04:130]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs#L130
[F04:157]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs#L157
[F05]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.Options.cs#L16
[F05:16]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.Options.cs#L16
[F05:35]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.Options.cs#L35
[F05:54]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.Options.cs#L54
[F06]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.ManualMappings.cs#L20
[F06:20]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.ManualMappings.cs#L20
[F06:52]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.ManualMappings.cs#L52
[F06:118]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.ManualMappings.cs#L118
[F06:133]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.ManualMappings.cs#L133
[F06:148]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.ManualMappings.cs#L148
[F07]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.SavedRules.cs#L8
[F07:8]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.SavedRules.cs#L8
[F07:19]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.SavedRules.cs#L19
[F07:27]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.SavedRules.cs#L27
[F08]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs#L41
[F08:41]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs#L41
[F08:54]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs#L54
[F08:69]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs#L69
[F08:91]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs#L91
[F08:109]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs#L109
[F09]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs#L19
[F09:19]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs#L19
[F09:32]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs#L32
[F09:54]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs#L54
[F09:80]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs#L80
[F09:129]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs#L129
[F09:145]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs#L145
[F09:183]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs#L183
[F09:200]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs#L200
[F09:213]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs#L213
[F10]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Choices.cs#L12
[F10:12]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Choices.cs#L12
[F10:24]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Choices.cs#L24
[F10:40]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Choices.cs#L40
[F10:82]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Choices.cs#L82
[F10:116]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Choices.cs#L116
[F10:128]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Choices.cs#L128
[F10:142]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Choices.cs#L142
[F11]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Slots.cs#L13
[F11:13]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Slots.cs#L13
[F11:46]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Slots.cs#L46
[F11:56]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Slots.cs#L56
[F11:82]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Slots.cs#L82
[F11:100]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Slots.cs#L100
[F12]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs#L20
[F12:20]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs#L20
[F12:68]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs#L68
[F12:84]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs#L84
[F12:135]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs#L135
[F12:152]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs#L152
[F12:228]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs#L228
[F12:245]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs#L245
[F12:266]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs#L266
[F12:351]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs#L351
[F12:409]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs#L409
[F12:449]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs#L449
[F13]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Options.cs#L7
[F13:7]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Options.cs#L7
[F14]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.RunSupport.cs#L8
[F14:8]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.RunSupport.cs#L8
[F14:20]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.RunSupport.cs#L20
[F14:46]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.RunSupport.cs#L46
[F14:63]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.RunSupport.cs#L63
[F14:72]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.RunSupport.cs#L72
[F15]: ../../../src/NvtFwCombiner.Cli/SavedRuleCliSupport.cs#L8
[F15:8]: ../../../src/NvtFwCombiner.Cli/SavedRuleCliSupport.cs#L8
[F15:16]: ../../../src/NvtFwCombiner.Cli/SavedRuleCliSupport.cs#L16
[F15:31]: ../../../src/NvtFwCombiner.Cli/SavedRuleCliSupport.cs#L31
[F16]: ../../../src/NvtFwCombiner.Cli/CliOptionParser.cs#L5
[F16:5]: ../../../src/NvtFwCombiner.Cli/CliOptionParser.cs#L5
[F16:19]: ../../../src/NvtFwCombiner.Cli/CliOptionParser.cs#L19
[F16:39]: ../../../src/NvtFwCombiner.Cli/CliOptionParser.cs#L39
[F16:59]: ../../../src/NvtFwCombiner.Cli/CliOptionParser.cs#L59
[F16:71]: ../../../src/NvtFwCombiner.Cli/CliOptionParser.cs#L71
[F17]: ../../../src/NvtFwCombiner.Cli/CliFixedWorkflowInputReader.cs#L12
[F17:12]: ../../../src/NvtFwCombiner.Cli/CliFixedWorkflowInputReader.cs#L12
[F17:44]: ../../../src/NvtFwCombiner.Cli/CliFixedWorkflowInputReader.cs#L44
[F17:64]: ../../../src/NvtFwCombiner.Cli/CliFixedWorkflowInputReader.cs#L64
[F17:77]: ../../../src/NvtFwCombiner.Cli/CliFixedWorkflowInputReader.cs#L77
[F18]: ../../../src/NvtFwCombiner.Cli/CliCompositionRunSupport.cs#L17
[F18:17]: ../../../src/NvtFwCombiner.Cli/CliCompositionRunSupport.cs#L17
[F18:29]: ../../../src/NvtFwCombiner.Cli/CliCompositionRunSupport.cs#L29
[F18:39]: ../../../src/NvtFwCombiner.Cli/CliCompositionRunSupport.cs#L39
[F18:355]: ../../../src/NvtFwCombiner.Cli/CliCompositionRunSupport.cs#L355
[F19]: ../../../src/NvtFwCombiner.Cli/CliBundleOptions.cs#L8
[F19:8]: ../../../src/NvtFwCombiner.Cli/CliBundleOptions.cs#L8
[F19:17]: ../../../src/NvtFwCombiner.Cli/CliBundleOptions.cs#L17
[F19:52]: ../../../src/NvtFwCombiner.Cli/CliBundleOptions.cs#L52
[F19:71]: ../../../src/NvtFwCombiner.Cli/CliBundleOptions.cs#L71
[F19:74]: ../../../src/NvtFwCombiner.Cli/CliBundleOptions.cs#L74
[F19:85]: ../../../src/NvtFwCombiner.Cli/CliBundleOptions.cs#L85
[F19:105]: ../../../src/NvtFwCombiner.Cli/CliBundleOptions.cs#L105
[F20]: ../../../src/NvtFwCombiner.Cli/CliApplication.cs#L95
[F20:78]: ../../../src/NvtFwCombiner.Cli/CliApplication.cs#L78
[F20:95]: ../../../src/NvtFwCombiner.Cli/CliApplication.cs#L95
[F21]: ../../../src/NvtFwCombiner.Cli/Program.cs#L20
[F21:7]: ../../../src/NvtFwCombiner.Cli/Program.cs#L7
[F21:20]: ../../../src/NvtFwCombiner.Cli/Program.cs#L20
[P15]: ../../../docs/governance/authority-policy.json#L15
[P18]: ../../../docs/governance/authority-policy.json#L18
[P40]: ../../../docs/governance/authority-policy.json#L40
[P41]: ../../../docs/governance/authority-policy.json#L41
[P42]: ../../../docs/governance/authority-policy.json#L42
[P43]: ../../../docs/governance/authority-policy.json#L43
[P44]: ../../../docs/governance/authority-policy.json#L44
[P132]: ../../../docs/governance/authority-policy.json#L132
[P239]: ../../../docs/governance/authority-policy.json#L239
[CODE25]: ../../../.github/CODEOWNERS#L25
[ADR466]: ../../adr/0080-governance-reset.md#L466
[ADR513]: ../../adr/0080-governance-reset.md#L513
[ADR816]: ../../adr/0080-governance-reset.md#L816
[ROOT149]: ../../../AGENTS.md#L149
[ROOT161]: ../../../AGENTS.md#L161
[ROOT186]: ../../../AGENTS.md#L186
[BRANCH58]: ../../governance/branch-version-and-release-governance.md#L58
[BRANCH90]: ../../governance/branch-version-and-release-governance.md#L90
[BOARD788]: ../1.2.x.md#L788
[BOARD803]: ../1.2.x.md#L803
[MATCH255]: ../../../scripts/authority_check.py#L255
[MATCH278]: ../../../scripts/authority_check.py#L278
[TEST142]: ../../../tests/scripts/test_authority_policy.py#L142
[TEST280]: ../../../tests/scripts/test_authority_policy.py#L280
[TEST417]: ../../../tests/scripts/test_authority_policy.py#L417
[POLY]: ../../policies/polytail.md#L1
[X01]: ../../../src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.Usage.cs#L5
[X02]: ../../../src/NvtFwCombiner.Cli/ApplicationCompositionGlobalUsings.cs#L1
[X03]: ../../../src/NvtFwCombiner.Cli/CliApplication.Profiles.cs#L26
[X04]: ../../../src/NvtFwCombiner.Cli/CliApplication.Result.cs#L7
[X05]: ../../../src/NvtFwCombiner.Cli/CliApplication.Usage.cs#L5
[X06]: ../../../src/NvtFwCombiner.Cli/CliApplication.VersionSelfTest.cs#L23
[X07]: ../../../src/NvtFwCombiner.Cli/CliApplication.Workflows.cs#L21
[X08]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.Result.cs#L8
[X09]: ../../../src/NvtFwCombiner.Cli/MergeCliCommandHandler.Usage.cs#L5
[X10]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Report.cs#L8
[X11]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Result.cs#L5
[X12]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Usage.cs#L7
[X13]: ../../../src/NvtFwCombiner.Cli/SavedRuleCliCommandHandler.cs#L26
[X14]: ../../../src/NvtFwCombiner.Cli/SavedRuleCliCommandHandler.V2.cs#L14
[B01]: ../../../src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs#L65
[B02]: ../../../src/NvtFwCombiner.Bootstrap/CompositionHostServices.Toolchain.cs#L8
[B03]: ../../../src/NvtFwCombiner.Bootstrap/CompositionHostServices.CatalogProbe.cs#L31
[B04]: ../../../src/NvtFwCombiner.Bootstrap/ManagedDistributionLauncherHostServices.cs#L178
[B05]: ../../../src/NvtFwCombiner.Bootstrap/UpdateSourceRegistryLocator.cs#L14
[B06]: ../../../src/NvtFwCombiner.Bootstrap/ApplicationCompositionGlobalUsings.cs#L1
