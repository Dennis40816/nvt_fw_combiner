# V200 — 2.0.0 定位重新定義提案

修訂紀錄：2026-09-29 — 依 Claude Fable 5.1 的 accept-with-changes 逐項採納 P2×4、P3×7；補齊待批 gaps、RO-1 時序、非認證用語與 correction rebinding，推薦 B、5 題 owner 決定維持；修訂版待獨立複核。

日期：2026-09-29。狀態：**1.2.1 評估提案，供 owner 決定；未成為 accepted contract，未實作、整合或發布。**
來源：`<worktrees>/e121`，detached HEAD `6703e25179cb1112131c108337dbacf232a8da65`（1.1.14 內容）。
評估者依任務指定為 Codex gpt-6-astra、最高 reasoning effort；未取得可另行驗證 effort 設定的 runtime 回報。原版已依 decision 169 由 Claude Fable 5.1 獨立審查，結論 accept-with-changes（P2×4、P3×7，無 P0/P1）；本次修訂不等於已獲修訂版再審通過。輸入為本報告原版與同目錄 `V200-redefinition.review.md`。

## 結論

**建議現在將 2.0.0 定義為「現行產品契約與證據基線正式版」（候選 B）**：以可追溯的現行 support／evidence、跨版差異與升降版契約，取代「完整 Launcher 正式發布」主題；提議明示撤回「歷史 64 routes 全數完成 v0.9.16 terminal certification」的整體承諾，改用逐 route 的證據與缺口處置、必要 Golden、逐版 rolling comparison，以及綁定 exact candidate 的 firmware／release owner 核准。這是**認證承諾的變更，不宣稱與原 terminal 證明等價**；不為 major 人造破壞性功能，也不把 `ContractOnly` 升格。選 B 就是定下 RO-1 的退役／替代方向，**不修改 decision 64**；其要求的 v0.9.16 milestone 仍是正式處置記錄的前置。定位、替代內容與驗收條件可以現在選定，但在該證據、正式規則變更及替代 gate 驗證完成前，現行 2.x terminal 阻擋保持有效。〔E01、E03、E11〕

## 1. 權威與本次範圍

- 本次 owner 指示優先：decision 186 將 Launcher 完成移入 1.2.x；Q8 已選定新增 **1.2.13 完成版、1.2.14 修復備援**。本 worktree 的 board 僅到 181，186 與 Q8 的這項選擇以本次指示為來源，不能偽稱已入 repo。
- owner 已拒絕 R03 Q9 的「細節留到 2.0.0 planning」建議。本報告現在提出完整定位選項及替代條件；不再次詢問是否啟動重定義。R03 其他信任、簽章、容量選項不因 Q8 的版位選擇而全部獲准。〔E23〕
- allocation 的 1.2.1 表格仍是盤點／契約／決定；V200 是本次追加評估，不冒列為原 17 項之一。沿用 O01／O02／O03／O18／O30 作關聯索引，問題 VQ1–VQ5 不是新增 board decision 或 canonical O 編號。〔E01、E22〕
- 本文的 terminal 指 ADR 0057 的認證完成鏈，不是 CLI、Application 的一般終止狀態。ADR 0078 標頭仍為 `Proposed`，但其具體 owner 決策與 P-1 契約已被後續分配採用；不能因此宣稱 P-2/R-5 已完成，也不能忽略已記錄的決定。〔E12、E20〕

## 2. 目前掛在 2.0.0 的義務與承諾

下表區分「仍有效」「移走」「待決」，包括跨版繼承義務；原本理由取自來源，不從版號或實作存在推定。

