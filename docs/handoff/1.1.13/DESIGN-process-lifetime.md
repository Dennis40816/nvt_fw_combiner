# 1.1.13 wave 5 設計：程序取消與視窗生命週期

Status: 第 6 版設計已獲 R2 設計批准，2026-09-27，Claude Code（Opus 5.5）撰寫，commander
為 Claude。審查歷程（`codex/gpt-6-astra`）：第 1 版 REJECT（F-1 同步阻塞 tree kill）；
第 2 版 ACCEPT-WITH-CHANGES；第 3 版 REJECT（新 P1 F-9 與 F-4b/F-10/F-11）；第 4 版
ACCEPT-WITH-CHANGES（餘 F-12 至 F-15）；第 5 版 ACCEPT-WITH-CHANGES（餘 F-16、F-17 與
ADR 措辭）；第 6 版 ACCEPT-WITH-CHANGES、scoped Polytail PASS，建議給予設計批准（僅
餘 repo 外證據檔的 F-18，已修正）。record 的 `designReview.outcome` 為 `approved`。
ADR 0081 仍是 Proposed，要等 owner 接受全文後另行改狀態。

## 0. 身分與依據

- 分支 `feature/1.1.13/wave5-process-cleanup`，從 `1.1.x` trunk `e6e991af3`（#459 合併）
  建立，worktree `<worktrees>/w5`。record 的 `integrationBase` 是 evidence checkpoint
  `241327de1`。第 6 版 patch 原本在 `557a9ee6a` 上產生與驗證；它在新 trunk 上的套用與
  測試結果記錄在 admission 與實作 commit 之後的回報中。
- Owner 決定 85–88、92 與 ADR 編號 0081 已在新 trunk 的看板上（`docs/handoff/1.1.12.md`、
  `docs/handoff/1.1.13.md`）。
- 版本分配以 [roadmap](../../architecture/nfc_roadmap.md) 的 `1.1.13` 列為準：
  F03/F06 先於 F01/F02/F25。驗收條件：取消、終止確認與 held-pipe drain 都有上限；
  handoff 失敗後恢復存檔，並允許第二次 Close；READY 取消與過期 callback 被隔離。
- finding 內容：repo 只保存摘要與驗收條件，也就是 roadmap 2026-09-17 的分配
  （`3edb02598`）與 [audit handoff](../../architecture/post-v1.1.8-audit-handoff.md)
  的 AUD-01/AUD-02。外部報告原文與附件都沒有進 repo，本設計因此沒有讀到原文。
  roadmap 將 F01/F02/F03 記為有條件的 P2；外部報告的 P1 標籤不代表本地已重現。
- repo 內的摘要：
  - F03+F06：取消 callback 不外漏預期中的 OS 錯誤；執行、終止確認與 pipe drain
    都要有上限；測試需涵蓋子程序持有 pipe、kill 被拒，以及 timeout 與 cancel
    競態。AUD-02 指定由單一 owner 處理，並要求保留 F19 的 ownership。
  - F01+F25：依序 Close → handoff 失敗 → 設定與歷史存檔 → 第二次 Close，必須能
    完成；進行中的工作要在最終 dispose 前收尾；過期 callback 不可再發布結果。
  - F02：READY 取消被隔離。

## 1. 現況總表

分類：**已重現**表示在 test area 以確定性步驟觀察到；**程式碼推論**表示依程式碼
判斷、沒有執行；**尚未取得真實 OS 重現**表示目前只以 seam 覆蓋。

| 項目 | Owner（檔案:行） | 分類 | 證據 |
| --- | --- | --- | --- |
| F03-a 取消 callback 外漏 OS 錯誤 | `SystemExternalProcessRunner.cs:25-30`（callback 內 `TryKill`）、`:75-84`（只接 `InvalidOperationException`） | 已重現 | 後代程序拒絕 `PROCESS_TERMINATE` 時，`Cancel()` 拋出 `AggregateException(Win32Exception 5)`，`RunAsync` 也以同一例外失敗 |
| F03-b timeout 路徑外漏 | `SystemExternalProcessRunner.cs:35-38` | 已重現 | 同一條件下，timeout 沒有回傳 `TimedOut`，而是讓 `RunAsync` 以 `AggregateException` 失敗 |
| F03-c UI/CLI 後果 | `MainWindow.axaml.cs:145,184`（`async void OnClosing` 呼叫 `CancelActiveRun`）、`CompositionRunPresentationViewModel.cs:84-87,197-241`、CLI `Program.cs` `HandleCancel`、`CliApplication.cs:97-106` | 程式碼推論 | catch filter 都不含 `AggregateException`；`src` 內沒有全域 `UnhandledException` handler |
| F03-d kill 與程序結束的競態 | 同 F03-a | 程式碼推論 | 程序正在結束時，`Kill` 可能丟出 `Win32Exception`（存取被拒） |
| F06-a 正常結束後 pipe 仍被持有 | `SystemExternalProcessRunner.cs:49-54`、`BoundedProcessOutputReader.cs:30-32` | 已重現 | Timeout 2 秒，背景孫程序持有 stdout 約 10 秒；`RunAsync` 等了 10.4 秒，最後回傳成功 |
| F06-b drain 期間取消，卻回傳成功 | 同上 | 已重現 | 3 秒時取消，`RunAsync` 回傳 `exit=0` |
| F06-c 超出 tree walk 範圍的孤兒程序 | `SystemExternalProcessRunner.cs:37-40` | 已重現 | Timeout 2 秒，孤兒持有 pipe，`RunAsync` 到 10.2 秒才回傳 `TimedOut` |
| F06-d kill 後等不到 exit | `SystemExternalProcessRunner.cs:86-95`（`WaitForExitAfterKillAsync` 沒有上限） | 程式碼推論；尚未取得真實 OS 重現 | 以 seam 覆蓋：termination 阻塞、termination 不生效、exit 觀察失敗 |
| F06-e 存活的後代卡住 staging | `ExternalStagingDirectory.cs:45-62`（`Dispose` 吞掉 `IOException`） | 程式碼推論（測試 fixture 觀察到同類現象） | 存活程序把目錄當成目前工作目錄，刪除失敗 |
| F01 handoff 失敗後的存檔閂鎖 | `LatestSnapshotPersistenceCoordinator.cs:31-34,52-59`；`MainWindow.axaml.cs:141-204,564,631`；`MainWindow.VersionManagement.cs:127-137` | 程式碼推論 | 見 §7.1 |
| F02 子程序端 READY 取消 | `MainWindow.axaml.cs:243-273`；`ManagedApplicationStartupCoordinator.cs:35-46`；`AnonymousPipeManagedApplicationProcess.cs:406-407` | 程式碼推論 | 見 §7.2；父程序端已隔離（`:156-170`，有既有測試） |
| F25 dispose 前沒有觀察進行中的工作 | `MainWindow.axaml.cs:229-240`；`CompositionRunPresentationViewModel.cs:170-270`；`MainWindow.axaml.cs:273` | 程式碼推論 | 見 §7.3 |

