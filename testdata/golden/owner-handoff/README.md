# Owner Golden Handoff

目前唯一有效的蒐集清單是
[`0718-missing-owner-evidence/README_請先看.md`](0718-missing-owner-evidence/README_請先看.md)。
該目錄只列出尚缺項目；未列出的 IC／模式不要重複提供。

舊 `ctrlram-replace/<IC>/` 目錄保留作歷史 handoff 記錄，不代表目前仍欠該 IC，
也不要再照舊 `CASE.md` 建立 `base.bin`。正式環境無法匯出 pre-replacement base，
本次 evidence contract 使用同一次 run 的實體 Postbuild inputs、完整 final expected
FlashCode 與命令／provenance 資訊。

## 接收規則

- 保留原始技術檔名、project token 與 `AUTO_PRJ`；移除姓名、帳號、私人路徑、
  私人網址等個人資訊。
- 未審查的 incoming payload 維持 Git ignore。通過 byte、hash、provenance、privacy
  與 owner review 後，才會連同 manifest 移入正式 `testdata/golden/<workflow>/`。
- 不要重傳已 hash-pinned 的工具或已存在的 golden。
- Owner evidence intake 不會自動促成 runtime/support promotion。
