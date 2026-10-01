# A1a Launcher 發佈者信任判定（1.2.1 評估）

**修訂紀錄（修訂版 4，2026-10-01）**：依據 `evidence/1.2.1/A1a.review3.md`（Codex `gpt-6-astra` 複審修訂版 3，verdict **reject**，P0 0／P1 1／P3 1；前次 6 項中 5 項 resolved、R2-P1-2 partly；條件式 disposition 與 gate 順序已接受）。2 項 finding 全部同意並修訂：**R3-P1-1 同意（已修）**，Q2 的「巡查最大間隔 7 天＋漏巡次一工作日補巡」無法支持「最長 7 天未偵測」（第 7 天漏巡、第 8 天補巡即超界）；修訂版把最長未偵測窗口 **W 改為 owner 選定的目標，須由 A1b 注入量測（T1−T0）證明，從不作為保證**，並明定兩種能在規格上界定 W 的規則——**告警制**（任何寫入／ACL 事件即時告警，W＝T0 所在工作日的次一工作日結束，假日順延並接受推得的最大日曆天數）與**巡查制**（W 以日曆天計，任意兩次完成巡查的實際間隔含代理人與假日不得超過 W，排程須留餘裕，**間隔超過 W 即記窗口違反、補巡只是補救而非合規**，做不到即改提較長 W 交 owner 接受）；發布核對期限自寫入事件 T0 起算、不等待不存在的發布完成時點；A1b 注入新增「巡查剛完成後立即注入」與「主複查者缺席／假日由代理人完成」情境，並明列 W 的截止計算規則（§3 A3、§4.1 E2、§4.2、Q2）。**R3-P3-1 同意（已修）**，信任分岔的裸「Q4」改寫為「decision 188 Q4（plan Q4／O06-trust）」，與本文 publisher 問題 Q4 區分（§1 第 1 點、§2 T9、§4.1 E8、§4.2 尾）。修訂版 3 的 SHA-256 前綴 `bebdfaa34dac5052`，保留為 `evidence/1.2.1/A1a.v3.md`。**同日補正**：依 `evidence/1.2.1/A1a.review4.md`（verdict **accept-with-changes**，P0 0／P1 0／P2 1）的 **R4-P2-1 同意（已修）**——告警制截止「T0 所在工作日的次一工作日結束（假日順延）」在 T0 為假日時有雙重解讀；改為「不論 T0 是否工作日，截止為 T0 日曆日期之後第一個核准工作日的明定截止時刻」，E2 記錄時區、工作日曆與截止時刻，發布核對用同一算法，A1b 注入加 T0 落在假日的案例（§4.1 E2、§4.2、Q2）。原地修正，不另存副本。以上為作者修訂處置，尚非 reviewer 對補正後版本的確認。

**修訂紀錄（修訂版 3，2026-10-01）**：依據 `evidence/1.2.1/A1a.review2.md`（Codex `gpt-6-astra` 複審修訂版 2，verdict **reject**，P0 0／P1 2／P2 3／P3 1；前次 P1-2、P2-3、P2-4、P3-1 resolved，P1-1、P1-3、P2-1、P2-2 partly）。6 項 finding 全部同意並修訂：**R2-P1-1 同意（已修）**，「E1–E8 齊備即記 A1a 成立」漏掉補證後的獨立安全判定；§1 gate 順序新增「補證複核」：指定獨立 security reviewer 對具日期／版本的 E1–E8 證據逐項判接受／不接受／仍未知 → owner 最終接受 → A1a 才記為成立（條件：A1b）；A1b 清單補列 E6–E8 的決定與撤權／再發布機制未偏離（§1、§4.1 尾、§4.2、Q10）。**R2-P1-2 同意（已修）**，E2 注入驗收改為量測事件至偵測延遲：每案記注入時間 T0、告警或人工發現 T1、開始處置 T2、撤權生效 T3，T1−T0 比對 owner 核准的最長未偵測窗口、T3−T1 比對處置目標，**未偵測、證據缺漏或超時均失敗**；補排程內 publisher 直接寫入的核對規則與期限（每筆寫入事件須在期限內對應到一筆獨立核准的 publication 紀錄，否則視為非預期變更）；「每週＝最長 7 天」改以最大巡查間隔與漏巡規則支持；「唯一偵測手段」改為「本案擬採的補償控制」（§4.1 E2、§4.2）。**R2-P2-1 同意（已修）**，E4 改為每個 client 群的允許／拒絕矩陣（認證協定、目標身分驗證、完整性要求），A1b 補「所需完整性保護無法建立時必定拒絕」的負案例，涵蓋 Registry／Catalog／package 目標與 alias／DFS；Q5 分開「owner 主動限縮支援群」與「機制未證明而暫歸手動」，非網域 client 按實際機制分類而非 join 狀態（§4.1 E4／E5、§4.2、Q5）。**R2-P2-2 同意（已修）**，限制 (a) 與 Q9 改記 primary 不可達只是例子、兩份合法 stale replicas 同樣構成反例、runtime 只選當次最高可接受 revision、沒有已證明的最大陳舊時間或最低 publication；Q9 拆成 Q9a（首次 freshness）與 Q9b（schema continuity，對應 ADR 0066 明確延後的 per-root schema floor），各自可接受或拒絕並列所需契約決定，風險依實際變更評估；E6 改為逐項接受紀錄（§2 (a)、§4.1 E6、Q9a／Q9b）。**R2-P2-3 同意（已修）**，§5.4 的 Case B 觸發改為「新增無法證明受相同核准控制的 mirror／發布 authority」，映射條件只適用仍依賴 `G:` 的 client／過渡階段，部署變更先重開 A1a 評估再決定修正部署或轉 Case B（§5.4）。**R2-P3-1 同意（已修）**，清除殘留 `C4` 索引（改為 E5 的「不佈建 override」）與「§6」誤指；`proposal :98` 補完整路徑 `docs/architecture/launcher-update-proposal-20260923.md:98`；User-scope 環境變數移入 T4、T5 只列 Machine scope／全機映射／GPO 本機結果覆蓋；Q7 改問各階段代理角色與目標時效，「當日再發布」標為新提案待驗證（§2 T4／T5、§5.3、Q7）。修訂版 2 的 SHA-256 前綴 `66f0492278a7f4f3`，保留為 `evidence/1.2.1/A1a.v2.md`。以上為作者修訂處置，尚非 reviewer 對修訂版 3 的複核結論。

**修訂紀錄（修訂版 2，2026-10-01）**：依據 `evidence/1.2.1/A1a.review.md`（Codex `gpt-6-astra`，verdict reject，P0 0／P1 3／P2 4／P3 1）；8 項 finding 全部同意並修訂：P1-1 分開報告完成／disposition 成立／A1b 准入／候選 package 驗收，解除 A1b 與 R06-02 的循環；P1-2 新增攻擊者能力矩陣 T1–T9；P1-3 分開發布核對與非預期變更偵測；P2-1 SMB 列實際協定與強制點；P2-2 刪除「首次回放需 share 寫入權」與「canary 建立全體高水位」的錯誤敘述，補 direct／manual-pin schema 回讀限制；P2-3 分列撤權四段時效、失陷 client 轉交隔離程序、revision 耗盡處置；P2-4 無 hosting 不撤銷 decision 188 Q3，F14 兩案比較與相對成本；P3-1 風險標 M/R3、引用更正。原稿 SHA-256 前綴 `ade9d535b4ad0c30`，保留為 `evidence/1.2.1/A1a.v1.md`。