| ID／義務 | 來源與原本理由 | 目前效力、2.0.0 所需處置 | 代表證據 |
| --- | --- | --- | --- |
| C01 完整 Launcher、實際 Catalog/Registry GO | roadmap 將 trust/security、preproduction、Installer、完整候選驗收接到 2.0.0 正式交付；版本排程不能代替 activation 批准。 | **主題已移走，驗收未取消**：依 owner 186/Q8 在 1.2.13 完成，必要時用 1.2.14 修復。security／release／activation 證據與明示 GO 留在 Launcher 完成邊界；2.0.0 不重做該首發。 | E02；本次 owner 指示 |
| C02 2.x terminal promotion gate | decision 47、ADR 0057／0033：原 terminal 認證延至 2.0.0，沒有取消；rolling 是額外檢查。 | **仍有效**：未正式保留／退役／替代前，所有 `>=2.0.0` 發布需要成功 terminal chain；1.x 不需此鏈。改叫 2.0.1 不能繞過。 | E03 |
| C03 v0.9.16 terminal 模式的完整證明 | ADR 0057：固定 64 route/fingerprint，14 Standard、6 AB、44 CtrlRAM；53 predecessor 與 64 candidate scenarios。完整 bytes／size／hash；11 shortened TP 用配對 full-output 的 prefix 等式與 immutable-tail 證明。 | **仍是原 terminal 契約**，不是 37 條抽樣通過即可。已批准的 NT51951 NF correction 保留 exact output、2,816 differing bytes 與五個半開區間；不要求復原前版已知錯誤。沒有命名或跨版 operation 完全相同的承諾。 | E04、E06 |
| C04 27 條歷史缺 input 的債務 | decision 47：22 contract-only、4 synthetic-oracle、1 none，當時均無 owner-certified Golden；ADR 0057 將缺 input 放在 64 denominator，阻擋 terminal 執行。 | **待逐條處置**：補 canonical input／獨立證據，或經正式契約變更撤回、替代相應 claim。原 terminal 下僅寫 accepted gap 不能產生 pass；1.x 的 not-covered 不自動延伸成 2.x 豁免。數字是歷史集合，不是 current support census。 | E05 |
| C05 candidate／package／workflow 跨版綁定 | ADR 0057：固定 plan、歷史 policy/Golden、candidate source executor、runtime closure、package artifact 與 comparator；H1→H4 transfer 只在聲明的樹與 policy 不變時成立。目的在防止把別版或別包的證據移用。 | **保留 terminal 時必須更新適用性**：建立受審的後繼 binding，不原地改寫 immutable v1 歷史。現行 source、受保護 workflow、package 的身分各自證明；相同 apphost hash 或共同祖先不夠。 | E07 |
| C06 independent firmware attestation＋finalize | ADR 0057：compare 先產 provisional；獨立 firmware-owner verifier 驗證精確 digest 與人員，finalize 才能 terminal pass；release owner 另行批准。 | **仍有效，不能被 rolling `clear` 代替**。same-run/head/attempt/artifact 與 protected environment 只是序列證據，不能自己證明 owner 身分。選 B 時須明定新的 owner 綁定，不只刪三個 jobs。 | E08 |
| C07 terminal parser／executor／correction schema 舊契約適用性 | ADR 0078 記錄固定 member／empty `Issues` 與新報告不合、v1 baseline locked restore `NU1004`、舊 fingerprints；plan schema／loader 只容一條 approved correction，decision 12 已批准的 NT51950 TP-work 第二條差異仍會被 terminal 拒絕。P-2 明確只做 1.x reader/executor v2。 | **terminal rebinding 的保留工作或退役對象**。parser 問題記錄為 suspected，未在本次重現；v2 修復不能直接當成 terminal v1 已修。採 A 須補安全 reader、executor、workflow 證據，並在後繼 plan/schema/loader 容納多條具名 approved corrections、逐條重審 exact output／差異範圍；不原地改 immutable v1、不放寬成任意差異。採 B 可退役舊路徑但保留共用驗證。 | E09、E29 |
| C08 NT51950 cascade 與 1.x amendment 的收束 | decisions 12、62、63，ADR 0078：TP-work NF correction 要精確重現；特定 cascade full-flash binding 在 v0.9.16 Preview 拒絕，另列 not-applicable；v1 executor 保留 terminal。 | **2.0.0 明定再審**：不能推廣為整個 IC 不適用，也不能把拒絕算 equal。amendment Lifetime 原文為 A 併入 rebound plan、B「deleted together with the terminal chain」。選 B 須同步修改 Lifetime，明定從 active contracts 退役、Git 歷史及歷史比較 bytes／records／provenance 保留；本提案不將原文的 deleted 冒稱為既有的保留條款。 | E10 |
| C09 RO-1 前的 v0.9.16 milestone | decision 64、ADR 0078：在既定 milestones 及 RO-1 決定前重新錨定歷史關係，避免逐版比較漂移。 | **仍欠正式證據**：1.x mode 使用歷史 inputs，37 bound／27 unbound 分列；結果僅 `consistent/inconsistent/invalid`，`certification: none`。1.2.0/1.2.1 的 decision 176 release waiver 沒有明文豁免「RO-1 前」這次要求。 | E11、E20 |
| C10 每版 rolling comparison／R-5 | decisions 57–64、ADR 0078、R-5：捕捉前一 published stable 到 exact candidate 的未宣告 byte、輸入接受性與 coverage 變更；Golden 仍是認證 oracle。 | **1.x 義務存在；2.x 適用契約要明寫**。R-5 設計說 every mode 額外 gate，但 ADR 0078/contract 明定 1.x。不得聲稱已覆蓋 2.x。rolling chain 只覆蓋每一版都有比較的相同 scenario/input revision，不能填補歷史豁免或缺口。 | E12、E13 |
| C11 1.x→2.x 相容／破壞性變更 | 版本治理要求 breaking product/support contract 有 migration、support/schema/protocol 與 owner 批准；已有 managed protocol 1、Catalog v1/v2、CLI 與 historical Report 的各自相容契約。 | **未找到指定給 2.0.0 的具體 breaking change**，也未見「所有 1.x 無人直升」保證。major 不自動升 protocol/schema、不批准刪舊 Report/draft/command。應列支援 predecessor、直接升級／bridge／回復及故意失去的能力。 | E15、E16 |
| C12 支持與認證的界線 | ADR 0038／0046／0055：publication、authoring、execution、evidence 分立；Supported＋ContractOnly 可以是已批准產品政策，不是 parity certification。 | **跨版本保留**：route/fingerprint 變動須重新審；不能靠 terminal 退役自動改 support。`Supported + Missing` 仍擋 promotion/CI/release；缺 comparator inputs 與 Missing evidence 不是同一事實。 | E17、E32 |
| C13 release 保護、staging 與清理 | decisions 53–55：RO-9 staging、清除 silent promote skip/舊規則、dry-run、低優先加速；後續 decisions 174/181 限定例外與排程。原意為發布失敗可見且可回復。 | **通用 release 義務，非新增 2.0.0 產品功能**：exact-source CI、每次 applicable Golden、package/manifest/SBOM/provenance/smoke 與 owner gate 仍在。變更 terminal/promotion 路徑須負向 staging；不得沿用 1.1.14 例外。R-4/R-6 已排 1.2.6，不移回 2.0.0。 | E14、E21 |
| C14 11 條 candidate route accepted gaps 待批 | decision 60、ADR 0078 Consequences／Open items：當時 rolling universe 為 74 routes，37 可比較；其餘 37 為 decision 12 的 26 條 published routes 及 11 條無 inputs 的 candidate routes。後者的 accepted-gap 核准仍待 owner。 | **1.2.2 report of record 與 VQ5 證據鏈的前置**：按當版 exact routes 核對仍有的缺口，取得 firmware-owner 核准、declaration entry 與 CHANGELOG id，才能形成 rolling report of record。補 inputs 或退役也須按 coverage change 處置。這 11 條不是原 11 shortened TP proof，也不繼承歷史 27 的豁免；批准 1.2.2 gap 不代表批准後續 2.x gap。 | E27 |

**排程脈絡**：decision 154 將 P-2 移到 1.1.14；167 再移到 1.2.x；175 定在 1.2.2，R-5 在 1.2.6；176 僅處置 1.2.0/1.2.1 的 ADR 0078 release waiver。這串決定沒有取消 C02–C09，也沒有證明比較已執行。〔E19、E20〕

