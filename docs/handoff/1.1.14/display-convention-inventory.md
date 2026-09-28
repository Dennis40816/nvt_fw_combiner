# 1.1.14 顯示慣例盤點：owner 決策總表

## 1. 摘要

五份盤點共 **46 個 finding**：視覺 20、文字 16、互動 10。其中 **45 個分配至 1.2.x**，`TXT-15` 已另立為 bug，排於 1.1.13。以下版本是盤點建議，不代表實作或參考圖已核准；`TXT-14` 的慣例定於 1.2.5，視覺驗收延至 1.2.6；`INT-08` 的退出入口定於 1.2.4，鍵盤驗收延至 1.2.6。

| 組別 | 1.2.4 | 1.2.5 | 1.2.6 | 另立 bug | 合計 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 視覺 VIS | 3 | 1 | 16 | 0 | 20 |
| 文字 TXT | 0 | 11 | 4 | 1 | 16 |
| 互動 INT | 2 | 1 | 7 | 0 | 10 |
| 合計 | **5** | **13** | **27** | **1** | **46** |

截圖證據為測試區 `evidence/ui-inv-screens/` 的 **29 種狀態、58 張截圖**，每種各有 English Light 與繁體中文 Dark，viewport 為 `1024×850`。截圖只證明呈現；`Output delivery confirmation` 未執行 firmware Build，`Build completed`、Report、F08 等使用合成結果或資料，不能據此主張 firmware bytes 或 Golden parity。

跳過四種請求畫面：Customized Merge 與 Customized Replace 的正式入口／Mode selector 隱藏，且無對應 release example id，強制畫面會是不可達的半成品；Bin Inspector 是未掛入此 `MainWindow` 的獨立 control，缺少完整畫面的 host；Home optional preload 底部狀態列在獨立 headless 行程等待 10 秒仍未同時可見，未以人工狀態冒充。`INT-07`／`INT-10` 因此只有 F08 的畫面證據，未有兩條底列同屏截圖。

## 2. 依版本分組的 finding

每項固定三行：首行為 ID、面向與一句問題；次行為建議慣例；末行為主分配版本、需參考圖與主要風險。面向代碼沿用原盤點；「需參考圖」表示該項入版時須由 owner 核准的新／重核准參考圖。

### 1.2.4

#### 視覺

**VIS-03｜V1｜**Output Delivery 的 Done 勾號幾何重複三份。
建議慣例：共用 `NfcDoneIconGeometry`，保持三個 16 px Path 的現有外觀。
版本：1.2.4｜需參考圖：否｜風險：樣式

**VIS-06｜V2｜**Output Delivery 同一來源列提供三個內容密度不同的 tooltip 入口。
建議慣例：來源整列提供一個 `FileName→Size→Sha256` 詳情 tooltip，保留輸出檔名的純文字 tooltip，並確認鍵盤可開啟。
版本：1.2.4｜需參考圖：否｜風險：行為

**VIS-12｜V6｜**主視窗與 workflow 設定的裸間距缺少角色界線。
建議慣例：保留已核准幾何，將 field、workflow group 與 workspace column 的間距命名後按相同角色收斂。
版本：1.2.4｜需參考圖：否｜風險：樣式

#### 互動

**INT-02｜I2／I6｜**三個取消命令的次要按鈕顯示 `Close`。
建議慣例：取消未提交草稿用 `Cancel`／`取消`，只關閉既有資訊用 `Close`／`關閉`，保持原命令效果。
版本：1.2.4｜需參考圖：是｜風險：字串

**INT-08｜I2｜**Modal 退出入口在右上 X、底部 Close 與雙入口間缺少共同條件（含 I2-5）。
建議慣例：按短首入、長檢視、可捲動選擇定義退出入口；雙入口須執行同一關閉命令，並先依 INT-02 定名。
版本：1.2.4｜需參考圖：是｜風險：行為

### 1.2.5

#### 視覺

**VIS-13｜V6｜**Report Audit 同一面板交錯使用 7／8／10 px 間距。
建議慣例：保留 section、fact row、compact action 的有效間距，明名其角色，再隨完整 Report layout 判定幾何。
版本：1.2.5｜需參考圖：否｜風險：樣式

#### 文字

**TXT-01｜T2｜**Customized mapping、Base image 與 Hex bytes 在繁中固定英文。
建議慣例：保留 mode token，翻譯普通敘述為 `Customized Replace 對應`、`Customized Merge 對應`、`基準映像`、`十六進位位元組`。
版本：1.2.5｜需參考圖：否｜風險：字串

