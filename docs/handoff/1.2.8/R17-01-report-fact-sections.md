# R17-01 — typed Report facts / physical sections inventory

## Dispatch envelope

- **Outcome**: Map every existing typed Report fact (root and nested input, operation, mutation, validation, output, delivery and diagnostic facts) to its physical section, with type member, section, source and the reading for historical replay (decision 297).
- **Non-goals**: No change to the Report contract, the typed projector, the schema, fixtures, UI or layout; no reference image; the retired experience is recorded as read-only replay and does not become an executable feature.
- **Authority**: the worker may edit this file and record evidence outside Git, and make a local commit on the task branch; push, pull request and integration belong to the commander. Risk class: R0 (documentation under `docs/handoff/**`; no production, contract or test change).
- **Branch and worktree**: local task branch `feature/queue/r17-01-report-fact-sections` (never published, so its commits have no public SHA), base `b5d996c5f` on the `1.2.x` trunk; a local task worktree whose path is not kept here.
- **Write lock**: `docs/handoff/1.2.8/R17-01-report-fact-sections.md`. Everything else is read-only for the worker.
- **Read first**: Inventory row R17-01, the Report projector types, decision 297. Owner-search disposition: `reuse` (documents only).
- **Model reason**: Codex implementation worker run with its configured default model and effort of that day (not recorded per run); a bounded R0 documentation or measurement task.
- **Acceptance**: the brief's document-shape check on this file and `python scripts/verify.py --structure-only`, both exit 0 outside the worker's sandbox.
- **Stop and ask**: any need to edit outside the write lock, to change production code, a contract, an ADR or a test, or a result that would change an owner decision: stop and record it under Open.

狀態（2026-10-04）：依 owner 本次任務所述決策 **297** 交付文件 inventory。
來源 snapshot：`b5d996c5f444e0c8ed5a4a82d84753f3ca03c11c`。
本 checkout 未附決策 297 原文；其授權範圍採本次任務的明示要求。
[R17-01 / R17-02 原始 inventory](../1.1.14/1.2.x-inventory.md) 是歷史分配；
[現行 roadmap](../../architecture/nfc_roadmap.md) 將 Report 工作排在 `1.2.8`。

本文件逐項盤點既有 `CompositionRunReport` 物件圖的 public instance facts，
包括 positional record 成員與既有 computed properties。它是資料落點對照，
不增加 Report facts、schema、typed projector 或 UI 行為，也不宣稱完整 layout 已核准。
型別中的 `?` 保留 nullable 意義；collection 成員再以元素型別逐項展開。
Application typed projection 與 frozen canonical
[composition-report-v1](../../contracts/composition-report-v1.md) 是不同模型，
不能把下表當作 canonical wire schema 的新增欄位清單。

## Section 與 replay 解讀

`Physical section` 欄記錄每個 fact 的閱讀落點與已有的 byte/identity 關係，
不是新 section classifier、tab 次序、間距、geometry 或 approved layout。
沒有 physical range 的 run facts 保持 run 範圍；不為湊 physical grouping 推算區域。

| Section | 既有資料關係與落點 |
| --- | --- |
| Run | run/profile/IC/mode/experience/compilation identity、時間與 admission；沒有 byte section 的全域 facts。 |
| Inputs | 各 input address space / binding / immutable accepted snapshot；AB TP A/TP B primary 與來源長度。來源 range 不等於 output write range。 |
| Operations | 原 plan order、source/target spaces 與 ranges、processor 權限、provenance、實際 invocation；無 physical parent fact 時保留 target space/range。 |
| Changes | mutation trace 與 Replace final-output differences 分開閱讀；difference 的 physical parent 採既有 `Semantic.ParentLabel` / `SectionLabel` 關係。 |
| Validations | rule/stage/outcome/severity/code；目前 row 沒有 physical range，保持 run/rule 範圍，不由 code 猜區域。 |
| Output | final image identity、initialization、captured source envelope、名稱與 token provenance。 |
| Delivery | 額外交付檔案的 source range、atomic bundle receipt 與各實際檔案。 |
| Diagnostics | 原 issue list、input issue-index 關聯、General admission blockers、plan-only coverage/readiness；不等同 output 或已執行 mutation。 |

Changes 的現有 typed 路徑先用非空 `Semantic.ParentLabel`，再用非空
`SectionLabel`、`Semantic.CategoryLabel`、既有 classification 顯示 fallback。
歷史 JSON 路徑保留其既有 `SectionLabel` null fallback（不在此統一空字串處理）；
兩條路徑見 [既有 difference projection][DG]。`ParentId` / `SubjectId` 是記錄中的
identity，不能從 offset、檔名或目前 profile 補造。Mutation 的 `TargetRange`
不是 final-output difference；processor allowed range 也不是實際 changed range。
跨區域的 operation/mutation 保留完整原 range，不裁切或合併成新的 audit evidence。
所有 `ByteRange` 均以其所屬 address space 下的 `[Start, EndExclusive)` 解讀。

下表 Replay 欄的「原值」均指 **read-only replay**：保留儲存的 identity、順序、
range、hash、狀態與 bytes，不重開舊路徑，不依目前 settings/profile 重算。
nullable／後來加入的欄位缺失代表未記錄；collection 的空投影不證明當年沒有該行為。
[既有 JSON completeness owner][J] 決定歷史結果可否辨識；不是以新版所有成員必填
來拒絕舊資料。未辨識資料保留原 JSON/Raw 與中性 `Unknown`，不捏造成功或 firmware error。
`Output.Committed`、operation status、validation status、difference acceptance 各自保留意義。

已退休 experience（包括舊 DP Replace）只顯示原 `ExperienceId` / `ModeId` 與已記錄
input、operation、mutation、output details；維持 **read-only replay**。
這不重新 admission、啟用 route 或把舊 report 轉成可執行 request。
歷史 fixture 選集、schema/version replay 驗證屬 R17-02；本 inventory 不宣稱完成那些 gates。

## Root facts

宣告：[CompositionRunReport][T]；一般 producer：[CreateReport][R]。
Source 欄是既有宣告/producer 與資料來源，不是新 projector 的實作設計。