- **評估日期：** 2026-10-01。
- **評估者：** Claude Fable 5.1，依任務指定為 A1a 獨立 security evaluator；本回合工具未另回報可驗證的 reasoning-effort 設定。獨立交叉審查：Codex `gpt-6-astra`（原稿 reject；修訂版 2 reject、範圍收窄；修訂版 3 reject、僅餘一項 P1；修訂版 4 accept-with-changes，其唯一 P2（R4-P2-1）已同日原地補正、待確認）。
- **風險分類：** A1a 為 **M/R3** publisher-trust security disposition（`docs/handoff/1.1.14/1.2.x-allocation.md:88`；`docs/handoff/1.2.1/R03-launcher-plan.md:86`）。本文是 owner 作 decision 188 Q4 最終接受的證據，不是接受本身；唯讀審查不執行產品測試，與判定屬 R3 不矛盾。
- **固定來源：** `<worktrees>/e121b`，detached HEAD `d5770e52ee6da60bc26747588cc8951a67b7cc7a`；評估起始 `git -C <worktree> rev-parse HEAD` 為此值，`git status --short` 無輸出。
- **輸入：** owner 部署事實 `evidence/1.2.1/A1a-facts.md`（2026-10-01 四輪 AskUserQuestion 回答）與問卷 `evidence/1.2.1/A1a-questionnaire.md`。owner 回答在本文中視為陳述事實；未回答處列為未知，不以推測補值。
- **驗收依據：** board decision 188 Q3／Q4／Q8（`docs/handoff/1.2.x.md:71–77`、`:83–86`）；accepted plan 的 F17／F18／F19／F20（`docs/handoff/1.2.1/R03-launcher-plan.md:43–46`）、D1／D6／D7（`:63`、`:68`、`:69`、`:73`）、A1a／A1b／A7（`:86–88`）、Q4（`:193`）；accepted R06-01 契約報告修訂版 2 的 §4.2–§4.4、D7 有界限制表、A1–A9 定義與「A1a 的完成輸出」（`evidence/1.2.1/R05-R06-C01.md:94–101`、`:120–128`、`:134–138`、`:206–216`），及其審查確認（`evidence/1.2.1/R05-R06-C01.review.md:115–121`）。
- **方法：** 唯讀靜態核對。對 A1–A9 每項 owner 事實，對照 ADR 0053／0056／0062／0066、`update-source-registry-v1`、`release-manifest-v1`、locator／reader／editor 原始碼，判「接受／不接受／未知」；以攻擊者能力矩陣比較 Case A／B；依 plan `:86` 與 R06-01 `:216` 作條件式 disposition。只用 `git -C … rev-parse`／`status --short`、`sed`／`grep` 定位 `path:line`。
- **排除：** 未建置、未執行測試或 verifier、未連網、未讀 credentials／DPAPI／`.git/config`、未探測任何 share／GPO／稽核／SMB／client；不修改 repository、ADR、契約或 production；不設計 Case B 的簽章方案。Windows SMB／Kerberos／NTLM／磁碟映射／session 與 ticket 的一般行為標示為一般知識，不是 repository 或現場證據。

## 1. 結論

**Disposition（條件式）：建議路線為 Case A（受控 Windows ACL，無 cryptographic publisher authentication），但 A1a 在 `1.2.1` 的本輪尚不能宣稱「成立」。** 本報告完成的是獨立評估、威脅模型能力矩陣、逐項事實判定與補證規格。依 plan `:86`「缺現場權限事實或審核人員時 disposition 為未閉合／NO-GO」與 R06-01 `:216`「不能以問卷填完替代獨立判定」，A1a 要記為「Case A 成立（條件：A1b 現場確認）」，須依序完成：

1. **owner 決定**：對 §2 能力矩陣 T4–T8 逐列表態（特別是 T6 publisher 帳號失竊與 T7 伺服器被外人攻破），逐項接受 §2 的無簽章與 D7 限制 (a)–(f)，回答本文末「owner 需決定的問題」Q1–Q10。若 owner 要求防範 T6 或 T7，Case A 不足，依 decision 188 Q4（plan Q4／O06-trust）轉 Case B。
2. **A1a 補證**（§4.1 E1–E8；owner／IT 提供事實與設定，不需要新 package）。
3. **補證複核**：owner 指定的獨立 security reviewer（非提供證據的 owner／IT、非 R06 實作者；依 `1.2.1` 模式為 Claude Fable 5.1 或 Codex `gpt-6-astra`，Q10）對**具日期／版本**的 E1–E8 證據逐項判接受／不接受／仍未知，出具補證 disposition。本報告的通過不自動推導未來補證通過；本次 reviewer 無法預先認證尚不存在的權限、稽核與 SMB 證據。
4. **owner 最終接受**補證 disposition → A1a 記為「Case A 成立（條件：A1b）」。任一步驟不齊 → A1a 維持未閉合／NO-GO，相依 R06 實作不得開始。

**Gate 順序：**

| 序 | Gate | 版位 | 產出／判據 | 依據 |
| --- | --- | --- | --- | --- |
| 1 | A1a 評估報告（本文）→ 獨立複核 → 送 owner | `1.2.1` | 條件式 disposition、能力矩陣、補證規格 | plan `:86`；R06-01 `:216` |
| 2 | owner 決定 | `1.2.1` | Q1–Q10 回答；T4–T8 與限制 (a)–(f) 的逐項接受／排除紀錄（E6） | decision 188 Q4（`docs/handoff/1.2.x.md:75–77`） |
| 3 | A1a 補證 E1–E8 | `1.2.1` 內或 `1.2.6` 開始前 | 具日期／版本的事實、設定與文件 | plan `:86` |
| 4 | **補證複核** | 同上 | 獨立 security reviewer 對 E1–E8 逐項 disposition（接受／不接受／仍未知）；任何「不接受／仍未知」→ 補證或 NO-GO | plan `:86–88`；R06-01 `:216` |
| 5 | owner 最終接受 | 同上 | A1a 記「Case A 成立（條件：A1b）」 | decision 188 Q4 |
| 6 | A1b 現場確認 | `1.2.6` 開始、早於 R06-02 實作 | §4.2 證據；確認 E1–E5 仍成立、E6–E8 的決定與機制未偏離、部署未偏離；**不檢查尚不存在的 UNC package** | plan `:87`；allocation `:186`；R06-01 `:134` |
| 7 | R06-02 實作與實包驗收 | `1.2.6` | UNC defaults rebuilt package、Registry `catalogPath` 改向、換站／回切 runbook 與演練、F07 表（§4.3） | allocation `:184–185`；R06-01 `:135–137` |
| 8 | A3 staging canary、E7 runbook 計時演練、A7 最終複核 | `1.2.13` | exact candidate 綁本 disposition、補證 disposition 與 A1b 證據重驗；環境或 source 改變即重驗 | plan `:88`、`:151`、`:155` |