**目前的 2.x 執行邊界**：靜態讀取顯示 eligibility 對 `>=2.0.0` 要求 success，但 parity jobs 仍只在 `2.0.0` 啟動；因此後續 2.x 不能假設直接發布可用，需在新契約中安排 gate。這是已保留的規劃／驗證需求，本次沒有跑 workflow 或判為新產品 bug。〔E24〕

## 3. 三個候選定位

| 候選 | 主題與必要內容 | major 代表什麼 | 對後續版本的影響 | 優點與風險 |
| --- | --- | --- | --- | --- |
| **A：v0.9.16 完整 terminal 認證版** | 現在選擇保留 C02–C08；補足原 64 集合及後繼 candidate binding、27 debt、11 transitive、correction/cascade 處置、parser/executor、independent attestation、finalize 與 package 證據。後繼 plan schema／loader 須由 single-row correction 擴充為多條具名核准列並重審，容納 NT51950 TP-work correction，保留逐列 exact output／差異範圍。若刪任何原 claim，須另外批准，不再稱原 64 完整認證。〔E29〕 | major 為先前延期的完整 terminal 保證正式生效；不必有 runtime breaking change，但要由 owner 明定此版號語意。 | 1.2.2/P-2、1.2.6/R-5 照常；1.4.0/1.4.1 提供可用範圍內的獨立證據。2.0.0 等全部 required evidence；每個後續 2.x 的重新綁定／執行規則亦須閉合。 | 最接近原承諾，historical parity 說法最清楚；但凍結集合包含已演化／退出的 route，且 27 缺口不等於 1.4.x 必能補齊。外部材料成本與工期無法估定，可能為舊比較能力長期阻擋產品。 |
| **B：現行產品契約與證據基線正式版（建議）** | **對應 decision 47 保留的「Retire it」選項，加上本報告的明確替代基線**，不是 keep／retire 之外的新路。明示撤回「原 64 全部 v0.9.16 terminal pass」承諾；保留 owner-certified Golden 案例，以及 `certification: none`／`diagnostic-only-not-admitted` 的歷史比較觀測。至今沒有任何 v0.9.16 parity 結果是 admitted 認證證據。以現行 route/fingerprint 的 support/evidence/disposition、必要 Golden、逐版 rolling、跨 1.x→2.x migration 矩陣、綁定 candidate 的 firmware/release 核准作為發布基線。〔E28、E30〕 | major 為公開認證／發布契約換代，須由 owner 明示接受此 major 語意；**不等於 BIN、CLI、Report 或更新 protocol 必須破壞相容**。它不是新增 evidence rank，也不把 `certification: none` 改成 terminal pass。 | 1.2.1 定方向；1.2.2 先閉合 C14 缺口處置，提供 report of record 與正式歷史比較；1.2.6 保持原 R-5 工作；1.3.x 完成原 authoring，1.4.x 提供證據與 migration 輸入；2.0.0 消費成果、建立現行基線，後續 2.x 持續逐版 gate。 | 對準實際產品與使用者承諾，重用既有 owners／比較器，不為舊版限制另造執行模型；但**放棄原全體歷史 parity claim 是實質改變**，缺口保留代表盲區仍在，必須 firmware/release owners 明示接受，不能包裝成等價強化。 |
| **C：只為具體破壞性契約保留 2.0.0** | 現在定義「有實際 breaking product/support/schema/protocol 需求才開 2.0.0」，不填功能；開版前要具名需求、舊新契約、migration／rollback 與驗收。可與「只退役 terminal、無新整體 certification」記錄組合，但須在 VQ1 明選並完成同樣的正式處置前置；否則 terminal 義務保持 C02。 | major 明確代表使用者可觀察的不相容；目前沒有已證實的具體變更可支撐此版。 | 1.2.x Launcher、1.3.x authoring、1.4.x evidence 照常交付；2.0.0 無日期與固定 backlog，不將空出的 1.5.x 填工作。不能藉持續用 1.x 宣稱 terminal 已完成。 | 版號訊號直接、不為 milestone 製造功能；但此刻只得到觸發條件，沒有具體 2.0.0 交付內容。未另選並完成退役處置時，舊 terminal debt 亦未解決，較不符合 owner 現在要定下可交付定位的目的。 |

版本治理的版號建議表標示為「before 1.0.0」，不是一份已定案的 post-1.0 SemVer 細則；但同表已把「Breaking product/support contract」連到「a later major」（`docs/governance/branch-version-and-release-governance.md:19`）。**C 最接近既有文字的傾向；B 需要 owner 明示把 major 語意擴充為認證／發布契約換代**，A 亦須明定其完整 terminal 保證的版號語意。不能以表頭為由忽略既有傾向，也不能宣稱已有規則唯一要求其中之一。本文候選 C 是 breaking 觸發條件，不是 decision 47 原選項 C「Decide later」的同義詞。〔E15、E30〕

## 4. 建議 B 的具體契約與完成條件

建議 B 的理由是：Launcher 已有 1.2.x 完成邊界，而獨立證據與 authoring 各有 1.4.x、1.3.x owner；2.0.0 應把它們形成**有明示範圍的產品發布承諾**。復用 canonical capability policy、Support Matrix projection、Golden validator、P-2 validator／runner 與 R-5 evidence binding；不建立第二個 support 清單、firmware executor 或泛用認證平台。〔E12、E17、E25〕

以下是**建議的新契約**，不冒稱既有要求已變更：

