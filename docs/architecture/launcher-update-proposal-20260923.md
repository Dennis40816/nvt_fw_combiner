# Launcher 差異更新、自更新與內網遷移方案

日期：2026-09-23。狀態：**待討論的方案，尚未批准實作或修改契約**。
依據 `73b3d6e5d` 的唯讀檢視，沒有執行新測試、部署或讀取憑證。
[roadmap](nfc_roadmap.md) 擁有版本分配；下次 release 只含原 `1.2.0` 前的
修復／變更，本方案不加入該次 release。批准後才分批回寫既有 SPEC／ADR／contracts。

## 建議流程

已簽署 Registry／Catalog → 選定版本 → 使用者下載／安裝 → 取得缺少的 chunks →
重組原始 release ZIP → 既有完整驗證 → staging／安裝 → App READY →
後續啟動更新 Launcher → Launcher READY。任一步失敗保留可運行的舊版本。

採用三個原則：差異傳輸復用既有安裝器；Launcher 復用現有 Bootstrap 自更新；
發布身分與存放位置分開，使新 client 日後換內網只需調整外部來源配置。
舊客戶端可能需要一次 bridge，不能承諾所有歷史版本只換 URL 就具備新能力。

## 現況與缺口

| 範圍 | 已有基礎 | 還需要完成 |
| --- | --- | --- |
| App 安裝與切換 | VersionManagement Application、side-by-side staging、完整 package 驗證、READY／rollback | 差異取得與現有安裝入口接線 |
| Launcher 自更新 | ADR 0056：不可變 Root Bootstrap、版本內 Launcher、獨立 identity、READY、精確 LKG 回復 | 真升級相容矩陣、錯誤與 UI 狀態、遷移證據 |
| 來源 | Registry 有 file／HTTP adapters；Catalog／package 仍依 local／UNC path | typed file／UNC／HTTPS transport、認證、resume |
| 信任 | ACL、size／SHA、Registry revision／digest | Publisher 簽章、root rotation、expiry／revocation、持久化安全版本 |
| Publisher | 現有 Catalog／Registry 產生與部署 scripts | chunks、transfer manifest、簽署及不可變發布順序 |

「存在程式與契約」不等於新方案已通過驗證。健康的本機啟動目前不需要遠端 I/O，
新功能也必須保持這個行為。

## 差異更新

第一版建議針對 **canonical release ZIP 做內容定址 chunks**。每個 chunk 以
SHA-256 與 length 識別；已簽署的 transfer manifest 列出有序 chunks、演算法版本、
完整 ZIP identity、總長度與 hash。Client 重組的 ZIP 必須與完整下載逐 byte 相同，
再交原 `ManagedPackageVerifier`／materializer；不新增逐檔安裝或另一套 executor。

採用成熟且有界的 content-defined chunking，先以真實 release 樣本量測邊界參數。
不預設每次都有固定節省率：壓縮方式、大型單檔 EXE 與 cold cache 都會影響重用率。
可以跨版本重用任意相同 chunks，跳版本不需串接歷代 patch；不能從已安裝檔案
重新壓縮來假裝取得相同 ZIP。若 ZIP chunk 重用實測不佳，再另提逐檔物件格式，
連同 package identity／驗證契約重新審查。

完整 ZIP 一直保留作 fallback。缺少可選 transfer manifest、伺服器不支援加速格式，
或預估差異流量不划算時，可以取得**同一個已選定 ZIP**。簽章錯誤、撤銷、來源衝突、
回放或不安全路徑必須拒絕，不能退回較弱信任。Chunk 損壞可清除該 cache entry、
有限次乾淨重取；完整包仍需相同 authority 和完整驗證。

現行 Check 在一些流程會讀完整 package。必須先延伸既有狀態契約，區分
「已驗證發布資訊的可下載版本」與「完整 bytes 已驗證的 candidate」；
否則只優化 Install，Check 已經用掉完整流量。新狀態不可冒充既有 Verified。
下載中的 target identity 固定；安裝／啟用前重檢適用的撤銷與安全限制。

## Launcher 如何更新自己

復用 [ADR 0056](../adr/0056-rollback-safe-launcher-self-update.md)，保留三個角色：
下載／Setup 用的 Distribution Launcher、managed root 的最小 Bootstrap、
App 版本目錄內可更新的 Launcher。捷徑指向穩定入口，不覆寫正在執行的 EXE。