**TXT-02｜T2／T4｜**`Report history` 與報告普通詞在繁中有三套稱呼。
建議慣例：UI 普通名詞用 `報告`，歷史列表用 `報告記錄`，保持 JSON key 與歷史 identity。
版本：1.2.5｜需參考圖：否｜風險：字串

**TXT-03｜T2｜**繁中普通計量詞混用 `byte(s)` 與錯譯 `填充值元組`。
建議慣例：敘述用 `位元組`，改為 `填充位元組`、`取代輸入檔案`，保留專有 token 與 hex 值。
版本：1.2.5｜需參考圖：否｜風險：字串

**TXT-04｜T3｜**Hex Editor 動作使用三點省略號，與進行中訊息的單字形不同。
建議慣例：開啟下一步的動作用 `…`，保留 `0x...` 的十六進位表示。
版本：1.2.5｜需參考圖：否｜風險：字串

**TXT-05｜T3｜**同一 Report 就緒狀態在不同位置有大小寫差異。
建議慣例：採 `Report ready`／`報告已就緒`，保留 partial result 的獨立狀態文案。
版本：1.2.5｜需參考圖：否｜風險：字串

**TXT-09｜T3／T6｜**Build readiness 的結果、原因、計數與下一步順序不一（含 E-14）。
建議慣例：統一 `結果 → 原因／計數 → 下一步`，並核對同一 typed warning 的 Build 前摘要與事後 issue，保持 blocker 分類。
版本：1.2.5｜需參考圖：是｜風險：字串

**TXT-10｜T6｜**Report 儲存失敗 toast 把具體原因置於風險與重試之後。
建議慣例：按 `結果 → 具體原因 → 檔案狀態風險 → 下一步` 排列，無原因時省略空句。
版本：1.2.5｜需參考圖：否｜風險：字串

**TXT-12｜T1｜**同一 A／B bank 在槽位、版本與 SVN 欄位使用 `TPA`／`TPB` 與 `(A)`／`(B)` 兩套稱呼（含 E-01、E-02）。
建議慣例：保留 BIN、Version、SVN 欄位語義與 canonical 識別字，讓可見 bank 修飾語採一致規則。
版本：1.2.5｜需參考圖：是｜風險：字串

**TXT-13｜T3｜**同一 `Load Report` 動作的按鈕與 tooltip 英文名稱不同（含 E-06）。
建議慣例：tooltip 與按鈕都用 `Load Report`，繁中維持 `載入報告`。
版本：1.2.5｜需參考圖：是｜風險：字串

**TXT-14｜T5｜**AB Code 長 BIN 名稱把 `.bin` 拆成 `.bi` 與孤立的 `n`。
建議慣例：允許長名稱換行或省略中段，但完整保留副檔名，並由 tooltip／可及名稱提供全名。
版本：1.2.5｜需參考圖：是｜風險：字串

**TXT-16｜T5｜**同一報告在 Run reports 被裁切日期與類型，在 Report history 完整顯示。
建議慣例：在 `1024×850` 直接呈現完整日期與 workflow 類型，或提供明確的完整值 hover／focus 入口。
版本：1.2.5｜需參考圖：是｜風險：樣式

#### 互動

**INT-09｜I3｜**同一無報告歷史狀態在 Run reports 與 History 使用不同空狀態容器（含 I3-1）。
建議慣例：保留列表與面板的導航結構，但統一空態訊息、留白節奏與讀屏名稱。
版本：1.2.5｜需參考圖：是｜風險：樣式

### 1.2.6

#### 視覺

**VIS-01｜V1｜**頂欄與 toast 使用字型相依的文字字形圖示。
建議慣例：Settings、Message Center 與 toast 使用同語義向量圖示，保留可及名稱和文字。
版本：1.2.6｜需參考圖：是｜風險：樣式

**VIS-02｜V1／V4｜**Message Center 計數徽章獨用 9 px 字與硬寫 `White`。
建議慣例：定義 notification count 角色，採 Light／Dark 成對 token 與 `NfcFontSize10`，按數字寬度設定內距。
版本：1.2.6｜需參考圖：是｜風險：樣式