順帶發現的 bug（suspected）：
[BUG-20260926-process-start-failure-escapes-typed-result](../bugs/BUG-20260926-process-start-failure-escapes-typed-result.md)。
不在 AUD-02 範圍內，建議另開同 owner 的 R1。審查意見補充：它也可能由穩定的 OS
啟動限制觸發，所以「一般使用不易遇到」的頻率判斷證據不足。

## 2. F03/F06 的重現方法

- 在 test area 用暫時 detached worktree（`557a9ee6a`），加上 repo 外的 console
  harness（只呼叫公開的 `SystemExternalProcessRunner` API，不做斷言）。每個 shell
  都先載入 `NFC_TEST_AREA_ROOT`，並把 `TEMP`/`TMP`/`TMPDIR` 設為其 `temp`。
  build 改寫過的 `packages.lock.json` 都已還原。
- 情境：
  - `cmd /c "start /b ping"`：正常結束後仍有 pipe 被持有。
  - outer → inner → ping：inner 結束後 ping 成為孤兒，不在 tree walk 範圍內。
  - F03-a/b：一個會拒絕終止要求的後代程序（細節只留在 repo 外的證據檔）。
- 平台探針（不是產品碼）：在 .NET 10、Windows 上，被孫程序持有的 redirected
  stdout，其 `ReadAsync(token)` 可以準時取消。審查意見指出這不保證所有 reader 都會
  立即返回，所以第 2 版不依賴它：不合作的 reader 會在 deadline 被放手（§4.4）。
- 完整輸出記在 scratchpad `w5/repro-evidence.txt`，harness 原始碼在
  `w5/repro-harness/`。本批沒有使用 UI host，也沒有碰到本機狀態檔。

## 3. Owner 盤點與 capability-reuse gate

| 關注點 | Owner | 備註 |
| --- | --- | --- |
| 外部程序執行、timeout、取消、終止 | `Infrastructure.ExternalTools.SystemExternalProcessRunner` | SPEC 第 27 條、ADR 0006、`docs/architecture/external-combiner-tool-runner.md` |
| pipe drain | `BoundedProcessOutputReader`（runner 的 helper） | 保留上限內的診斷文字 |
| 結果對應到 issue code | `ExternalCombinerProcessor`、`LegacyCombinerPostbuildProcessor`、`RuntimeTrustProbeProcess` | 只有這三個 production consumer |
| 啟動與 staging（F19） | `ProcessLaunchGate`、`ExternalStagingDirectory` | 本批不改 |
| run 取消的來源 | `CompositionRunPresentationViewModel.CancelActiveRun`/`CancelRun`；CLI `Program.HandleCancel` | 本批不改 |
| 視窗關閉與 handoff | `MainWindow.OnClosing`、`MainWindow.VersionManagement`、`SettingsViewModel.HandleLauncherHandoffFailureAsync`、`StableLauncherHandoff` | 第二批 |
| 本機狀態存檔 | `LatestSnapshotPersistenceCoordinator`（F08 在 wave 2 也改這個檔） | 第二批 |
| READY | 子程序端：`InheritedPipeApplicationReadySignal`、`ManagedApplicationStartupCoordinator`、`MainWindow.RunStartupPreloadAsync` | 第二批 |

Gate 判定為 `extend-owner`：延伸 runner，採用與 `ManagedProcessTermination` 相同
的不確定性分類，再加上 `NotSupportedException`。審查已確認沒有第二條外部工具執行
路徑，三個 consumer 都有處理，F19 owner 未修改，也不必為此跨層引用
VersionManagement。搜尋證據寫在 record 的 `searchEvidence`。

## 4. 第一批設計第 6 版：`PROCESS-CLEANUP-1113-01`（F03/F06）

完整契約寫在 ADR 0081 草稿（scratchpad `w5/ADR-0081-external-process-cleanup.draft.md`，
patch 內為 `docs/adr/0081-external-process-cleanup.md`）。本節只列設計重點與審查
意見的對應。

### 4.1 審查意見的回應

已閉合：F-1、F-2、F-5、F-6、F-7（第二輪前）；F-3、F-4a、F-8（第三輪）；F-9（CAS
硬上限）、F-4b、F-10、F-11（第四輪）。本版（第 5 版）處理第四輪的四項：