1. 新 package 完整驗證，在同 volume staging，沿原 lease／journal 原子提交 installed。
2. 使用者切換 App 時，由已 admitted 的 Launcher 啟動；收到精確 READY 才提交 App active。
3. App 成功後，新版本附帶的 Launcher 才能在後續 Bootstrap 啟動時成為候選。
4. Bootstrap 啟動新 Launcher，收到精確巢狀 READY 並重新核對 state／identity，
   才提交 Launcher active／LKG。
5. 新 Launcher 失敗，先確認 process tree 已終止，再回到相容的 exact LKG。
   終止不確定時不啟動第二程序；掉電沿原 journal 恢復，不掃最高版目錄猜 active。

App version、Launcher version、management protocol、metadata schema、publisher revision
分別表達不同身分。新 App 必須與目前及 fallback Launcher 相容；不相容時先發 bridge。
首版建議仍由 App package 附帶獨立版本 Launcher。**Launcher-only hotfix** 若需要，
再批准獨立 package ownership、保留／刪除與 rollback 契約。

Root Bootstrap 目前不可變且使用 protocol 1。若新 state/schema 使舊 strict reader
無法相容，須先發布能合法讀寫新舊格式的 bridge，或以新的 distribution/root 做
一次受控遷移。不能把舊 root-bound state 直接複製到新 root。原位置完全無人操作
升級 Bootstrap 是額外項目，不能以普通 Launcher 自更新代替。

## 內網來源與遷移

延伸現有 `IUpdateCatalogSource`、`IManagedVersionRepository` 與 Registry owner，
加入封閉的 typed location／bounded content reader。UI 不解析 URL、選 mirror 或決定信任。

| Transport | 建議用途與認證 |
| --- | --- |
| Local file | 開發、離線媒體，exact root 與受限相對路徑 |
| UNC | 公司 share，沿執行者 Windows identity／share 與 NTFS ACL，不依賴 mapped drive |
| HTTPS | 正式 Web storage，正常 TLS 與企業認證；首批只驗收一種實際認證環境 |

Logical publisher/channel identity 不隨 server／share／URL 改變。新 client 的 locator
放在受控外部 deployment 配置或已簽署 Registry，與 initial trust anchor 分開。
換 locator 不能換 publisher trust。認證資訊由 OS credential facility／企業機制管理，
不放 URL、Catalog、command line、log 或 repo；redirect 不跨 origin 洩漏 credentials。

遷移步驟：複製同一 objects／ZIP／Catalog 到新站 → 逐項 hash 驗證 → canary 驗
下載、續傳、安裝與 rollback → 發布較高 revision 的 signed Registry／受控 locator →
保留舊 mirror 觀察 → 退役舊站。回切也發布更高 revision，不能回放舊 Registry。
這個「無痛」保證適用於完成新基線的 client：不重建 updater、不重裝現有 App即可換源。

## Cache、故障與安全

- Cache 只加速。每次重用仍驗 hash／custody，與 installed/state 分離；不 hard-link 到可變 cache。
- 最小 resume 單位採完整 chunk；partial chunk 可重抓。HTTP range 是可選加速，
  ETag 不是內容權威。200／206、Content-Range、長度不符都需明確處理。
- Per-content lock 合併下載；取消一位 waiter 不取消其他人的工作。產品 mutation 仍由原 lease／fence 管理。
- Disk full、AV／鎖檔、斷線、認證過期都有有界重試與 typed failure，不刪 LKG 來騰空間。
- 健康離線啟動保持可用。新的離線安裝需要完整 cache 及仍有效的簽署授權；過期資料不自行延長信任。
- 安全狀態由既有 Application state owner 管理；最高已接受 revision／root version
  持久化。正常 App rollback 不降低 metadata high-water mark。
- 簽章採成熟更新安全模式／函式庫，可評估 TUF。初始 root 由受控 distribution 提供，
  離線 root keys 輪替發布 key；rotation 需舊／新 root 的指定 threshold 及完整鏈。
- 拒絕重複 JSON keys、未知必需 schema、越界、unsafe path、錯誤 scope、expiry、revocation。
  SHA 與 HTTPS 本身不能取代 publisher 簽章；Authenticode 也不取代 Catalog 簽章。
- 離線機器無法立即得知新撤銷；本地管理員可操作 state／時鐘也是實際信任邊界，
  不承諾遠端即時封鎖所有離線安裝。

## Publisher 與驗收

延伸現有 `create_update_catalog.py`、`edit_update_source_registry.py`、
`deploy-update-source.ps1`。順序為凍結 ZIP／manifest → chunks 與完整重組比對 →
簽署不可變 transfer／Catalog → 上傳並回讀所有內容 → 最後以 expected revision
原子發布 Registry pointer。兩個 replicas 不宣稱跨伺服器原子；同 revision 異 bytes 拒絕。
記錄 source SHA、artifact hashes、key ID 與操作結果，不記錄 secrets。

