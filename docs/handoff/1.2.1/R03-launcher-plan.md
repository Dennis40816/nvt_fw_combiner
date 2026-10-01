# R03 — Launcher 功能必要性與「在 1.2.x 完成」方案

修訂紀錄：2026-09-29 — 依 Claude Fable 5.1 的 accept-with-changes 修訂 P2×2、P3×6；前移 A1、明定 compiled-in locator 換源、補修復版位並重算權重；修訂版待獨立複核。

日期：2026-09-29。狀態：**供 owner 決定的評估提案；未批准、未實作、未整合、未發布。**
來源：`<worktrees>/e121`，detached HEAD `6703e25179cb1112131c108337dbacf232a8da65`（1.1.14 內容）。
評估者依任務指定為 Codex gpt-6-astra、最高 reasoning effort；本次未取得可另行驗證 effort 設定的 runtime 回報。原版已由 Claude Fable 5.1 獨立審查（decision 169），結論 accept-with-changes（P2×2、P3×6，無 P0/P1）；本次修訂不等於已獲修訂版再審通過。
輸入：本報告原版、`R03-launcher-plan.review.md`、已修訂 R03-01.md 與其原版 review，以及指定 roadmap、allocation、inventory、handoff、ADR 與直接相關 owners。

## 結論

**建議以「受控內網的完整 Launcher 交付」為最小目標：A1 的獨立信任邊界判定前移至 1.2.1，1.2.6 在 R06 實作前完成現場證據確認，再做可靠性／換源與 release 管線；1.2.13 完成最終候選驗收與正式 GO。** 主案沿用 compiled-in production locator；Catalog/package 搬站以較高 revision 的 Registry 內容改向，Registry 自身搬站才重建 package 並雙站過渡。必須完成 cold-health 回應、Setup／雙層 READY／exact LKG／journal recovery、相容範圍與安全發布。delta、Launcher-only hotfix、HTTPS package、新簽章／金鑰系統依具體需求決定；repository 拆分與全歷史無人遷移建議刪除。ACL 主案須有 release/security owner 的明示接受及獨立安全證據；A1 不成立就暫停相依工作，調整部署或採 Case B，不能等到 1.2.13 才分岔。簽署案以 1.2.13 為技術版、1.2.14 為最終交付；主案 A4/A5 若發現需修缺陷，也保留 1.2.14 修復／重新凍結版位，未通過不得 GO。所有保留的 Launcher 工作在 1.2.x 閉合，2.0.0 不再保留其未完成範圍。〔E01、E04、E13、E14〕

## 評估邊界與判定方式

- owner 2026-09-29 的 **board decision 186**（Launcher 在 1.2.x 完成，先評估必要性）取代「完整 Launcher 留 2.0.0」的排程；decision 175 的「1.2.1 只盤點／契約／決定」保持有效。186 編號依本次任務及獨立審查提供，本 worktree 的 1.1.14 記錄尚未收錄。搬版本不會自動批准新契約、部署、金鑰操作或刪除權限。
- 這是 R03-02 的擴大決定輸入，沿用 R03～R06、R37、C01 身分；下列 F／A 編號只是本報告索引，不建立第二份 ticket backlog。
- 「必要」有已接受契約、明確 owner 需求、具體故障或發布 gate；「有價值但可延後」指延至 **1.2.x 內的選擇點**。若不採納，就關閉為未納入本次產品範圍，保留理由，不留下 1.4.2／1.5.x／2.0.0 的開放 Launcher TODO。
- 主案的適用範圍是假設：Windows x64、單一實際受控 file／UNC 部署、Windows identity／share 與 NTFS ACL，App package 耦合 Launcher。尚未證明現場條件成立；O06 必須確認，否則本案不能宣稱完成。
- 規模是**剩餘工作**的 S=1、M=2、L=4，相對量而非工期；風險是後續變更風險。本表同一驗收可能跨列，不能逐列相加；唯一可加總帳在排程節。
- 已有 entry、Setup、state／lease、READY、rollback、mutation reconcile、active-attempt guard，不重建。W6-A lifetime／handoff、H3 typed result、redirect notice、R-3 automation 已交付，只保留相關回歸。〔E02、E03〕

## 功能必要性表（29 列）