| 審查意見 | 第 5 版的處理 |
| --- | --- |
| F-12 [P2] 併發測試逐一呼叫 `RunAsync`，名額沒有真的競爭 | `CapacityIsAHardCapUnderConcurrentStarts` 改為每個 run 各用一條獨立執行緒（`TaskCreationOptions.LongRunning`），全部在同一個 `Barrier` 就緒後一起呼叫 `RunAsync`（保留名額的動作在第一個 await 之前），並保留 exit gate。斷言恰好 3 個啟動、5 個容量拒絕；打開 exit gate 後等每個已啟動 run 發布 `ResourcesReleased`，再斷言容量歸零。另新增純測試 `TryReserveIsAtomicUnderContention`：32 條由 `Barrier` 同時放行的執行緒直接呼叫 `TryReserve`，重複 200 輪，驗證恰好上限個成功、每個拒絕觀察到的值都不低於上限。ADR 的併發驗證宣稱同步修正 |
| F-13 [P2] `Finish()` 例外安全 | `Finish()` 對每一項 handle 各自 try（透過新的 `DisposeResource` seam 呼叫），一項失敗不會跳過其他；處置例外被觀察、不外拋；名額在 `finally` 一定歸還；處置失敗時改發布新的 `ResourcesReleaseFailed`，不發布成功的 `ResourcesReleased`；detached continuation 另掛 fault 觀察。新增故障注入測試：inline 與 detached 兩條路徑各一，讓 Process 的處置拋例外，驗證 5 項處置都有嘗試、名額歸零、只發布失敗訊號。ADR 第 5 條寫入此設計 |
| F-14 [P2] 容量診斷誤指 detached | 例外欄位 `DetachedInvocations` 改名為 `InUseInvocations`，值為原子保留在拒絕當下觀察到的在用數（不再事後重讀 `Outstanding`）；例外訊息與使用者訊息都改成「仍在執行或仍在清理的 external-tool run 已占滿容量」，保留 restart 指引，使用者訊息不顯示數字。內部名稱同步：`DetachedCleanupBudget` → `ExternalProcessCapacity`、seam `DetachedCleanup` → `Capacity`、`Outstanding` → `InUse`。ADR 與架構摘要加上正常執行相容性的限定：因為執行中的 run 也占名額，只有在同時使用中的 run 少於上限時行為才與以前相同 |
| F-15 [P3] 本文件的名額生命週期過時 | §4.4 改為：啟動前保留 → detached 延續原名額 → 處置及歸還完成後才發布最終訊號 |

另外，第 5 版把一個依賴環境的真實 OS「拒絕終止」fixture 從 lifetime 測試移除；第五輪
審查接受此做法，不需要替代 fixture。修正後的契約由 refusal seam 與 cancellation seam
支撐，base 的缺陷由保留的 F03-a/b 重現紀錄證明。本文件不宣稱任何一版已重新通過真實 OS
拒絕終止情境。

第 6 版處理第五輪的兩項與 ADR 措辭：

| 審查意見 | 第 6 版的處理 |
| --- | --- |
| F-16 [P2] 併發測試的 exit seam 把「開閘」當成「程序已退出」 | `HeldExitAsync` 的 gate 只控制放行：開閘後仍等待真實的 `process.WaitForExitAsync(observationToken)` 才成功返回；observation 被取消時以取消結束，不會轉成成功。對每個獲准執行的 run 補斷言 `ExitCode == 0`、`TimedOut == false`、`Cleanup == Complete`，保留最終訊號與容量歸零的檢查。檢查其他 seam：`TerminationSeam.Block` 有同樣模式（開閘後未終止卻回報成功），已改為開閘後執行真實的 tree kill（慢 kill）；`Ignore` 是刻意的無效終止（測試斷言 unconfirmed），不是 gate；reader、exit-observation fault 與 disposal seam 都以 fault 結束，不會把開閘當成成功 |
| F-17 [P3] ADR 第 10 項黏在第 9 項尾端 | 在 `10.` 前恢復換行，patch 內與外部草稿同步；ADR 的 10 個編號項目現在各自獨立成行 |
| ADR 措辭 | 「compare-and-set itself observed」改為「atomic reservation observed」（容量一開始就滿時，值來自初次讀取，不一定執行過 CAS）；`ResourcesReleaseFailed` 的「reported」限定為只發布給 test observer 的內部 phase，production 沒有 observer、日誌或使用者通知，不構成公開通知契約。原始碼註解與 record 同步 |

### 4.2 時間軸（單一 deadline，decision 86）

```text
terminal signal (T0) = 自然結束 | manifest timeout | 呼叫端取消 | exit 觀察失敗
T0            timeout、取消或 exit 觀察失敗時，立即啟動唯一的 termination 工作（背景 task）
T0 .. T0+2s   只在自然結束時：給 stream 的 held-output grace；逾時仍開著，就記為 held，
              並啟動 termination（tree walk 仍能找到直接後代）
.. T0+4s      等待 termination、exit 觀察與兩條 stream 完成
T0+4s         stream 還沒結束就要求 reader 停止（最後 1 秒保留給 reader 返回）
T0+5s         做出 terminal 決定並返回；不再等待尚未結束的工作
```

- 常數放在 `ExternalProcessCleanupTiming.Default`（5 秒 / 2 秒 / 1 秒）。這是 host
  常數，不是 manifest 或 profile 的 timeout；測試透過 internal seam 注入較短的值。
  時程由純函式 `Schedule(signaledAt)` 算出三個絕對時間點（grace、reader-stop、
  deadline），reader-stop 落在 deadline 之內（deadline − reserve），不是加在其後。
- `Cancel()` 只呼叫一次 `TaskCompletionSource.TrySetResult`，並設定
  `RunContinuationsAsynchronously`，不做任何程序工作。

### 4.2a 呼叫容量（decision 92）

