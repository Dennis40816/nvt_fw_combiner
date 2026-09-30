# BUG-20260929-memory-review-required-query: Pending projection queries an unavailable authoring port

Status: fixed
Severity: P1
Found: 2026-09-29, Codex gpt-6-astra, complete Release UiSmoke correction gate.
Where: MergePresentationViewModel.Memory.cs
Observed: The first P3-1 correction calls GetRequiredAddressSpaces during a
zero-authorable catalog publication, including an empty selected IC.
Expected: Reuse the already applied Application requirements; pending rendering
must remain query-safe when there is no available authoring route.
Evidence: review-full-uismoke.trx; ZeroGlobalAuthoringPublicationDispatchesExactlyZeroAuthoringPortCalls,
ColdStartZeroGlobalPublicationKeepsWorkflowBindingsQuerySafe and loaded-workflow
zero-global cases fail. They pass at aba286bae in review-base-ui.trx.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: Reuse _appliedStandardMergeRequired without issuing a new authoring
query. The five affected cases pass in review-followup-green.trx; complete final
project gates are recorded in WS-MEMLAYOUT.md.
