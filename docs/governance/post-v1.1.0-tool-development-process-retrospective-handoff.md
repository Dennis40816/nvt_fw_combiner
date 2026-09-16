# Post-v1.1.0 Tool development process retrospective handoff

Status: owner-approved near-term AI-skill/documentation priority; begin only
after the official `v1.1.0` release and exact release-evidence closure.

This handoff is not a `v1.1.0` release gate. It does not authorize a process
rewrite, new repository, tool migration, or deletion by itself.

The canonical roadmap owns the exact version allocation for the bounded
AI-skill/workflow pilot and evidence-preserving documentation convergence:
[`nfc_roadmap.md`](../architecture/nfc_roadmap.md). This handoff preserves the
retrospective evidence and discussion boundary; it does not duplicate release
scope or activate a workflow change without separate approval.

## 1.1.8 local instruction pilot — 2026-09-16

The owner prioritized repository skills, `AGENTS.md` and role/document reuse
before further product-code refactoring. This bounded pilot follows the earlier
1.1.8 source-size advisory, governance-pointer and audit-scope corrections.
It does not close the broader retrospective below or certify integration/release.

Observed conflicts and adopted corrections:

- Root `AGENTS.md` owns task/risk/test applicability. Documentation instructions,
  the execution runbook and Polytail policy now point to that owner instead of
  maintaining parallel R0/R1/R2/R3 summaries. Polytail retains verdict/waiver
  ownership; the capability record contract and executable gates are unchanged.
- Four role configurations reuse the corresponding skills. In particular,
  the implementer's frozen-handoff wording no longer conflates every local
  handoff with the integration/release full-suite boundary. Sandbox, network,
  reasoning and delegation settings are unchanged. Existing agents retain their
  already-loaded developer instructions; these file edits apply on a future
  configuration load, not retroactively to this session.
- `implement` reuses a regression that demonstrates the same failure and uses
  characterization/document checks for unchanged refactors/prose. It does not
  invent failing tests to satisfy a process step.
- `diagnosing-bugs` permits evidence-led code inspection before reproduction,
  labels unresolved causes honestly, and uses bounded discriminating probes.
  Fixed hypothesis/stress counts and Bash-only human feedback are removed;
  the existing Bash template remains optional. Diagnosis still cannot authorize
  a product fix, evidence substitution or unsafe access to firmware inputs.

Independent instruction simulation covered ordinary prose, an existing UI
regression, unchanged-function extraction, an unreproduced crash, an Application
public-contract change, a firmware offset/expected-hash change and an attempt to
reuse another candidate's Golden run for release. The resulting plans retained
scope, appropriate tests, R2 admission and firmware/release evidence boundaries;
no routing conflict was found. This was a read-only decision simulation, not
execution of those product/release tasks or a measured productivity study.
Skill inventory (23), selected governance lifecycle/classifier tests (5), both
changed-skill metadata checks, role TOML/settings comparison and affected-link
checks passed before fixed-head review. Exact review/final-gate evidence is
tracked in [V118-AGENT-INSTRUCTION-REUSE-02](change-records/V118-AGENT-INSTRUCTION-REUSE-02.json).

The first structure attempt failed: role TOMLs were incorrectly included in
record `mutablePaths`, although the current classifier excludes them. Original
branch/head `0c977bd5` and its active record are preserved, not integration-ready.
A new branch from valid checkpoint `876df961` retains the reviewed content with
an eight-governed-path admission; the four role files remain in whole-diff review.
No validator, history or safety requirement was changed to recover.