| Fact | Physical section | Source | Replay |
| --- | --- | --- | --- |
| `CompositionRunReport.RunId` — `string` | Run / identity | [R] request.RunId | 原 run identity。 |
| `CompositionRunReport.ProfileId` — `string` | Run / profile | [R] compiled V2Details.ProfileId | 原 profile id；不改綁新版。 |
| `CompositionRunReport.ProfileVersion` — `string` | Run / profile | [R] compiled V2Details.ProfileVersion | 原版本；缺失不補目前版本。 |
| `CompositionRunReport.IcId` — `string` | Run / IC | [R] compiled context.MemberId | 原宣告 IC，不由檔名推定。 |
| `CompositionRunReport.ModeId` — `string` | Run / mode | [R] compiled context.ModeId | 原 mode；退休 mode 唯讀。 |
| `CompositionRunReport.ExperienceId` — `string` | Run / experience | [R] compiled V2Details.ExperienceId | 原 experience；退休 route 不執行。 |
| `CompositionRunReport.CompositionKind` — `CompositionKind` | Run / Merge or Replace | [R] compiled V2Details.CompositionKind | 原 kind，不由 Changes 有無推定。 |
| `CompositionRunReport.StartedAtUtc` — `DateTimeOffset` | Run / time | [R] captured startedAtUtc | 原時間。 |
| `CompositionRunReport.CompletedAtUtc` — `DateTimeOffset` | Run / time | [R] captured completedAtUtc | 原時間；缺失不估算。 |
| `CompositionRunReport.Inputs` — `IReadOnlyList<InputArtifactSummary>` | Inputs / each address space | [R], [IL] accepted input summaries；[DP] plan-only accepted stamps | 原 bindings/snapshots，展開下表。 |
| `CompositionRunReport.Operations` — `IReadOnlyList<OperationRunSummary>` | Operations / each target space/range | [R] Plan.OrderedOperations；[DP] planning ReplaceRange rows | 原 plan order/status；planned 不等於 executed。 |
| `CompositionRunReport.Mutations` — `IReadOnlyList<MutationRunSummary>` | Changes / mutation target space/range | [R] execution.Mutations → ToMutationSummary | 原 traces；不當作 final differences。 |
| `CompositionRunReport.Issues` — `IReadOnlyList<CompositionIssue>` | Diagnostics / run issues | [R] execution.Issues + additionalIssues；[DP] blocker issue | 原 list/index/code/severity；不由文案猜結果。 |
| `CompositionRunReport.Output` — `OutputArtifactSummary` | Output / final image | [R] execution.OutputBytes + commit result；[DP], [J] diagnostic exception | 原產物事實；plan-only 顯示無 output，見下文。 |
| `CompositionRunReport.OutputDifferences` — `IReadOnlyList<OutputDifferenceSummary>` | Changes / physical parent and exact diff range | [OD], [R] reference/final-output comparison | 原 Replace rows；缺失/空不證明未發生 mutation。 |
| `CompositionRunReport.CompilationFingerprint` — `string?` | Run / compilation identity | [R] CompiledComposition.CompilationFingerprint | 原 fingerprint；缺失不重新 compile 補值。 |
| `CompositionRunReport.MapId` — `string?` | Run / canonical map | [R] map-bound context.ResolvedMap.ImageMap.MapId | 原 map id，不視為 output length。 |
| `CompositionRunReport.SourceEnvelope` — `SourceEnvelopeRunSummary?` | Inputs / captured DP extent → Output / extent | [T], [R] compiled SourceEnvelopeExtent | 原 captured extent；缺失不推算。 |
| `CompositionRunReport.Validations` — `IReadOnlyList<ValidationRunSummary>` | Validations / rule and stage | [VI], [VF], [R] compiled validation outcomes | 缺失採既有空集合投影，不代表全部 Passed。 |
| `CompositionRunReport.OutputNaming` — `OutputNamingSummary?` | Output / naming provenance | [N], [NR] compiled renderer + accepted inspection | 原 tokens/name；缺失不重新命名。 |
| `CompositionRunReport.DeliveryArtifacts` — `IReadOnlyList<DeliveryArtifactSummary>?` | Delivery / additional files | [DL], [RS] completed primary-output delivery | 原 receipts；缺失不宣稱曾交付。 |
| `CompositionRunReport.GeneralAdmission` — `GeneralAuthoringAdmissionSummary?` | Run / admission; Inputs / resources; Operations / occupancy; Diagnostics / blockers | [GA] admission.ToSummary；[R], [DP] request/admission | 原 Parent/Saved Rule/limits，不重新 admission。 |
| `CompositionRunReport.ImageInitialization` — `ImageInitializationSummary?` | Output / initialization | [IN] FromCompiled；[R] General Merge；[DP] plan-only Replace | 原 blank/reference provenance；缺失不由 kind 補值。 |
| `CompositionRunReport.DiagnosticPreview` — `GeneralReplaceDiagnosticPreviewSummary?` | Diagnostics / non-executing plan | [DP] accepted diagnostic projector | 保留 plan-only，不宣稱 final integrity。 |
| `CompositionRunReport.BundleDelivery` — `CompositionOutputBundleDeliverySummary?` | Delivery / atomic bundle | [BD], [RS] commit receipt | 原實際交付；Preview/loose output 無此 receipt。 |
| `CompositionRunReport.AbMergeFormat` — `AbMergeFormatRunSummary?` | Inputs / TP A/B format evidence; Run / captured configuration | [AB] execution admission selection；[R] request.AbMergeFormat | 缺失是未記錄，不推定 Common format。 |
| `CompositionRunReport.InputDiagnostics` — `IReadOnlyList<InputDiagnosticSummary>?` | Diagnostics / input-linked issues | [ID], [R] CreateInputDiagnostics(final Issues) | 原零基底 issue index；不靠 code 猜關聯。 |

## Input 與 captured extent / format facts