**為何建議 Case A 而非 Case B（在 owner 維持「只防外人」且接受 T6／T7 殘餘風險的前提下）：** §2 矩陣顯示簽章相對 ACL 的真實增量只在 T2（傳輸竄改）、T6（publisher 帳號失竊，需 key custody 獨立）、T7（server 被外人攻破）與 T9（不可信 mirror）。T2 由 SMB 認證與 signing 覆蓋（待 E4／A1b 證實）；T9 主案沒有；T6／T7 是 owner 必須明示決定的殘餘風險。簽章對已失陷 client（T4／T5）沒有幫助，對 T6／T7 的事故仍需人工處置。Case B 另需 F19 key custody／rotation／revocation／expiry 與 strict-reader 遷移（plan `:45`、`:167`）。若 owner 接受 T6／T7 為殘餘風險並以 E2／E3／E7 補償，Case A 的成本效益成立；若不接受，Case B 是正確答案，本文不反對。

## 2. 威脅模型：攻擊者能力矩陣

owner 選擇「只防外人」（`evidence/1.2.1/A1a-facts.md:20`），並排除 client 端動手腳（`:25`）。「外人」一詞不區分攻擊者取得的能力，故下表按能力分列；每列標出可改哪些 client 的來源、影響範圍、Case A 既有控制、Case B 的增量與建議處置。owner 須逐列表態（Q1）。

| ID | 攻擊者能力 | 可改向／可寫入 | 影響範圍 | Case A 控制 | Case B 增量 | 建議處置 |
| --- | --- | --- | --- | --- | --- | --- |
| **T1** | 網路外、無任何存取 | 無 | 無 | 不適用 | 無 | 防範（已成立） |
| **T2** | LAN 上有網路位置、無帳號 | 須假冒 share 伺服器或竄改 SMB 流量；可使 primary 或 backup 不可達（可用性／freshness，見限制 (a)） | 所有連該 share 的 client | 依賴 Windows 網域身分對 share 的 SMB 相互驗證與 signing／加密；**產品層無 Kerberos-only 或 SMB 完整性強制**，reader 只做路徑／reparse 檢查後交 OS 開檔 | 簽章可在傳輸被竄改時拒絕 payload | 防範；條件 E4，A1b 以有效連線與拒絕證據證實 |
| **T3** | 竊取一般使用者帳號，未控制 client | 只能讀 share（ACL 只允許 owner 與少數 publisher 寫入） | 無 | share＋NTFS ACL | 無 | 防範（依 A3 事實成立，待 E1 表證實） |
| **T4** | 在某 client 以**一般使用者權限**執行程式 | 可對該 client 設 process scope 或自己的 User scope `NFC_UPDATE_SOURCE_REGISTRY_PATH`（precedence 無管理員檢查）、以 Browse/Confirm 建 manual pin、直接改寫 per-user managed root 的檔案（Setup 以使用者身分、不提權） | 僅該 client | 無；也無法由更新通道補救——攻擊者已可直接替換安裝檔 | 無（簽章驗證在同一失陷 process 內執行） | **明示排除**；owner 須接受單一 client 失陷的殘餘風險，且簽章不能恢復其可信狀態 |
| **T5** | client 本機管理員／elevated 執行 | T4 全部，加 Machine scope 變數、為所有使用者重映射磁碟、覆蓋 GPO 的本機結果 | 僅該 client | 無 | 無 | **明示排除**（同 T4）；E5 的「不佈建 override」只防止「合法佈建」，不防止注入 |
| **T6** | 竊取 publisher 帳號（可寫 share） | 可發布更高 revision、內容自洽的惡意 Registry／Catalog／package | **所有健康線上 client** | runtime 無法與合法發布區分；只有稽核偵測（E2）、撤權與更高 revision 再發布（E7） | **有增量**：若 signing key 由獨立 custody 保管，單憑 publisher 登入不能產生有效簽章；但 key 失竊仍需人工處置 | **owner 決定**：接受殘餘風險（Case A＋E2／E3／MFA 補償）或納入防範（Case B） |
| **T7** | 外人攻破 file server／share 主機（非 IT 內部人） | 可改所有檔案、ACL 與本機 log | 所有 client | 無 | **有增量**：簽章在 client 端驗證，server 失陷不能偽造 | **owner 決定**：是否把 server 視為與網域控制站同級的受信任基礎設施而排除 |
| **T8** | 惡意／被脅迫的 publisher、Registry editor、ACL／GPO 管理者、伺服器／備份管理者 | 全部 | 全部 | 無 | 部分（取決 key custody 是否同一批人） | owner 已排除（facts `:20`） |
| **T9** | 不可信 mirror／跨管理域發布者（無法證明受相同核准控制的發布 authority） | 主案無此來源；既有兩個 Registry replicas 是同一 publication 的副本，不屬此類 | — | compiled-in locator、不搜尋 share | 簽章可容許不可信 mirror | 不在主案；出現即依 decision 188 Q4 轉 Case B（§5.4） |

依據：`src/NvtFwCombiner.Bootstrap/UpdateSourceRegistryLocator.cs:6`、`:14–27`；`src/NvtFwCombiner.Bootstrap/ManagedDistributionLauncherHostServices.cs:215–218`；`docs/adr/0053-fixed-update-source-registry.md:86–91`、`:96–98`、`:131–134`；`docs/adr/0062-first-run-managed-setup.md:285–293`；`docs/contracts/update-source-registry-v1.md:14–19`、`:32–40`、`:219–222`、`:263–266`；`src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemUpdateSourceRegistry.cs:37–52`；`evidence/1.2.1/R05-R06-C01.md:94`、`:99`、`:101`、`:126`、`:211`、`:214`。

**Case A 必須明示接受的限制（owner 接受時逐項表態，記入 E6）：**

