# R16-01 — remaining custom-options 頁面 inventory

## Dispatch envelope

- **Outcome**: The list of remaining custom-options pages that need no reference image, with the required data, existing operations, data density and source per page, and the data a later preview needs (decision 297). Completed, hidden and retired pages are named and not reopened.
- **Non-goals**: No UI implementation, image generation, reference-image approval, feature reopening or layout design; no change to existing handoff history, the board or other briefs' documents.
- **Authority**: the worker may edit this file and record evidence outside Git, and make a local commit on the task branch; push, pull request and integration belong to the commander. Risk class: R0 (documentation under `docs/handoff/**`; no production, contract or test change).
- **Branch and worktree**: local task branch `feature/queue/r16-01-remaining-options-pages` (never published, so its commits have no public SHA), base `b5d996c5f` on the `1.2.x` trunk; a local task worktree whose path is not kept here.
- **Write lock**: `docs/handoff/1.2.7/R16-01-remaining-options-pages.md`. Everything else is read-only for the worker.
- **Read first**: Inventory row R16-01, the custom-options layout handoff, decision 297. Owner-search disposition: `reuse` (documents only).
- **Model reason**: Codex implementation worker run with its configured default model and effort of that day (not recorded per run); a bounded R0 documentation or measurement task.
- **Acceptance**: the brief's document-shape check on this file and `python scripts/verify.py --structure-only`, both exit 0 outside the worker's sandbox.
- **Stop and ask**: any need to edit outside the write lock, to change production code, a contract, an ADR or a test, or a result that would change an owner decision: stop and record it under Open.

日期：2026-10-04。對照來源：`b5d996c5f444e0c8ed5a4a82d84753f3ca03c11c`，branch `feature/queue/r16-01-remaining-options-pages`；開始時 working tree clean。

本次依 owner 的決策 297 任務 brief，只交付不需參考圖的頁面清單與後續 preview 資料需求。此 head 的 [board](../1.2.x.md) 決策區收錄至 296，未收錄 297 原文；297 的本次範圍以 owner 本輪 Goal／Scope／Not in scope 為來源，不虛構 board 引文。這是 R0 現況盤點，不是新頁面規格、視覺核准或 R16-02 實作完成證據。

[R16-01／O14 inventory](../1.1.14/1.2.x-inventory.md) 與 [1.2.7 allocation](../1.1.14/1.2.x-allocation.md) 定工作項；[現行 roadmap](../../architecture/nfc_roadmap.md) 定版本。[v1.1.x-custom-options-layout-handoff.md](../../ui/v1.1.x-custom-options-layout-handoff.md) 保留既有要求與完成紀錄，但其開頭的 `1.2.4` 密度排程、CtrlRAM AB 尚未實作等 dated 敘述不能當作本 head 現況；R16 現排 `1.2.7`。本文件不回寫歷史 handoff、board 或其他 brief。

## 判讀方式

- `remaining`：有已記錄的待完成呈現項；既有操作可存在。另案項目明示其 owner，不因此加入 R16 preview。
- `complete`：表內有界 surface 已有完成／保留依據，或只有既有資料與操作而無已記錄的 R16 改動需求；不宣稱整頁所有未來工作完成，也不重開它。
- `hidden`：current head 普通 UI 入口不可見，功能仍保留；是否可列後續公開 preview 受 O05／交付決策約束。
- `retired`：owner 已決定退休；可讀歷史與程式殘留分別記錄，不因殘留而列為 remaining。

Density 是 current head 的欄位數、重複資料與狀態分支描述，不是新欄寬、斷點、字級或版面建議。Page 含會承載 options 的主頁、Settings 子頁與既有共用 modal／surface；共享 slot、Memory Layout 分開列出以固定不重開邊界。Commands 同時記錄現有 UI 操作與 VM command；列出 `Preview*Command` 不表示此 head 有獨立可見的 Preview button，也不指本次要製作的視覺 preview。

## 逐頁清單