| ID／功能與原排程 | 誰需要／防止什麼失敗／需求證據 | 判定 | 理由與剩餘缺口 | 刪除或不做的契約／文件影響 | 規模／風險 |
| --- | --- | --- | --- | --- | --- |
| F01 單一入口、健康離線啟動、typed 失敗（R03；原 1.5.3） | 一般使用者需正常開啟；避免網路故障阻斷本機、unknown 誤進 Setup。已接受 ADR 0062。〔E05〕 | 必要 | 已有；補實包零遠端／零完整 inventory、分類與 exit mapping 證據。 | 不可移除入口／離線承諾；延期即未完成。 | M／R2 |
| F02 cold-health（C01；條件式 1.2.6） | cold 啟動可能在 payload projection／state／root 觀察時耗盡 250 ms，擋住 recovery；歷史 262.0998 ms 與 decision 160。〔E06〕 | 必要 | 必須做產品回應與 cold/warm 驗收；不預定改數值，不把受控測試時鐘當修復。 | 取消須保留已知問題且撤回「完整交付」；改 budget 要改 ADR 0062。 | M 政策＋M 回應／R2 |
| F03 首裝 Setup、精確 payload／root、shortcut（R03；原 1.5.2） | 新使用者需首次安裝；避免混合 root、錯 package 或第二套 installer。ADR 0062。〔E07〕 | 必要 | 已有主路徑；補乾淨機、無權限／空間不足／中斷結果；僅修證實缺陷。 | 不能以 portable 手動解壓替代已承諾的 Launcher Setup。 | M／R3 |
| F04 切版 close／handoff、錯誤與 recovery UI（R03/R09；原 1.5.2） | 切版使用者需知道啟動是否失敗；避免晚啟動、重複 Close、把 Started 當 READY。已有 W6-A/H3。〔E02〕 | 必要 | 維持既有 UI／typed owners，補實程序關聯證據；redirect notice 不是 managed-state repair。 | 不重開已完成 UI 功能；刪既有回復會破壞契約。 | M／R2 |
| F05 有界 recovery、install/delete reconcile、active-attempt guards（R03/R05；原 1.5.2/3） | 中斷後重啟者需安全恢復；避免誤清 journal、範圍外刪除或啟第二程序。既有 owners／ADR 0056。〔E08〕 | 必要 | Setup action/plan/lease、install admission、delete 結果、程序 Exited 與 durable clear 各有責任；補實包 fault matrix。 | 不能以泛用 repair engine 取代；沒有故障證據不能閉合。 | L／R3 |
| F06 耦合 Launcher 自更新、巢狀 READY、exact LKG 與保留／刪除（R05-02） | owner 明訂自更新；避免執行中覆寫、雙 journal 推進、刪掉 active/pending owner。ADR 0056。〔E09〕 | 必要 | 狀態機已存在；剩相容接線／實包回復，不重做 updater。Launcher 失敗不代表已 READY 的 App 自動回滾。 | 刪除違反已接受自更新與 rollback 契約。 | L／R3 |
| F07 App／Launcher／Bootstrap／schema 相容矩陣（R05-01、R06-03） | 維運者需知道哪些舊 client 可升級；避免舊 strict reader 讀壞新 state。ADR 0056；1.0.2 有實際 manual 邊界。〔E10〕 | 必要 | 每個宣告支援組合明列直接／bridge／拒絕，測 exact artifact；不推定支援所有歷史版。 | 刪矩陣會使升級承諾無界；必須保留已知 manual 限制。 | M／R3 |
| F08 一次受控 bridge／新 distribution 遷移（R06-03） | 只有宣告支援的舊 client 無法直升時才需要；提案給兩種受控路徑。〔E11〕 | 有價值但可延後 | 先做 F07；需要時採最小橋接或新 root，原 root-bound state 不直接搬。沒有阻斷組合就無需開發 bridge。 | 修改 R06-03「如核准」範圍；不得虛稱全歷史直升。 | L／R3 |
| F09 Launcher-only hotfix（R05-01 待 O06） | 只有 App 不能重發但 Launcher 必須獨立修復才需要；指定資料沒有此實際情境。〔E11〕 | 有價值但可延後 | 建議不納入；可先重發 App package，Launcher identity 仍獨立。 | 現行 ADR 0056 本就沒有獨立 package；刪提案候選不需改既有 runtime 契約。 | L／R3 |
| F10 原位置自動替換 Root Bootstrap／所有舊版無人遷移（額外提案） | 沒有全歷史部署清單或零操作硬需求；ADR 0056 明訂不可變。〔E11〕 | 不必要（建議刪除） | 受控新 distribution 足以作例外處置；泛用自替換增加 trust-anchor 風險。 | 刪「額外選项」承諾，保留 immutable Bootstrap；日後真有需求另准入。 | L／R3 |
| F11 Check／metadata／重試／fallback 流量基準（R04-01） | owner 要省下載；目前候選檢查讀完整 ZIP，可能抵消 delta。程式與已批准評估。〔E12〕 | **Q7≠C 時必要；Q7-C 時縮限** | Q7-A/B 保留真實相鄰 ZIP 的總流量基準；Q7-C 撤 delta 評估，只量 UNC 上 Check 整包讀取成本。本報告未量測。 | 不採 delta 不保留無決策用途的 delta benchmark；維持原 Verified 語意，metadata-only 狀態須另准。 | Q7-A/B：M=2；Q7-C：S=1／R0；改狀態 R2 |
| F12 canonical ZIP delta chunks／full fallback（R04-02；原 1.5.2 重複） | owner 曾提出差異更新；提案明說收益取決於壓縮與 cache，目前無量測。〔E15〕 | 有價值但可延後 | 建議先 full ZIP 完成交付；只有總流量／時間效益達 owner 門檻才做，不能用 chunk reuse 單指標。 | 需 owner 改 allocation 的 R04-02 承諾與 handoff；不能自行把已排項標完成。 | L／R3 |
| F13 持久 cache／chunk resume／去重 lock（R04-02 提案） | 長下載／中斷者可能受益；沒有現場頻率或收益證據。〔E16〕 | 有價值但可延後 | 跟 F12 一起決定；cache 非信任來源，bounded disk／重驗／取消／重試是採納後必要條件。 | 不採 delta 可不建此 subsystem；保留現有 staging，不能削弱 package 驗證。 | M，包含在 R04 估值／R3 |
| F14 Catalog/package HTTPS、一種實際認證（R06-02） | 只有首套 Web storage 才需要；目前僅 Registry 有 HTTPS，Catalog/package 是 filesystem。〔E17〕 | 有價值但可延後 | UNC 足夠則不做；owner 指定 HTTPS 現場後轉必要，含 TLS、auth expiry、redirect 不洩憑證與 typed failure。 | 縮限 R06 接受 transport；不能把 Registry HTTPS 宣稱整包 HTTPS 已支援。 | L／R3 |
| F15 file／UNC 換源與 locator／publisher 邊界（R06-02） | owner 要未來換內網來源；ADR 0053 的 locator、revision/digest 規則已有 owner。〔E04〕 | 必要 | **主案沿用 compiled-in locator**：Catalog/package 複製同 bytes 後以新 Registry revision 改向；Registry 自身搬站要 rebuilt package＋舊新雙站過渡。沿既有 typed owner，不新增外部 locator 配置。 | 可在相同 package identity 下換 Catalog/package 來源；不可承諾更改 production locator 仍維持相同 package identity。外部配置候選交 Q3-C。 | M=2（主案操作／實包驗收）；外部配置 L=4／R3 |
| F16 多種企業認證、HTTP range／進階下載排程（額外擴充） | 沒有第二套認證或高併發需求；提案只要求一種實際 auth，range 可選。〔E16〕 | 不必要（建議刪除） | 第一套已可 full download／安全重試；不建立外掛認證平台或下載服務。 | 關閉額外候選，不移除既有能力；若選 HTTPS，基本錯誤／取消仍必要。 | L／R3 |
| F17 publisher trust／安全邊界閉合（原 1.5.0） | release/security owner 與使用者需知道誰可發布；防止不受控位置被當可信來源。ADR 0053＋roadmap gate。〔E13〕 | 必要 | A1a 於 1.2.1 獨立判定 ACL／操作者／攻擊面與 Case A/B；A1b 於 1.2.6 的 R06 實作前確認實際證據；最後 A7 複核 candidate。**SHA 不等於 publisher 身分驗證。** | 不刪 security closure；主案要明示縮減 roadmap 的 signing 實作預期。證據不足為 NO-GO，不能靠後移審查開工。 | A1 合計 L=4，拆 M+M；最終複核含 A7／R3 |
| F18 Registry/Catalog/transfer publisher 簽章（原 1.5.0） | 跨非同一管理域、可寫 mirror 或需獨立驗證發布者時有必要；目前 v1 接受 ACL；roadmap 已排 signing closure，但具體簽章契約仍是提案。〔E14〕 | 有價值但可延後 | 對主案尚不能證明必要；若 O06 選獨立 publisher trust，轉為必要且 1.2.x 內完成。 | 不採納須更新安全方案、標示「無 cryptographic publisher authentication」；不可對外承諾 signed updates。 | L／R3 |
| F19 金鑰 custody／rotation／expiry／revocation／安全 high-water（原 1.5.0 提案） | **採簽章後**需換金鑰與撤銷被入侵發布者，避免永遠接受舊授權；提案有具體 failure contract。〔E14〕 | 有價值但可延後；簽章案必要 | 與 F18 同進同退；不自製 crypto，不承諾離線即時撤銷；保留既有 Registry revision anti-rollback。 | 主案不新增 key lifecycle；不能把既有 revision 保護一起刪除。 | L／R3 |
| F20 Windows Authenticode／EXE signing（原 signing 候選） | 可能改善企業執行政策／辨識，但未提供強制要求；release 文件的 first-sample v1.0.0 段明載不要求 package signing，當前現場要求待確認。〔E18〕 | 有價值但可延後 | 待現場政策；與 metadata 簽章分開，不能互代。 | 不新增不能等同豁免現行 signing gate；需 release owner 確認適用政策，安全／法律審查仍必須。 | M／R3 |
| F21 publisher 不可變內容、Registry 最後切換、追溯（原 1.5.0/1） | 發布操作者需避免半套來源、同 revision 衝突、錯候選；ADR 0053 原子更新與提案發布順序。〔E19〕 | 必要 | 復用 scripts；驗 content/readback、expected revision、replica conflict 與失敗恢復。無 delta 不產 chunks；無簽章不造 signer。 | 不能取消身份／發布完整性；沒有需要就不重寫 publisher。 | M／R3 |
| F22 R37／R-5 正式 parity release gate（1.2.6） | release owner 防錯 source/run、失敗 compare 被 promote；inventory 已明定。〔E20〕 | 必要（共用發布依賴） | 保留四項與 P-2 依賴，非新 Launcher 功能；R-3 自動起跑已完成。 | 刪除須另改 release 契約，本評估不建議。 | L，拆項合計 14／R3 |
| F23 R-4 re-run recovery／pre-merge gate（decision 181 → 1.2.6） | release owner 處理 merge 後失敗、tag 已建但發布未完；design 有具體事故／狀態路徑。〔E21〕 | 必要（共用發布依賴） | 保留 scoped 剩餘治理／rehearsal，不算成 Launcher recovery engine。 | 不可用新 run 覆寫 immutable release；不能因 Launcher 縮範圍漏掉此分配。 | M 暫估／R2–R3 |
| F24 R-6 CI setup 共用、candidate/Golden 與 CI 重疊（decision 181 → 1.2.6） | owner decision 55 已採低優先加速，有歷史 serial 成本；不是 correctness 前提。〔E22〕 | 有價值但可延後 | 基線維持 1.2.6；**Q8-C 可將 L=4 移到 1.2.11，作為容量槓桿**，不縮 Launcher 必要驗收。 | 移版須 owner 明示修改 decisions 55/181 的本次分配；Golden／同 source 成功 CI gate 保留。 | L 暫估／R3 |
| F25 Catalog/Registry 預備上線與來源回切（原 1.5.1） | 操作者需在真環境證明 package／來源／權限可用；roadmap 明定 production GO 前驗證。〔E23〕 | 必要 | 預設同一受控 share＋staging Registry＋diagnostic override canary client；換站／回切／權限負案例綁候選。回切用新 revision。 | 不以首次正式使用代替 gate；override 只是受控 canary 路徑，不是新增外部 production locator 配置。 | L=4 保留；owner 接受縮限演練範圍後可重估 M=2／R3 |
| F26 乾淨 Windows 完整候選、coverage 與故障驗收（原 1.5.3；R33-04） | 新裝／更新者需真環境可靠性；ADR 0056/0062 與 roadmap 明列，lab 不能取代。〔E24〕 | 必要 | 無額外 .NET/Python；首裝、更新、離線、失敗、重啟、rollback；entrypoint gaps 按行為處置。 | full-suite 或 project reference 不等於此驗收，缺案例就不完成。 | L，分情境計帳／R3 |
| F27 Launcher/publication repository 拆分（原 1.4.2） | 指定來源沒有第二產品／團隊／獨立生命週期需求；roadmap 明說 optional、非先決。〔E25〕 | 不必要（建議刪除） | 1.2.1 記「不拆」理由即可，不安排獨立 extraction 研究或重複 semantic owner。 | 刪 1.4.2 active TODO；保留未來真需求需 ADR／migration／deletion／rollback 的規則。 | S 決定；若拆 L／R2–R3 |
| F28 泛用 Installer/Recovery 擴充：moved-root adoption、自動 repair、uninstall/reset、history UI（舊 deferred 邊界） | 沒有本次必需場景；ADR 0062 列延後，inventory 說 uninstall 未分配。〔E26〕 | 有價值但可延後 | 保留 typed fail-closed、既有精確 recovery；只處理 F03/F05 驗收證實缺陷，不順帶做整套管理工具。 | 在 1.2.1 明記未納入；不冒稱它們原本已排 1.5.2，也不把未知殘留授權刪除。 | L／R3 |
| F29 exact candidate release、Catalog/Registry 明示 GO（原 2.0.0） | owner 要可用交付；roadmap 規定 security/evidence/publication closed 且 actual GO。〔E23〕 | 必要 | GO 移至最後一個 Launcher 1.2.x 版本；若只 acceptance-ready 而無 GO，分開報狀態。 | 改版本不等於部署批准；若 GO 留 2.0.0，不能稱完整 Launcher 已在 1.2.x 發布。 | L／R3 |