- **無 cryptographic publisher authentication。** v1 Registry 與候選資料夾只依賴管理者 ACL；固定 `registryId` 是 scope assertion 不是簽章；`Verified` 只表示 integrity 與 closed-package 檢查通過，不宣稱 publisher 簽章；若 metadata 與 payload 被同一有權者改寫，重算 SHA 不證 publisher 身分（`docs/adr/0053-fixed-update-source-registry.md:96–98`；`docs/contracts/update-source-registry-v1.md:55–60`、`:113–116`、`:237–238`；`SPEC.md:2670–2673`；`evidence/1.2.1/R05-R06-C01.md:99–100`）。
- **D7／ADR 0066 有界限制**（`evidence/1.2.1/R05-R06-C01.md:120–128`；plan `:69`、`:73`）：
  - **(a) 首次 freshness 缺口。** 每個 client 只在**自己**的首次 durable package／source admission 後才有跨 restart 的高水位；之前 `AcceptedRevision == 0` 不檢查 rollback。新 client 接受它**當次**能讀到的最高可接受 publication，runtime 不保證全域最新：primary 不可用而 backup 是合法較舊 publication**只是一個例子**；兩份 replicas 都可讀、都停留在同一合法舊 publication 時，新 client 同樣接受它。**沒有攻擊者改寫 share 的必要條件，也沒有已證明的最大陳舊時間或最低 publication。** staging canary 與 Self-test 不替其他 client 建立高水位——canary 只影響該 client 自己的 state，Self-test 不取 writer lease、不寫 state。若要求「新 client 最低 publication／最大陳舊」，是新的契約決定（Q9a）（`docs/contracts/update-source-registry-v1.md:32–39`、`:105–107`、`:272–275`；`docs/adr/0066-update-catalog-v2-notification-policy.md:115–119`；`VersionManagementExperience.Registry.cs:681`）。
  - **(b) 無 sticky Catalog schema floor。** direct／manual-pinned root 移除 v2 後可回讀 v1，**不需要更高 Registry revision**；Registry 路線另可由 release owner 用更高 revision 明示 v1（R3 決定）。per-root 持久 schema floor 是 ADR 0066 明確延後的契約能力，與 (a) 的 freshness 是**兩個不同決定**（Q9b）（`docs/adr/0066-update-catalog-v2-notification-policy.md:151–160`）。
  - **(c)** 已掌握發布權者（T6／T7／T8）可發布更高 revision 的惡意更新，runtime 不拒絕（R06-01 `:126`）。
  - **(d)** replicas 不提供跨站原子性；接受新 revision 後只剩 stale backup 的 client 拒絕 rollback，可用性下降不能靠降 revision 解決（契約 `:165–169`）。
  - **(e)** 稽核、撤權與再發布不是 runtime 保證；撤權不撤銷已接受／已下載／已安裝 payload；已執行惡意程式的 client 不因安裝更高版本的乾淨 package 而恢復可信（R06-01 `:128`、`:214`；契約 `:125–126`）。
  - **(f) revision 耗盡。** editor 只在 revision **等於** Int64 上限時拒絕遞增，低於上限仍可前進；若有寫入權者把 revision 寫到上限，合法再發布無法「更高」，`registryId` 不可換、manual pin 保留已接受 revision，契約不授權重設高水位。此時不得嘗試自動復原，須升級為另行批准的 recovery／migration 並保留既有 authority。此情境需要 T6／T7／T8 能力（`scripts/edit_update_source_registry.py:648–649`；契約 `:58–60`、`:120–125`；ADR 0053 `:86–88`）。

## 3. 逐項事實評估 A1–A9

判定詞：**接受**＝事實成立即滿足 Case A 對應前提；**不接受**＝現狀不滿足，須改部署或補機制；**未知**＝owner 未答或只部分回答。所有「接受」都是對 owner 陳述的判定，由 E1–E8 補證、補證複核判定、A1b 現場證實。

| ID | owner 事實（`evidence/1.2.1/A1a-facts.md`） | 判定 | 適用 client／所防威脅（§2） | 依據與說明 |
| --- | --- | --- | --- | --- |
| **A1** 環境 | 多數 domain-joined，少數不是（`:16`）；部分完全離線（`:17`）；OS edition 與 SMB 政策未答（`:25`） | **部分接受；SMB 與非網域 client 未知 → E4／E5** | domain-joined 線上 client：Windows identity 對 share 授權，與主案假設一致。T2 的防線完全是 SMB 認證／signing，產品無強制點；domain-joined 不等於該次連線用 Kerberos，須按群列實際協定與允許／拒絕矩陣。非網域 client 按**實際連線機制**分類，不按 join 狀態。離線 client 不在 Case A 內（E5） | plan `:19`、`:63`；R06-01 `:94`、`:206`；`FileSystemUpdateSourceRegistry.cs:37–52` |
| **A2** 拓撲與 `G:` | GPO 映射（`:8`）；一般使用者不能移除／改向（`:9`）；所有 `G:` 原則上同一 share（`:10`）；接受改 UNC 並重發 package（`:15`）；第二站／同步未答 | **接受，條件：UNC defaults（R06-02 產物，§4.3）** | 所有 managed client／T2、T9。現行 compiled-in primary／backup 與 Registry `catalogPath` 都是 `G:` 磁碟代號路徑；reader 不檢查磁碟類型，解析取決於該 logon session 的映射，映射不在 package identity 內、不受 share ACL 保護。GPO 與 share ACL 同一群組管理（`:14`）→ 在 T8 排除下可接受；UNC 可直接移除此層。改 UNC 須 rebuilt、重新識別 package，**是 R06-02 產物，不是 A1b 檢查項** | `UpdateSourceRegistryLocator.cs:7–11`；ADR 0053 `:133–134`、`:193–196`、`:207–210`；契約 `:17–19`、`:27–30`、`:245–247`；`ManagedPathSafety.cs:29`、`:175–176`；R05-R06-C01.review `:26` |
| **A3** ACL 與稽核 | 只有 owner 與少數 publisher 可寫、取代、刪除（`:12`）；有稽核但沒有人看（`:13`）；GPO 與 share 權限同一群組（`:14`）；誰能改 ACL、偵測時效未答（`:25`） | **寫入 ACL：接受（待 E1 表）。稽核：不接受（現狀）→ E2** | 所有 managed client／T3、T6。ADR 0053 把「依賴管理者 ACL」列為 mandatory release/security evidence。Case A 以偵測＋撤權補償無簽章；無人複查等於偵測時效無上界。被竊 publisher 帳號的寫入在 log 中就是 publisher，「非 publisher 寫入告警」不涵蓋 T6；log 若可被同一帳號改動也不可信。須分開「發布核對」與「非預期變更偵測」，量測事件至偵測延遲；最長未偵測窗口 W 由 owner 選定為目標、以能界定它的告警制或巡查制規則支撐、由 A1b 注入量測證明（E2），不是保證 | ADR 0053 `:96–98`、`:111–113`；R06-01 `:128`、`:208–209`；R05-R06-C01.review `:31` |
| **A4** 發布者 | 只有 owner 與少數 publisher（`:12`）；人數與發布證據保存未答（`:25`） | **角色：接受。人數、紀錄：未知 → E3** | 所有 managed client／T6。editor 要求 expected revision、exclusive lock、保留並驗證 security descriptor、locator 鏈拒 reparse、原子替換並遞增 revision；直接手寫不受支援。ACL 無法在技術上阻止 publisher 繞過 editor，故需獨立保存的發布紀錄供比對 | ADR 0053 `:110–116`；契約 `:128–133`、`:162–170`、`:216–217`；`edit_update_source_registry.py:83–87`、`:192`、`:253`、`:638–665` |
| **A5** 現有 client | 手動下載解壓，沒有人用 Launcher（`:19`） | **接受；只簡化「現存 managed client 的過渡清單」** | 首次 rollout／F07。無 managed root → 無帶 `G:` defaults 的 managed source client 需 locator 過渡，首個 managed baseline 可直接用 UNC defaults。**不能取代**：首次 Setup 驗收、未來 App／Launcher 更新與回切邊、新 Distribution Launcher→既有 root 的 identity 邊、`1.2.0` 曾出貨的 `G:`-default Launcher 資產處置、manual-only package 並存。A1b 確認無零星 managed root | plan `:33`；ADR 0062 `:93–106`；`release-manifest-v1.md:22`、`:25`；R06-01 `:53`、`:62`；review `:28` |
| **A6** client 改向面 | 使用者是本機管理員（`:11`）；一般使用者不能移除／改向 `G:`（`:9`）；GPO 同一群組（`:14`）；env var 的 GPO 管控未答；威脅模型排除 client 端動手腳（`:25`） | **接受為 owner 排除項，但須按 T4／T5 分開明示** | T4／T5。process／User scope override 不需管理員；manual pin 是使用者操作；managed root per-user 可寫。排除的理由是攻擊者已可直接改安裝檔，更新通道認證無增量。條件 E6：owner 明示接受單一 client 失陷殘餘風險；E5 的「不佈建 override」只防「合法佈建」 | `UpdateSourceRegistryLocator.cs:14–27`；`ManagedDistributionLauncherHostServices.cs:215–218`；ADR 0053 `:75–80`、`:86–91`、`:135–138`；ADR 0062 `:292–293`；契約 `:263–270` |
| **A7** 換站／replicas／還原／撤站 | 未答（`:25`） | **未知；R06-02 runbook 與演練（§4.3）** | 所有 managed client／可用性與誤回放。契約已定雙 replica 語意與 backup restore 後果（還原較舊快照對已接受新 revision 的 client 是 `RevisionRollback`）；缺操作程序與角色 | 契約 `:32–39`、`:120–121`、`:162–170`；ADR 0053 `:198–205`；plan `:134`；R06-01 `:127`、`:135`、`:212` |
| **A8** HTTPS hosting | 目前沒有（`:18`） | **未知；與 Case A 無關，影響 F14（§5.1）** | F14 的 O2 唯一認證無法在不知 hosting 類型時選定；**無 hosting 不撤銷 decision 188 Q3** | `docs/handoff/1.2.x.md:71–74`、`:83–85`；R06-01 `:95`、`:196`、`:213` |
| **A9** 威脅模型／簽章／撤權／接受者 | 只防外人（`:20`）；無 Authenticode 要求（`:21`）；owner 可當日停發與撤權（`:22`）；owner 最終接受（`:23`）；runbook／演練未答 | **威脅模型：接受為 owner 決定，但須按 §2 細化（Q1）。Authenticode：接受，記 signing disposition（E8）。撤權：「當日」只覆蓋停發／撤權，不含偵測、生效與安全再發布 → E7。接受者：接受** | 全部。F20 維持「有價值但可延後」；release 文件不要求 package signing，但每個 unsigned package 須 release owner 明示核准 | plan `:46`、`:193`；`docs/ci/release-package.md:475`；`release-manifest-v1.md:19`；R06-01 `:128`、`:214` |