**VIS-04｜V2｜**同類 tooltip 的延遲、邊位、位移、外形與內容順序缺少角色規則（含 V2 IssueDetailsCard 指針、V7 Issue card 外框、V2 IssueDetailsCard placement、V2 Report History delete tip）。
建議慣例：分別明名 issue card、metadata、路徑詳情與操作 tip，保留 TP SVN 及 issue card 已核准形態，按角色定延遲、放置與內容順序。
版本：1.2.6｜需參考圖：是｜風險：樣式

**VIS-05｜V2｜**Support Matrix 密集 tooltip 固定 560 px，缺少已核准尺寸界線。
建議慣例：保留 catalog 詳情，以上限 560 px 與 viewport 可用寬度約束，靠邊時翻向且不遮焦點 cell。
版本：1.2.6｜需參考圖：是｜風險：樣式

**VIS-07｜V3｜**Settings 已選導覽項的焦點框與藍色選取面重色。
建議慣例：selected 保留明確色面，focus-visible 使用可區分的外層 focus ring，驗收 selected+focus 等三種狀態。
版本：1.2.6｜需參考圖：否｜風險：樣式

**VIS-08｜V4｜**Hex Editor 正常輸入區用 disabled-text 顏色畫外框。
建議慣例：正常外框用 `NfcBorderBrush`，停用時才用 `NfcTextDisabledBrush`，保留 2 px 與焦點／錯誤優先序。
版本：1.2.6｜需參考圖：否｜風險：樣式

**VIS-09｜V5｜**Modal 與內頁標題混用 20／22／24／28 px 的局部階層。
建議慣例：保留已核准有效尺寸，建立 compact、Settings、Message Center 與 hero 等具名標題角色。
版本：1.2.6｜需參考圖：否｜風險：樣式

**VIS-10｜V5｜**Workflow 卡與 Report 區塊標題繞過 `cardTitle`／`panelTitle` 角色。
建議慣例：明名 workflowCardTitle、cardTitle、reportSectionTitle、panelTitle 的尺寸與字重，避免局部隱性覆寫。
版本：1.2.6｜需參考圖：否｜風險：樣式

**VIS-11｜V5｜**Output Delivery 的 `fieldLabel` 字重覆寫不一致。
建議慣例：一般欄位保留原 `fieldLabel`，段落標頭與 CtrlRAM 技術欄位分別使用具名 variant。
版本：1.2.6｜需參考圖：否｜風險：樣式

**VIS-14｜V6｜**Message Center、Settings Event Buffer 與小 modal 的間距缺少跨畫面角色。
建議慣例：保留主要有效值並明名 section、title/detail、page gap；以同 viewport render 判定 Hex Editor Save 的例外。
版本：1.2.6｜需參考圖：否｜風險：樣式

**VIS-15｜V8｜**Settings Toolchain 來源列的上下節奏與 divider 不對稱。
建議慣例：來源列共用上下 padding，divider 只繪一次，User issue 區改由內層間距承載。
版本：1.2.6｜需參考圖：否｜風險：樣式

**VIS-16｜V8｜**Merge／Replace 的同名 Mode 選擇控制寬度為 170／158 px。
建議慣例：同 viewport 比較選項與頁首容量後，核准共同寬度或明示容量例外。
版本：1.2.6｜需參考圖：是｜風險：樣式

**VIS-17｜V8｜**兩個 firmware mismatch modal 的同類標籤欄起點為 112／132 px。
建議慣例：依雙語長標籤核准共同最小欄寬，或明示隨文字伸縮的規則。
版本：1.2.6｜需參考圖：是｜風險：樣式

**VIS-18｜V3｜**Settings 與 Message Center 的 Dark selected 導覽項使用相反色面與文字對比。
建議慣例：先定義共用 selected、hover、focus 語義角色，再依兩 modal 完整狀態圖定共同基準。
版本：1.2.6｜需參考圖：是｜風險：樣式

**VIS-19｜V2｜**Support Matrix 與 Memory coverage 的焦點詳情卡在 Dark 使用兩套材質語法。
建議慣例：分清 catalog tooltip 與 memory interaction card，明定哪些表面、邊框和指針差異可保留。
版本：1.2.6｜需參考圖：是｜風險：樣式

**VIS-20｜V2／V6｜**Memory coverage 卡在 `1024×850` 蓋住 `Output layout` 標題與摘要。
建議慣例：保留卡片內容與指針，按 viewport 空間選上／下定位，優先避開 heading 與範圍。
版本：1.2.6｜需參考圖：是｜風險：樣式

#### 文字

