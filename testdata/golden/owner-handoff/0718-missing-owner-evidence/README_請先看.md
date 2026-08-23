# 0718 Owner Golden Evidence 歷史交接（已取代）

這份清單保留 2026-07-17 當時的交接內容。2026-07-18 final intake 與後續
owner 決定已關閉所有 owner 補件 gate；以下 P0/P1 舊列不得再當成目前補件要求。
剩餘 parity、route、retirement、review 都是 agent-owned engineering gate。

## 最重要的規則

- 不要建立或提供 `base.bin`。目前正式環境只能取得最終 FlashCode，這是已知限制。
- `expected_output/` 要放正式流程產生的完整最終 FlashCode BIN，不是我方產生的檔案。
- `postbuild_inputs/` 要放同一次 build／Postbuild 實際使用的原始實體 BIN。
- 保留原始檔名、專案 token、`AUTO_PRJ` 等技術資訊；移除姓名、帳號、私人路徑、私人網址等個人資訊。
- 不要為了符合表內長度自行裁切或補齊 BIN。保留原始檔；表內數字是目前 Postbuild 宣告的最大消耗 bytes，實際 copy／truncate 政策由驗證端判定。
- 每個 case 都附一份 `Reply.md`，至少寫 IC、模式、實際 IC 顆數、Common FW 版本、最終檔名、是否同一次 run、Postbuild BAT／命令與 Combiner 版本。
- 若有正式 log，請原樣附上；若沒有，至少列出命令列、輸入／輸出檔名及 SHA-256。

## 當時要求提供的項目（歷史）

| 優先 | Case | 仍缺內容 |
| --- | --- | --- |
| P0 | NT51932 cascade | 正確 `DiffDLM.bin` 或其生成命令／log／hash；以及 DiffNFMerge 完成後的 `NF_Ctrlram.bin` 或等價 input/output hash 與 log |
| P0 | NT51950 CtrlRAM cascade | 同一次 run 的 Normal、VN、NF、DiffDLM 與完整最終 FlashCode |
| 已關閉 | NT51951 CtrlRAM single | AUTO_PRJ-695 修正版已納入；V1/V2 full-byte 相同，1.11 expected 與 1.13 結果只差四個已分類 CRC words，不再需要 owner 補件 |
| 範圍排除 | NT51951 CtrlRAM cascade | 目前沒有實際 product project，排除於 v0.9.9 release scope，不是缺少 evidence |
| P1 | NT51926 Common FW 2.0.0 | 提供 single/cascade evidence，或只在 `Reply.md` 明確核准排除於 stable v0.9.9 範圍 |
| P1 | General Replace migration | 先核准 protected ranges、mapping envelope、overlap/alignment 與 release IC/count；再提供代表性的 TP/CtrlRAM-touching input、mapping 與完整 expected FlashCode |

## 已經有，不要再提供

- NT51929 AB direct golden。
- NT51950 AB direct golden（兩個 product case）。
- NT51932 AB：已核准使用 NT51929 的 fact-scoped family evidence，不另要 product golden。
- NT51951 AB：已核准使用 NT51950 的 workflow-logic evidence，不另要 product golden。
- NT51923 single/cascade 的目前實體 inputs 與 expected output。
- NT51926 Common FW 1.4.1 single/cascade 的目前 inputs 與 expected output。
- NT51927 single、2-chip、3-chip 的目前 inputs 與 expected output。
- NT51929 non-AB single 的目前 inputs 與 expected output。
- NT51950 CtrlRAM single 的目前 inputs 與 expected output。
- `DiffNFMerge.exe`：repository 已有相同 SHA-256 的 hash-pinned package，不要重傳。

## 這批檔案不等於直接開放 support

檔案收齊後，還要完成完整 bytes、命令順序、staging read/write ranges、allowed diff、
V2 route parity 與 firmware-owner review。Header CRC／Header Copy CRC 是允許分類的區段，
CtrlRAM replacement 本身則是明確 replacement operation，不會被假裝成 CRC drift。