| Page | Required data | Commands | Density | Status | Source |
| --- | --- | --- | --- | --- | --- |
| Standard Merge | 已選 IC／Number context、route 支援／證據狀態、TP／DP 等 profile 宣告的 slots、inspection／readiness；output 事實留在既有 Build Settings。 | 選既有 context、Browse／Clear、Details；`PreviewMergeCommand`／`BuildMergeCommand` 受既有 readiness 限制。沒有獨立的 mode-specific option editor。 | 以 slots 與共用 output surface 為主；此分支沒有 AB 的兩個 option toggles，不補造 Options 頁。 | complete | [Workflow templates][workflow] `:175`；[Merge VM][merge-vm] `:35`；[handoff][custom-handoff] `:609–621`。 |
| AB Code Merge — custom-options | IC／Number、TPA／TPB／DP inspection、Common／Desay 等已判定格式、`UseSameTpForAbMerge`、`UseDummyDpForAbMerge`、輸入衝突／confirmation 與 readiness；Dummy 的非 TP 區 `0xFF` 說明和非檔案身分。 | `ToggleAbSameTpCommand`；衝突時 Keep TPA／Keep TPB／Cancel；`ToggleAbDummyDpCommand`／Confirm／Cancel；保留 Browse／Clear、既有 Preview／Build。 | 目前兩個 toggles 直接位於 Input files 內；Dummy On 增加 disabled DP display card，另有 TPA／TPB cards、readiness 與兩種確認狀態。既有 custom-options 提案仍未核准。 | remaining | [Workflow templates][workflow] `:264`；[Merge VM][merge-vm] `:23`；[handoff][custom-handoff] `:609–621,646–668`。 |
| CtrlRAM Replace — Mode settings | Reference 自動辨識的 Standard／AB、IC／Number、AB readiness、替換 A／B／A+B 草稿、Base 與各 section 的 accepted inputs；替換 bank 與右側觀看 bank 是不同狀態。 | AB 才顯示 `SelectCtrlRamBanksCommand`；既有 Reference／CtrlRAM Browse／Clear、Details、`ShowReplaceSelectionCommand`、`PreviewReplaceCommand`／`BuildReplaceCommand`；觀看 A／B 沿既有 Memory Layout 操作。 | Standard 無 bank option；AB 是一個三選項 dropdown，輸入量由 profile／topology 決定。已核准並完成的 dropdown／Mode settings 位置保留。 | complete | [Workflow templates][workflow] `:17,56`；[bank VM][bank-vm] `:11–26`；[Replace VM][replace-vm] `:20–32`；[1.1.10 delivery][ctrlram-delivery] `:567–577`。 |
| Customized Merge（General Merge） | 現有 draft 的 Output length、Fill byte；每 mapping 的穩定身分、Source start、Target start、Length、source BIN／accepted stamp、row issue 與 readiness；不是新的完整 authoring 規格。 | 保留的 `AddGeneralMergeMappingCommand`、row Browse／Remove、`PreviewMergeCommand`／`BuildMergeCommand`；普通 Home／mode choices 現不可見，內部／CLI 存在不等於公開入口。 | 2 個 output fields，加可增刪的 mapping rows；每 row 有索引、3 個 range fields、BIN 與操作／issue，資料量隨 row 數增加。 | hidden | [Workflow templates][workflow] `:193–258`；[mapping VM][mapping-vm] `:28–80`；[visibility][visibility] `:9–15`；[Home／Settings templates][pages] `:94`；[board][board] decision 229；O05。 |
| Customized Replace（General Replace） | 僅記殘留：Base、來源種類、Start／Length、BIN 或 inline bytes、mapping issues／readiness；歷史 report 的 experience／輸入／變更資料仍需可讀。 | 現有 hidden Home command、mapping／Preview／Build 與 CLI 分支仍在此 head；記為 R54 移除／拒絕的對象，不能列成待恢復操作或 preview 需求。 | 隱藏模板仍含 Base／validation 與可重複 mapping rows；密度只是殘留現況，不提出新呈現。 | retired | [Workflow templates][workflow] `:79–171`；[pages][pages] `:41–53`；[Replace CLI][replace-cli] `:22,60`；[board][board] decision 230；[R54 plan][retirement]。 |
| DP Replace | 可讀歷史 experience、input／mutation／report facts；DP metadata、DP CMI、共用 Replace engine 仍由存活 consumers 使用。 | 無現行普通 authoring 頁或 DP Replace command；僅保留歷史報告讀取。不能借 Customized 或 CtrlRAM 路徑重新啟用。 | 不存在待設計的 options 頁；歷史資料量不構成新 authoring UI。 | retired | [roadmap][roadmap] `:863–899`；[Home templates][pages] `:28–106`；[Replace CLI][replace-cli] `:22`。 |
| 共用 Build Settings／output confirmation（含 CtrlRAM FW Version） | Canonical filename、target／mode／effective map、primary output size、source count／size／Event Buffer、warnings、Bundle destination／contents、optional A FlashCode name／size；CtrlRAM 額外有目前版本、Keep／Edit、Version／SubVersion byte draft、validation，AB 逐 bank 投影。 | 既有 name／folder inline edit、Choose parent、Bundle／Additional toggles、source disclosure、Cancel／Confirm；CtrlRAM `SelectCtrlRamFirmwareVersionPreserveCommand`／Edit，AB row `PreserveCommand`／`EditCommand`。 | 既有 single／bundle／additional／combined 四種交付狀態；source rows 可展開，FW version Edit 每組增加 2 個 byte inputs／validation。命名、交付及版本編輯留在原 surface。 | complete | [Output modal][output] `:30–147,152–244,276–394`；[Build Settings history][custom-history] `:245–310`；[1.1.10 delivery][ctrlram-delivery] `:577`。 |
| 共用 firmware selection slots／Input Details | Slot role、required／optional、selected path、accepted inspection、primary facts、Details、pending／warning／blocking issues；不新增 firmware 推導。 | Browse／drag-drop、Clear、Details disclosure、既有 retry／cancel（依 slot state）。 | 檔名／主要 facts 常駐，Details 展開增加技術資料；已完成的對齊、group header、Browse 位置與 disclosure 保留。 | complete | [Slot card][slot]；[handoff][custom-handoff] `:875–1077`；[1.1.10 delivery][ctrlram-delivery] `:570–573`。 |
| 共用 Memory Layout | 既有 typed ranges／address spaces、source／coverage／preservation／changes、bank locators、display error 與 detail facts；不從畫面重新決定 mapping。 | 沿既有 rail／legend／endpoint／detail interactions 與 AB A／B view；view 切換不改替換草稿。 | 既有 overview、legend、detail 與 bank 分支；全部作 options preview 的固定背景，不產生重設計需求。 | complete | [Memory handoff][memory-handoff]；[Workflow templates][workflow] `:302`；[1.1.10 delivery][ctrlram-delivery] `:574–575`；[roadmap][roadmap] `:432`。 |
| Settings > Overview（舊 handoff General） | App version；canonical catalog 的可編輯 IC 數、Standard Merge／CtrlRAM Replace availability；尚無 publication 時的 pending facts。 | `SelectSectionCommand`、Close；資料唯讀，沒有 Save／export／whole-app Reset。 | 目前 3 個 overview summary rows＋1 個 capability row；每 row 為 title／value／status／description。 | complete | [Settings VM][settings-vm] `:202–240`；[pages][pages] `:435–448`；[handoff][custom-handoff] `:50–72`。 |
| Settings > Preferences（舊 handoff Appearance） | Theme、language、reduced motion、input Details default 與當前 choices／值；ordinary shell preferences lifecycle。 | 2 個 ComboBox＋2 個 ToggleSwitch 的即時套用／既有 persistence；section switch／Close；無 Config Save footer。 | 固定 4 個 preference rows；兩個開關含說明，不新增泛用 import／export 或 Reset。 | complete | [pages][pages] `:452–523`；[Settings VM][settings-vm] `:119–130`；[handoff][custom-handoff] `:50–72`。 |
| Settings > Config > Event Buffer Format | Owner-defined Unique ID choices、Alias、recognition byte memberships、唯讀 Output effect／applicability、loading／effective status、draft dirty／validation／close confirmation。 | identity／alias 修改、`BeginAddRecognitionValueCommand`／Add／Remove；Reload、Restore defaults、Discard、Save and apply；dirty close Keep editing／Discard。 | 現行每 row 有 4 類資料，recognition values 是可變長集合，另有 status 與固定 footer；既有 bounded editor 已交付，不能延伸成 Output rule editor。 | complete | [Event Buffer template][event-buffer] `:42–197`；[handoff][custom-handoff] `:50–72,168–196`。 |
| Settings > Config > Toolchain Runtime | Bundled／User selection、user path draft、effective path／file version／architecture／source／verification、operation／dirty／close state。 | Select Bundled／User、Detect／Browse、Discard／Save、dirty close Keep editing／Discard；Restore defaults 用既有 Select Bundled，不新增下載／安裝／自動 fallback。 | 2 種來源選擇、path control、5 類驗證 facts 與狀態／footer；屬獨立 transactional Config session。 | complete | [Toolchain template][toolchain] `:20–105`；[handoff][custom-handoff] `:50–72`；[Toolchain handoff][toolchain-handoff]。 |
| Settings > Version（現行 operational surface） | Running／installed／available version、health／activation state、verified release notes、update source、retention 與 version rows；不是 preference payload。 | 既有 Check now、source Edit／Browse／Confirm／Cancel、row primary／其他版本操作、release notes、self-test、activation retry／confirmation；依現有 availability。 | 多組狀態＋可變 version table＋confirmation；本次只記現況，Launcher／update 改動由原工作項承接。 | complete | [Version template][version] `:22–280`；[pages][pages] `:179–239`；[handoff][custom-handoff] `:50–72`；[board][board] decision 234。 |
| Settings > Support Matrix（R60 邊界） | Canonical per-IC／workflow capability cells、execution／publication／evidence、route detail、source hash／resolution token；不由 filename 或 golden 觀察推定支援。 | 既有 section selection、cell hover detail、catalog details disclosure；唯讀。R60 新呈現／keyboard 尚待其 preview 核准。 | Grid 與 per-cell 可變 route details；決策 293 記錄 6–8 routes 的 tooltip 過高及 560 宣告寬度被主題壓為 320，不能當成本次重新量測。 | remaining | [pages][pages] `:531–616`；[allocation][allocation] `:258`；[board][board] decision 293。獨立 R60，不加入 R16-02。 |