| Fact | Physical section | Source | Replay |
| --- | --- | --- | --- |
| `InputArtifactSummary.AddressSpaceId` — `string` | Inputs / source address space | [I], [IL] binding.AddressSpaceId | 原 space。 |
| `InputArtifactSummary.ArtifactId` — `string` | Inputs / artifact binding | [I], [IL] binding.BindingId | 原 local/binding id，非 portable path。 |
| `InputArtifactSummary.Size` — `long` | Inputs / complete source extent | [I], [IL] accepted source length | 原 whole-source size；不混同 accepted prefix。 |
| `InputArtifactSummary.Sha256` — `string` | Inputs / complete source identity | [I], [IL] source snapshot hash | 原 whole-source hash。 |
| `InputArtifactSummary.OriginalFileName` — `string?` | Inputs / filename | [I], [IL] plain binding filename | 缺失不從 ArtifactId/path 補造。 |
| `InputArtifactSummary.ExecutionSnapshot` — `InputArtifactExecutionSnapshotSummary?` | Inputs / accepted prefix and ignored tail | [I], [IL] compiled input inspection | 缺失不推定截尾/padding。 |
| `InputArtifactExecutionSnapshotSummary.AcceptedRange` — `ByteRange` | Inputs / accepted source prefix | [I], [IL] AcceptedSnapshotRange | 原 source-space range。 |
| `InputArtifactExecutionSnapshotSummary.AcceptedSize` — `long` | Inputs / accepted source prefix | [I] AcceptedRange.Length | 既有 computed property，非新 fact。 |
| `InputArtifactExecutionSnapshotSummary.AcceptedSha256` — `string` | Inputs / accepted source identity | [I], [IL] AcceptedSnapshotSha256 | 原 prefix hash，不用 whole-source hash 代替。 |
| `InputArtifactExecutionSnapshotSummary.IgnoredTrailingRange` — `ByteRange?` | Inputs / ignored source tail | [I], [IL] inspection.IgnoredTrailingRange | 原 tail；null 不補 range。 |
| `InputArtifactExecutionSnapshotSummary.IgnoredTrailingBytes` — `long` | Inputs / ignored source tail | [I] IgnoredTrailingRange?.Length ?? 0 | 既有 computed property；不回寫歷史 JSON。 |
| `SourceEnvelopeRunSummary.SourceSlotId` — `string` | Inputs / captured DP slot | [T] SourceEnvelopeExtent.SourceSlotId | 原 slot。 |
| `SourceEnvelopeRunSummary.RootRegionId` — `string` | Output / canonical container anchors | [T] SourceEnvelopeExtent.RootRegionId | 原 region id，不延伸寫入權限。 |
| `SourceEnvelopeRunSummary.LayoutTemplateMapId` — `string` | Output / canonical layout template | [T] compiled envelope.LayoutTemplateMapId | 原 template map，非新 layout approval。 |
| `SourceEnvelopeRunSummary.LayoutTemplateCapacity` — `long` | Output / template capacity | [T] compiled envelope.LayoutTemplateCapacity | 原 template length，不當 actual output length。 |
| `SourceEnvelopeRunSummary.ActualOutputLength` — `long` | Inputs / captured DP → Output / actual extent | [T] compiled envelope.ActualOutputLength | 原完整 extent；不依目前 map 重算。 |
| `SourceEnvelopeRunSummary.ExpectedOuterLengths` — `IReadOnlyList<long>` | Inputs / declared outer-length advisory | [T] compiled envelope.ExpectedOuterLengths | 原 advisory values，非新的硬性 admission。 |
| `SourceEnvelopeRunSummary.UnexpectedLengthIssueCode` — `string` | Diagnostics / source-envelope warning | [T] compiled envelope.UnexpectedLengthIssueCode | 原 typed nonblocking code。 |
| `AbMergeFormatRunSummary.FormatId` — `string` | Inputs / admitted AB format | [AB] captured selection.FormatId | 原 format，缺失不推定 Common。 |
| `AbMergeFormatRunSummary.DisplayName` — `string` | Inputs / captured format label | [AB] captured selection.DisplayName | 原 alias，不讀目前 Settings。 |
| `AbMergeFormatRunSummary.ConfigurationGeneration` — `long` | Run / format configuration | [AB] captured selection generation | 原 generation。 |
| `AbMergeFormatRunSummary.ConfigurationSourceSha256` — `string` | Run / format configuration | [AB] captured configuration hash | 原 hash；不讀 config path。 |
| `AbMergeFormatRunSummary.FamilyId` — `string` | Run / captured family | [AB] captured selection.FamilyId | 原 family id。 |
| `AbMergeFormatRunSummary.FamilyVersion` — `string` | Run / captured family | [AB] captured selection.FamilyVersion | 原 version。 |
| `AbMergeFormatRunSummary.FamilyContentHash` — `string` | Run / captured family | [AB] captured selection.FamilyContentHash | 原 content identity。 |
| `AbMergeFormatRunSummary.TpA` — `PrimaryEvidence` | Inputs / TP A primary structure | [AB] CapturePrimary(CompositionAddressSpaceIds.TpAInput) | 展開既有 PrimaryEvidence；不重新 classify。 |
| `AbMergeFormatRunSummary.TpB` — `PrimaryEvidence` | Inputs / TP B primary structure | [AB] CapturePrimary(CompositionAddressSpaceIds.TpBInput) | 展開既有 PrimaryEvidence；A/B byte 可不同。 |
| `AbMergeFormatRunSummary.PrimaryEvidence.InputBindingId` — `string` | Inputs / TP A or B binding | [AB] admitted binding.BindingId | 關聯父 report input，非 path。 |
| `AbMergeFormatRunSummary.PrimaryEvidence.StructureId` — `string` | Inputs / TP primary structure | [AB] primary.MetadataStructureId | 原 canonical structure。 |
| `AbMergeFormatRunSummary.PrimaryEvidence.AddressSpaceId` — `string` | Inputs / TP primary address space | [AB] resolved primary range.AddressSpaceId | 原 source space。 |
| `AbMergeFormatRunSummary.PrimaryEvidence.Start` — `long` | Inputs / TP primary structure range | [AB] resolved range.Start | 原 structure start，非 format-field/write start。 |
| `AbMergeFormatRunSummary.PrimaryEvidence.EndExclusive` — `long` | Inputs / TP primary structure range | [AB] resolved range.EndExclusive | 原 structure end。 |
| `AbMergeFormatRunSummary.PrimaryEvidence.FormatByte` — `byte` | Inputs / observed TP primary format | [AB] admitted observed formatByte | 原 scalar observation，不存整個 source。 |

## Operation 與 mutation facts

| Fact | Physical section | Source | Replay |
| --- | --- | --- | --- |
| `OperationRunSummary.OperationId` — `string` | Operations / plan identity | [O], [R] CompositionOperation.OperationId | 原 id，連到 mutation/issue evidence。 |
| `OperationRunSummary.Sequence` — `int` | Operations / plan order | [O], [R] operation.Sequence | 保留 order，不按 physical section 重排執行語意。 |
| `OperationRunSummary.Kind` — `CompositionOperationKind` | Operations / primitive | [O], [R] operation.Kind | 原 kind。 |
| `OperationRunSummary.Status` — `OperationRunStatus` | Operations / execution status | [O], [R] execution/invocation evidence；[DP] Skipped | 保留 Succeeded/Failed/Skipped；plan-only 無執行。 |
| `OperationRunSummary.SourceSpaceId` — `string?` | Operations / source address space | [O], [R] operation.SourceSpaceId | 原 space；null 不補。 |
| `OperationRunSummary.SourceRange` — `ByteRange?` | Operations / source range | [O], [R] operation.SourceRange | 原 source range，不當 output range。 |
| `OperationRunSummary.TargetSpaceId` — `string` | Operations / target address space | [O], [R] operation.TargetSpaceId | 原 target space。 |
| `OperationRunSummary.TargetRange` — `ByteRange` | Operations / physical target range | [O], [R] operation.TargetRange | 原完整 range；不推定每 byte 都改變。 |
| `OperationRunSummary.OverlapPolicy` — `OverlapPolicy` | Operations / target overlap authority | [O], [R] operation.OverlapPolicy | 原宣告 policy。 |
| `OperationRunSummary.ProcessorId` — `string?` | Operations / processor identity | [O], [R] ExternalProcessorInvocation.ProcessorId | 原 id，非可執行路徑。 |
| `OperationRunSummary.ToolBindingId` — `string?` | Operations / processor binding | [O], [R] invocation.ToolBindingId | 原 binding，不解析成目前工具。 |
| `OperationRunSummary.ProcessorAllowedReadRanges` — `IReadOnlyList<ByteRange>` | Operations / processor allowed reads | [O], [R] invocation.AllowedReadRanges | 原權限範圍，不當觀察結果。 |
| `OperationRunSummary.ProcessorAllowedWriteRanges` — `IReadOnlyList<ByteRange>` | Operations / processor allowed writes | [O], [R] invocation.AllowedWriteRanges | 原權限範圍，不當實際 changes。 |
| `OperationRunSummary.ExecutedCommands` — `IReadOnlyList<ExternalProcessInvocation>` | Operations / completed invocation audit | [O], [R] executedCommandsByOperationId | 原完成的 command evidence；不重播 process。 |
| `OperationRunSummary.Reason` — `string` | Operations / declared reason | [O], [R] operation.Reason | 原 reason，不由文案判定成功。 |
| `OperationRunSummary.Provenance` — `OperationProvenance` | Operations / origin | [O], [R] operation.Provenance | 保留既有 producer/default 行為；不為舊 JSON 補寫 origin。 |
| `OperationProvenance.Kind` — `string` | Operations / origin kind | [OP] built-in-profile/runtime-general-mapping/saved-rule | 原 kind，不推定新的 authority。 |
| `OperationProvenance.SourceId` — `string?` | Operations / mapping or rule identity | [OP] recorded mapping/rule id | 原 id；built-in 可 null。 |
| `OperationProvenance.SourceVersion` — `string?` | Operations / rule revision | [OP] recorded saved-rule version | 原 revision，不載入現在的 rule。 |
| `ExternalProcessInvocation.ExecutablePath` — `string` | Operations / host invocation audit | [EP] ProcessStartInfo.FileName capture | 原文字 evidence，不執行、不視為 portable locator。 |
| `ExternalProcessInvocation.WorkingDirectory` — `string` | Operations / host staging audit | [EP] ProcessStartInfo.WorkingDirectory capture | 原 staging evidence，不重開路徑。 |
| `ExternalProcessInvocation.Arguments` — `IReadOnlyList<string>` | Operations / expanded argv | [EP] ordered ProcessStartInfo.ArgumentList capture | 原順序/values，不轉成執行命令。 |
| `MutationRunSummary.OperationId` — `string` | Changes / mutation operation link | [M], [R] MutationRecord.OperationId | 原 operation link。 |
| `MutationRunSummary.Kind` — `CompositionOperationKind` | Changes / mutation primitive | [M], [R] MutationRecord.OperationKind | 原 kind。 |
| `MutationRunSummary.TargetSpaceId` — `string` | Changes / mutation address space | [M], [R] MutationRecord.TargetSpaceId | 原 space，與 target range 一起閱讀。 |
| `MutationRunSummary.TargetRange` — `ByteRange` | Changes / mutation physical target range | [M], [R] MutationRecord.TargetRange | 原 trace range，非 final diff range。 |
| `MutationRunSummary.ChangedByteCount` — `long` | Changes / mutation changed bytes | [M], [R] sum(MutationRecord.ChangedRanges.Length) | 原 count，不用 TargetRange.Length 代替。 |
| `MutationRunSummary.BeforeSha256` — `string` | Changes / pre-mutation identity | [M], [R] MutationRecord.BeforeSha256 | 原 mutation hash，非 replay-plane hash。 |
| `MutationRunSummary.AfterSha256` — `string` | Changes / post-mutation identity | [M], [R] MutationRecord.AfterSha256 | 原 mutation hash，非最終整檔 hash。 |
| `MutationRunSummary.Reason` — `string` | Changes / mutation reason | [M], [R] MutationRecord.Reason | 原 reason。 |