**覆蓋範圍：** Case A 的保證只對「以核准身分與完整性保護連 share、未設 override、以 Launcher 安裝、未失陷」的 client 成立。各 client 群是否屬此由 E4 的允許／拒絕矩陣決定；離線 client 不在 Case A 內。

## 4. 條件與順序

### 4.1 A1a 成立前的 owner 補證 E1–E8（不需要新 package）

| # | 需補的事實／設定 | 為何是 A1a 成立前提 | 提供角色 | 補證複核的成立判據 |
| --- | --- | --- | --- | --- |
| **E1** effective-permission 表 | 匿名化、具日期：對兩個 Registry replica、Catalog、`packages`、各父目錄，誰可 read／write／delete-rename-replace／change ACL-owner，含 replication、backup、服務帳號 | A3／A4 只有「owner 與少數 publisher」的陳述；T3／T6／T7 的界線靠這張表 | IT／share 管理群組 | 寫入與 ACL 變更權限限於已列角色；無未列帳號 |
| **E2** 稽核與偵測機制 | (i) 事件範圍：上述路徑的寫入／刪除／改名／取代與 ACL 變更；(ii) log 保存位置、保存期、**誰能改或刪 log（publisher 不得能）**、缺漏偵測；(iii) **發布核對**：每筆稽核寫入事件須在期限內對應到一筆 E3 的獨立核准 publication 紀錄；期限自**該寫入事件的時間 T0** 起算，截止算法與 (iv) 告警制相同：**不論 T0 是否工作日，截止為 T0 日曆日期之後第一個核准工作日的明定截止時刻**；不等待任何「發布完成」時點；**位於發布窗口內不給默認豁免**，無法對應者視為非預期變更並啟動 E7；(iv) **非預期變更偵測與最長未偵測窗口 W**：W 是 **owner 選定的目標**，由 A1b 的注入量測（§4.2，T1−T0）證明後才記「已證明」，**不是保證**。可選兩種能在規格上界定 W 的規則——**告警制**：對上述路徑的任何寫入／刪除／改名／ACL 事件（含 publisher 帳號、含排程內）即時告警給 owner 與代理人；截止規則：**不論 T0 是否工作日，截止為 T0 日曆日期之後第一個核准工作日的明定截止時刻**（T0 落在週六、週日或假日時，同樣取其後第一個工作日的截止時刻，不先把起算日移到下一工作日再往後推一日）；E2 須記錄適用時區、核准的工作日曆與截止時刻（建議：公司當地時區、該工作日 18:00，由 owner 定案）；owner 須同時接受由此推得的最大日曆天數（連假時可達數天）；**巡查制**（平台不支援告警時）：W 以**日曆天**計，規則為「任意兩次**完成**巡查之間的實際間隔（含代理人與假日）不得超過 W」，排程間隔須留餘裕（例如 W＝7 天則每 ≤5 天排一次並由代理人覆蓋假期），**間隔一旦超過 W 即記為窗口違反並開事故紀錄，補巡只是補救而非合規**；無法承諾該 W 時改提可推導的較長 W（例如 10 或 14 日曆天）交 owner 明示接受並如實記錄風險；(v) 複查者與代理人。這是本案擬採的**補償控制**，不是 runtime 保證 | A3 的「不接受」；T6 的偵測依賴；D7 要求偵測時效分「目標」與「已證明」 | owner＋IT | 機制、核對期限與目標 W 由 owner 明示接受；所選規則（告警制或巡查制）能在規格上推得 W；W 在 A1b 以 §4.2 注入量測通過前只是目標，通過後才記「已證明」並寫入 E6 |
| **E3** publisher 名單與發布紀錄 | 角色、人數、核准權（owner）與執行權分離；每次發布記 `registryRevision`、`publishedAtUtc`、Registry／Catalog／package SHA-256、兩站 Self-test 同 revision 確認、執行者；紀錄保存在**與 share 憑證不同的系統**（例如 GitHub release notes，需另一組憑證）；全部經 `scripts/edit_update_source_registry.py`（dry-run → staging Self-test → primary → backup） | E2 的比對基準；T6 下被竊帳號不能同時改紀錄 | owner | 名單確認；至少一筆完整紀錄格式 |
| **E4** SMB 允許／拒絕矩陣 | 按 OS／identity 群各列一行，欄位：核准的認證協定（Kerberos；若某群只能 NTLM，須明列並由 owner 決定允許或歸 E5）、目標身分驗證（相互驗證／SPN）、完整性要求（signing 或加密必須）、client 強制點（例如要求 signing、限制 NTLM 的政策）、server 強制點（要求 signing／加密、拒絕未簽章連線）、alias／DFS 是否存在；覆蓋 Registry、Catalog、packages 目標（同一 share）；非網域 client 以實際機制填入同一矩陣 | T2 的唯一防線；產品層無強制 | IT | 每群有唯一明確的允許／拒絝規則；無法填出者歸 E5「機制未證明而暫歸手動」 |
| **E5** client 分類與 client 端設定 | (i) **owner 主動限縮**：離線 client、owner 決定不支援的群 → 手動 ZIP 發行（附 `SHA256SUMS.txt`），在 A5／F07 標 not managed；(ii) **機制未證明而暫歸手動**：E4 尚無法證明其允許規則的群，待證明後可轉 managed；(iii) production 的 GPO／映像／登入腳本**不佈建** Machine／User scope `NFC_UPDATE_SOURCE_REGISTRY_PATH`，該變數只作 canary／診斷（契約 `:263–266`）；Launcher 本機離線啟動（F01）不變 | Case A 對 (i)(ii) 無保證；(iii) 防「合法佈建」改向 | owner／IT | 分類表與設定摘要 |
| **E6** owner 逐項接受紀錄 | §2 T4／T5 排除、T6／T7 的處置、無簽章、限制 (a)–(f) **逐項**接受／拒絕，附日期；不以 Q1／Q9 的概括回答代替 | decision 188 Q4 的「明示接受」 | owner | 每項有紀錄 |
| **E7** 事故 runbook 文件（計時演練留 `1.2.13`） | 角色含各階段代理人；步驟：偵測（E2）→ 撤銷 publisher 寫入權或暫設 share 唯讀 → 確認撤權**生效**（既有 SMB session、已開啟 handle、Kerberos ticket 在生命週期內可能仍有效——一般知識，須實測）→ 以 editor 用更高 revision 發布乾淨 Registry／Catalog／packages → 兩 replicas readback → 人工收集受影響 client（無 telemetry）→ manual-pin client 需 Resume；**分列**偵測、撤權生效、來源恢復、再發布各自的目標時效（owner 的「當日」只覆蓋停發／撤權；其餘為新提案、待驗證）；失陷 client 轉交組織核准的隔離／重灌程序，不承諾普通更新恢復可信；revision 耗盡時停止自動復原、升級另行批准 | A9 只答停發／撤權；D7／R06-01 `:214` 要求 | owner＋代理人 | 文件存在、角色與各階段時效分列；演練在 §4.4 |
| **E8** signing disposition | Case A＝無 cryptographic publisher authentication、無 Authenticode 要求；每 release 的 unsigned 核准（`release-manifest-v1.md:19`）；roadmap signing 預期依 F17／F18／plan Q4（O06-trust）縮減 | decision 188 Q4 要求 | owner 接受後由 canonical owner 同步 | 文件一致 |