**TXT-06｜T5｜**檔案大小與長度混用 KiB、bytes、B、KB／MB 與十六進位。
建議慣例：精確 count 用十進位 `bytes`／`位元組`，可讀容量用 `KiB`／`MiB` 並保留精確值；位址才用 `0x`。
版本：1.2.6｜需參考圖：是｜風險：字串

**TXT-07｜T5｜**同一絕對位址在 Memory Coverage 有補位與不補位兩種格式。
建議慣例：同 address space 的絕對位址用 `0x`、大寫 hex 與足夠寬度，保留 `[start, endExclusive)` 語義。
版本：1.2.6｜需參考圖：是｜風險：字串

**TXT-08｜T5｜**診斷數字用 invariant 分組，進度與回執用 CurrentCulture。
建議慣例：UI 數字跟選定語言文化，跨機器資料、hex、SHA-256 與 JSON 保持 invariant。
版本：1.2.6｜需參考圖：否｜風險：字串

**TXT-11｜T6｜**Diagnostics 匯出失敗只有結果，缺少下一步。
建議慣例：失敗文案按 `結果 → typed 原因（若有）→ 重試／改選位置`，無原因時給安全通用指示。
版本：1.2.6｜需參考圖：否｜風險：字串

#### 互動

**INT-01｜I2｜**15 個 modal 的鍵盤循環、Esc 與預設焦點沒有共用契約。
建議慣例：依現有命令定開啟焦點、Tab 範圍、Esc 與關閉回焦，取消流程與完成檢視分別驗收。
版本：1.2.6｜需參考圖：否｜風險：行為

**INT-03｜I4｜**成功與失敗共用缺少結果等級的 shell toast 呈現。
建議慣例：由 typed 結果或呼叫點提供 Success／Information／Warning／Error，不解析標題猜等級，並讓讀屏取得類別。
版本：1.2.6｜需參考圖：是｜風險：行為

**INT-04｜I4｜**`System activity` 的整份動態列表設為 `Polite`，可能重複朗讀。
建議慣例：列表仍可逐列聚焦，單一 Polite 目標只播最新事件或簡短摘要，不重播全史。
版本：1.2.6｜需參考圖：否｜風險：行為

**INT-05｜I5｜**Modal 可及名稱分散在根、內層或缺席。
建議慣例：每個 active modal 的焦點範圍只宣告一個與可見標題一致的本地化名稱。
版本：1.2.6｜需參考圖：否｜風險：行為

**INT-06｜I5｜**`Bin Inspector` 欄位值缺少明確讀屏列契約。
建議慣例：聚焦列提供 `DisplayName`、`FieldId` 與完整 `Value`，依原生 peer 朗讀結果避免重複。
版本：1.2.6｜需參考圖：否｜風險：行為

**INT-07｜I1｜**同名 Retry 在 F08 為純圖示，在 startup preload 為文字（含 I1-1）。
建議慣例：以同屏狀態決定 Retry 的可見文字規則，保留各自命令及 F08 tooltip 的位置要求。
版本：1.2.6｜需參考圖：是｜風險：樣式

**INT-10｜I4｜**主視窗底部 F08 與 optional preload 狀態列缺少跨狀態呈現規則（含 I4-1）。
建議慣例：定義持續錯誤與可選準備進度兩種狀態角色及同時顯示優先序，和 INT-07 一起核准 Retry。
版本：1.2.6｜需參考圖：是｜風險：樣式

### 其他

無另行分配的 finding；`TXT-15` 依下一節另立 bug。

## 3. 另立為 bug

**TXT-15｜T6｜**Replace selection 的 `Selected replacements` 三列顯示 `NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportLineViewModel` 類別全名，未顯示來源。
建議慣例：沿 `ReportSelectionRowTemplate` 呈現各列 title/detail，並驗證首入路徑也載入正確 template。
版本：1.1.13 bug `BUG-20260928-replace-selection-shows-type-name`｜需參考圖：是｜風險：行為

此項由 commander 另立 bug 排程；不列入 1.2.x 的 45 項分配，修正後仍需以三列實際內容重核准參考圖。

## 4. 需要 owner 決定的問題

五份來源原列 **18 題**；`TXT-15` 已另立 bug，將重複的 tooltip 與 modal 退出問題合併後，剩 **14 題**。下列依影響 finding 數由多至少排序；同數時依相關版本及閱讀脈絡排列。建議答案是待 owner 定案的候選，並非核准結果。