共 15 列：`remaining` 2、`complete` 10、`hidden` 1、`retired` 2。R16 可進下一階段的已記錄 remaining 是 **AB Code Merge custom-options**；Support Matrix 的 remaining 只指向既有 R60。Home／首次 IC context／Cancel／Back 屬 R15；Report 屬 R17 等原工作項；Hex Editor 與未掛入主頁的 Bin Inspector 不因本次盤點成為 options preview 候選（screen disposition 另見 R21／O16）。

## O05／退休邊界與 current-head 差異

1. [O05](../1.1.14/1.2.x-inventory.md) 的舊 `1.2.1` 驗收疑問須連同 [決策 229／230](../1.2.x.md) 讀：`1.2.4` 在交付並驗收 large-file 路徑時重開 **Customized Merge**；完整 General Merge authoring 仍排 `1.3.1`，saved/custom rules `1.3.2`、maintainer UI `1.3.3`。本 head 的 `CustomizedEntriesVisible => false` 同時隱藏兩個 Home entry 與 ordinary choices，R16 不修改它，也不以 internal／CLI 可執行推定公開驗收已完成。
2. Customized Replace 依 decision 230 分類 `retired`，但本 head 的 XAML、VM 與 CLI 仍保留；[R54 retirement plan](../1.2.4/R54-retirement-plan.md) 承接 code／route／profile／Saved Rule（含 NT51926）退休與 typed refusal。此處只揭示決策與實作的差距，不聲稱移除已完成，不把它當一般 hidden 待重開頁，也不為它準備 preview。
3. DP Replace 已退休；保留歷史 Report 解讀及存活 workflow 使用的 DP inspection／CMI／共用 Replace engine 不等於重開功能。Customized Replace 的歷史可讀性同理。
4. 既有 AB custom-options 圖只證明當時的 baseline／proposal：[handoff `Current status`](../../ui/v1.1.x-custom-options-layout-handoff.md#current-status) 明記舊來源、NT51929、Dummy On、無 TP、English／Light、1440×900，以及 owner 尚未接受。不能把它當此 head 的核准圖。CtrlRAM v7 bank dropdown／Mode settings、slot 與 Memory Layout 的完成記錄則保持關閉；R46／R53 等另案不併入 R16。

## 後續 preview 所需資料（本次不產生）

- **固定來源與可達入口**：開始 preview 時的新 head、AB Code Merge 的實際 IC／Number／declared route、可用的載入入口；來源改變須重核對本清單。Customized Merge 需先有 O05／1.2.4 公開交付驗收事實，才能把當時實際 remaining options 補入其後續 preview 範圍；本次不預先指定新 authoring controls。
- **可重現的 input／option 狀態**：非私密的 canonical case／manifest 與 input 身分、slots 的實際空／載入／pending／error／ready 狀態，TPA／TPB 是否相同、Same TP／Dummy On／Off、衝突確認與 Dummy 確認狀態。記錄真正適用的 IC／topology／format，不能用 illustrative filename 推論 firmware facts；截圖不等於 Golden 輸出證據。
- **需要保留的資料與操作**：兩個 toggles 的文字、有效選項摘要、Dummy 非 TP `0xFF` 說明、選項切換的原命令／confirmation／readiness；collapse 不清 files 或改 settings，啟用的 output-changing option 不能隱沒。命名／交付／版本編輯仍用既有 Build Settings，slot／Memory Layout 只作固定 context。[既有 handoff](../../ui/v1.1.x-custom-options-layout-handoff.md) `:609–621` 是約束來源，本次不決定 disclosure、巢狀 border 或 Dummy row 的位置。
- **實際 capture 條件**：待 owner 指定的 viewport、語言、theme、DPI／scale、Details 開關與載入完成時點，以及同一條件下的 current-head baseline。舊圖的 1440×900 與生成圖 1586×992 不自動成為新驗收尺寸；不得以生成圖取代 actual render。
- **後續完成證據**：AB 每個保留 command／state 的驗收案例與 baseline／proposal 對照，先取得 owner 的有界 preview 核准才進入 R16-02。R60 另取其 actual screen／route detail 資料並依 decision 293 決定呈現及 keyboard；本次不選形式。這些是下一階段的資料需求，不表示本次已捕捉、設計或核准任何 preview。

[workflow]: ../../../src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowWorkflowTemplates.axaml
[pages]: ../../../src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowPageTemplates.axaml
[merge-vm]: ../../../src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.cs
[replace-vm]: ../../../src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.cs
[bank-vm]: ../../../src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.CtrlRamBanks.cs
[mapping-vm]: ../../../src/NvtFwCombiner.Presentation.Avalonia/ViewModels/GeneralMappingRowViewModel.cs
[visibility]: ../../../src/NvtFwCombiner.Presentation.Avalonia/WorkflowModeDisplayConverters.cs
[replace-cli]: ../../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs
[output]: ../../../src/NvtFwCombiner.Presentation.Avalonia/Views/OutputDeliveryConfirmationModal.axaml
[slot]: ../../../src/NvtFwCombiner.Presentation.Avalonia/Views/FirmwareSlotCard.axaml
[settings-vm]: ../../../src/NvtFwCombiner.Presentation.Avalonia/ViewModels/SettingsViewModel.cs
[event-buffer]: ../../../src/NvtFwCombiner.Presentation.Avalonia/Resources/SettingsEventBufferFormatPageTemplate.axaml
[toolchain]: ../../../src/NvtFwCombiner.Presentation.Avalonia/Resources/SettingsToolchainPageTemplate.axaml
[version]: ../../../src/NvtFwCombiner.Presentation.Avalonia/Resources/SettingsVersionPageTemplate.axaml
[custom-handoff]: ../../ui/v1.1.x-custom-options-layout-handoff.md
[custom-history]: ../../ui/v1.1.x-custom-options-layout-history.md
[ctrlram-delivery]: ../../ui/v1.1.10-delivery.md
[memory-handoff]: ../../ui/v1.1.x-memory-layout-interaction-handoff.md
[toolchain-handoff]: ../../ui/v1.1.9-toolchain-runtime-handoff.md
[roadmap]: ../../architecture/nfc_roadmap.md
[allocation]: ../1.1.14/1.2.x-allocation.md
[board]: ../1.2.x.md
[retirement]: ../1.2.4/R54-retirement-plan.md

## Checkpoints

### 2026-10-04 Delivered and checked
State: verified (documentation and structure checks only)
Commits: this pull request's integration commit; the worker's local task commit on `feature/queue/r16-01-remaining-options-pages` is not published and has no stable public SHA.
Evidence: the brief's document-shape check -> exit 0; `python scripts/verify.py --structure-only` -> exit 0, both run on the worker's local task commit outside its sandbox; they apply to the document text that this pull request integrates. The only later change to this file is the envelope and checkpoint themselves. The head of this pull request is checked by its own CI.
Open: The preview of the remaining pages and its owner approval are separate steps and are not started.
Next: None in this file.