**補證複核（gate 4）：** 獨立 security reviewer 對上表每項依「成立判據」判接受／不接受／仍未知，引用具日期／版本的證據；全部接受 → owner 最終接受（gate 5）→ A1a 記為成立（條件：A1b）。任何「不接受／仍未知」→ 補證或 NO-GO。本報告不預先認證這些尚不存在的證據。

### 4.2 A1b 須以現場證據確認（`1.2.6` 開始、R06-02 實作前）

- E1–E5 的事實抽樣證實（含「所有 `G:` 同一 share」、publisher 人數、無零星 managed root、無佈建 override——以未設 override client 的 Self-test 顯示 production defaults）；E6–E8 的決定未變、runbook 與撤權／再發布機制（editor 可用、publisher 寫入權可撤、紀錄系統可用）仍在。
- **偵測注入測試**（每案記 T0 注入、T1 告警或人工發現、T2 開始處置、T3 撤權生效；**T1−T0 比對 owner 選定的目標 W，T3−T1 比對 E7 處置目標；未偵測、證據缺漏或任一超時均失敗**。W 的截止計算：告警制與發布核對同一算法——不論 T0 是否工作日，截止為 T0 日曆日期之後第一個核准工作日的明定截止時刻（依 E2 記錄的時區、工作日曆與截止時刻，並記錄實際日曆天數）；巡查制以日曆天計，T1 須 ≤ T0＋W；期限一律自 T0 起算，不等待不存在的發布完成時點）：(i) 無排程發布時的一次寫入；(ii) 以 publisher 身分**繞過 editor** 的直接寫入——分「排程外」與「排程內但無對應 E3 紀錄」兩案；(iii) 一次 ACL 變更；(iv) 一次 log 缺漏／刪除嘗試；(v) **巡查剛完成後立即注入**（巡查制的最差情境，驗證下一次完成巡查仍在 T0＋W 內）；(vi) **主複查者缺席／假日**情境，由代理人完成，間隔仍須 ≤ W；(vii) **T0 落在假日**（告警制與發布核對：驗證截止確為 T0 之後第一個核准工作日的截止時刻，且 T1 在其內）。全部通過的 W 值才是「已證明」並寫入 E6；任一未通過則 W 維持目標、E2 不成立。
- **SMB 有效連線與拒絕證據**（匿名化）：每群抽樣 client 對 Registry／Catalog／package 目標（含 alias／DFS）的實際 dialect／認證協定／signed／encrypted，比對 E4 該群的允許規則；負案例：(i) Kerberos 不可用時是否拒絕或降級；(ii) 目標身分不符時是否拒絕；(iii) **所需完整性保護（signing／加密）無法建立時是否必定拒絕**。任一群出現未核准的降級或連線成功 → 該群 E4 不成立，歸 E5(ii) 或重開 A1a。
- 部署自 A1a 補證以來未偏離。
- 任一不成立 → 重開 A1a／decision 188 Q4，不得邊做 ACL 接線邊等最終安全決定（plan `:87`；R06-01 `:134`）。

### 4.3 R06-02 產物（`1.2.6`，A1b 通過後；由 A7 綁候選最終確認）

- compiled-in primary／backup 改 UNC；Registry `catalogPath` 以更高 revision 改 UNC（package path 為 Catalog-relative）；rebuilt、重新識別的 package；若 UNC 為 DFS namespace，驗 reader 的 reparse 檢查不拒絕（`FileSystemUpdateSourceRegistry.cs:41`）。無 managed client 時不需為 managed client 保留舊 locator；`G:` 映射照常供人工使用，兩個名稱解析到同一 share。
- replicas／還原 runbook 與一次換站／回切演練：primary → backup 順序、兩站同 revision 才算完成、backup 還原不得發布低於任何 client 已接受的 revision、一律先 staging（契約 `:128–170`）。
- F07 表（§5.2）。
- 實包驗收：未設 override 的 client 讀到同 revision 的兩個 replicas；override client 驗 staged source（R06-01 `:135–137`）。