- runner 有一個 process-wide 的 `ExternalProcessCapacity`，固定**硬上限 8**，計算的是
  **仍在執行或仍在清理**的 invocation。透過靜態的 Production seam，所有 runner 實例
  （三個 consumer 與 trust probe 的 `CreateDefault`）共用同一個計數。
- **硬上限（F-9）**：`RunAsync` 在 `ProcessLaunchGate.Start` 之前以
  `TryReserve(out observedInUse)` 原子保留一個名額（compare-and-set，只在計數低於上限時
  取），所以在用數永遠不超過上限，與併發無關。名額在整個 invocation 期間持有，並在啟動
  失敗、完成或 detached 工作 settled 後恰好歸還一次（處置失敗時也一樣）。保留失敗時在
  啟動任何程序前丟出 `ExternalProcessCleanupCapacityException`，其 `InUseInvocations`
  是原子保留在拒絕當下觀察到的值（容量一開始就滿時來自初次讀取，否則來自失敗的 CAS）。
- 上限 8 的依據：外部工具的執行在每個 workflow 內實質上是序列化的（一次一個 UI run
  或一個 CLI 呼叫；ADR 0075 的 preload worker 不是外部工具），所以同時使用中或卡住的
  invocation 超過個位數就代表 host 層級的故障，而不是正常負載；8 給偶發、暫時的慢終止
  留了餘裕，同時把資源上限壓在一個小常數。代價：同時有 8 個 run 仍在執行時，第 9 個會被
  拒絕，即使沒有任何卡住；一般操作一次只啟動一個，不會遇到。
- 這不是 5 秒 deadline 的替代，而是它的補充：deadline 保證單次返回有界，硬上限保證
  累積有界。名額生命週期與最終訊號見 §4.4。
### 4.3 cleanup 值與 terminal outcome

- `Cleanup` 的值：`Complete`、`TerminationUnconfirmed`、`OutputStreamHeldOpen`、
  `OutputReadFailed`。定義與優先順序見 ADR 0081 第 6 條。
- 取消只要在決定點之前提出就優先，以帶有呼叫端 token 的 `OperationCanceledException`
  結束。例外訊息會寫出觀察到的 cleanup 值，讓內部事實不會遺失，但它不是可以用來
  分支判斷的契約。
- 決定點之後才取消，不會改變已決定的結果。
- consumer 的對應：
  - timeout 與非零 exit 保留原本的 issue code，訊息後加上 cleanup 說明。
  - exit 0 且 `Cleanup != Complete` 時，在把任何 staged 檔案當成結果讀取之前，以
    `external-tool.process.cleanup-incomplete` fail closed。
  - 只有 `Complete` 能把 captured output 當成成功結果或協定輸入；timeout／非零 exit
    的錯誤診斷仍可引用部分輸出。
  - trust probe：timeout 優先回傳 `runtime.trust.timeout`；非 timeout 的 incomplete
    才回傳 `runtime.trust.probe-failed`。
  - capacity 拒絕：staged processor 回傳 `external-tool.process.cleanup-capacity`，
    trust probe 回傳 `runtime.trust.probe-failed`。

### 4.4 所有權：handle、逾時工作與晚到的 fault

- 每次呼叫都由一個 `Invocation` custody 擁有程序 handle、兩個 stream reader、
  exit 觀察、唯一的 termination 工作與各個 cancellation source。
- deadline 到時仍在執行的工作（例如阻塞中的 kill、忽略停止要求的 reader）會被放手，
  不強制中止。等到所有背景 task 都結束後，custody 才 dispose `StandardOutput`、
  `StandardError`、`Process` 與 cancellation source，同時觀察所有晚到的 fault，
  所以它們不會變成 unobserved exception，也不會被算到下一次 run。
- runner 的 `finally` 一定會呼叫 `Release()`。如果直接子程序的 exit 一直沒有被觀察到
  （例如 terminal signal 之前就發生非預期的例外），`Release()` 會補啟動 termination，
  但不等待它完成。
- 代價：被放手的工作在結束之前，會一直持有程序 handle 與 pipe handle，直到它結束或
  host 程序結束為止。文件因此宣稱「有界返回、保留所有權、settled 後回收、detached
  數量有上限」，不宣稱「5 秒內沒有殘留 handle」。這一點寫在 ADR 0081 的 Consequences。
- decision 92 的上限（§4.2a）保證這種殘留不會無限累積：達到上限就拒絕新執行。
  名額的生命週期：**啟動前原子保留**一個名額 → run 返回時若 cleanup 仍在進行，
  detached 路徑**延續同一個名額**（`Detached` phase 只是資訊）→ 背景工作 settled 後，
  逐一處置 handle 並**歸還名額**，之後才發布最終訊號：全部處置成功發布
  `ResourcesReleased`，任一處置失敗則發布 `ResourcesReleaseFailed`（名額仍會歸還，
  不發布成功訊號）。這兩個最終訊號都只是發布給 test observer 的內部 phase；production
  沒有 observer，也不寫日誌、不通知使用者，所以不是公開的通知契約。
- 最終步驟例外安全（F-13）：每項 handle 各自 try，一項失敗不會跳過其他；處置失敗被
  觀察而不外拋；名額在 `finally` 中一定歸還；detached continuation 本身也被觀察。

### 4.5 UI 與 CLI 的呈現（decision 85、Q3）

- Presentation 與 CLI 的程式碼，以及既有的分類通道都不變。新的訊息文字、exit 0
  改判失敗、有上限的等待，都是可以觀察到的行為變更。
- 取消後維持既有畫面：UI 不發布結果，CLI 印 `error: operation canceled`、exit 70。
  已確認兩者都沒有「已確認全部停止」這類字樣。第一批不新增 warning。