| 邊界 | B 必要內容與成功條件 | 未通過時的處置 |
| --- | --- | --- |
| 對外聲明 | Release notes 說明撤回原整體 terminal 承諾、哪些 exact routes 仍有何種證據、哪些缺比較或缺獨立 output；不稱「所有 supported 均 Verified」。保留 owner-certified Golden 案例與非認證歷史比較觀測；v0.9.16 比較狀態仍為 `certification: none`／`diagnostic-only-not-admitted`，至今沒有 admitted v0.9.16 parity 認證結果，不回填歷史 pass。〔E28〕 | 未逐項列清，不完成 RO-1 替代，也不稱 2.0.0 evidence-complete。 |
| 現行 denominator | 以 exact candidate 的 canonical route/policy/evidence 為準。所有 published supported/candidate rows 都有處置；其中 authoring available 的 rows 進 rolling universe，其他 rows 說明 unavailable／retired 等事實。另將原 64、特別是 27 debt，對照現行 route/fingerprint 或明確退役，不硬湊回 64；C14 的 11 條 candidate gaps 亦須追蹤至當版逐列處置。 | 缺 disposition、未批准 coverage reduction 或 Supported＋Missing，均阻擋。不得將歷史 27 的固定豁免或 1.2.2 對 11 條 candidate gaps 的批准自動轉為新 2.x accepted gaps。 |
| 未有比較 inputs 的 route | firmware owner 逐 row 選補證據、保留既有 support/evidence 並批准**當版**比較缺口，或另行退役 claim／功能。保留 ContractOnly 需沿既有合法 admission；gap 不豁免 required Golden、unknown integrity、P0/P1 或 protected checks。 | 不自動降級 support，也不自動升級 evidence；必要證據缺失仍擋發布。明示「少了哪一種保證」，而不是統一標 pass。 |
| 原 11 shortened TP 的特定條件 | 逐 route 明定保留原 transitive proof、以受審的獨立 TP-only Golden／full-tail 保存證據替代，或撤回相應 claim。替代時須由 firmware owner 明改 ADR 0057 的特定 Supported 條件，不只套用一般政策；此集合與 C14 的 11 條 candidate gaps 分別核對。〔E32〕 | `Supported + ContractOnly` 的通則不能自動豁免這 11 條原有條件；沒有新的精確 disposition，就保留原 proof 要求。 |
| byte／歷史證據 | 每版執行全部 applicable owner-certified Golden，保留允許差異界線、完整輸出與 exact write-range。RO-1 前執行正式 v0.9.16 1.x milestone，精確再現 correction／cascade rejection；必要時提出受審的 amendment。 | `invalid` 不能批准為 harmless rejection；不抄 candidate output 當 expected、不 mask CRC、不以輸入 hash 代替執行。 |
| 跨版本行為 | 2.0.0 對前一 eligible published stable 做完整 rolling，coverage 與 input revision 變更明列 declaration／CHANGELOG。CLI/Report/draft/settings/profile/rule import、managed protocol/Catalog 的 read/write/upgrade/rollback 能力用矩陣逐項驗。 | 有 intentional breaking row 才變更相應契約與 migration；使用 major 標籤本身不授權刪舊資料或 API。 |
| 核准綁定 | 重用 R-5 candidate 或 sibling evidence manifest，綁 exact source/tree、baseline tag/commit、contract/comparator、policy/ledger/Golden/declaration、完整 report digest、package/run/artifact 身分與 owner decision。firmware owner 批准 claim/difference/coverage；release owner 批准 exact candidate。獨立 reviewer 核對報告與 bound evidence，不以部署狀態代替身分。 | 缺 role、舊 head、錯 report/package/run、宣告先後不符皆阻擋。若此綁定改變現行 approval rule，還需 governance-owner 批准與對應證據。 |
| 切換／後續 2.x | R3 變更同時更新 ADR、contract/schema、eligibility/workflow 與失敗語意。用 payload-free negatives 與受控 staging 證明 stale/wrong-source/undeclared/failure/cancel/skip 會阻擋，成功才可切換。每一後續 2.x 都跑當版 gate，不能重用 2.0.0 blanket pass。 | 新 gate 未有效前保留舊阻擋。舊 environment／secret 的清理由 owner 另授權，且在替代 gate 已於真實 release 成功後才做；歷史 records 不刪。 |

### terminal 保留／替代／退役需要哪些 owner 證據

| 選擇 | firmware owner 必須審的證據 | release owner 必須審的證據 |
| --- | --- | --- |
| A 保留 terminal | 64 exact routes 與 27 補證據；所有原 proof kinds、correction、cascade 與完整 bytes/write-range；後繼 plan/schema/loader 的多列 approved-correction rebinding 及每列精確差異證據（含 NT51950 TP-work，不能只修 reader）；重新綁定的 baseline/candidate executor；獨立 verifier 對 evidence/approval 的驗證及其 admission 記錄。 | 同候選的 package、same-run compare→attestation→finalize、保護 workflow／artifact／source 綁定；2.0.0 及後續 2.x 的 fail-closed 正負驗收；個別 release gate。 |
| B 替代（建議） | RO-1 前正式 milestone、C14 的當版 candidate-gap 處置與 rolling report of record；原 64→現行集合對照、27 每條處置、11 TP 的特定支持條件；撤回哪些 claim、保留哪些 Golden／alias／ContractOnly；現行候選全適用 Golden、rolling、差異範圍與獨立複核。不得聲稱證明了未執行 routes。 | 承諾撤回的 release disclosure、1.x→2.x 相容矩陣、完整新 evidence/owner binding；ADR 0033/0057/0078、amendment Lifetime 與 executable policy 同步；staging、實際 candidate／發布 gate 證據及 cleanup 時序。 |
| 只退役 terminal、無新整體 certification（可與候選 C 組合） | 同樣要逐 claim／27 route disposition、C14 當版 gap 處置及正式 milestone，明說不再提供哪種 historical assurance。現行 support/evidence 及 required Golden 仍有效。 | 同樣走 decision 47 保留的「Retire it」記錄；仍要明定每次 rolling／package／owner gate 與綁定，不可只刪 jobs；新版文案不得使用 terminal pass。若沒有其他產品契約成果，可在 VQ1 選 C 並明選此退役方向，不另增決定題。〔E30〕 |

上述保留／退役的 joint-owner 要求已有來源；B 的具體發布基線是本報告提案。**現在核准方向不等於正式退休舊義務**：decision 64 的報告與人審是已知前置，不是把定位設計再次延至 2.0.0 planning。〔E08、E11、E26〕

## 5. Owner 決定題（5 題）