## Validation 與 issue facts

| Fact | Physical section | Source | Replay |
| --- | --- | --- | --- |
| `ValidationRunSummary.RuleId` — `string` | Validations / rule | [V], [VI], [VF] compiled requirement id | 原 id；不重新查現行 rule。 |
| `ValidationRunSummary.Stage` — `CompiledValidationStage` | Validations / lifecycle stage | [V], [VI], [VF] captured validation stage | 原 stage，不一律當 final-output check。 |
| `ValidationRunSummary.Status` — `ValidationRunStatus` | Validations / outcome | [V], [VI], [VF] evaluated Passed/Failed or Skipped | Skipped 不算 Passed；缺失不新增 row。 |
| `ValidationRunSummary.Severity` — `CompiledValidationSeverity` | Validations / publication significance | [V], [VI], [VF] compiled requirement severity | 原 severity；warning 不升成 error。 |
| `ValidationRunSummary.IssueCode` — `string` | Validations / diagnostic link | [V], [VI], [VF] declared/emitted issue code | 原 code，不由 message 反推。 |
| `CompositionIssue.Code` — `string` | Diagnostics / issue identity | [IS], [R] compiler/execution/additional issue | 原 code。 |
| `CompositionIssue.Message` — `string` | Diagnostics / recorded text | [IS] emitted issue text | 原 text；不以字串猜 outcome。 |
| `CompositionIssue.Severity` — `string` | Diagnostics / info, warning, error | [IS] typed severity；[J] legacy recognition | 缺失/legacy alias 依既有 reader 解讀，不改原 JSON。 |
| `CompositionIssue.OperationId` — `string?` | Diagnostics / associated subject | [IS] producer-associated operation/subject id | 保留原關聯；不假設一定能找到 operation row。 |
| `InputDiagnosticSummary.IssueIndex` — `int` | Diagnostics / final Issues index | [ID], [R] exact issue-instance index | 零基底 index；不可先重排 Issues 再套用。 |
| `InputDiagnosticSummary.SlotId` — `string` | Diagnostics / input slot | [ID] compiler-declared slot | 原 slot，連到 input evidence。 |
| `InputDiagnosticSummary.Evidence` — `InputDiagnosticEvidence` | Diagnostics / immutable input finding | [ID] retained input-load evidence | 缺失不靠同 code 或目前檔案重建。 |
| `InputDiagnosticEvidence.AddressSpaceId` — `string` | Inputs / diagnostic source space | [ID] compiler-owned address space | 原 space。 |
| `InputDiagnosticEvidence.ActualLength` — `long?` | Inputs / observed source coverage | [ID] captured actual length | 原 length；null 未記錄。 |
| `InputDiagnosticEvidence.RequiredEndExclusive` — `long?` | Inputs / required source coverage | [ID] compiled required end | 原 exclusive bound，不讀目前 profile。 |
| `InputDiagnosticEvidence.SourceRange` — `ByteRange?` | Inputs / exact diagnostic range | [ID] compiler-declared source range | 原 source range，不當 output write。 |
| `InputDiagnosticEvidence.RepeatedByte` — `byte?` | Inputs / repeated-byte finding | [ID] captured scalar observation | 原單 byte evidence，非完整 source content。 |

## Output、Changes semantic 與 byte replay facts

