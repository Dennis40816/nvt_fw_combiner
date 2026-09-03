# Owner Golden Handoff

目前沒有待 owner 補資料或補決策的 input gate。歷史缺項摘要保留於
[`0718-missing-owner-evidence/README_請先看.md`](0718-missing-owner-evidence/README_請先看.md)，
但不再是目前收件清單。

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

## Current Golden Evidence Status

最後一批 owner golden 已在 2026-07-18 完成收件；目前沒有待 owner 補資料或補決策的
input gate。當前 canonical Golden inventory 與 `ctrlram-replace` direct cases 是
[`../canonical/manifest.json`](../canonical/manifest.json)；owner-gap status 是
[`v0.9.9 final owner Golden gap matrix`](../../../docs/governance/v0.9.9-final-owner-golden-gap-matrix-20260718.md)。

`0718-missing-owner-evidence/` 與舊 per-IC `CASE.md` 僅為 Historical 追蹤，不能再當成
收件清單。剩餘 hash/manifest、V1/V2/expected parity、tool experiment、route、code-size、
R2/R3 review 與 release verification 都是 agent-owned gates，不是 owner-input。