1. **詳情 popup 的角色、尺寸、材質與遮擋界線如何定？** 建議：明名 issue card、TP SVN metadata、catalog tooltip、memory card 與操作 tip；保留既有指針契約，catalog 上限 560 px 且不遮焦點 cell，memory card 優先不蓋 `Output layout` heading，依同 viewport 展開圖重核准。影響：VIS-04、VIS-05、VIS-19、VIS-20（4）。
2. **頂欄向量圖示與診斷數字徽章以何圖定案？** 建議：保留現有語義與紅底警示，採共用 16 px 向量和 `NfcFontSize10`，以 English Light／繁體中文 Dark 頂欄對照圖一次核准。影響：VIS-01、VIS-02（2）。
3. **取消未提交草稿與不同 modal 的退出入口如何定？** 建議：草稿次要動作用 `Cancel`／`取消`；短首入用底部動作，長檢視用右上 X，可捲動選擇可保留雙入口且命令相同，依完整圖核准。影響：INT-02、INT-08（2）。
4. **F08 與 optional preload 的 Retry 及底部狀態角色如何定？** 建議：保留持續錯誤／可選準備進度兩角色，同名 Retry 採可見文字；待取得 optional 與同時顯示畫面後核准。影響：INT-07、INT-10（2）。
5. **Mode 選擇器與 firmware mismatch 標籤欄是否各自跨頁對齊？** 建議：Mode 採容納選項的共同寬度，兩 mismatch modal 採容納雙語長標籤的共同最小欄寬；窄畫面例外須明列。影響：VIS-16、VIS-17（2）。
6. **Settings 與 Message Center 的 selected／focus 導覽色彩如何共用？** 建議：建立共同狀態角色，保留兩 modal 既有尺寸，在 selected、selected+focus、other+focus 的兩語系兩主題圖上核准；焦點須可與選取區分。影響：VIS-07、VIS-18（2）。
7. **繁中普通名詞 `report` 是否統一為 `報告`？** 建議：是，歷史列表用 `報告記錄`，保留 JSON key 與歷史 identity。影響：TXT-02（1）。
8. **可讀的二進位檔案容量採哪套單位？** 建議：精確值用 `bytes`／`位元組`，可讀值用 `KiB`／`MiB`，不更動原始 report 值。影響：TXT-06（1）。
9. **短暫 shell toast 是否按 typed 結果區分四個等級？** 建議：是；保留位置與尺寸，另以真實主視窗圖核准語義圖示、token 和讀屏類別。影響：INT-03（1）。
10. **A／B bank 可見修飾語採哪一套？** 建議：欄位名稱和 canonical 識別字維持，bank 修飾語統一 `(A)`／`(B)` 並以對照圖核准。影響：TXT-12（1）。
11. **`Load Report` 的英文 tooltip 是否與按鈕同名？** 建議：是，tooltip 用 `Load Report`，繁中仍用 `載入報告`。影響：TXT-13（1）。
12. **無報告歷史的列表與面板可否用不同容器？** 建議：可保留兩種導航結構，但訊息、留白與讀屏名稱一致，以同資料空態圖核准。影響：INT-09（1）。
13. **長 BIN 名稱是否必須完整保留 `.bin`？** 建議：是；可換行或省略中段，全名由 tooltip／可及名稱提供，AB 與 CtrlRAM 並排核准。影響：TXT-14（1）。
14. **Run reports 在 `1024×850` 是否須可判讀完整日期及 workflow 類型？** 建議：是；調整欄寬／行高或提供明確的完整值入口，保留結果與操作可見。影響：TXT-16（1）。

## 5. 矛盾處

1. **原排除判定被 phase2b 更新：**phase2 三份報告把 9 個候選排除，phase2b 恢復為 VIS-16／17、TXT-12／13、INT-07／08／09／10；其中 E-01 與 E-02 共用 TXT-12，所以是 9 個候選、8 個新 ID。依 phase2b 保留新 finding，不再採原排除結論。
2. **原排除判定改為併入：**phase2 排除的 6 個候選，phase2b 併入 VIS-04（4）、TXT-09（1）、INT-08（1）；主 finding 保留，候選不另列 ID。
3. **VIS-04 參考圖需求：**phase2-visual 寫「不需要」，phase2b 明確改為「需要 owner 重新核准參考圖」；phase3 沿用較新要求，本表記「是」。

`TXT-15` 在 phase3 建議 1.2.4；本次任務提供 commander 已另立 1.1.13 bug 的較新排程，因此移至「另立為 bug」。這是本次明示處置，不計入五份輸入互相矛盾的三處。