### 4.4 A3／A7（`1.2.13`）

- staging canary 只驗 publication／操作流程，不替任何正式 client 建立高水位（§2 限制 (a)）。
- E7 runbook 的計時演練：偵測、撤權生效（含撤權後寫入被拒的實測）、來源恢復、再發布、兩 replicas readback 各記時間，分「目標」與「已證明」。
- A7 以 exact candidate 重驗本 disposition、補證 disposition、A1b 證據與 R06-02 產物。

## 5. 對後續版本的影響

### 5.1 F14：Catalog／package HTTPS 與唯一認證（`1.2.13`）

現況：decision 188 Q3 使 F14 必要，Q8 接受 `1.2.13` 的 HTTPS +4；allocation 記 `1.2.13` 為 9 項 28 單位、系列 428（`docs/handoff/1.2.x.md:71–74`、`:83–85`；`docs/handoff/1.1.14/1.2.x-allocation.md:68`、`:73`、`:75`）。owner 現答「沒有 HTTPS hosting」（A8）。**無 hosting 只證明部署輸入未就緒，不撤銷 Q3；F14 必要性與 28 單位維持到 owner 明示重決。** 完全離線 client 不受益，不代表其他 client 或未來用途不需要。供 Q3 比較的選項：

| 選項 | 內容 | 成本與風險 |
| --- | --- | --- |
| **A：保留必要，設決定時點** | F14 維持必要；owner 在 `1.2.13` 開版前指定 hosting 類型並作 O2 認證選擇（Negotiate／Kerberos-only 或 Entra ID／OAuth，R06-01 `:95`、`:196`） | `1.2.13` 維持 28；若屆時仍無 hosting，F14 無法現場驗收，plan 規則下必要項未滿足即不得 GO，須在該時點再決 |
| **B：明示撤回本輪 HTTPS 能力要求** | 新 decision supersede Q3 的 HTTPS 子項；F14 回「owner 指定 hosting 後才必要」；R06-01 的 F14 契約文字保留為日後設計 | `1.2.13` 28→24、系列 428→424（局部變動，批准後才成立）；日後 hosting 出現需新 decision 並重估（≥ +4，含 TLS／auth／同 origin redirect 負案例） |

建議：維持 A 到 `1.2.13` 開版；若該時點仍無 hosting，再採 B。Case A 本身不需要 HTTPS；現有 HTTPS reader 只讀 Registry、無 credentials、允許跨 origin redirect，屬診斷路徑，不在 Case A production 路徑內（`HttpUpdateSourceRegistry.cs:12`、`:128–140`、`:246–248`）。

**Case B 成本基準：** plan 的 Case A／B 總量 403／423＋G 是固定來源 `e121`（1.1.14 內容）的歷史模型，不含 HTTPS；含 HTTPS 為 407／427＋G（plan `:180–182`）。現行 allocation 基準已是 428（含 F14 +4 與其後新增項）。比較應用**相對增量**：Case B 相對 Case A 約 +20＋G（`1.2.1` +2 契約、`1.2.6` −4、`1.2.13` 技術版 22、`1.2.14` 最終版 24＋G），而非過時絕對總量。

### 5.2 首次 rollout 與 F07

- 無 managed client（A5）只簡化**現存 managed→managed 的過渡清單**（無需 `G:`/UNC 雙 locator 過渡、無 bridge 需求）；R06-03 可維持 M=2（allocation `:185`）。
- F07 仍須宣告並驗證：（i）新 Distribution Launcher（UNC defaults）→ 全新 root：`direct`；（ii）新 Distribution Launcher → 既有 root：descriptor 綁定的 Bootstrap identity 相同為 `direct`，不同為 `refuse`（installer/recovery migration）（ADR 0062 `:102–106`）；（iii）`1.2.0` 曾出貨的 `G:`-default Launcher 資產：在 release notes 與 F07 標為不支援的 managed 起點，immutable Release 不原地修補（`docs/governance/branch-version-and-release-governance.md:114–115`），A1b 確認無由它建立的 root；（iv）manual-only ZIP（含 manifest `1.3`）：not managed，與 Launcher 安裝並存，改用 Launcher 是全新 Setup（`release-manifest-v1.md:22`）；（v）未來 App／Launcher 更新、回切與 exact LKG 邊（F06／F07，plan `:32–33`）。
- 高水位：每個新 client 在自己首次 durable admission 後才有；首次 freshness 缺口（§2 (a)）適用每個新 client，canary 不解決。

### 5.3 事故處置（E7 的依據）

- owner 的「當日」只回答停發／撤權，不外推成偵測、撤權生效與安全再發布都當日完成；runbook 須分列四段時效，演練後才記「已證明」；任何「當日再發布」目標是新提案、待演練驗證。
- 撤權生效：ACL 變更對新開啟的 handle 生效，既有 SMB session／已開啟 handle／有效 Kerberos ticket 在生命週期內可能仍有效（一般知識）；演練須實測撤權後寫入被拒。
- 再發布：editor 自動遞增、要求 expected revision、原子替換（契約 `:128–133`；`edit_update_source_registry.py:638–665`）；乾淨 publication 必須高於惡意 revision，回切也用更高 revision、不能回放舊 Registry（契約 `:120–122`；`docs/architecture/launcher-update-proposal-20260923.md:98`）；兩 replicas readback 後才算完成。
- 失陷 client：安裝更高版本乾淨 package 只是檔案替換，不證明已執行惡意程式的 client 恢復可信；轉交組織核准的隔離／重灌程序。manual-pin client 不自動跟隨 Registry，需 Resume（契約 `:219–222`）。
- revision 耗盡：停止自動復原、升級另行批准的 recovery／migration、保留既有 authority（§2 (f)）。

### 5.4 部署變更與轉 Case B 的處理

- **任何部署或邊界變更先重開 A1a 評估**（依變更後的實際邊界判定），再決定修正部署或轉 Case B；不是每個變更都無條件轉案。
- **直接觸發轉 Case B 的條件：** owner 要求防範 T6 或 T7；**新增無法證明受相同核准控制的 mirror／發布 authority**（同一受控信任域內新增 replica 不屬此類——主案本就有兩個 Registry replicas，`docs/contracts/update-source-registry-v1.md:32–40`；R06-01 `:101` 的條件是信任邊界，不是數量）；E4 證明 SMB 無法對某支援群強制相互驗證與完整性且 owner 不願將該群歸手動。
- **重開 A1a 的條件：** A1b 發現 ACL 比陳述更寬、稽核無法複查或 log 可被 publisher 改動、publisher 人數或紀錄機制改變；對**仍依賴 `G:` 映射的 client／過渡階段**（R06-02 完成前，或若 owner 保留 `G:`-default 資產為支援起點），GPO 不再控制映射。R06-02 完成後 production defaults 與 `catalogPath` 已不依賴映射，映射條件不再適用。
- 轉 Case B 後 trust/schema 契約 M2 移到當時版本，技術版／最終版依 plan `:171–180` 重排。