## 「Launcher 在 1.2.x 完成」的最小定義

以下適用 O03 採主案後；共 9 個可判定條件。保留的必要項未滿足，就只報「開發完成／驗收待補」，不能報完整完成。

| 條件 | 成功證據 | 失敗／不得算完成 |
| --- | --- | --- |
| D1 範圍與信任有界 | 1.2.1 R06-01→A1a 獨立 security disposition，記實際 Windows／來源／ACL 管理者、支援 client 與 Case A/B；1.2.6 A1b 在 R06 相依實作前確認現場證據，寫入 canonical owner。 | 假設 share 權限、用 SHA 代替 publisher 邊界，或將 A1 NO-GO 留到最終版才處理。 |
| D2 正確 entry 與 cold-health | 真套件 cold/warm 分測 payload/state/root；O07 預算內正確分類；已知 recovery fixture 綁 exact root；健康啟動零遠端／完整 inventory。 | 只暖機通過、逾時後晚啟動、unknown 變 Setup、無 exact root 卻給 mutation session。 |
| D3 首裝與安全內容 | Launcher→Setup→immutable Bootstrap→版本 Launcher→Desktop READY；安裝完整 bytes 與核准 ZIP 一致、來源不改、無額外 runtime。 | 第二 installer、未驗內容、foreign root 被接管、reparse/空間/鎖檔故障被掩蓋。 |
| D4 更新／自更新／刪除保護 | App 先 durable READY，後續啟動才更新耦合 Launcher；outer READY 重讀 exact identity/state；active/pending owner 不刪，LKG 先退休再刪。 | Started 當 READY、掃最新目錄猜 fallback、混合 journal 或錯 admission commit。 |
| D5 中斷與精確回復 | 每個必要交易階段有中斷／重啟證據；install 僅合法 admission 收斂，delete 僅核准結果提交；active guard 先 Exited＋durable clear。 | inventory/save 失敗還清 journal、未確認終止仍 fallback；Bootstrap pending fence 被當自動 reconcile。 |
| D6 有界相容與換源 | 支援組合有直接／一次 bridge／受控新 distribution 結果。主案 compiled-in locator 不變時，以新 revision 的 Registry 將同一 package identity 改向新 Catalog/package 來源；Registry 自身搬站用 rebuilt package＋雙站過渡。auth/outage/revision conflict 保留既有有效狀態。 | 承諾所有舊版只換 URL；混稱 Registry 搬站不需重建；複製 root-bound state；覆寫 immutable Root Bootstrap。 |
| D7 安全與 publisher 閉合 | A1 獨立安全判定與最終 A7 複核；不可變內容先就緒、Registry 最後切；expected revision/readback/回切。**凍結實包具名負案例：較低 Registry revision 回放拒絕、同 revision 異 bytes/digest 拒絕、舊 Catalog 不符當前 Registry publication assertion 拒絕、`deprecated` 不得自動被選。** | 把 unsigned 說 signed；上述負案例未驗或失敗；未取得現場證據卻聲稱通過；將合法較高 Registry revision 指向舊 Catalog 誤稱為既有 runtime 必定拒絕。 |
| D8 frozen candidate 綜合驗收 | clean Windows 首裝／更新／失敗／recovery／rollback、cold 指標與本機離線；R37、適用完整 verifier／Golden／parity、independent R2/R3 review 均綁實際 source/artifact。 | 用 lab probe／test project reference／另一 HEAD 的 pass 代替 exact candidate；required failed/cancelled/skipped 仍發布。 |
| D9 交付與實際 GO | preproduction 通過後，owner 對 exact candidate 與實際 Catalog/Registry activation 明示 GO；最後 1.2.x release／運維回復交接完成。 | 只有排程、打包、asset 已存在或 acceptance-ready，卻宣稱已上線；任何保留 Launcher TODO 留在 1.5.x/2.0.0。 |

D7 的「舊 Catalog」精確指其 bytes/hash、schema 或 latest version 不符目前 Registry 的 publication assertion，並非虛構 Catalog 自有 revision。ADR 0053 :99–104 規範的是 Registry revision；ADR 0066 :151–157 明載 direct/manual-pinned root 無持久 schema floor，且 owner 可用較高 Registry revision 明示舊 Catalog schema。A1 必須接受或否決這項限制；Case A 不能宣稱可抵抗已掌控 ACL/publisher 的攻擊者發布較高 revision。首次接受尚無 durable high-water 時也不能宣稱有歷史回放基準；驗收使用已持久接受較高 revision 的 client。〔E33〕

入口負案例至少區分 `Busy→Busy(12)`、`Damaged→RecoveryRequired(11)`、`StartFailed→LaunchFailed(14)`、`Unavailable→HealthUnavailable(13)`、無效 handoff shape→`TerminationUnconfirmed(17)`。這是 typed／MapExitCode 對照；Setup/Recovery UI 路徑最後由 `App.ConfigureAndRun` 返回，不能把 mapping 當所有情境的最終 process exit。〔E27〕
若採簽章／HTTPS／delta，對應 F14/F18/F19 或 F12/F13 即轉必要：新增同信任 fallback、簽章／回放／撤銷／expiry／rotation 負案例，或 full/cold/warm delta 的完整 ZIP 與 installed-byte 一致性。不能只加入功能而省其故障 gate。

## 必要項排程與容量

### A1 前移與分岔：最早 1.2.1，實作入口 1.2.6

A1 原 L=4 拆成 **A1a M=2＋A1b M=2**，總權重不變，不重複計入 R06-01 或 A7：