| 題目／O 關聯 | 選項 | 建議與決策作用 |
| --- | --- | --- |
| **VQ1／O01、O03、O30：2.0.0 現在採哪個定位與 terminal 方向？** | A：完整保留歷史 terminal；B：現行產品契約／證據基線，按本報告條件替代原整體承諾；C：僅為具體 breaking change 保留版號，可與 terminal 退役記錄組合（須在本題明選；未明選則維持 C02）。 | **B**。明示將 major 語意擴充為認證／發布契約換代，原全體 historical pass 不再是目標。**選 B 就定下 RO-1 退役／替代方向；本題不修改 decision 64**：1.2.2 milestone 仍是正式處置記錄（逐 route disposition、claim 撤回清單、新 binding）的前置，並須完成 VQ5 的決定前重跑。選 B 不等於 RO-1 已完成，也不免除 milestone；證據與正式規則／gate 閉合後才生效。〔E11〕 |
| **VQ2／O02、O01：27 debt 與其餘比較缺口如何處置？** | A：逐 exact route 補證據、當版 accepted comparison gap 或另審退役，**含 ADR 0078 的 11 條 candidate gap 當版批准**（C14；仍有缺口時），維持 support/evidence 分立；B：所有歷史 27 均補成原 terminal 可執行證據才允許 2.0.0，C14 的 1.2.2 當版處置仍須完成。 | **A（配合候選 B；亦適用 C＋退役）**。逐條明示未證明事項及 owner；選本策略不等於已批准尚未具名／綁定的 11 條 gaps。required Golden、Supported＋Missing 等阻擋不豁免。若選候選 A，需 VQ2-B 或明示修改原 64 claim，不能混用較小 denominator 卻宣稱完整認證。〔E27〕 |
| **VQ3／O03、O18：1.x→2.x 承諾多大相容範圍？** | A：具名 predecessor／schema／protocol 矩陣，至少驗最後 Launcher 1.2.13 或修復版 1.2.14，以及最後 1.x stable 的升級／回復；B：承諾所有歷史 1.x 均可原位無人值守直升至 2.0.0；C：指定具體 breaking 契約並逐項批准 migration。 | **A**。命令、歷史 Report、draft/settings 與 rule/profile import 依其版本化契約逐項判定；需要 bridge 就明說。B 沒有證據支持；C 必須有實際需求，不能為 major 湊不相容。 |
| **VQ4／O01、O30：2.x 的持續 gate 與 v0.9.16 lifecycle？** | A：每個 2.x rolling＋applicable Golden＋owner/package gate；RO-1 前完成最後正式 v0.9.16 歷史錨定，之後保留 records，除新決定外不承諾反覆 terminal；B：每個 2.x 保留 v0.9.16 terminal 與 rolling 雙 gate。 | **A（配合候選 B 或 C＋退役）**。把 ADR 0078/R-5 的 2.x 適用性寫清；沒有連續比較的舊版區間仍是缺口。ADR 0078 已在 1.x 設計否決每版與 v0.9.16 比較，理由是 intentional change 要重複批准、correction rows 須反覆更新；VQ4-B 重現此成本。該否決不自行解除現行 2.x terminal：若選候選 A，仍須 VQ4-B 及每版重新綁定，不能只改版本條件。〔E31〕 |
| **VQ5／O01、O30、O18：何時生效、在哪裡收尾？** | A：現在核准定位與條件；1.2.2 先閉合 C14 當版 gap 核准及 declaration／CHANGELOG，提供 report of record 與首次正式 milestone；1.4.x 整理證據；2.0.0 首個 admission 前以當時選定的 exact 1.x source 重跑 RO-1 報告並完成正式 joint-owner 決定，再實作／驗證所選 gate（候選 B／C＋退役為替代，候選 A 為 rebinding）；B：要求 1.2.1 就正式解除舊 terminal 義務。 | **A**。保留 decision 64 的正式決定前置，並以本提案的決定前重跑避免只沿用較早的 1.2.2 結果。B 需要另行明改 decision 64 與 R3 authority，且本次沒有必要證據，不建議。R-5 在 1.2.6 維持原範圍；新增 2.x gate 改造屬 2.0.0，不塞入 1.2.1。 |

**修訂後五題交叉檢查**：建議組合仍為 **VQ1=B、VQ2=A、VQ3=A、VQ4=A、VQ5=A**。VQ1 定方向，VQ2 管逐列處置，VQ3 限定相容承諾，VQ4 定持續 gate，VQ5 管生效前置；沒有一題代為完成 owner 核准或產生證據。C14 是 rolling report of record 的前置；v0.9.16 1.x mode 不讀 declaration，另按歷史 plan／amendment 產生 milestone，兩者在 1.2.2 證據包與 VQ5 鏈會合，不能互相代替。候選 A 須配 VQ2-B／VQ4-B；C＋退役沿用相同的正式處置前置，不新增第六題。〔E11、E27〕

## 6. 對後續版本的具體輸入