| 情況 | 現在 | 本批之後 |
| --- | --- | --- |
| UI 取消或 Close，後代拒絕終止 | `Cancel()` 在 UI 執行緒丟例外，推論會導致程式終止 | `Cancel()` 立即返回；run 在 5 秒內以取消結束 |
| UI timeout，kill 被拒或阻塞 | `AggregateException` 穿過 run session | Build/Preview 失敗，列出 `external-tool.process.timeout`，訊息帶 cleanup 說明 |
| 工具結束後子程序持有輸出 | 一直等到子程序結束，然後成功 | 最多約 5 秒，然後以 `cleanup-incomplete` 失敗 |
| CLI Ctrl+C，kill 被拒或阻塞 | handler 內出現未處理例外（推論） | 5 秒內印 `error: operation canceled`，exit 70 |
| 仍在執行或清理中的 run 達上限 8 | 資源無上限累積（推論） | 新執行以 `external-tool.process.cleanup-capacity` 失敗，提示重新啟動 |
| 一般正常 run | - | 不變 |

### 4.6 變更範圍

- `mutablePaths` 共 15 項：
  - governed 10 項：`docs/adr/0006-external-combiner-tool-runner.md`、
    `docs/adr/0081-external-process-cleanup.md`，以及
    `src/NvtFwCombiner.Infrastructure/ExternalTools/` 下的
    `SystemExternalProcessRunner.cs`、`SystemExternalProcessRunner.Invocation.cs`（新增）、
    `BoundedProcessOutputReader.cs`、`ExternalProcessResult.cs`、
    `ExternalCombinerProcessor.cs`、`LegacyCombinerPostbuildProcessor.cs`、
    `RuntimeTrustProbeProcess.cs`、`ToolchainRuntimeCandidateInspector.cs`（F-11 第 4 版新增）。
  - 輔助測試 5 項。
- 第 4 版比第 3 版多一個 governed 檔：F-11 讓 trust probe 把容量原因傳到 inspection
  result，動到既有的 `ToolchainRuntimeCandidateInspector.cs`（既有的 R2 Infrastructure
  檔，owner 不變）。
- `docs/architecture/external-combiner-tool-runner.md` 改成指向 ADR 0081 的摘要。
  validator 不把它列為 governed，所以不列入 `mutablePaths`，但在同一個 diff 裡。
- 第 5 版的檔案集合與第 4 版相同（16 個 patch 檔，其中 15 個是 `mutablePaths`）；
  F-14 只在既有檔案內改名（`ExternalProcessCapacity`、`Capacity` seam、`InUse`、
  `InUseInvocations`），F-13 新增的 `DisposeResource` seam 也在既有檔案內。
- code-size（第 5 版）：Infrastructure + Contracts + CRC worker slice 由 33,195 行
  增加到 33,820 行（+625 個非空行）。base 本身已經超過該 slice 與其他幾項 advisory
  review threshold，本批沒有改 ratchet。
- 不做：job object（decision 85，另排 R2）、啟動失敗的分類（bug 檔）、任何
  Presentation 或 CLI 程式碼、VersionManagement 的終止 owner、manifest timeout。

## 5. 風險分級：R2

- 屬於 R2 的理由：
  - 改變 runner 的 terminal contract：新增公開 enum 與屬性、取消優先順序、host
    deadline。
  - 新增一個 issue code，並把 exit 0 但 cleanup 不完整的情況改判為失敗。
  - 新增 ADR 並修改 ADR 0006。validator 從 `docs/adr` 推得的最低風險就是 R2。
- 不屬於 R3 的理由：
  - 不改韌體 bytes、ranges、順序、CRC/Header、命名、profile、manifest、release
    或權限。
  - 若之後加入 containment 或需要平台權限的能力，要重新評估。
- 非回歸證據：pre-freeze 時跑既有的真工具 smoke 與 Golden 篩選子集，證明正常路徑
  bytes 不變。這不是 Golden 認證。

## 6. 測試計畫與目前結果

### 6.1 lifetime 測試（`SystemExternalProcessRunnerLifetimeTests`，24 個方法、30 個案例）

第 6 版：`CapacityIsAHardCapUnderConcurrentStarts` 的 exit seam 改為開閘後仍等真實程序退出，並斷言每個獲准的 run 為 `ExitCode == 0`、`Cleanup == Complete`（F-16）；`TerminationSeam.Block` 改為開閘後執行真實 tree kill。
第 5 版：`CapacityIsAHardCapUnderConcurrentStarts`（原 `DetachedBudgetIsAHardCapUnderConcurrentStarts`）
改為獨立執行緒＋`Barrier` 同時起跑（F-12）；新增 `TryReserveIsAtomicUnderContention`
（F-12）、`DisposalFailureStillReturnsSlotAndSignalsFailure` 與
`DetachedDisposalFailureStillReturnsSlotAndSignalsFailure`（F-13）；移除依賴環境的真實
OS 拒絕終止 fixture（分類改以 refusal seam 測試為準）。
第 4 版：改寫 `OrphanHoldingOutputAfterTimeoutIsBoundedAndReported` 加入 PID-based
的 parent-exited handshake（F-4b），把 `DetachedCleanupIsBoundedAndRefusesNewRunsAtTheLimit`
的計數斷言改為等 `ResourcesReleased` 後讀（F-10）。第 3 版新增：schedule 純函式測試、
`CancelWithinAsync` 守門、`CleanupDiagnosticsDescribeOnlyTheObservation`。