| Fact | Physical section | Source | Replay |
| --- | --- | --- | --- |
| `OutputArtifactSummary.FileName` — `string` | Output / primary file | [OUT], [R] requested/resolved output filename | 原名稱；無 host path。 |
| `OutputArtifactSummary.Size` — `long` | Output / complete image extent | [OUT], [R] execution.OutputBytes.Length | 原 size；零不證明有產物。 |
| `OutputArtifactSummary.Sha256` — `string` | Output / complete image identity | [OUT], [R] output bytes hash / empty-output sentinel | 原 hash；不把 empty hash 當成功/完整性證明。 |
| `OutputArtifactSummary.Committed` — `bool` | Output / writer commit | [OUT], [R] writer-port commit result | 原 commit 事實；false 不等於沒有 preview bytes。 |
| `OutputDifferenceSummary.DifferenceId` — `string` | Changes / exact difference row | [D], [OD] stable report-order row id | 原 row id/navigation。 |
| `OutputDifferenceSummary.Range` — `ByteRange` | Changes / physical parent → output diff range | [D], [OD] final vs reference ByteDiff | 原 output-space range，不合併 gaps。 |
| `OutputDifferenceSummary.ChangedByteCount` — `long` | Changes / final changed bytes | [D], [OD] observed changed bytes | 原 count，不用 mutation count 代替。 |
| `OutputDifferenceSummary.Classification` — `string` | Changes / recorded classification | [D], [OD] Application difference policy | 原 classification，非 UI 重新分類。 |
| `OutputDifferenceSummary.IsAccepted` — `bool` | Changes / recorded verdict | [D], [OD] declared range/processor acceptance | 原 verdict，不等同 Golden/release approval。 |
| `OutputDifferenceSummary.Evidence` — `string` | Changes / authorization evidence | [D], [OD] operation/processor evidence | 原 evidence，不新增寫入權限。 |
| `OutputDifferenceSummary.Explanation` — `string` | Changes / recorded explanation | [D], [OD] Application policy explanation | 原 reason；缺 semantic 保留既有 fallback。 |
| `OutputDifferenceSummary.SectionLabel` — `string` | Changes / physical section label | [D], [OD], [DG] captured section + existing grouping | 原 label；不從 offset 推造新 section。 |
| `OutputDifferenceSummary.BeforeSha256` — `string` | Changes / reference changed-range identity | [D], [OD] reference slice hash | 原 changed-range hash，非完整 replay-plane hash。 |
| `OutputDifferenceSummary.AfterSha256` — `string` | Changes / final changed-range identity | [D], [OD] final slice hash | 原 changed-range hash，非整檔 hash。 |
| `OutputDifferenceSummary.BeforeHexPreview` — `string` | Changes / reference preview | [D], [OD] captured bounded preview | 截短 preview 不當完整 bytes。 |
| `OutputDifferenceSummary.AfterHexPreview` — `string` | Changes / final preview | [D], [OD] captured bounded preview | 保留 preview，缺 Replay 不補 byte planes。 |
| `OutputDifferenceSummary.HexPreviewByteCount` — `int` | Changes / preview coverage | [D], [OD] captured preview count | 原 count。 |
| `OutputDifferenceSummary.IsHexPreviewComplete` — `bool` | Changes / preview coverage | [D], [OD] captured completeness | 不替代 Replay validation。 |
| `OutputDifferenceSummary.Semantic` — `OutputDifferenceSemantic?` | Changes / physical parent and subject | [S], [OD] Application semantic classifier | 舊檔可缺失；不從位址推欄位。 |
| `OutputDifferenceSummary.Replay` — `OutputDifferenceReplaySegment?` | Changes / exact byte viewport | [B], [OD] persistable aligned context capture | 缺失/驗證失敗，Diff preview unavailable。 |
| `OutputDifferenceSemantic.CategoryId` — `string` | Changes / semantic category | [S] Application category identity | 原 category，不重分類。 |
| `OutputDifferenceSemantic.CategoryLabel` — `string` | Changes / category fallback | [S], [DG] captured label | 原 label，依既有 fallback。 |
| `OutputDifferenceSemantic.SubjectId` — `string` | Changes / field or section subject | [S] Application subject identity | 原 subject，不由地址推定。 |
| `OutputDifferenceSemantic.SubjectLabel` — `string` | Changes / field or section subject | [S] captured subject label | 原 label，不改 identity。 |
| `OutputDifferenceSemantic.Explanation` — `string` | Changes / semantic explanation | [S] Application explanation | 原 explanation。 |
| `OutputDifferenceSemantic.ParentId` — `string` | Changes / physical parent identity | [S] explicit parent or existing category default | 保留記錄/default；不補新 physical taxonomy。 |
| `OutputDifferenceSemantic.ParentLabel` — `string` | Changes / physical parent heading | [S], [DG] explicit parent or existing category default | 既有 grouping owner，缺失走既有 fallback。 |
| `OutputDifferenceReplaySegment.Range` — `ByteRange` | Changes / output-space context envelope | [B] bounded aligned context | 驗證原 output bound/unique envelope，勿擴張。 |
| `OutputDifferenceReplaySegment.BeforeBytes` — `ReadOnlyMemory<byte>` | Changes / Original byte plane | [B] captured immutable reference bytes | 只讀且驗證，原 Base64/bytes 不改寫。 |
| `OutputDifferenceReplaySegment.AfterBytes` — `ReadOnlyMemory<byte>` | Changes / final byte plane | [B] captured immutable output bytes | 只讀且驗證，不補未記錄 gaps。 |
| `OutputDifferenceReplaySegment.BeforeSha256` — `string` | Changes / complete reference-plane identity | [B] hash of entire BeforeBytes | 驗證 plane 與 changed-range 兩層 hash。 |
| `OutputDifferenceReplaySegment.AfterSha256` — `string` | Changes / complete output-plane identity | [B] hash of entire AfterBytes | 驗證 plane 與 changed-range/count，不代替整檔證明。 |

保留已完成 **Changes cards/navigation**，包括 selection、range order、Why/Result、
Original comparison、scrollbar gutter、virtualization 與 return navigation；
[既有 approved cards handoff](../../ui/v1.1.x-report-changes-compare-handoff.md)
只證明其已核准範圍，不能延伸成 R17 完整 layout approval。
同 section 的多個 differences 仍是各自的 audit rows。Replay 只含已記錄且驗證的
changed range 與兩側最多兩個 aligned 16-byte context rows；不橋接未知 gaps、
不重新讀 inputs、不持久化整個 BIN 來滿足 navigation。
缺 Replay、invalid Base64、range/length/hash/count/context 不符時保留 facts，
但 byte preview unavailable；歷史截短 Hex preview 不升格成完整 replay。

## Output initialization 與 naming facts

| Fact | Physical section | Source | Replay |
| --- | --- | --- | --- |
| `ImageInitializationSummary.Kind` — `ImageInitializationKind` | Output / initialization | [IN] compiled OutputInitialization.Kind | 原 blank/reference kind；不執行。 |
| `ImageInitializationSummary.Capacity` — `long` | Output / initialized extent | [IN] compiled capacity | 原 extent；與 SourceEnvelope/Output size 各保留意義。 |
| `ImageInitializationSummary.FillByte` — `byte?` | Output / blank fill | [IN] compiled blank fill; reference null | 原 fill；不據此重建未記錄 bytes。 |
| `ImageInitializationSummary.ReferenceSpaceId` — `string?` | Inputs / reference → Output / initialization | [IN] compiled reference space | 原 link；不重開 reference path。 |
| `OutputNamingSummary.RendererKind` — `string` | Output / naming renderer | [N], [NR] compiled renderer | 原 kind，不執行新版 renderer。 |
| `OutputNamingSummary.Template` — `string` | Output / naming template | [N], [NR] compiled profile template | 原 template，非可重新套用的 UI policy。 |
| `OutputNamingSummary.AutomaticFileName` — `string` | Output / automatic candidate | [N], [NR] accepted-input rendering | 原候選，不重新計算。 |
| `OutputNamingSummary.ActualFileName` — `string` | Output / actual name | [N], [NR] requested/resolved name | 原 requested/committed 名稱。 |
| `OutputNamingSummary.IsExplicitOverride` — `bool` | Output / name provenance | [N], [NR] explicit override fact | 原 override，勿由名稱相等推算。 |
| `OutputNamingSummary.DateSource` — `string` | Output / date-token source | [N], [NR] captured clock-source id | 原 source。 |
| `OutputNamingSummary.ResolvedAtUtc` — `DateTimeOffset` | Output / naming instant | [N], [NR] single captured run instant | 原時間，不用 replay 當日日期。 |
| `OutputNamingSummary.Tokens` — `IReadOnlyList<OutputNamingTokenSummary>` | Output / tokens → Inputs / provenance | [N], [NR] accepted metadata/token values | 原 tokens；缺失不重 parse 舊檔案。 |
| `OutputNamingSummary.Admission` — `OutputNamingAdmissionSummary?` | Output / exact naming publication | [N], [NR] admitted naming identity | 原 publication；缺失不新 admission。 |
| `OutputNamingAdmissionSummary.RouteId` — `string` | Output / naming route identity | [N] exact admitted capability route | 原 route；退休 route 的 `RouteId` 只是唯讀的歷史 identity，不代表可重新執行。 |
| `OutputNamingAdmissionSummary.CompilationFingerprint` — `string` | Output / naming compilation identity | [N] admitted compilation fingerprint | 原 hash，不用 root/current hash 覆寫。 |
| `OutputNamingAdmissionSummary.ResolutionToken` — `string` | Output / publication token | [N] exact publication token | 原 token；不是 replay 執行權限。 |
| `OutputNamingAdmissionSummary.AuthoringRevision` — `long` | Output / admitted revision | [N] captured authoring revision | 原 revision。 |
| `OutputNamingTokenSummary.TokenId` — `string` | Output / token identity | [N], [NR] compiled token id | 原 id。 |
| `OutputNamingTokenSummary.Value` — `string` | Output / token value | [N], [NR] captured parsed/rendered value | 原 value，不由 filename 拆回。 |
| `OutputNamingTokenSummary.IsKnown` — `bool` | Output / token knowledge | [N], [NR] typed metadata result | Unknown 不變成 known。 |
| `OutputNamingTokenSummary.SourceAddressSpaceId` — `string?` | Inputs / token source space | [N], [NR] accepted source identity | 原 space，null 不猜。 |
| `OutputNamingTokenSummary.AcceptedSnapshotSha256` — `string?` | Inputs / token source identity | [N], [NR] accepted snapshot hash | 原 hash，不用目前 input 代替。 |
| `OutputNamingTokenSummary.ParserId` — `string` | Output / token parsing provenance | [N], [NR] declared parser identity | 原 parser id，不重跑 parser。 |