| 消費版本／項目 | 使用本報告的內容與界線 |
| --- | --- |
| **1.2.1：V200、R03-02、O01/O02/O03/O18/O30** | 記錄 VQ1–VQ5 最終選擇、claim disposition 的欄位／owner 與新版定位；仍只文件／決定。R03 的已定版位不重問；本提案也不代為接受其餘 R03 選項。 |
| **1.2.2：R35-05/06、R36-01～03** | 完成 v0.9.16 1.x mode/executor v2 與首次正式 milestone；rolling report of record 前，先對 C14 的 11 條 candidate gaps 取得當版逐列核准及 declaration／CHANGELOG，或依已批准的補證據／退役變更閉合。歷史 mode 保留 37 bound／27 unbound、correction/cascade、exact-source 與 clean-settings 契約；report of record 與 milestone 均提供 VQ5／RO-1 輸入，不能改成 terminal pass。rolling baseline 按實際 eligible stable tag 選；歷史 mode 仍固定 v0.9.16。 |
| **1.2.5：R07/R08/R10/R31** | Combiner、B-bank/Header-copy 與共用宣告變更的每項 byte/write-range/allowance disposition，成為後續 rolling 與現行 evidence 基線；不因 2.0.0 存在而延後本版 mandatory Golden。 |
| **1.2.6：R37-01～04、R-4/R-6** | R-5 的同 run/source report digest binding、負向 staging 為 2.x 復用基礎。保持既有 terminal 不變的本版驗收；若前移替代 gate 實作，須另改 allocation、重估容量與 R3 admission。 |
| **1.2.13／1.2.14：Launcher 完成／修復備援；R25 文件收尾** | F29 的正式 GO 和最終 Launcher compatibility/evidence 在此閉合；2.0.0 僅驗新候選的升級／回復。R25 對 Launcher 的最終文件同步隨完成邊界，是否移 R25-01/02 須在 allocation 記錄，不假稱原 1.2.12 已覆蓋後來成果。 |
| **1.3.0～1.3.3：General／Saved Rules／maintainer authoring** | 照原版本實作；輸出其 accepted schema、讀寫／migration 規則與 exact route admission，作為 C11 的相容矩陣。沒有把這些功能搬到 2.0.0 或提前宣稱支持。 |
| **1.4.0／1.4.1：獨立 Golden、IC/capability evidence intake** | 消費 C04/C08/VQ2 的 gap 對照，逐條提交可採用的獨立證據／退役建議。既有這兩版範圍不保證涵蓋全部 27；新增 gap closure 工作須重估，未完成不能冒稱已補齊。 |
| **原 1.4.2、1.5.0～1.5.3** | 已無可照舊執行的 Launcher 排程；依 186/R03 最終決定逐項記「移入 1.2.x／不採納」。不自動保留空版、不創新功能補位；本報告不批准 repository 拆分或新版本內容。 |
| **2.0.0 與每個後續 2.x** | 按 VQ1 選定 A/B/C；建議 B 完成現行基線、跨版契約與替代 gate，最後跑 exact candidate 的必要證據。每個後續版本續行當版 comparison／Golden／owner release gates，沒有一次認證永久沿用。 |

配置依據以 allocation 與目前 owner 補充為準；後續版本用途來自 roadmap 的原工作，不宣稱有新工期或容量承諾。〔E19、E20、E25〕

**批准後要同步的 canonical owners（本次未改）**：roadmap 現行版本表及 Launcher work packages、allocation、inventory R03/R35/R36/R37 與 O01/O02/O03/O18/O30、v1.2.1 handoff；若選 B，再由相應 owner 修改 ADR 0033/0057/0078、predecessor/amendment/terminal contracts、release-package 與 executable workflow/policy。amendment Lifetime 須明訂 active contract 退役及 Git 歷史／provenance 保留，避免把現行「deleted」誤解為刪除歷史證據。新的決定 supersede dated decisions；roadmap :250/:267 等日期記錄、舊 plan、舊核准及比較結果保留歷史原文。變更 support／schema/protocol 時才改其真正 owner，不順帶全面改寫 SPEC。〔E02、E07、E26〕

## 7. 證據索引（每個論點最多三個代表）

以下相對路徑皆基於上列固定 repo；每個 `path:line` 指讀到的實體行。E 索引可重用，不代表另做一次測試。