必備驗收：full／cold delta／warm delta 的完整 ZIP 及安裝內容一致；跨版跳躍；
惡意／損壞 metadata／chunk；expiry／key rotation／回放；file／UNC／HTTPS 相同 identity；
取消／斷線／磁碟滿／鎖檔／並行／掉電；App 與 Launcher 每個交易階段的 READY／rollback；
舊 reader 與 bridge 相容；真實 clean Windows 無額外 .NET/Python 的升級與換源。
性能量測包含 **Check、重試、metadata、delta、fallback 的總流量**、時間、CPU 及磁碟峰值。
最終候選仍須適用的完整驗證、Golden、獨立 R2/R3 review 與 release 證據。

## 工期及需討論的決策

2026-09-23 估算說明：下列 35–60 日把差異傳輸、三種 transport、企業認證、
新版簽署／輪替、legacy bridge 與 clean Windows 故障驗收全部相加，屬於完整方案
的人工作量模型，不是 Codex 的實際等待時間，也不是僅完成差異更新所需的時間。
既有 Launcher 自更新已有基礎；未量測真實 package 的 chunk 重用率、未定義首套
認證環境及 legacy 相容矩陣之前，這個區間不能作為交付日期。
先以一條既有來源 → 差異取得 exact ZIP → 共用安裝／READY／rollback 的端到端
切片收斂工期。新 client 的 locator abstraction 同步設計；多種企業認證、獨立
Launcher-only 發布與所有歷史版本無人操作遷移按需求另排，不因本草稿自動開工。
生產信任與 release gates 仍依適用政策，不能用 lab 切片宣稱已全部完成。

粗估以一位熟悉 repo 的開發者專注工作日計，包含實作、測試及 review 修正，
不含企業權限／金鑰／測試機等待；尚未用新模型分工量測，也不是曆日承諾。

| 階段 | 工作日 |
| --- | ---: |
| 現況閉合、真實 package 差異量測 | 2–4 |
| 契約、相容及 bridge | 5–8 |
| 三種來源與一種企業認證 | 5–8 |
| 差異下載、cache、publisher 接線 | 7–12 |
| Launcher 自更新整合 | 4–7 |
| 簽署、輪替、安全與發布流程 | 5–9 |
| Clean Windows、故障矩陣及最終修正 | 7–12 |
| **完整方案** | **35–60** |

下一個 Launcher 開發批次可先做樣本量測＋local/UNC 端到端差異更新切片，
約 **10–18 日，包含在總量內**。先前 6–12／28–50 日是舊範圍估算，不套用本次新增需求。
Launcher-only 發布與所有舊安裝原位置無人操作遷移各可能再增加約 5–10 日，需實際矩陣後重估。

需要討論三件事：
1. 首版是否接受「App package 附帶 Launcher」，或必須獨立 Launcher-only hotfix？
2. 第一個真實內網先驗收 UNC＋Windows identity，還是 HTTPS＋指定企業認證？
3. 是否接受一次 bridge／受控新 distribution 遷移，之後換來源只調配置？

## 主要依據

- [Version Management spec](../specs/v0.10.6-version-management.md)：既有 owner、安裝及身份契約。
- [ADR 0053](../adr/0053-fixed-update-source-registry.md)：Registry admission、重新驗證及來源權威。
- [ADR 0056](../adr/0056-rollback-safe-launcher-self-update.md)：Bootstrap／Launcher 自更新、READY及兩交易順序。
- [ADR 0066](../adr/0066-update-catalog-v2-notification-policy.md)：通知政策及 Check 的 package I/O 例外。
- [Registry contract](../contracts/update-source-registry-v1.md)：現有 strict schema 與路徑限制。
- [Catalog source port](../../src/NvtFwCombiner.VersionManagement.Application/VersionManagement/UpdateCatalogSource.cs)、
  [package repository](../../src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedVersionRepository.cs)：local／UNC、完整 ZIP 驗證。
- [Launcher coordinator](../../src/NvtFwCombiner.VersionManagement.Application/VersionManagement/LauncherBootstrapCoordinator.cs)、
  [entry coordinator](../../src/NvtFwCombiner.VersionManagement.Application/VersionManagement/ManagedLauncherEntry.cs)：既有 state／lease／READY 及本機啟動。
- [Release package](../ci/release-package.md)：目前 package/signing 政策；本提案不回溯修改它。