| 階段／版位 | 依賴與交付 | 阻擋條件 |
| --- | --- | --- |
| **A1a／1.2.1**：獨立 trust-boundary security disposition，M=2 | R06-01 先列實際部署、來源／locator、ACL 管理與發布者、支援 client；獨立安全審核依 owner 提供的非機密證據判定 Case A 可接受、改部署或採 Case B。涵蓋既有 override、Registry/replica 路徑、publisher、撤站、D7 防回放及限制；若 Q3-C，加入外部配置檔的 ACL／篡改攻擊面。 | 缺現場權限事實或審核人員時 disposition 為未閉合／NO-GO；不得把暫選 A 當接受，不得開始相依的 R06 實作。 |
| **A1b／1.2.6 開始，早於 R06 實作**：現場證據確認，M=2 | 複核 A1a 的實際 ACL、replica／操作者與選定機制仍成立，確認拒絕矩陣與接線驗收前提；Case B 核對其新信任契約與過渡限制，簽章的執行證據仍由技術版產出。 | 變更部署或邊界不符就重開 A1a/Q4，先閉合再做相依工作；本版 release gate 必須帶此結果。 |
| **A7／最後候選版**：最終確認，含既有 A7 L=4 | 核對 A1 disposition、A3～A6 實包／現場證據與 exact candidate；環境或 source 改變即重驗。 | 不是第一次選信任路線，也不能把早期 review 當最終候選安全證據。 |

**為何這是依賴允許的最早位置：** A1a 依賴 1.2.1 的 R06-01 和部署事實，不依賴 P-2、UI 版或尚未寫出的 Launcher 功能；它只產出盤點／契約判定，符合 decision 175。因此不等 1.2.6，更不等 1.2.13 才決定 A/B。A1b 屬相依實作的現場准入與 release 證據確認，放在首個 Launcher/release 版 1.2.6 的入口；A3 的 canary 和最終候選 gate 留後段。這是後續工作的排程，**本次報告修訂沒有執行 A1 安全審核，也沒有授權讀 key 或操作正式來源**。

### 主案 A：沿用受控 file／UNC＋ACL，1.2.13 完整交付

先後關係：1.2.1 R06-01→A1a→Q4 路線閉合；1.2.2 P-2→1.2.6 A1b→核心／來源驗收／R37→1.2.13 A2→A3→A4/A5/A6→A8 文件對齊→A7 最終 review／GO。1.2.3 自動化、1.2.11 coverage 供後段使用。1.2.6 觸及的自更新／來源行為須當版完成 ADR 0056 clean-Windows smoke；A4/A5 是最終凍結候選的完整重跑。

| 版本 | 現行表權重 | 本案權重 | 調整與依賴 |
| --- | ---: | ---: | --- |
| 1.2.1 | 34 | **38** | 原 34＋C01-1 M2＋A1a M2；R06-01 原 M2 寫契約，A1a 是新增獨立審核，沒有重複計帳。Q7-A 為基線。 |
| 1.2.2 | 34 | 34 | R35/R36 維持；R35-08 已交付，**實際剩 33**，兩欄皆同減 1。 |
| 1.2.3 | 28 | 28 | 自動化／測試選擇不變。 |
| 1.2.4 | 28 | 28 | 大檔工作不變。 |
| 1.2.5 | 28 | 28 | Firmware 工作不變。 |
| 1.2.6 | 30 | **32** | 原報告 30＋前移 A1b M2；保留 R-4/R-6。詳見下表與 Q8 容量槓桿。 |
| 1.2.7 | 25 | 25 | UI 項目不變。 |
| 1.2.8 | 38 | 38 | Report／文字不變。 |
| 1.2.9 | 35 | 35 | 共享視覺不變。 |
| 1.2.10 | 32 | 32 | 保留含 R38 的現行值。 |
| 1.2.11 | 32 | 32 | 保留 R33-04／O24；Q8-C 移入 R-6 時為 36。 |
| 1.2.12 | 33 | 29 | R25-01/02 各 M2 移最後整合版；條件式效能全 no-go 時約 17。 |
| **1.2.13（新增）** | — | **24** | A1 的 4 單位已前移；本版只計 A2～A8，最終安全確認含 A7。 |
| 1.2.14（僅修復備援） | — | **未觸發不列入基線；觸發後依 F＋E＋U 估值** | A4/A5 或其他最終 gate 發現需修缺陷時才用，詳見修復規則；不是把修復成本估為 0。 |

主案權重仍為 allocation **377→403（+26）**：原報告 1.2.1/1.2.6/1.2.13 的 36/30/28 改為 **38/32/24**，合計不變；同扣 R35-08 後 **376→402**。1.2.1 的 38 在現行約 25–38 帶寬上緣；1.2.6 的 32 超過 allocation 對 R3 版偏好的 28–30，須 Q8 明示接受或使用槓桿，不能仍稱「守 30」。1.2.13 的 24 低於一般帶寬也不必湊功能。相對量不是日數，所有新估值仍待實際範圍校準。〔E01、E28〕

### 1.2.6 的 32 單位與容量槓桿

| 項目 | 原 → 本案 | 交付與不重複計帳 |
| --- | ---: | --- |
| R04-02 delta | 4 → 0 | Q7-A 尚未達採納條件的基線；Q7-C 直接不納入。若採納，另加技術工作與相應 gate。 |
| R05-02 | 4 → 4 | 既有 App/Launcher fault matrix、journal／active guard／READY／delete protection；F04～F06 共用。 |
| R06-02 | 4 → **2** | **沿用 `UpdateSourceRegistryLocator.ProductionDefaults`**；只做 Registry 內容改向、同 bytes Catalog/package 換站及 rebuilt package 雙站過渡的 runbook／實包驗收，沿 Windows identity 與現有 typed owners。無外部 locator loader、無新 HTTPS reader。規模 M，發布／來源權限風險仍 R3。 |
| R06-03 | 4 → 2 | F07 相容表＋既有直升／受控新 distribution 實證；需要真正 bridge 才回升 L4。 |
| R37-01～04 | 14 → 14 | M+L+L+L；依賴 P-2、O01 與實際權限；已交付 R-3 不加價。 |
| C01-2 | 0 → 2 | 核准的有界回應＋窄 cold/warm 證據。 |
| R-4 | 未計 → 2 | decision 181 已分配，本次 M 暫估。 |
| R-6 | 未計 → 4 | decision 181 已分配，本次 L 暫估；Q8-C 可移版。 |
| A1b | 0 → **2** | 從原最終版 A1 移入；R06 相依實作前確認，不能只在發布前補審。 |
| ADR 0056 clean-Windows smoke | **含既有項目，另增 0** | R05-02／R06-02 觸及的 startup、update、rollback、offline、delete protection 在當版 exact package 上驗證；費用含各包與 release gates，不能全延至 A4/A5。 |
| **合計** | **30 → 32** | 30−4−2−2＋2＋2＋4＋2=32。 |

主案換源的兩條路徑必須分開驗收：

1. **只搬 Catalog/package：** compiled-in Registry primary/backup 不變，複製同一 ZIP／Catalog 並核 hash，以較高 Registry revision 改向；回切也用更高 revision。package identity 可維持不變。
2. **Registry 本身搬站：** production defaults 是 package identity 的一部分；建立含新 defaults 的 rebuilt package，舊 Registry／舊來源必須仍能把支援的舊 client 帶到該 package，保留新舊站直到矩陣內 client 完成過渡，才退役舊站。這不等於原位置替換 immutable Root Bootstrap；有不相容就由 R06-03 給受控新 distribution。若舊站已無法服務，不能保證零操作遷移。既有 CLI/env override 可供診斷／canary，不冒充新的 production 外部配置契約。〔E04〕

| 變化（各列相對上述基線，除註明可疊加） | 版本權重影響 | 系列總量影響 |
| --- | --- | --- |
| Q7-C：撤 delta 評估，F11 M2→S1 | 1.2.1：38→37 | 403→402（扣 R35-08 後 401） |
| Q3-C：新增外部 locator 配置 | R06-02 M2→L4；1.2.6：32→34；A1 納入配置檔 ACL／篡改面 | +2→405；HTTPS 等另計，不把新信任輸入藏在 M |
| R06-03 證實需 bridge，單獨回升 L4 | 1.2.6：32→34 | +2→405 |
| R06-02/03 均回升 L4 | 1.2.6：32→36 | +4→407 |
| **Q8-C：R-6 L4 移到 1.2.11** | 基線 1.2.6：32→28；1.2.11：32→36。若一個 R06 回升 L，1.2.6 為 **30**；兩個均回升則 **32** | 只移版，總量不變；需明改 decisions 55/181，不能宣稱兩項回升後仍守 30 |
| Q8 接受 A3 的縮限演練估為 M2 | 最終候選版 24→22 | −2；預設保留 L4，須先證明場景不減、操作成本確實縮小 |