| 證據 | 抽樣 path:line |
| --- | --- |
| E01 1.2.1 範圍、獨立評估 | `docs/handoff/1.1.14/1.2.x-allocation.md:68`；`docs/handoff/1.1.12.md:1020` |
| E02 原 Launcher 主題與 GO | `docs/architecture/nfc_roadmap.md:382`；`docs/architecture/nfc_roadmap.md:960`；`evidence/1.2.1/R03-launcher-plan.md:55` |
| E03 2.x terminal 現行義務 | `docs/handoff/1.1.12.md:373`；`docs/adr/0033-ci-owned-stable-release-promotion.md:68`；`docs/ci/release-package.md:326` |
| E04 原因、64 與 117 執行集合 | `docs/adr/0057-v0916-black-box-parity-certification.md:12`；`docs/adr/0057-v0916-black-box-parity-certification.md:78`；`docs/adr/0057-v0916-black-box-parity-certification.md:190` |
| E05 歷史 27 與 current drift | `docs/handoff/1.1.12.md:377`；`docs/adr/0057-v0916-black-box-parity-certification.md:94`；`docs/adr/0078-predecessor-comparison-for-1x-releases.md:28` |
| E06 correction／TP proof／命名範圍 | `docs/adr/0057-v0916-black-box-parity-certification.md:55`；`docs/adr/0057-v0916-black-box-parity-certification.md:125`；`docs/adr/0057-v0916-black-box-parity-certification.md:379` |
| E07 candidate／authority transfer／immutable plan | `docs/adr/0057-v0916-black-box-parity-certification.md:215`；`docs/adr/0057-v0916-black-box-parity-certification.md:282`；`docs/adr/0078-predecessor-comparison-for-1x-releases.md:168` |
| E08 attestation／finalize | `docs/adr/0057-v0916-black-box-parity-certification.md:308`；`docs/adr/0057-v0916-black-box-parity-certification.md:342`；`docs/adr/0057-v0916-black-box-parity-certification.md:347` |
| E09 parser、baseline 仍未成 terminal closure | `docs/handoff/bugs/BUG-20260926-terminal-parity-rejects-ctrlram-issues.md:3`；`docs/contracts/v0916-parity-1x-amendment-v1.md:133`；`docs/handoff/1.1.13/DESIGN-rolling-parity.md:772` |
| E10 cascade／correction／amendment lifetime | `docs/adr/0078-predecessor-comparison-for-1x-releases.md:123`；`docs/contracts/v0916-parity-1x-amendment-v1.md:108`；`docs/contracts/v0916-parity-1x-amendment-v1.md:148` |
| E11 RO-1 前 milestone／non-terminal | `docs/handoff/1.1.12.md:472`；`docs/adr/0078-predecessor-comparison-for-1x-releases.md:64`；`docs/contracts/predecessor-comparison-v1.md:21` |
| E12 rolling 範圍、report、連續性的限制 | `docs/adr/0078-predecessor-comparison-for-1x-releases.md:52`；`docs/adr/0078-predecessor-comparison-for-1x-releases.md:106`；`docs/adr/0078-predecessor-comparison-for-1x-releases.md:152` |
| E13 R-5 額外 gate 與同 run 綁定 | `docs/handoff/1.1.13/DESIGN-release-workflow-cleanup.md:211`；`docs/handoff/1.1.14/1.2.x-inventory.md:374`；`docs/handoff/1.1.14/1.2.x-allocation.md:126` |
| E14 decisions 53–55／staging 與限定例外 | `docs/handoff/1.1.12.md:411`；`docs/handoff/1.1.13/DESIGN-release-workflow-cleanup.md:436`；`docs/handoff/1.1.12.md:1055` |
| E15 major／protocol／Catalog 不連動 | `docs/governance/branch-version-and-release-governance.md:19`；`docs/adr/0056-rollback-safe-launcher-self-update.md:96`；`docs/adr/0066-update-catalog-v2-notification-policy.md:68` |
| E16 既有相容與升降版義務 | `docs/architecture/nfc_roadmap.md:449`；`docs/governance/branch-version-and-release-governance.md:146`；`docs/architecture/v1.2.1-handoff.md:116` |
| E17 support／evidence 分立 | `docs/adr/0038-versioned-publication-policy-and-evidence-status.md:57`；`docs/adr/0046-capability-and-compilation-fingerprint-boundary.md:150`；`docs/adr/0055-runtime-reference-replace-supported-admission.md:61` |
| E19 decisions 154→167→175 分配 | `docs/handoff/1.1.12.md:930`；`docs/handoff/1.1.12.md:1004`；`docs/handoff/1.1.12.md:1059` |
| E20 1.2.2、有限 waiver 與尚待介面 | `docs/handoff/1.1.14/1.2.x-allocation.md:98`；`docs/handoff/1.1.12.md:1068`；`docs/contracts/predecessor-comparison-v1.md:466` |
| E21 通用 release gates／R-4/R-6 | `docs/adr/0033-ci-owned-stable-release-promotion.md:61`；`AGENTS.md:193`；`docs/handoff/1.1.12.md:1110` |
| E22 O 索引（不是 board decisions） | `docs/handoff/1.1.14/1.2.x-inventory.md:462`；`docs/handoff/1.1.14/1.2.x-inventory.md:479`；`docs/handoff/1.1.14/1.2.x-inventory.md:491` |
| E23 R03 的狀態、Q8 版位與被拒絕的 Q9 建議 | `evidence/1.2.1/R03-launcher-plan.md:5`；`evidence/1.2.1/R03-launcher-plan.md:197`；`evidence/1.2.1/R03-launcher-plan.md:198` |
| E24 目前 2.x 執行邊界（靜態） | `scripts/release_promotion_policy.py:159`；`.github/workflows/release.yml:837`；`.github/workflows/release.yml:422` |
| E25 後續版本的既有主題／收尾 | `docs/architecture/nfc_roadmap.md:371`；`docs/architecture/nfc_roadmap.md:868`；`docs/handoff/1.1.14/1.2.x-allocation.md:189` |
| E26 退役要求與 R3 角色 | `docs/handoff/1.1.13/DESIGN-release-workflow-cleanup.md:177`；`docs/handoff/1.1.13/DESIGN-release-workflow-cleanup.md:189`；`AGENTS.md:186` |
| E27 11 candidate gaps 的待批前置 | `docs/adr/0078-predecessor-comparison-for-1x-releases.md:146`；`docs/adr/0078-predecessor-comparison-for-1x-releases.md:218`；`docs/handoff/1.1.12.md:448` |
| E28 歷史 v0.9.16 比較均非 admitted 認證 | `docs/adr/0057-v0916-black-box-parity-certification.md:68`；`docs/adr/0078-predecessor-comparison-for-1x-releases.md:16`；`docs/adr/0078-predecessor-comparison-for-1x-releases.md:135` |
| E29 單列 correction schema／loader 待 rebinding | `docs/adr/0078-predecessor-comparison-for-1x-releases.md:31`；`docs/handoff/1.1.13/DESIGN-rolling-parity.md:476` |
| E30 decision 47 保留的 keep／retire 選項 | `docs/handoff/1.1.13/WS-GOV.md:1025`；`docs/handoff/1.1.13/WS-GOV.md:1037`；`docs/handoff/1.1.13/DESIGN-release-workflow-cleanup.md:189` |
| E31 已否決每版 v0.9.16 比較的成本理由 | `docs/adr/0078-predecessor-comparison-for-1x-releases.md:160` |
| E32 Missing 阻擋與 11 shortened TP 特定支持條件 | `docs/adr/0015-canonical-firmware-map-and-compiled-composition.md:531`；`docs/adr/0057-v0916-black-box-parity-certification.md:359` |

## 8. 限制、待確認與審查狀態