## Delivery facts

| Fact | Physical section | Source | Replay |
| --- | --- | --- | --- |
| `DeliveryArtifactSummary.DeliveryKind` — `string` | Delivery / additional-file role | [DL], [RS] declared delivery role | 原 role。 |
| `DeliveryArtifactSummary.FileName` — `string` | Delivery / actual additional file | [DL], [RS] selected plain filename | 原 name，非 host path。 |
| `DeliveryArtifactSummary.Size` — `long` | Delivery / delivered extent | [DL], [RS] delivered bytes length | 原 size。 |
| `DeliveryArtifactSummary.Sha256` — `string` | Delivery / delivered identity | [DL], [RS] delivered byte hash | 原 hash，與 primary output 分開。 |
| `DeliveryArtifactSummary.Committed` — `bool` | Delivery / atomic commit | [DL], [RS] additional writer result | 原 commit，勿用 primary Committed 代替。 |
| `DeliveryArtifactSummary.SourceRange` — `ByteRange` | Delivery / primary-output physical slice | [DL], [RS] declared copied primary-output range | 原 output-space range；不重新切出 bytes。 |
| `CompositionOutputBundleDeliverySummary.ResolvedDirectory` — `string` | Delivery / actual bundle location | [BD] FromReceipt.ResolvedDirectory | 原 suffix-resolved host evidence，不重開/重建目錄。 |
| `CompositionOutputBundleDeliverySummary.Artifacts` — `IReadOnlyList<CompositionOutputBundleDeliveredArtifactSummary>` | Delivery / bundle files in canonical order | [BD] FromReceipt.Artifacts | 原 actual filenames/order；缺失不宣稱 bundle success。 |
| `CompositionOutputBundleDeliveredArtifactSummary.Role` — `string` | Delivery / bundle file role | [BD] receipt artifact.Role | 原 output/source role。 |
| `CompositionOutputBundleDeliveredArtifactSummary.BindingId` — `string?` | Delivery / bundle source binding | [BD] receipt artifact.BindingId | 原 input link，null 不猜。 |
| `CompositionOutputBundleDeliveredArtifactSummary.DeliveredFileName` — `string` | Delivery / actual bundle filename | [BD] receipt artifact.DeliveredFileName | 原 delivered name，不用自動 naming candidate 代替。 |
| `CompositionOutputBundleDeliveredArtifactSummary.Size` — `long` | Delivery / bundle file extent | [BD] receipt artifact.Size | 原 size。 |
| `CompositionOutputBundleDeliveredArtifactSummary.Sha256` — `string` | Delivery / bundle file identity | [BD] receipt artifact.Sha256 | 原 hash；不重新讀 host 檔案。 |

## General admission facts（只盤點已存在的 Report graph）

下列不是新增 General 執行入口；僅沿 `GeneralAdmission` 展開其既有型別成員。

