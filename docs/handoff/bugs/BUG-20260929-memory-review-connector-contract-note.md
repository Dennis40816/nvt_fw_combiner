# BUG-20260929-memory-review-connector-contract-note: Superseding note omits the passive connector

Status: fixed
Severity: P3
Found: 2026-09-29, independent review and Codex gpt-6-astra correction,
at feature/1.1.15/memory-layout over 8010d7770.
Where: docs/ui/v1.1.x-memory-layout-interaction-handoff.md
Observed: The historical connector transit text appeared active despite connector hit testing being disabled.
Expected: Decision 189 removes the connector as an input surface.
Evidence: Independent review P3-4 and transparent-spacing regressions.. Test artifacts are under
`evidence/1.1.15/test-results/`; see the correction checkpoint in WS-MEMLAYOUT.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: The superseding note now explicitly includes connector pointer retention. Included in the correction commit containing this record.