## owner 需決定的問題

| 問題 | 建議答案 | 依據／取捨 |
| --- | --- | --- |
| **Q1：§2 能力矩陣逐列表態——T4／T5 排除？T6（publisher 帳號失竊）與 T7（server 被外人攻破）是接受為殘餘風險，還是納入防範？** | **T4／T5 排除**（攻擊者已可直接改安裝檔）；**T7 排除**，把 file server 視為與網域控制站同級的受信任基礎設施；**T6 接受為殘餘風險**，以 E2 告警、E3 獨立紀錄與公司 MFA 補償。若 owner 不接受 T6 或 T7 的殘餘風險 → Case B。逐項記入 E6 | §2；Case B 對 T6／T7 有真實增量，對 T4／T5 無 |
| **Q2：稽核由誰複查；選定哪個最長未偵測窗口 W（事件至偵測）作為目標；用告警制或巡查制界定；發布核對期限；log 存在哪裡、誰不能改？** | owner＋一名 publisher 代理；**首選告警制**：任何寫入／ACL 事件即時告警，截止為**不論 T0 是否工作日，T0 日曆日期之後第一個核准工作日的明定截止時刻**（建議公司當地時區、該工作日 18:00；owner 定案並接受推得的最大日曆天數）；**平台不支援告警才用巡查制**：W＝7 日曆天為硬上界，每 ≤5 天排一次並由代理人覆蓋假期，任意兩次完成巡查間隔 >7 天即記窗口違反（補巡是補救不是合規）；做不到就改提 10 或 14 日曆天並明示接受；發布核對期限自寫入事件 T0 起算、同一截止算法；log 保存於 publisher 無修改權的位置，保存 ≥ 90 天（建議值）。**W 在 A1b 注入量測證明前只是目標，不是保證** | E2；A3 現狀不接受；A1b 以 T1−T0 比對 W 判定 |
| **Q3：F14 HTTPS 選 A（保留必要＋決定時點）或 B（明示撤回）？** | **A**；`1.2.13` 開版前仍無 hosting 則改 B | §5.1；無 hosting 不撤銷 Q3 |
| **Q4：publisher 人數、全部經 editor、帳號保護、發布紀錄位置？** | 含 owner ≤ 3 人（建議值）；全部經 editor；publisher 帳號納入 MFA／密碼政策（部署事項）；紀錄放 GitHub release notes 等需另一組憑證的系統 | E3；T6 補償 |
| **Q5：哪些 client 群 owner 主動限縮為手動發行？哪些因機制未證明暫歸手動？** | **主動限縮**：完全離線 client；**暫歸手動**：E4 無法填出允許規則的群（含只能 NTLM 的非網域 client，除非 owner 明示允許 NTLM）；非網域但能以核准 Kerberos 身分連 share 的 client 可留 managed | E4／E5；按機制分類，不按 join 狀態 |
| **Q6：`1.2.0` 的 `G:`-default Launcher 資產如何處置？** | release notes 與 F07 標為不支援的 managed 起點；immutable Release 不原地修補；A1b 確認無由它建立的 root | §5.2；governance `:114–115` |
| **Q7：事故各階段（偵測、撤權生效、來源恢復、再發布）的代理角色與目標時效？** | 每階段指定 owner 不在時的代理人；停發／撤權沿 owner 的「當日」；偵測沿 Q2 窗口；來源恢復與再發布的目標時效由 owner 提出，標為新提案，`1.2.13` 演練後才記已證明 | E7；facts `:22` 只答停發／撤權 |
| **Q8：接受時同步記錄 signing disposition（E8）？** | 是 | decision 188 Q4；`release-manifest-v1.md:19` |
| **Q9a：首次 freshness 缺口（新 client 接受當次可讀的最高合法 publication，無最大陳舊時間或最低 publication）——接受現狀，或要求新契約？** | **接受現狀**（在「只防外人」下，讀到的仍是合法 publication；影響範圍與陳舊程度未經證明，記為已接受的未量化限制）。若拒絕，需新的「新 client 最低 publication／最大陳舊」契約決定，風險依實際變更評估 | §2 (a)；契約 `:32–39`、`:105–107`；ADR 0066 `:115–119` |
| **Q9b：direct／manual-pin root 的 schema 回讀（移除 v2 後可讀 v1）——接受現狀，或要求 per-root 持久 schema floor？** | **接受現狀**（只影響 direct／manual-pin root；Registry 路線保留 revision 高水位）。若拒絕，啟用 ADR 0066 明確延後的 per-root schema floor 契約（含 state 與使用者確認契約），風險依實際變更評估 | §2 (b)；ADR 0066 `:151–160` |
| **Q10：誰執行 E1–E8 的補證複核（gate 4）？** | 依 `1.2.1` 模式指定一位獨立 security reviewer（Claude Fable 5.1 或 Codex `gpt-6-astra`，非提供證據者、非 R06 實作者），另一方交叉確認；owner 據其 disposition 作最終接受 | plan `:86–88`；R06-01 `:216`；`docs/handoff/1.2.1/README.md:10–15` |

## 限制

- **報告狀態：** 本文為修訂版 4，已處理 `evidence/1.2.1/A1a.review3.md` 的 2 項 finding（R3-P1-1、R3-P3-1），並依 `evidence/1.2.1/A1a.review4.md`（accept-with-changes）原地補正 R4-P2-1，待 reviewer 確認；不是 owner 接受、不是 A1a 成立、不是補證複核、不是 A1b／A7，也不是 ADR／契約／schema 變更或實作授權。A1a 成立需 §1 gate 2–5 依序完成；本報告通過不自動推導未來補證通過。
- **事實性質：** A1–A9 是 owner 在對話中的陳述，不是現場證據；「原則上」「少數」「多數」未量化；由補證複核與 A1b 驗證。
- **未探測：** 未讀取或探測任何 share、ACL、GPO、稽核、SMB、session／ticket、client 或 replicas；未執行產品、測試、verifier、建置或 GitHub 操作。
- **一般知識標示：** SMB signing／Kerberos／NTLM、process／User／Machine scope 環境變數、磁碟映射、DFS reparse 可見性、ACL 變更對既有 session／handle／ticket 的生效行為，皆為一般 Windows 知識，非 repository 或現場證據；由 E4／A1b／演練實測。
- **行號有效性：** 所有 `path:line` 對固定 HEAD `d5770e52ee6da60bc26747588cc8951a67b7cc7a` 有效；`1.2.x` 前進後須重新定位。
- **未涵蓋：** Case B 的簽章演算法、key custody、rotation／revocation／expiry 設計；手動下載通道（GitHub Release＋`SHA256SUMS.txt`）本身的信任；`G:` 完整路徑字串未寫入本文，僅以 `path:line` 引用。
- **自檢：** A1a 為 M/R3 security disposition；本文以 `nfc-review` 式唯讀靜態核對自檢引用與結論邊界，不以 R0 文件規則替代 R3 判定。結論為「可送獨立複核與 owner 決定」，不是 integration／release gate。