### 1.2.13 的 24 單位與缺陷修復版位

| 包／規模 | 承接來源／內容 | 順序與可驗收產物 |
| --- | --- | --- |
| A1（本版另計 0） | F17；M2 已在 1.2.1、M2 已在 1.2.6 | 本版 A7 依凍結候選複核既有 disposition 與新增證據；不重算整個 A1。 |
| A2 M=2 | 原 1.5.0/1 publisher 操作完整性，F21 | 復用 scripts，驗 immutable content/readback/expected revision/replica conflict 與 runbook；主案無新 signer/chunks。 |
| A3 L=4 | 原 1.5.1 controlled preproduction，F25 | A1/A2＋R37→**同一受控 share＋staging Registry＋diagnostic override canary client**。L 是換站／更高 revision 回切／權限拒絕／故障與回讀證據的工作，不是預設搭獨立環境；owner 接受相同覆蓋可降 M2。另以未設 override 的 client 驗 production defaults，避免 canary 掩蓋正式 locator 錯誤。 |
| A4 L=4 | 原 1.5.3 clean Windows 正常路徑，F01/F03/F26 | A3→在**最終凍結候選完整重跑**無額外 .NET/Python 的首裝、App 更新、Launcher 下次啟用、離線、shortcut/UI；不取代 1.2.6 smoke。 |
| A5 L=4 | 原 1.5.2/3 integrated 故障路徑，F05/F06/F26 | **最終凍結候選完整重跑** 1.2.6 fault matrix：journal、保存失敗、guard、錯 root、並行、disk/AV/lock、READY/LKG；另具名驗較低 Registry revision 回放、同 revision 異 bytes/digest、舊 Catalog 與目前 publication assertion 不符、`deprecated` 不自動選。依 D7 的既有能力邊界，不新增 Catalog revision。 |
| A6 M=2 | 原 1.5.3 cold 性能與環境證據，F02 | 最終套件 cold/warm P50/P95/P99、硬預算與環境；不是把早期窄驗證重標 fresh pass。 |
| A7 L=4 | 原 2.0.0 release／activation，F29 | A3～A6 及 A8 通過→最終安全複核、exact-source gates／Golden／parity、獨立 review、release/security owner 核准、正式 GO／activation 回讀及交接。 |
| A8 M+M=4 | R25-01/02 自 1.2.12 移入 | 最終行為凍結後對齊 current SPEC／handoff／retained TODO；在 A7 GO 前閉合。 |
| **合計** | **2＋4＋4＋4＋2＋4＋4＝24** | A1 已前移，不是取消安全 gate。 |

**1.2.13 仍有必要：** A1 前移後仍剩 20 單位 publisher／preproduction／完整候選／GO，加移入最終文件 4，共 24；若全部併回原 1.2.12，會是 33＋20＝53，而不是目前重排後的 29。這些工作依賴前面實作與 coverage 收斂，需要同一 final candidate；不以另加無需求功能補到 25。

**Case A 修復路徑：** A4/A5（或任一必要 gate）發現需修缺陷，1.2.13 不得帶缺陷 GO；先記 finding 與 exact source，**觸發 1.2.14 修復／重新凍結／重驗版位**。只修已證實且授權的問題；A7、A8 未完成部分隨最後候選移入，受影響 A2～A6 重驗，且 actual candidate 的必需 release／Golden／parity gate 仍完整執行。不能改包後沿用 1.2.13 原 pass。若修正可在 1.2.13 尚未發布的候選階段完成，也必須重凍結及重驗，並重估該版容量；不得覆寫已發布資產。

條件式 1.2.14 權重為 **F（具體修復）＋E（重驗新增工作）＋U（從 1.2.13 移入的未完成工作）**；每個 finding 出現後才按 S/M/L 展開。系列總量是 **403＋F＋E**，U 只移版不重算，未採納選項另依敏感度表調整。這是一個明確版位與估值方法，不是固定 0 成本，也不是預先保證一次修復就能 GO。

### 條件式案 B：新增 publisher 簽署信任，1.2.14 完整交付

A1a 在 1.2.1 就選出新 trust 路線；契約先定，不能先做完 ACL-only 的 R06 接線再到 1.2.13 推翻。選 Case B 不代表已批准 crypto 設計；實際演算法／custody／rotation／revocation／expiry 與 strict-reader 遷移仍由相應 owner 閉合。

| 版本 | 相對主案的調整 | Case B 權重 |
| --- | --- | ---: |
| **1.2.1** | A1a 選路後加 trust/schema 契約 M2（原報告放在 1.2.13）；若未閉合不做相依實作。高於一般帶寬須 Q8 明示接受，不能因「契約」而藏成本。 | **40** |
| **1.2.6** | 主案 32−R06-02 M2−R06-03 M2＝28；A1b、核心可靠性、R37/R-4/R-6 與當版 smoke 保留。信任敏感的來源驗收和相容遷移留新 trust 技術版，避免重做。 | **28** |
| 1.2.2～1.2.12 其餘版本 | 與主案各版相同，R25-01/02 仍隨最終版。 | 同主案 |
| **1.2.13 技術版** | client 簽章驗證 L4＋rotation/revocation/expiry/high-water L4＋publisher signing/custody L4＋有界相容／受控遷移 L4（bridge 僅在證實不相容時實作）＋早期安全負案例 L4＋移入 R06-02 來源操作驗收 M2。R06-03 的 M2 被有界遷移 L4 取代，不再另加。 | **22** |
| **1.2.14 最終版** | A2～A8 移來；A7 複核新 trust，A3～A6 涵蓋採納的新功能。 | **24 暫估下限，新增驗收差額 G 待定** |
| 另選 HTTPS | 技術版＋Catalog/package HTTPS／一種實際 auth L4；最終 auth/TLS 負案例差額入 G。 | 1.2.13：**26** |
| 再選 delta | 技術版加 R04-02 L4，含基本 cache/resume/publisher；新簽署 schema／publisher 先於端到端 delta；最終驗收差額入 G。 | 無／有 HTTPS：**26／30** |
| 最終 gate 發現需修缺陷 | 保留 **1.2.15** 修復／重新凍結版位，依 F＋E＋U 重估，A7 GO 與文件收尾隨行。 | 未觸發不列；不保證 1.2.14 帶缺陷完成 |

Case B 全系列基礎帳為 **403＋2−4＋22＝423**（扣已交付 R35-08 後 **422**）；另加 G，不把新功能的最終驗收增量估為已確認的零。含 HTTPS 為 **427＋G**，再含 delta 為 **431＋G**；只加 delta 也是 **427＋G**。R06 外部配置若採納，移入技術版的 M2→L4 再＋2。若再採 Launcher-only hotfix／更廣 bridge，需獨立估值並可能用 1.2.15，不能塞入既有 L4。

只選 HTTPS、不選簽章時仍與 Q4 分開：主案 1.2.13 先加 HTTPS/auth L4，24→**28**（系列 **407＋最終驗收差額**），來源／安全接線完成才凍結 A3～A7。只選 delta 同為技術估值＋4，但須先閉合 transfer metadata 信任契約：原提案要求 signed manifest，不能假定 ACL 相容或把 signer 成本藏在 L4；維持 signed 要求就用 Case B。若功能實作與完整候選驗收無法同版收斂，拆為 1.2.13 技術＋1.2.14 最終，重新計權重，不能降 gate。

**完成版位判定：** Case A 無阻擋缺陷以 **1.2.13** 為目標，**1.2.14 是修復備援而非固定必需版**；Case B 的 **1.2.13 技術／1.2.14 最終版** 有明確相依理由，另以 1.2.15 作缺陷或擴大範圍的條件式版位。1.2.14／1.2.15 不因預留而自動建立或取得發布權限；任何新 protocol/schema 的 immutable Bootstrap 不相容都仍需受控遷移。

## Owner 決定題（9 題；沿用 O 編號，後綴僅區分本報告子題）