| Fact | Physical section | Source | Replay |
| --- | --- | --- | --- |
| `GeneralAuthoringAdmissionSummary.TrustedParentId` — `string` | Run / exact Parent admission | [GA] admission.ToSummary | 原 Parent id；不重新解析 trust。 |
| `GeneralAuthoringAdmissionSummary.SavedRuleId` — `string?` | Run / Saved Rule identity | [GA] accepted optional rule id | 原 id；null 不補。 |
| `GeneralAuthoringAdmissionSummary.SavedRule` — `SavedRuleExecutionIdentity?` | Run / exact rule revision | [GA], [SR] accepted content identity | 原 identity，不載入目前 rule。 |
| `GeneralAuthoringAdmissionSummary.EffectiveLimits` — `GeneralResourceLimits?` | Run / admitted resource limits | [GA], [GL] resolved technical/Parent/rule limits | 原 limits；null 是未成功解析，非 unlimited。 |
| `GeneralAuthoringAdmissionSummary.InputResources` — `IReadOnlyList<GeneralInputResource>` | Inputs / observed whole files | [GA], [GL] admitted observed resources | 原 source lengths，不重查 filesystem。 |
| `GeneralAuthoringAdmissionSummary.OccupancySegments` — `IReadOnlyList<GeneralOccupancySegment>` | Operations / authored target occupancy | [GA], [GL] canonical accepted occupancy | 原 mappings/ranges；planned 不等於 written。 |
| `GeneralAuthoringAdmissionSummary.Issues` — `IReadOnlyList<GeneralAuthoringAdmissionIssue>` | Diagnostics / admission blockers | [GA], [GL] canonical blockers | 原 blockers；不同於 root Issues list/index。 |
| `GeneralResourceLimits.MaximumMappingCount` — `int` | Run / admission ceiling | [GL] effective limit layer | 原 ceiling，不套用目前 policy。 |
| `GeneralResourceLimits.MaximumTotalWriteBytes` — `long` | Run / authored write ceiling | [GL] effective limit layer | 原 ceiling，非實際 changed count。 |
| `GeneralResourceLimits.MaximumFileBytes` — `long` | Inputs / whole-file ceiling | [GL] effective limit layer | 原 ceiling，非 source length。 |
| `GeneralResourceLimits.MaximumSafeMaterializationBytes` — `long` | Operations / materialization ceiling | [GL] effective limit layer | 原 ceiling，非實際 allocation 證據。 |
| `GeneralResourceLimits.SlotLimits` — `IReadOnlyList<GeneralSlotLengthLimits>` | Inputs / per-slot admission | [GL] effective ordered slot limits | 原 limits/order。 |
| `GeneralSlotLengthLimits.SlotId` — `string` | Inputs / constrained slot | [GL] Parent/rule slot declaration | 原 slot。 |
| `GeneralSlotLengthLimits.MinimumBytes` — `long` | Inputs / inclusive lower length limit | [GL] effective slot minimum | 原 inclusive bound。 |
| `GeneralSlotLengthLimits.MaximumBytes` — `long` | Inputs / inclusive upper length limit | [GL] effective slot maximum | 原 inclusive bound，非 ByteRange end。 |
| `GeneralSlotLengthLimits.AllowedLengths` — `IReadOnlyList<long>` | Inputs / discrete accepted lengths | [GL] effective slot allowed lengths | 原 list；空表示區間內長度皆可，不是零 input。 |
| `GeneralInputResource.SlotId` — `string` | Inputs / observed General slot | [GL] resource adapter slot identity | 原 slot。 |
| `GeneralInputResource.LengthBytes` — `long` | Inputs / whole-file observation | [GL] observed whole-file length | 原 length，不混同 accepted prefix。 |
| `GeneralOccupancySegment.MappingId` — `string` | Operations / authored mapping | [GL] canonical occupancy mapping | 原 mapping id。 |
| `GeneralOccupancySegment.SourceKind` — `GeneralMappingSourceKind` | Operations / mapping source primitive | [GL] admitted mapping source kind | 原 kind。 |
| `GeneralOccupancySegment.TargetAddressSpaceId` — `string` | Operations / occupancy target space | [GL] admitted mapping target space | 原 address space。 |
| `GeneralOccupancySegment.TargetRange` — `ByteRange` | Operations / physical target occupancy | [GL] admitted mapping target range | 原計畫範圍，非 final difference。 |
| `GeneralAuthoringAdmissionIssue.Code` — `string` | Diagnostics / admission code | [GL] canonical blocker code | 原 code。 |
| `GeneralAuthoringAdmissionIssue.IssueId` — `string` | Diagnostics / stable blocker identity | [GL] row-order-independent issue identity | 原 id，不以 root issue index 代替。 |
| `GeneralAuthoringAdmissionIssue.Message` — `string` | Diagnostics / admission text | [GL] blocker detail | 原 message。 |
| `GeneralAuthoringAdmissionIssue.MappingIds` — `IReadOnlyList<string>` | Diagnostics / involved mappings | [GL] ordinal involved mapping ids | 原 ids/order，不重新判定 overlap。 |
| `GeneralAuthoringAdmissionIssue.Intersection` — `ByteRange?` | Diagnostics / target occupancy intersection | [GL] exact occupancy blocker range | 原交集；無值不推算。 |
| `GeneralAuthoringAdmissionIssue.SlotId` — `string?` | Diagnostics / resource slot | [GL] named blocker slot | 原 slot；null 不猜。 |
| `SavedRuleExecutionIdentity.RuleId` — `string` | Run / admitted Saved Rule | [SR] exact rule identity | 原 id。 |
| `SavedRuleExecutionIdentity.RuleVersion` — `string` | Run / admitted Saved Rule revision | [SR] published rule version | 原 version。 |
| `SavedRuleExecutionIdentity.ContentHash` — `string` | Run / admitted Saved Rule content | [SR] canonical semantic content hash | 原 hash，非 path/name hash。 |
| `SavedRuleExecutionIdentity.Parent` — `SavedRuleParentIdentity` | Run / exact Trusted Parent | [SR] separately resolved Parent identity | 原 identity，不授予現在的 trust。 |
| `SavedRuleParentIdentity.BundleId` — `string` | Run / Parent bundle | [SR] exact Parent.BundleId | 原 id。 |
| `SavedRuleParentIdentity.BundleVersion` — `string` | Run / Parent bundle revision | [SR] exact Parent.BundleVersion | 原 version。 |
| `SavedRuleParentIdentity.BundleContentHash` — `string` | Run / Parent bundle content | [SR] exact Parent.BundleContentHash | 原 hash。 |
| `SavedRuleParentIdentity.ProfileId` — `string` | Run / Parent profile | [SR] exact Parent.ProfileId | 原 id，不用 root id 覆寫。 |
| `SavedRuleParentIdentity.ProfileVersion` — `string` | Run / Parent profile revision | [SR] exact Parent.ProfileVersion | 原 version。 |
| `SavedRuleParentIdentity.ProfileContentHash` — `string` | Run / Parent profile content | [SR] exact Parent.ProfileContentHash | 原 hash。 |
| `SavedRuleParentIdentity.FamilyId` — `string` | Run / Parent family | [SR] exact Parent.FamilyId | 原 id。 |
| `SavedRuleParentIdentity.FamilyVersion` — `string` | Run / Parent family revision | [SR] exact Parent.FamilyVersion | 原 version。 |
| `SavedRuleParentIdentity.FamilyContentHash` — `string` | Run / Parent family content | [SR] exact Parent.FamilyContentHash | 原 hash。 |
| `SavedRuleParentIdentity.MapId` — `string` | Run / Parent physical map identity | [SR] exact Parent.MapId | 原 map，不重新解讀 bytes。 |

## Plan-only diagnostic 與共用 range facts

`DiagnosticPreview` 不表示 retired route 可執行。既有 [diagnostic projector][DP]
在 typed DTO 的 `Output` 放零長度、empty SHA-256、`Committed = false` sentinel；
[SerializeDiagnosticPreview][J] 將外部 JSON 的 `Output` 設為 null，
[typed UI projector][TP] 的 suppressOutput 路徑也不呈現產物。
因此下列 `OutputProduced = false` / `ClaimsFinalIntegrity = false` 與既有 suppression
共同說明「plan only; no output was produced」，不把 sentinel filename/hash 當 firmware evidence。
這是既有表示差異的記錄，不修改 DTO、serializer 或 UI。