| 項目 | 狀態／對決定的限制 |
| --- | --- |
| 正式 RO-1 證據 | **待確認／未取得**：P-2 尚排 1.2.2；本次未跑 milestone、rolling 或 terminal。沒有可用來即刻退休舊 gate 的新正式報告。 |
| 11 candidate accepted gaps | **仍待 firmware-owner 逐列批准**：C14 引用的是 ADR 0078／decision 60 記錄的集合，並非本次重算後的 candidate 清單。未取得當版 exact route 核准、declaration／CHANGELOG 前，不能聲稱 report of record 前置已閉合；VQ2-A 只是處置策略。 |
| 64／27／現行集合 | **待逐 route 核對**：只引用既有文本的固定集合；未載入 private firmware，也未重算 active policy／manifest。不能聲稱 1.4.x 可以補齊全部 debt，或以舊 37/74 數字描述未來候選。 |
| 相容矩陣 | **待提供／執行**：具體 predecessor 清單、schema/protocol compatibility、舊資料 round-trip、bridge、downgrade／rollback 尚無本次證據；未發現已批准的 2.0.0 breaking contract 不等於證明全相容。 |
| 文件時間差 | **已知來源差異**：roadmap/handoff 仍寫 Launcher 2.0.0，allocation 到 1.2.12，ADR 0078 標 Proposed，部分 1.x contract interfaces 標 pending。依目前 owner 指示及較新 allocation 解讀；未擅自更正 repo。 |
| terminal 已知問題 | **沿既有記錄，不宣稱新重現**：parser issue 為 bug ledger 的 suspected；baseline restore failure、single-row correction schema／loader 限制為 ADR 0078 已記錄事項。未檢查所有 parser/runner 分支或重跑 restore；也未核對獨立 firmware-owner verifier 的 admission 記錄，不以程式碼存在推定 ADR 0057 的 admission 前置已成立。 |
| effort／工期 | 模型身分與最高 effort 依 task 指定，無額外 runtime attestation。沒有實測人日、hosted-runner 成本、fixtures 取得日期或未來候選；不給完成百分比／發布日期。 |
| 獨立審查 | **原版 accept-with-changes；修訂版待複核**：Fable 的 P2×4、P3×7 均採納，逐項見第 9 節。修訂者自查不取代 decision 169 的獨立複核，不代表任何 VQ 已獲 owner 決定。 |

## 9. 獨立審查意見處置與修訂自查

以下 ID 沿用 `V200-redefinition.review.md`；全部採納，沒有駁回項。每項來源已按固定 repo 重新核對，未把 reviewer 的建議當成 owner 核准。

| Finding | 處置 | 修訂位置與依據 |
| --- | --- | --- |
| P2-1 | 採納 | 新增 C14；VQ2、VQ5、1.2.2 輸入及限制同步納入 11 candidate gaps 的當版核准，明列為 report of record／VQ5 鏈前置；與 11 shortened TP proof 分開。E27。 |
| P2-2 | 採納 | 結論及 VQ1 直接寫明選 B 定 RO-1 方向、不修改 decision 64；milestone 先於正式逐列 disposition／撤回清單／新 binding。VQ5 保留決定前重跑。E11。 |
| P2-3 | 採納 | 候選 B 與對外聲明改為 owner-certified Golden 案例及非認證比較觀測；明示所有已記錄 v0.9.16 比較均非 admitted parity 認證。E28。 |
| P2-4 | 採納 | C07、候選 A、firmware-owner 證據列補上後繼 plan/schema/loader 的多列 approved-correction rebinding，含 NT51950 TP-work，仍保留 exact output／差異範圍及 immutable v1。E29。 |
| P3-1 | 採納 | 版號語意段補治理表第 19 行，指出 C 最接近既有文字、B 需 owner 明示擴充 major 語意；VQ1 同步。E15。 |
| P3-2 | 採納 | 候選 B 與 terminal 退役表明確對應 decision 47 保留的「Retire it」選項加替代基線；區分本文 C 與原 RO-1-C。E30。 |
| P3-3 | 採納 | 候選 C、退役表及 VQ1 明列 C＋terminal 退役可選組合；未明選仍維持 C02，沿用 VQ2/VQ4/VQ5 前置，不增第六題。E26、E30。 |
| P3-4 | 採納 | VQ3-B 改為「承諾所有歷史 1.x 均可原位無人值守直升至 2.0.0」；仍不建議無證據的全歷史承諾。 |
| P3-5 | 採納 | C08、release-owner 證據與 canonical 同步清單指出 Lifetime 原文的 deleted；B 須正式改為 active contract 退役、Git 歷史與 provenance 保留。E10。 |
| P3-6 | 採納 | VQ4 引 ADR 0078 已否決每版 v0.9.16 比較的重複批准／更新成本；限定該否決屬 1.x 設計，不能藉此自行解除 2.x terminal gate。E31。 |
| P3-7 | 採納 | E02 改 roadmap 行號為 960；E24 改 workflow 條件為 422；E17 補 ADR 0055 的第 61 行，原 Missing 阻擋來源保留於 E32。E23 明示第 197 行為 Q8、第 198 行為 Q9。 |

檢查方式：以 `Get-Content`、限定路徑 `rg`、`Get-FileHash`、`Test-Path` 與 read-only Git 查詢核對來源；用 `apply_patch` 僅改本報告，對照原版差異，並以 PowerShell 做文件行號／引用／表格檢查（先載入使用者 `NFC_TEST_AREA_ROOT`，將 `TEMP`／`TMP`／`TMPDIR` 指向既有 temp 子目錄）。`.codegraph/` 不存在，未建立 index。未跑 Python、產品測試、build、repository verifier、workflow、GitHub、網路下載或安裝，未讀憑證／token／DPAPI key。Repository 保持唯讀；本輪唯一寫入物為本報告。

輸入身分（SHA-256）：原版 V200 為 `1C5C3D01164BDD429D72E0B07BE9B826AC8A2DD5289217DEA3EA8B07E43137C5`；Fable review 為 `13F7CDDAFFA7A8584A5689631722BF9C999C336DAB688AA7C85018C35164A98D`；R03 為 `3335DDFEA355261FBFAFD583807D6566540A2F37D5F3E9DD6DA1C67A87EB8F1B`。雜湊只固定報告身分，不表示設計已批准。

自查結論：`PASS-WITH-HUMAN-GATE`，僅指本次**唯讀評估／外部提案修訂**的範圍、引用與承諾邊界；不是產品、CI、terminal 或 release readiness 結論。文件檢查為 185 行、14 類義務、5 題決定、P2×4／P3×7 處置完整，9 張表格欄數一致，89 處 path:line 均存在且未超出來源檔案，E 索引無缺項；這些結構檢查不代替逐項語意核對。`git diff --exit-code` 與 `git diff --cached --exit-code` 均為 0，`git status --short` 無變更，HEAD 仍 detached 於指定 commit，review／R03 輸入 hash 未變。現行契約維持；owner 的 5 題決定、修訂版獨立複核與後續 R3 證據／正式修改仍待完成。