| 問題／O 編號 | 選項 | 建議與決定後影響 |
| --- | --- | --- |
| Q1／O03：什麼算 Launcher 完成，首 tranche 如何改？ | A：採必要項，1.2.1 先選信任路線，1.2.6 核心／來源／release，主案 1.2.13 完成並保留修復版位；B：把 delta／hotfix／新信任一起列必要，接受重估與額外技術版；C：僅 acceptance-ready，GO 另指定 1.2.x 版。 | **A**；信任／transport 分由 Q3/Q4 確認。批准 F10/F16/F27 刪除、F28 不納本次及後段 TODO disposition；簽署案目標 1.2.14。未過 gate／未 GO 不稱完整交付。 |
| Q2／O07：cold-health 用哪個政策、何時閉合？ | A：保留 250 ms，先真套件量測後有界減工，1.2.6；B：量測後批准有總上限的分段 observation，health cutoff 與 progress delay 分開定義；C：保留現況，修正延至最後 1.2.x 驗收前。 | **先 A；若無法同時滿足正確分類與預算，再提 B，不自動放寬。** C 不關 bug、不允許跳過最終 D2。B 須解耦 `DefaultHealthObservationDeadline = ProgressDelay`、同步 ADR 0062；目前沒有新 ms 值可推薦。 |
| Q3／O06-transport/locator：首套來源與換源機制？ | A：file/UNC＋Windows identity／ACL，沿用 compiled-in production locator；B：Catalog/package HTTPS＋一種指定認證，locator 仍 compiled-in；C：新增受控外部 locator 配置，並指明 A 或 B 的 transport。 | **A**：Registry 內容改向；Registry 本身搬站則 rebuilt package＋雙站過渡。C 是新配置輸入與 loader 契約，R06-02 M2→L4／R3，A1 加配置檔 ACL／篡改面；若 transport 同 B，HTTPS L4 另外算。B 使 F14 必要，不把既有 Registry HTTPS reader 算成整包支援。Q4 信任問題獨立。 |
| Q4／O06-trust：完整交付是否要求獨立 publisher 簽章？ | A：受控 ACL，明示無 cryptographic publisher authentication，接受 D7/ADR 0066 的有界限制；B：新增 Registry/Catalog 簽章與最小 rotation/revocation/expiry/high-water；C：現場證據不足，先補證據／改部署再選 A/B。 | **A 僅在 1.2.1 A1a 獨立審核成立後採用**；1.2.6 A1b 在 R06 實作前再確認。這須明示縮減原 signing 實作預期；不能證明 ACL 或需抵抗不可信 mirror 時走 C/B，不能沿假設開工。B 的契約前移 1.2.1，技術 1.2.13、最終 1.2.14。Authenticode 現場是否強制一併記 signing disposition。 |
| Q5／O06-hotfix：需要 Launcher-only package 嗎？ | A：仍以 App package 附帶 Launcher；B：提供具體獨立 hotfix 場景，批准新的 ownership／retention／rollback。 | **A**；目前無不能重發 App package 的證據。B 必須先完成 R05-01 延伸與重新估量，不能把它塞進既有 R05-02 基礎狀態機。 |
| Q6／O06-bridge：舊 client 的保證界線？ | A：明列支援清單，能直升就直升，必要時接受一次受控新 distribution／bridge；B：要求指定舊版原位置無人遷移。 | **A**；F07 必做，bridge 實作由具體不相容決定。B 必須提供版本／protocol/schema 與部署證據，且仍不能把普通更新變成 Bootstrap 自覆寫。 |
| Q7／O03-delta（R04）：差異傳輸是否仍列必要？ | A：R04-01 後，總流量／時間收益達 owner 指定門檻才做，未達撤 R04-02；B：不論效益均做；C：直接 full ZIP、撤 delta。 | **A**；基線未採納 delta，但在 1.2.1 閉合 go/no-go，不能留到系列尾端。A/B 的 F11 M2 保留；C 時 F11 只量 Check 整包成本 S1，1.2.1 38→37。門檻含 Check/metadata/retry/fallback、cold/warm cache、儲存成本；採納的技術／信任與最終驗收增量按上表加入。 |
| Q8／O03/O30/O18：新增版、容量與收尾搬移如何選？ | A：接受主案 38/32/24（1.2.1/1.2.6/1.2.13）及簽署案 40/28/22/24＋G，R25-01/02 隨最後版；B：堅持到 1.2.12，明列挪出的既有工作與重新估量；C：沿 A 的版位，但將 R-6 L4 從 1.2.6 延到 1.2.11。 | **A**，不默改 R-6 已定排程；容量吃緊可選 **C**，須明改 decisions 55/181。C 下主案 1.2.6＝28、單一 R06 回升 L 時＝30、兩项均回升時＝32，1.2.11＝36。B 若全併需約 53，不能減 mandatory evidence。主案缺陷備援 1.2.14、簽署案備援 1.2.15 均依 F/E/U 重估；A3 若同覆蓋且較省可另核 M2，最後版再減 2。 |
| Q9／O01/O30（連 decision 47）：2.0.0 如何定位？ | A：以現有 terminal certification／跨版本契約關卡為定位，具體執行／retain-retire 決定留 2.0.0 planning；B：現在另啟 firmware/release owner 的 terminal 義務退役／替代提案。 | **A**；Launcher 不再是主題，但 terminal gate 保持。不能因移走 Launcher 或改版號就豁免 2.x chain；本次不順帶執行 B。 |

O07 要填的量測條件：實際 package/source、支援 OS／CPU／storage／AV、cold 定義、樣本數、每次硬預算與 P95 統計方法；health observation 與 5.5 s START/ADMITTED admission、45 s completion、各 READY deadline 分開，取消／cleanup 邊界也分開。〔E29〕

## 2.0.0 剩下什麼

**建議定位為 firmware 證據與重大契約的發布關卡，不再是 Launcher 發布版，也不新增無需求的「全面重構」。**
- 有明確既有義務：ADR 0057 terminal 64-route v0.9.16 certification、candidate plan／parser／workflow 的適用性，以及 decision 47 的 retain／retire 決定。現行 release policy 從 2.0.0 起要求成功 terminal chain；rolling comparator 是額外 gate，不能代替。〔E30〕
- 既有缺口／再審：27 routes 的 canonical-input disposition，及 ADR 0078 留給 2.0.0 的 cascade binding 再審；應消費 1.4.x 的獨立 evidence，不由 Launcher 成功推導 firmware support。這些數目是來源記錄，非本次重新清查。〔E31〕
- 若後來正式退役 terminal 義務且無其他重大產品契約，不為填版號虛構功能；owner 可屆時重定 2.0.0。現在不取消任何 gate，不把 General authoring 1.3.x 或 IC evidence 1.4.x 搬過來湊版。

## 對後續版本與 canonical 文件的輸入

| 版本／原項目 | 決定後應消費的輸入／同步位置 |
| --- | --- |
| 1.2.1 R03-02、R05-01、R06-01、C01-1、A1a | necessity/disposition、Q1～Q7、D1～D9，board decision 186；A1a 的 Case A/B 安全判定先閉合。Case B 加 trust/schema M2；只產清單／契約／決定，不當 accepted ADR 或已實作。 |
| 1.2.1 R04-01 | Q7-A/B 全流量基準與效益門檻；Q7-C 縮為 Check 成本 S1。沒有樣本不聲稱節省。 |
| 1.2.2 R35/R36 → 1.2.6 R37 | 原比較器與正式報告依賴保留；R37 的 hosted／staging 不與 Launcher clean-Windows 混為同一證據。 |
| 1.2.6 R05-02、R06-02/03、C01-2、R37、R-4/R-6、A1b | 主案 32 單位；A1b 先於相依 R06 實作，compiled-in locator 機制與 ADR 0056 當版 clean-Windows smoke 明列。Case B 為 28，R06 的來源／遷移驗收移技術版。 |
| 1.2.11 R33-04／O24 | 修正「Launcher 沒有 test reference」的過時前提；保留 entrypoint／真程序行為缺口，不由 full fallback 推成 coverage。 |
| 1.2.12 → 最後 1.2.x R25-01/02 | 將 current/spec 與 active handoff 對齊移到最後，保留歷史證據；其他 cleanup 不擴 scope。 |
| 原 1.4.2 | 本次在 R03-02 記「不拆」disposition；刪 active extraction 版位，非宣告已實作 extraction。 |
| 原 1.5.0 | A1 分到 1.2.1/1.2.6；A2 與 A7 最後閉合。signing 不採納要有 Q4 disposition；採納則契約在 1.2.1、技術在 1.2.13，不等系列末才選路。 |
| 原 1.5.1 | A3→最後候選版；同受控 share/staging Registry/diagnostic override canary＋正式 defaults 驗證，真權限／換站／回切。 |
| 原 1.5.2 | 必要故障修正併 R05/C01/A5；delta 只在 Q7 採納時入 1.2.x；未知「refinements」不得當無限工單。 |
| 原 1.5.3／2.0.0 Launcher | A4～A8 的最終凍結候選；主案 1.2.13／簽署案 1.2.14，缺陷修復依次後移且重新估量，GO 不先行。2.0.0 非 Launcher terminal authority 保留。 |

