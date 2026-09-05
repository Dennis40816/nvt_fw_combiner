# Documentation Instructions

- `SPEC.md` is the canonical product/high-level specification; do not duplicate it under `docs/`.
- Durable architecture or public-contract decisions belong in numbered ADRs; routine wording, links, and status updates use their existing document. Contracts include prose and normative schema together.
- When a normative contract changes, synchronize the affected schema, examples, implementation guidance and report semantics. A wording/link correction does not require unrelated code or schema changes.
- Use explicit dates/versions and distinguish proposal, decision and verified fact.
- `docs/architecture/nfc_roadmap.md` owns future version allocation; linked handoffs own detail, not a second competing TODO list. Specifications/contracts own behavior; changelogs and dated evidence own completed history.
- When authorized to update planning, apply the owner's accepted resequencing to the roadmap and affected handoff/entry links together. Preserve historical records as history; do not relabel an old release or reconstruct its evidence. A status-only request reports a stale allocation without silently rewriting it.
- Reuse an existing canonical document before creating another inventory, report, index, checklist, or archive structure. Retain a separate artifact only for a concrete reader or evidence need; document deletion/moving must preserve still-used references and remain within authorization.
- Memory ranges use half-open notation except clearly quoted legacy evidence.
- Commands must match scripts/CI; no proprietary firmware bytes, secrets or private URLs.
- Ordinary non-normative, non-classifier-governed prose needs only diff and affected-link review; run structure/consumer checks when layout or parsed inputs change. No extra issue, report, code-size census or full test suite is needed for that R0 path. AGENTS, governance and other classifier-governed documents retain their record/integration rules; normative, permission and release changes retain their affected gates. Use the root risk rules and existing documents, not a new exemption framework.