| 測試 | 類型 | 驗證內容 | 紅測方法（在 base 上） |
| --- | --- | --- | --- |
| `CancelReturnsAtOnceAndRunEndsWithinDeadlineWhileTerminationBlocks` | 阻塞 seam + 真 ping | `Cancel()` < 1 秒；run 在 deadline + 4 秒內結束；termination 只呼叫一次；termination 阻塞期間 custody 不 release，解除阻塞後才 release | 程式碼推論：base 在 callback 內同步 kill，kill 阻塞時 `Cancel()` 也會阻塞；harness 只觀察到丟例外，沒有觀察到阻塞 |
| `TimeoutWithBlockedTerminationReturnsUnconfirmedWithinDeadline` | 阻塞 seam | `TerminationUnconfirmed`；TimeoutSignaled → Returning 在上限內 | 程式碼推論：base 在 runner 執行緒上同步 kill，之後的等待也沒有上限 |
| `RefusedTerminationIsClassifiedAsUnconfirmed`（×4） | 拒絕 seam | 四種例外都分類為 unconfirmed | harness：`AggregateException` 外漏 |
| `RefusedTerminationOnCancellationEndsCanceled` | 拒絕 seam | `Cancel()` 不丟例外，run 以 OCE 結束 | harness denied-kill-cancel |
| `TerminationWithoutObservedExitIsBoundedAndUnconfirmed` | 不生效 seam | 有上限且為 unconfirmed | 程式碼推論（base 等待沒有上限） |
| `CancellationRightAfterTimeoutSignalEndsCanceled` | phase handshake | timeout 決定後立刻取消，仍以 OCE 結束 | 編譯層級（base 沒有 seam） |
| `CancellationRightAfterExitSignalEndsCanceled` | phase handshake | 自然結束後立刻取消，仍以 OCE 結束 | harness held-pipe-normal-cancel：base 回傳 exit 0 |
| `CancellationAfterTerminalDecisionKeepsResult` | phase handshake | 決定點之後取消，結果不變 | 編譯層級 |
| `UncooperativeReaderIsDetachedAtDeadlineAndObservedLater` | reader seam | 回傳 `OutputStreamHeldOpen`；reader 還在執行時 custody 不 release；reader 晚到的 fault 被觀察後才 release | 編譯層級 |
| `ReaderFaultWithoutCancellationIsOutputReadFailed` | reader seam | reader fault 分類為 `OutputReadFailed` | 編譯層級 |
| `ReaderFaultWithCancellationEndsCanceled` | reader seam + handshake | 取消優先於 reader fault | 編譯層級 |
| `ExitObservationFaultIsUnconfirmedOrCanceled`（×2） | exit 觀察 seam | 未取消時為 unconfirmed、exit -1，termination 只一次；取消時為 OCE | 編譯層級 |
| `HeldOutputAfterNaturalExitIsBoundedAndReported` | 真實 OS | `OutputStreamHeldOpen`；ExitSignaled → Returning 在 5 + 4 秒內 | harness：base 需要 10.4 秒並回傳成功 |
| `CancellationDuringHeldDrainEndsCanceledWithinDeadline` | 真實 OS + handshake | `Cancel()` < 1 秒，run 以 OCE 結束並在上限內 | harness：base 回傳 exit 0 |
| `OrphanHoldingOutputAfterTimeoutIsBoundedAndReported` | 真實 OS | `OutputStreamHeldOpen`，在上限內 | harness：base 需要 10.2 秒 |
| `DescendantWithoutRedirectedStreamIsNotObserved` | 真實 OS | 固定 decision 85 的限制：結果是 `Complete`，但後代仍在執行 | 固定限制，不是修正項目 |
| `CleanupScheduleKeepsReaderStopWithinTheSingleDeadline` | 純函式 | reader-stop = deadline − reserve（在 deadline 之內，不相加） | 若把 reserve 加在 deadline 之後就會失敗 |
| `CancelWithinGuardFailsPromptlyOnBlockingCancel` | 守門 | 同步阻塞的 callback 會在 1 秒內被判失敗，而非卡到 watchdog | 這是守門本身的回歸測試 |
| `CleanupDiagnosticsDescribeOnlyTheObservation`（×3） | 純函式 | 三個 cleanup 值的文字只描述觀察，不含 "another process"／" kept " | 若沿用第 2 版的措辭就會失敗 |
| `DetachedCleanupIsBoundedAndRefusesNewRunsAtTheLimit` | 阻塞 seam + 上限 1 | 達上限時新執行以 `ExternalProcessCleanupCapacityException` 被拒；等 `ResourcesReleased` 後 `InUse==0`，新執行成功 | 編譯層級（base 沒有預算） |
| `CapacityIsAHardCapUnderConcurrentStarts` | 獨立執行緒＋共同 `Barrier` 起跑、多 runner 共用容量、阻塞 exit-observation seam | 上限 3、共 8 個同時呼叫 `RunAsync`：恰好 3 個啟動程序、5 個在啟動前被拒；打開 exit gate 後每個已啟動 run 發布 `ResourcesReleased`、容量歸零。內含 10 次迭代 | 程式碼推論：base 的預檢在併發下可超過上限 |
| `TryReserveIsAtomicUnderContention` | 純測試，32 條執行緒由 `Barrier` 同時放行 | 恰好上限個成功、拒絕觀察值不低於上限、歸還後歸零；200 輪 | 非原子的讀取—判斷—遞增會讓超過上限的呼叫通過 |
| `DisposalFailureStillReturnsSlotAndSignalsFailure` | 處置故障注入（inline 路徑） | 5 項處置都嘗試、名額歸零、只發布 `ResourcesReleaseFailed`；歸還的名額可再用 | 編譯層級 |
| `DetachedDisposalFailureStillReturnsSlotAndSignalsFailure` | 處置故障注入（detached 路徑） | 同上，在 detached continuation 內 | 編譯層級 |

### 6.2 其他測試

- reader 單元測試：`StoppedDrainKeepsCapturedTextWithoutEndOfStream`、
  `CompletedDrainReportsEndOfStream`。