批准後由 canonical owners 同步：roadmap current rows 與有現行效力的 work packages、allocation、inventory R03/R04/R05/R06/C01、v1.2.1 handoff／Launcher proposal，均標 board decision 186 及本次 owner 最終選項。proposal :91–99 的外部 locator／signed Registry 構想須明示本案不採納或依 Q3-C/Q4-B 才啟用；不能留成主案默認契約。改 budget 才改 ADR 0062，改 trust/schema/獨立包/外部 locator 才更新相應 ADR 0053/0056 與 Registry/Catalog/release contracts。Q8-C 須明改 decisions 55/181 的排程；dated decisions 保留原文，由新決定 supersede。本次均未改 repo 文件。〔E01、E32〕

## 抽樣證據（每個論點最多三個代表定位）

Repo 路徑相對固定 HEAD；external 為本次指定輸入。程式／tests 存在不等於本次已執行。對 R03-01 的已交付清單採其修訂與原 review，並親讀關鍵 health、transport、reconcile、guard、fence owners；未重做全庫盤點。

| 證據／論點 | path:line |
| --- | --- |
| E01 排程權威與權重 | `docs/handoff/1.1.14/1.2.x-allocation.md:13`；`docs/handoff/1.1.14/1.2.x-allocation.md:51`；`docs/handoff/1.1.12.md:1059` |
| E02 已有能力／六項 review 修訂 | `evidence/1.2.1/R03-01.md:26`；`evidence/1.2.1/R03-01.md:140`；`evidence/1.2.1/R03-01.review.md:62` |
| E03 R-3 已整合，並非免核准 | `docs/handoff/1.1.13/DESIGN-release-workflow-cleanup.md:17`；`docs/handoff/1.1.12.md:1012` |
| E04 來源換移、既有信任與 compiled-in locator | `docs/adr/0053-fixed-update-source-registry.md:96–98`；`docs/adr/0053-fixed-update-source-registry.md:125–139`；`src/NvtFwCombiner.Bootstrap/UpdateSourceRegistryLocator.cs:7` |
| E05 entry 分類／零遠端 | `docs/adr/0062-first-run-managed-setup.md:74`；`docs/adr/0062-first-run-managed-setup.md:138` |
| E06 cold 故障與延期 | `docs/handoff/bugs/BUG-20260928-b2-joint-recovery-entry-health-unavailable.md:44`；`docs/handoff/1.1.12.md:969`；`src/NvtFwCombiner.VersionManagement.Application/VersionManagement/ManagedLauncherEntry.cs:528` |
| E07 Setup 與既有 materializer | `docs/adr/0062-first-run-managed-setup.md:336`；`docs/adr/0056-rollback-safe-launcher-self-update.md:170`；`evidence/1.2.1/R03-01.md:27` |
| E08 recovery 三種 authority | `evidence/1.2.1/R03-01.md:30`；`src/NvtFwCombiner.VersionManagement.Application/VersionManagement/VersionManagementExperience.Recovery.cs:5`；`src/NvtFwCombiner.VersionManagement.Application/VersionManagement/LauncherBootstrapCoordinator.ActiveAttemptRecovery.cs:23` |
| E09 既有自更新／失敗／刪除規則 | `docs/adr/0056-rollback-safe-launcher-self-update.md:40`；`docs/adr/0056-rollback-safe-launcher-self-update.md:119`；`docs/adr/0056-rollback-safe-launcher-self-update.md:184` |
| E10 相容與實際 manual 邊界 | `docs/adr/0056-rollback-safe-launcher-self-update.md:96`；`docs/handoff/1.1.14/1.2.x-inventory.md:183`；`docs/ci/release-package.md:435` |
| E11 hotfix／bridge／Root 更新只是條件式提案 | `docs/architecture/launcher-update-proposal-20260923.md:70`；`docs/architecture/launcher-update-proposal-20260923.md:75`；`docs/adr/0056-rollback-safe-launcher-self-update.md:28` |
| E12 Check 整包讀取與例外 | `src/NvtFwCombiner.VersionManagement.Application/VersionManagement/VersionManagementExperience.Registry.cs:446`；`src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedVersionRepository.cs:157`；`docs/adr/0066-update-catalog-v2-notification-policy.md:91` |
| E13 安全閉合必要，但 v1 明示無簽章 authority | `docs/adr/0053-fixed-update-source-registry.md:96`；`docs/architecture/nfc_roadmap.md:948` |
| E14 簽章與 lifecycle 提案／實際信任限制 | `docs/architecture/launcher-update-proposal-20260923.md:3`；`docs/architecture/launcher-update-proposal-20260923.md:109`；`docs/architecture/launcher-update-proposal-20260923.md:115` |
| E15 delta 條件、完整 fallback／相同 ZIP | `docs/architecture/launcher-update-proposal-20260923.md:35`；`docs/architecture/launcher-update-proposal-20260923.md:46`；`docs/handoff/1.1.14/1.2.x-inventory.md:178` |
| E16 cache／resume／range／單一 auth | `docs/architecture/launcher-update-proposal-20260923.md:85`；`docs/architecture/launcher-update-proposal-20260923.md:103`；`docs/architecture/launcher-update-proposal-20260923.md:140` |
| E17 Registry HTTPS 不等於 Catalog/package HTTPS | `src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/HttpUpdateSourceRegistry.cs:8`；`src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemUpdateCatalogSource.cs:38`；`src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/FileSystemManagedVersionRepository.cs:166` |
| E18 first-sample package signing 記錄；Authenticode 不替代 metadata | `docs/ci/release-package.md:475`；`docs/architecture/launcher-update-proposal-20260923.md:114` |
| E19 publisher revision／不可變順序 | `docs/adr/0053-fixed-update-source-registry.md:99`；`docs/architecture/launcher-update-proposal-20260923.md:120` |
| E20 R37 四項及失敗 gate | `docs/handoff/1.1.14/1.2.x-inventory.md:373`；`docs/handoff/1.1.14/1.2.x-allocation.md:126` |
| E21 R-4 內容／新分配 | `docs/handoff/1.1.13/DESIGN-release-workflow-cleanup.md:291`；`docs/handoff/1.1.13/DESIGN-release-workflow-cleanup.md:318`；`docs/handoff/1.1.12.md:1110` |
| E22 R-6 是已採低優先 speed batch | `docs/handoff/1.1.13/DESIGN-release-workflow-cleanup.md:381`；`docs/handoff/1.1.13/DESIGN-release-workflow-cleanup.md:415`；`docs/handoff/1.1.12.md:1115` |
| E23 preproduction／explicit GO | `docs/architecture/nfc_roadmap.md:379`；`docs/architecture/nfc_roadmap.md:959` |
| E24 clean-machine 不能被 lab 取代 | `docs/adr/0056-rollback-safe-launcher-self-update.md:245`；`docs/adr/0062-first-run-managed-setup.md:436`；`docs/ci/release-package.md:59` |
| E25 extraction 非必要 | `docs/architecture/nfc_roadmap.md:377`；`docs/architecture/nfc_roadmap.md:1064` |
| E26 延後 recovery 擴充／uninstall 未排 | `docs/adr/0062-first-run-managed-setup.md:419`；`docs/handoff/1.1.14/1.2.x-inventory.md:146` |
| E27 handoff typed 與 process exit/UI 分流 | `src/NvtFwCombiner.VersionManagement.Application/VersionManagement/ManagedLauncherEntry.cs:565`；`src/NvtFwCombiner.DistributionLauncher/Program.cs:40`；`src/NvtFwCombiner.DistributionLauncher/Program.cs:63` |
| E28 容量修正／文件收尾 | `docs/handoff/1.1.14/1.2.x-allocation.md:101`；`docs/handoff/1.1.14/1.2.x-allocation.md:189`；`docs/handoff/1.1.14/1.2.x-inventory.md:315` |
| E29 budget 與 progress 耦合 | `src/NvtFwCombiner.VersionManagement.Application/VersionManagement/ManagedLauncherEntry.cs:450–453`；`docs/adr/0062-first-run-managed-setup.md:123–124`；`docs/adr/0062-first-run-managed-setup.md:150–151` |
| E30 2.0.0 terminal 不是 Launcher gate | `docs/handoff/1.1.12.md:373`；`docs/ci/release-package.md:326`；`docs/adr/0078-predecessor-comparison-for-1x-releases.md:131` |
| E31 2.0.0 retained evidence／cascade | `docs/handoff/1.1.14/1.2.x-inventory.md:142`；`docs/handoff/1.1.12.md:377`；`docs/adr/0078-predecessor-comparison-for-1x-releases.md:123` |
| E32 舊排程需要新 disposition | `docs/architecture/v1.2.1-handoff.md:104`；`docs/handoff/1.1.14/1.2.x-inventory.md:176`；`docs/architecture/nfc_roadmap.md:933` |
| E33 防回放與舊 Catalog 的精確邊界 | `docs/adr/0053-fixed-update-source-registry.md:92–105`；`src/NvtFwCombiner.VersionManagement.Application/VersionManagement/VersionManagementExperience.Registry.cs:489–520`；`docs/adr/0066-update-catalog-v2-notification-policy.md:151–157` |