Reference method: OpenAI's [AGENTS.md guidance](https://developers.openai.com/codex/guides/agents-md)
and [skills guidance](https://developers.openai.com/codex/skills), consulted
2026-09-16, support scoped inheritance, progressive disclosure, focused triggers
and realistic prompt tests. They inform this pilot, not repository permissions.
For future authorized instruction reviews, reuse this compact review prompt:

> Given the task and affected paths, trace which instruction owns each decision.
> Identify conflicts, duplicate rules, stale scope and missing trigger links.
> Propose the smallest correction in the existing owner; preserve applicable
> safety and approval boundaries. Try realistic requests without supplying the
> intended answer, and report observed decisions separately from untested claims.

The pilot is reversible through reviewed new commits on the changed instruction
files; immutable admission/evidence history is retained. Observe subsequent
real tasks before claiming lower token use or faster delivery. No product
architecture refactor, new skill/router, invocation-policy change, external
publication or additional full product-suite run is part of this unit.

## Objective

Use repository evidence from the `0.10.x`, `1.0.x`, and `1.1.0` development
period to answer three questions:

1. Which decisions produced safer, faster, clearer, or more maintainable
   outcomes?
2. Which decisions caused avoidable delay, rework, ambiguity, duplicated
   review, or operational risk?
3. Compared with mature large-company software delivery, which practices are
   worth adopting, simplifying, or deliberately rejecting for future Tool
   projects?

The final output is a reusable Tool-development workflow, not a narrative
retrospective alone.

## Conductor/architect learning lens

The owner intends to use this project to learn how to direct large-software
development as a conductor and architect. The retrospective therefore evaluates
not only code and process, but the architect's decisions and operating model:

- framing the product boundary and choosing what deliberately does not belong;
- locating one semantic owner, dependency direction, and stable public
  contracts before assigning implementation;
- requiring a repository reuse inventory before scope or implementation:
  existing producer, callers, ports/adapters, helpers, and tests first; extend
  the owner when its contract is insufficient, refactor only when measured
  ownership/dependency debt blocks the change, and create a new path only when
  neither can safely satisfy the approved contract;
- separating product risk, firmware/safety risk, release risk, and ordinary
  implementation risk so ceremony is proportional;
- sequencing the dependency graph and protecting the critical path while safe,
  unrelated work proceeds in parallel;
- assigning clear file/capability ownership, reviewer independence, escalation
  points, and integration responsibility across humans and agents;
- making reversible decisions quickly while slowing down only for irreversible
  data, security, compatibility, or publication commitments;
- defining evidence before implementation, then distinguishing useful evidence
  from repeated proof that no longer reduces risk;
- communicating intent, tradeoffs, residual risk, deadlines, and owner-only
  decisions so teams do not optimize different goals;
- planning operability from the start: diagnostics, rollout, canary, rollback,
  recovery, cleanup, incident response, and support handoff; and
- measuring outcomes and changing the system without blaming individuals for
  incentives, queues, or missing information created by the process itself.

The final architect playbook must state what the conductor decides personally,
what is delegated, what evidence is required back, when to interrupt or defer a
workstream, and how to recognize that the architecture or process itself—not an
implementer—is causing repeated delay.

## Evidence to preserve before the discussion

- accepted and superseded ADR/spec decisions, including their original reason;
- change records, reviewer findings, owner gates, and exact-head evidence;
- release timelines, blocked intervals, review wait time, repeated test time,
  failed/retried CI, canary results, and post-release fixes;
- defects found early, defects found late, and any defect that escaped a gate;
- release/package/Registry/Catalog/Launcher incidents and recovery evidence;
- agent handoffs, parallel work, ownership conflicts, worktree/branch cleanup,
  temporary-work safety, and tasks delayed by missing authority;
- user-visible UI review rounds and cases where automated evidence did or did
  not predict the real Windows result; and
- concrete examples of governance that prevented a defect versus governance
  that only repeated already-established evidence.

Do not score a decision from memory or elapsed frustration alone. Each finding
must link to evidence, record uncertainty, and distinguish a poor decision from
poor execution of an otherwise sound decision.

## Comparison frame

Compare the observed process with applicable mature engineering practices,
without copying enterprise ceremony that does not fit a small Tool team:

- DORA measures: lead time for changes, deployment frequency, change failure
  rate, and time to restore service;
- trunk-based development or release trains, short-lived branches, CODEOWNERS,
  required checks, and bounded review service levels;
- RFC/ADR lifecycle, decision expiry, supersession, and a searchable decision
  log;
- hermetic/reproducible builds, layered test pyramids, test selection, flaky
  test budgets, and CI runtime targets;
- artifact registries, immutable provenance, SBOM/signing, SLSA-style supply
  chain controls, staged rollout, canary, rollback, and incident response;
- platform engineering: repository templates, golden paths, local bootstrap,
  one release interface, developer portal/documentation, and safe workspace
  isolation; and
- privacy-aware telemetry and support diagnostics that measure outcomes without
  collecting firmware or customer payloads.

For every candidate practice, record `adopt`, `adapt`, or `reject`, why it fits
the Tool context, expected benefit, operational cost, migration risk, and a
measurable success condition.

## Required outputs

1. A decision scorecard with `worked well`, `mixed`, and `should change`, each
   backed by evidence and confidence.
2. A bottleneck timeline identifying specification, implementation, testing,
   review, publication, and cleanup delays separately.
3. A common workflow from intake and risk classification through specification,
   implementation, narrow tests, review, frozen candidate, package, canary,
   release, cleanup, and retrospective.
4. Reusable templates/checklists and a prioritized automation backlog. The
   workflow must define when a lightweight path is sufficient and when R2/R3
   evidence is mandatory.
5. Baseline and target measures for end-to-end lead time, CI duration, full-test
   frequency, review wait, rework ratio, flaky retries, change failure rate,
   recovery time, and release-cleanup residue.
6. A bounded pilot plan on the next suitable Tool project before broad adoption.
   Preserve rollback to the prior process and do not migrate every repository
   at once.
7. A concise conductor/architect playbook covering system framing, dependency
   order, ownership, bounded parallelism, decision reversibility, evidence,
   communication, operational readiness, and escalation.

## Discussion prompts

- Which safety gates caught a real defect, and which gates could be combined or
  automated without reducing evidence quality?
- Which decisions arrived too late, and what minimum early design evidence
  would have exposed the issue sooner?
- Which tests belong in every change, every pull request, a frozen candidate,
  or a scheduled deep gate?
- Where did release scope become too broad, and what release-train boundary
  would have preserved user value sooner?
- Which responsibilities should become a shared Tool platform or template, and
  which must stay product-specific?
- Which independent reviews could close immediately in parallel, which require
  an integration boundary, and what concurrency limit keeps review cost
  bounded? Use the current default of at most two independent reviewers on
  disjoint scopes as one hypothesis to evaluate rather than permanent law.
- What information should a future handoff contain so a new engineer or agent
  can continue safely without replaying the entire project history?

The owner approves any resulting workflow separately. Until then, current
repository governance and release contracts remain authoritative.