- `ExternalCombinerProcessorTests.CleanupOutcomeIsClassifiedBeforeAnyStagedRead`（6 列，
  含 `Complete` 對照）、`TransformMapsCleanupCapacityRefusalToTypedIssue`。
- `LegacyCombinerPostbuildProcessorTests.CleanupOutcomeStopsSequenceBeforeAnyStagedRead`
  （6 列，兩個 command 的 profile，驗證 `RunCount == 1`）、`CleanupCapacityRefusalMapsToTypedIssue`。
- `ToolchainRuntimeCandidateInspectorTests`：`ProbeWithIncompleteCleanupFailsClosed`（3 列）、
  `ProbeTimeoutKeepsPriorityOverIncompleteCleanup`、`ProbeCleanupCapacityRefusalCarriesRestartGuidance`
  （probe 層帶 restart 訊息）、`CapacityRefusalDuringProbeReportsRestartGuidance`
  （inspector 層最終訊息含 Restart、不含「Windows could not verify」）。

### 6.3 執行結果（暫時 worktree，base `557a9ee6a` 加上第 6 版 patch；不是 w5 分支）

| 範圍 | 結果 | 時間 |
| --- | --- | --- |
| build（Infrastructure 與 Infrastructure.Tests，analyzer 開啟） | 0 警告、0 錯誤 | - |
| `SystemExternalProcessRunnerLifetimeTests` | 30/30 通過 | 32 秒 |
| `CapacityIsAHardCapUnderConcurrentStarts` 與 `TryReserveIsAtomicUnderContention` 單獨連跑 | 另外 3 次全綠（前者每次 10 迭代、後者每次 200 輪） | 各約 1 秒 |
| 窄篩選：`SystemExternalProcessRunner`、`BoundedProcessOutputReaderTests`、`ExternalCombinerProcessorTests`、`LegacyCombinerPostbuildProcessorTests`、`ToolchainRuntimeCandidateInspectorTests` | 126/126 通過（第 6 版的單次完整執行） | 31 秒 |
| Architecture.Tests（文件與 ADR 修改後） | 269/269 通過 | 15 秒 |

第五輪審查指出，`Barrier` 不保證每次都命中特定的指令交錯，所以重跑全綠只是競爭壓力
的證據，不是原子性的形式證明；上限的正確性另由 CAS 程式本身的推論支撐。

- analyzer 0 警告。
- 尚未跑：真工具 smoke、Golden 子集、`verify.py`。validator 目前會因為 record
  還沒 stage 而失敗，這是已知且只和 record 有關的阻擋，所以沒有執行。
- 測試結束後確認沒有殘留的 helper 程序。

### 6.4 環境注意事項

- 孤兒與「沒有接 pipe 的後代」兩個測試的 helper 會在 run 之後存活，所以必須在 workspace 外執行。
- 不需要 UI host，也不會寫到本機狀態檔。

## 7. 第二批（F01/F02/F25）：盤點與設計入口條件

### 7.1 F01：handoff 失敗後的恢復

- 程式碼推論的缺陷：
  - 第一次 Close 呼叫 `CompleteAsync()`，把兩個 coordinator 的 `_isCompleted` 永久
    設成 true。
  - handoff 失敗後視窗恢復可用，此時使用者改主題或語言、或產生新報告，會在
    `PropertyChanged` handler 中丟出 `InvalidOperationException`，沒有被處理。
  - `_isReportHistoryPersistenceComplete` 一直是 true，所以第二次 Close 不會再
    flush。
  - `_startupLoadCancellation` 已被取消，之後 preload 的 Retry 用的是已取消的 token。
- 依賴：F08（`feature/1.1.13/f08-save-notice`）也改了
  `LatestSnapshotPersistenceCoordinator.cs` 與 `MainWindow.axaml.cs`，而且保留了
  閂鎖。第二批要以 F08 合入後的版本為基礎。
- decision 88：launcher 起不來、pending activation 也清不掉時，第二次 Close 提供
  「Retry」與「Close anyway」，並說明 pending 設定仍保留、沒有還原。

### 7.2 F02：READY 取消

- 父程序端已經隔離，並有 `CallerCancellationPropagates`、`ReadyTimeoutFailsBoundedly`
  覆蓋。
- 子程序端（程式碼推論）：在 READY 寫入或 `InitializeAfterManagedReadyAsync` 期間
  關閉視窗，OCE 會從 `async void OnOpened` 漏出；`ApplyVersionSnapshot` 也可能在
  關閉後才發布。

### 7.3 F25：dispose 前收尾

- 程式碼推論：`OnClosing` 只呼叫 `CancelActiveRun()`，沒有等待；`Dispose()` 只處理
  startup CTS 與 preload；`MainWindowViewModel` 沒有實作 `IDisposable`；
  `RunCompositionAsync` 結束時仍會發布結果。
- decision 87：不提供強制關閉；有界等待後自動關閉，但前提是所有舊工作都已失去發布
  結果的權利。

### 7.4 第二批設計入口條件（審查 F-7）

第二批設計必須先滿足下列條件，才能進入審查：

1. **Close attempt 與 session generation**：
   - 每次 Close 嘗試都建立新的 close generation。
   - handoff 失敗後恢復時，建立新的 session generation，並配上新的 startup
     cancellation。
   - 所有發布結果的地方（run、inspection、Config、Report、版本管理、READY 後的
     版本快照）都必須同時核對 generation、取消狀態與 lifetime 三項。只檢查
     「視窗還活著」不夠：handoff 失敗後視窗仍然存活。