## 獨立審查逐項修訂紀錄

原版 SHA-256：`560FC91B2B6FA5BEF5B9812A60FC8462D4186F5AE84670F082D33531D5F9E6BC`。
審查檔：`evidence/1.2.1/R03-launcher-plan.review.md`，SHA-256：`B4AFC84C98A4575DCE0965A9052560A7B452FF54B2550171B30F789F6EE95B65`。以下是作者處置，不能代替 reviewer 複核。

| Finding | 處置 | 修訂／證據 |
| --- | --- | --- |
| **P2-1** | 同意；分岔比審查建議的 1.2.6 更早閉合 | A1a 1.2.1 獨立判定＋A1b 1.2.6 實作前確認；A7 最終複核。L4 拆 M2+M2，38/32/24 合計仍 403。補 Case A 的 1.2.14 修復版位與 F/E/U 帳；Case B 缺陷備援 1.2.15。 |
| **P2-2** | 同意 | F15／D6／R06-02／Q3 明定 compiled-in locator：Registry 內容改向；Registry 本身搬站須 rebuilt package＋雙站過渡。主案 M2/R3；新增外部配置列 Q3-C，L4/R3，A1 納配置攻擊面。〔E04〕 |
| **P3-1** | 同意具名負案例；精確化「Catalog 舊 revision」措辭 | D7/A5 列 lower Registry revision、同 revision 異 bytes/digest、舊 Catalog assertion mismatch、deprecated 不自動選。Catalog 無自有 revision；不宣稱拒絕合法較高 Registry revision 所指向的舊 Catalog/schema，也不新增 sticky schema floor。〔E33〕 |
| **P3-2** | 同意 | F24／Q8-C／容量表：R-6 L4 可移 1.2.11，明改 decisions 55/181；加計 A1b 後，兩個 R06 都回升 L 再移 R-6 仍為 32，不沿用舊帳的 30。 |
| **P3-3** | 同意 | F11／Q7／同步輸入條件化：Q7-A/B 為 M2；Q7-C 只量 Check 成本 S1，1.2.1 38→37、系列 403→402。 |
| **P3-4** | 同意 | A3 預設同受控 share＋staging Registry＋diagnostic override canary；L4 來自換站／回切／權限故障驗收，非環境搭建。可在同覆蓋下由 owner 重估 M2，系列再減 2。 |
| **P3-5** | 同意 | 1.2.6 表明列 ADR 0056 clean-Windows smoke，費用含既有各包 gate；A4/A5 改為最終候選完整重跑。〔E24〕 |
| **P3-6** | 同意 | 開頭／邊界／canonical 同步記 board decision 186；E04 改 ADR 0053 :96–98、:125–139 並加 locator :7，E29 改 ManagedLauncherEntry :450–453。 |

重檢結果：**29 列 F、9 項 D、9 題 Q**，每題都有選項及建議；主案各版合計 **403**、Case B 基礎 **423＋G**，已交付 R35-08 各扣 1；Q7-C、Q3-C、bridge、R-6 移版、A3 縮量與修復觸發均有明確差額／估值方法。1.2.13 保留最終整合必要性；1.2.14 在主案只作條件修復／拆版，在 Case B 承擔最終候選驗收。這些都是規劃權重，沒有新產品執行證據。

## 限制、待確認與交付狀態

- **待 owner：** 上述 9 題，尤其「受控內網完整交付」是否符合產品預期、實際 transport／ACL 信任範圍及 O07。這份建議尚未變更任何 accepted commitment。
- **待現場：** 受支持 client 名單、首套來源／認證與誰擁有 share/NTFS ACL、publisher/release/security 人員、測試機／OS／AV／儲存條件、staging／正式 activation 權限；未讀任何憑證、token、DPAPI key。
- **待量測：** 依 Q7 的 R04-01 流量或 Check 成本、cold/warm 實包性能、R06 主案 M2 的操作範圍及 R-4/R-6 精確工作量；A3 若降 M、Case B 最終增量 G、缺陷 F/E/U 均待具體證據。S/M/L 不是工時或節省率。
- **待設計／審查：** 若採簽署，演算法／library、root/key custody、clock/expiry/offline、撤銷與 strict reader 遷移都未批准；本報告不借「安全要求」直接選 TUF 或新增憑證管理平台。
- **待實證：** 沒有執行產品、建置、Python、測試、repository verifier、live catalog 檢查或 GitHub。cold 262.0998 ms 是歷史診斷，不是此候選 benchmark；歷史 CI／發布記錄未在線核對。
- **輸入審查狀態：** R03-01 原 review 的 accept-with-changes 修訂狀態沿前版記錄，未新增再審證據。本報告原版已完成 decision 169 的 Claude Fable 5.1 獨立審查（accept-with-changes，P2×2、P3×6，無 P0/P1）；上表逐項處理，修訂版仍待獨立複核及 owner 決定。
- **有界搜尋：** 僅指定文件及直接相關 owners；.codegraph 不存在，使用 bounded rg/Get-Content。沒有超出直接依賴擴大搜尋，沒有下載或安裝。無新重現的產品 bug；沿用 C01 ledger，不在唯讀 repository 增寫 ledger。
- **文件衝突留存：** 舊 handoff/R03-02 的 1.2.1 開發、inventory 的 R-4/R-6 舊排程、roadmap 的 Launcher 2.0.0 分別由 decisions 175、181、186 處理。proposal 的新外部 locator 仍只是候選；採本案後需 canonical owner 同步，不能聲稱 repo 已對齊。
- **自檢界線：** 依 `nfc-review` 對本報告修訂做規格、信任／相容與證據三面自檢；`nfc-release-readiness` 僅用來核對規劃中的 exact-candidate／Golden／clean-machine gate，未執行發布工作。R0 外部評估文件的 scoped Polytail 結果為 **PASS-WITH-HUMAN-GATE（僅報告自檢）**，不是 production、CI、integration 或 release-ready；A1、本報告修訂版獨立複核與 9 題 owner 決定仍待完成。
- **檔案／命令紀錄：** 唯讀 `git rev-parse HEAD`／`git symbolic-ref -q HEAD`／`git status --short`、`Test-Path`、`Get-Content`、bounded `rg`、`Get-FileHash`；原始碼與文件引用按固定 HEAD 抽查，版本表與 F/D/Q 編號、Markdown 表格作文字及算術核對。HEAD 保持 `6703e25179cb1112131c108337dbacf232a8da65`、detached、工作樹乾淨；審查檔 hash 未變。唯一寫入為本外部報告，未 commit、push、呼叫 GitHub 或建立 implementation goal。