| Fact | Physical section | Source | Replay |
| --- | --- | --- | --- |
| `GeneralReplaceDiagnosticPreviewSummary.RequiredStageId` — `string?` | Diagnostics / required POSTBUILD stage | [DP] exact compiled stage or missing Parent authority | 原 stage；null 不創造 stage。 |
| `GeneralReplaceDiagnosticPreviewSummary.Blocker` — `CapabilityActionBlocker` | Diagnostics / shared Build-readiness blocker | [DP], [CB] readiness.Build.PrimaryBlocker | 原 typed blocker；不重新檢查目前機器。 |
| `GeneralReplaceDiagnosticPreviewSummary.Coverage` — `IReadOnlyList<PlanOnlyCoverageSegment>` | Diagnostics / projected reference physical coverage | [DP] admitted occupancy + reference capacity | 原 Kept/Changed 計畫，不聲稱 bytes 曾改。 |
| `GeneralReplaceDiagnosticPreviewSummary.Mode` — `string` | Diagnostics / plan-only marker | [DP] diagnostic-plan-only constant | 原 marker，不視為 executable Preview。 |
| `GeneralReplaceDiagnosticPreviewSummary.Message` — `string` | Diagnostics / plan-only statement | [DP] accepted non-executing statement | 保留「no output was produced」意義。 |
| `GeneralReplaceDiagnosticPreviewSummary.PostbuildRequired` — `bool` | Diagnostics / required integrity stage | [DP] existing true marker | 需求不等同已執行。 |
| `GeneralReplaceDiagnosticPreviewSummary.OutputProduced` — `bool` | Diagnostics / absence of output | [DP] existing false marker | false，不能展示 sentinel 為產物。 |
| `GeneralReplaceDiagnosticPreviewSummary.ClaimsFinalIntegrity` — `bool` | Diagnostics / absence of integrity claim | [DP] existing false marker | false，不宣稱 Header/CRC/hash valid。 |
| `PlanOnlyCoverageSegment.Range` — `ByteRange` | Diagnostics / reference/output-plan range | [DP] projected reference coverage | 原 output-coordinate 計畫範圍，無執行 bytes。 |
| `PlanOnlyCoverageSegment.Disposition` — `PlanOnlyCoverageDisposition` | Diagnostics / planned Kept or Changed | [DP] accepted occupancy projection | 原 disposition，不當 mutation status。 |
| `PlanOnlyCoverageSegment.MappingId` — `string?` | Diagnostics / planned mapping link | [DP] occupancy.MappingId / Kept null | 原 mapping，null 不補。 |
| `CapabilityActionBlocker.Code` — `string` | Diagnostics / readiness code | [CB] shared typed readiness owner | 原 code，非重新發生的 Build failure。 |
| `CapabilityActionBlocker.Dimension` — `CapabilityReadinessDimension` | Diagnostics / readiness dimension | [CB] shared typed blocker dimension | 原 dimension，不從 text 判斷。 |
| `CapabilityActionBlocker.SubjectId` — `string` | Diagnostics / blocker subject | [CB] shared typed subject | 原 subject，非 processor execution evidence。 |
| `CapabilityActionBlocker.Message` — `string` | Diagnostics / captured readiness text | [CB] shared typed blocker text | 原 text，不用目前 runtime message 覆寫。 |
| `CapabilityActionBlocker.NextAction` — `CapabilityReadinessNextAction` | Diagnostics / recorded recovery guidance | [CB] typed next-action value | 只讀 guidance，不自動執行。 |
| `ByteRange.Start` — `long` | 所有 range 所屬 section / address space | [BR] checked inclusive start | 原 offset，必須連同其 parent space 閱讀。 |
| `ByteRange.Length` — `long` | 所有 range 所屬 section / extent | [BR] checked positive length | 原 length，非 observed changed count。 |
| `ByteRange.EndExclusive` — `long` | 所有 range 所屬 section / exclusive end | [BR] checked(Start + Length) | 既有 computed property，不改 half-open 邊界。 |

共用 `ByteRange` 三個成員適用上述所有 range，包括 input accepted/tail、operation
source/target/processor ranges、mutation、difference/replay、delivery source、diagnostic
source/intersection/coverage 及 General occupancy。space 由其 parent fact 決定；
`ByteRange` 本身沒有新增的 AddressSpaceId 或 physical section 成員。

## Source 索引

連結指向此 snapshot 的既有宣告/producer/reader。沒有列入物件圖的 preview result、
mutable drafts、current-machine runtime state 或 UI-derived counts，不能另補成 Report facts。

[T]: ../../../src/NvtFwCombiner.Application/Composition/CompositionRunReport.cs
[R]: ../../../src/NvtFwCombiner.Application/Composition/CompositionRunService.Reports.cs
[I]: ../../../src/NvtFwCombiner.Application/Composition/InputArtifactSummary.cs
[IL]: ../../../src/NvtFwCombiner.Application/Composition/CompositionRunService.Inputs.cs
[AB]: ../../../src/NvtFwCombiner.Application/Composition/AbMergeFormatRunSummary.cs
[O]: ../../../src/NvtFwCombiner.Application/Composition/OperationRunSummary.cs
[OP]: ../../../src/NvtFwCombiner.Domain/Composition/OperationProvenance.cs
[EP]: ../../../src/NvtFwCombiner.Application/Composition/ExternalProcessInvocation.cs
[M]: ../../../src/NvtFwCombiner.Application/Composition/MutationRunSummary.cs
[V]: ../../../src/NvtFwCombiner.Application/Composition/ValidationRunSummary.cs
[VI]: ../../../src/NvtFwCombiner.Application/Composition/CompositionRunService.InputLoadValidations.cs
[VF]: ../../../src/NvtFwCombiner.Application/Composition/CompositionRunService.FinalOutputValidations.cs
[IS]: ../../../src/NvtFwCombiner.Domain/Composition/CompositionIssue.cs
[ID]: ../../../src/NvtFwCombiner.Application/Composition/InputDiagnosticEvidence.cs
[OUT]: ../../../src/NvtFwCombiner.Application/Composition/OutputArtifactSummary.cs
[OD]: ../../../src/NvtFwCombiner.Application/Composition/CompositionRunService.OutputDifferences.cs
[D]: ../../../src/NvtFwCombiner.Application/Composition/OutputDifferenceSummary.cs
[S]: ../../../src/NvtFwCombiner.Application/Composition/OutputDifferenceSemantic.cs
[B]: ../../../src/NvtFwCombiner.Application/Composition/OutputDifferenceReplaySegment.cs
[DG]: ../../../src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewViewModel.OutputDifferences.cs
[IN]: ../../../src/NvtFwCombiner.Application/Composition/ImageInitializationSummary.cs
[N]: ../../../src/NvtFwCombiner.Application/Composition/OutputNamingSummary.cs
[NR]: ../../../src/NvtFwCombiner.Application/Composition/CompiledOutputNameResolver.cs
[DL]: ../../../src/NvtFwCombiner.Application/Composition/DeliveryArtifactSummary.cs
[BD]: ../../../src/NvtFwCombiner.Application/Composition/CompositionOutputBundleReport.cs
[RS]: ../../../src/NvtFwCombiner.Application/Composition/CompositionRunService.cs
[GA]: ../../../src/NvtFwCombiner.Application/Authoring/GeneralAuthoringAdmissionEvaluator.cs
[GL]: ../../../src/NvtFwCombiner.Application/Authoring/GeneralAuthoringResourceLimits.cs
[SR]: ../../../src/NvtFwCombiner.Application/Authoring/SavedRuleLifecycle.cs
[DP]: ../../../src/NvtFwCombiner.Application/Composition/GeneralReplaceDiagnosticPreview.cs
[CB]: ../../../src/NvtFwCombiner.Application/Capabilities/CapabilityActionReadiness.cs
[BR]: ../../../src/NvtFwCombiner.Domain/Composition/ByteRange.cs
[J]: ../../../src/NvtFwCombiner.Application/Composition/CompositionRunReportJson.cs
[TP]: ../../../src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportReviewTypedProjector.cs

## Checkpoints

### 2026-10-04 Delivered and checked
State: verified (documentation and structure checks only)
Commits: this pull request's integration commit; the worker's local task commit on `feature/queue/r17-01-report-fact-sections` is not published and has no stable public SHA.
Evidence: the brief's document-shape check -> exit 0; `python scripts/verify.py --structure-only` -> exit 0, both run on the worker's local task commit outside its sandbox; they apply to the document text that this pull request integrates. The only later change to this file is the envelope and checkpoint themselves; in R17-01 also one wording fix of the `RouteId` line. The head of this pull request is checked by its own CI.
Open: The physical layout of the sections is not approved here; reference images and their approval are separate owner steps.
Next: None in this file.