2. **每個 task owner 的收尾契約**：run、inspection、Config
   （`EventBufferFormatConfigurationSession`、`ToolchainRuntimeConfigurationSession`）、
   Report、版本安裝／切換／刪除、READY，都要各自定義等待上限、逾時後的失效方式
   （失去發布權），以及 fault 的觀察方式。第一批 runner 的 5 秒 deadline 只涵蓋
   外部程序，不能用來推導其他工作的完成上限。
3. **Decision 87**：自動關閉前，所有舊 generation 都必須已失去發布權，而且它們的
   fault 仍會被觀察，不會遺失。
4. **Decision 88** 的 UI 與文字，需要 Settings 或對話框的設計參考圖（依 owner 的
   UI mockup 規則）。
5. **必要測試**：
   - 「handoff 失敗 → 恢復 → 舊 callback 晚到」：舊 callback 不得覆蓋新 session。
   - Close → flush → handoff 失敗 → 修改設定 → 第二次 Close。
   - READY 期間關閉視窗。
   這些都需要 headless Avalonia host。要等「UI 測試寫到真實本機狀態檔」的 bug
   （[BUG-20260926-tests-write-real-local-state](../bugs/BUG-20260926-tests-write-real-local-state.md)）
   修好，或全部改用隔離的狀態檔之後，才開始寫測試。
6. **順序**：修正並驗收 F03/F06 → 納入 F08 與隔離本機狀態的測試前提 → F01+F25 共用
   Close 狀態機 → F02。F02 的 generation 與取消契約要一起設計；完整的 lifetime
   驗收要等 F02 完成。

## 8. Owner 決定（已決定）與剩下的問題

已決定：

- **85**（2026-09-26）：觀察到 cleanup 不完整就 fail closed，不讀輸出；接受「沒有接
  pipe 的後代可能看不到」；Job Object 另排 R2。
- **86**（2026-09-26）：終止確認與 reader 停止共用總共 5 秒的 host deadline；取消
  callback 只設訊號。
- **87**（2026-09-26）：不提供強制關閉；有界等待後自動關閉，但要先讓舊工作失去發布權。
- **88**（2026-09-26）：launcher 起不來、pending 清不掉時，第二次 Close 提供「Retry／
  Close anyway」，並說明 pending 設定仍保留、沒有還原。
- **92**（2026-09-27）：detached、尚未 settled 的 invocation 數量有小的固定上限，超過
  時新執行以型別化錯誤拒絕，提示重新啟動。本版以固定硬上限 8 實作（原子保留）。
- **Q3**（依審查建議）：第一批不新增 warning，但不能顯示「已確認全部停止」。

第三輪審查已明確表示：上限 8、5 秒內部分配與 OCE 訊息方案不需重新詢問 owner。以下
只保留仍值得 commander 知會的項目。

需要 commander 決定或確認：

1. **上限數值 8**：若 owner 想要更小（例如 4）或更大，改 `ExternalProcessCapacity.DefaultLimit`
   一個常數即可（併發硬上限的機制與數值無關）。
2. **ADR 0006 的反向連結**：本 patch 在 ADR 0006 加上
   `Amended by: ADR 0015, ADR 0081`。ADR 0081 目前是 Proposed，這行應在 ADR 0081
   被接受時才生效。建議保留在 patch 中，admission 時由重審確認。
3. **啟動失敗 bug 的順序**：建議第一批合入後，另開同 owner 的 R1。

## 9. 工作量預估

依據是 roadmap 的相對單位（S=1、M=2、L=3；包含實作、fault-injection、契約測試與
審查；單位不是天數）。

| 批次 | 估計 | 依據 | 不確定性 |
| --- | --- | --- | --- |
| 第一批 F03/F06 | M（2），其中剩餘約 0.5 | 第 6 版草稿已完成：lifetime 30/30、窄測試 126/126、Architecture 269/269、analyzer 0 警告，兩個併發測試另連跑 3 次穩定。第五輪的 F-16、F-17 與 ADR 措辭都已處理。剩下第六輪重審、admission（batch 2b 合入後改綁 checkpoint）、真工具與 Golden 子集的非回歸證據、固定 head 審查與 final evidence | 低：第五輪只剩 P2/P3 |
| 第二批 F01+F25 | L（3），可能超過 | Close 狀態機、generation、各 task owner 的收尾契約、UI 測試 | 高：依賴 F08、本機狀態隔離修正，以及 decision 88 的 UI 設計；Config 與 Report 的 dispose 路徑還沒盤完 |
| 第二批 F02 | S–M（1–2） | 與 F01 共用 generation 契約 | 中：READY 時序的測試控制 |

## 10. 產出與狀態

- 狀態：planned。只在本機寫了文件；沒有提交，也沒有改 w5 的產品碼。
- w5 內（未提交）：
  - 本文件。
  - [BUG-20260926-process-start-failure-escapes-typed-result](../bugs/BUG-20260926-process-start-failure-escapes-typed-result.md)。
- scratchpad `w5/`（repo 外）：
  - `PROCESS-CLEANUP-1113-01.json`：record 草稿，schema v2，`design-active`，15 個
    `mutablePaths`（10 governed＋5 aux），`designReview` 為 `codex/gpt-6-astra` /
    `blocked`，並記錄第 1 至 5 版的審查結論；`integrationBase` 為 `38b85b15b`；`implementationOwner` 是 `claude-code`。
  - `PROCESS-CLEANUP-1113-01.proposal.patch`：第 6 版 patch，從 `557a9ee6a` 產生，
    可以乾淨套用到 w5（`git apply --check`）。
  - `ADR-0081-external-process-cleanup.draft.md`：ADR 草稿，內容與 patch 內的
    `docs/adr/0081-external-process-cleanup.md` 相同。
  - `repro-evidence.txt`、`repro-harness/`：base 的重現證據，以及各版草稿的結果。
  - 各檔案的 SHA-256 在回報中提供。
