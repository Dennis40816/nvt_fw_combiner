# WS-PARITY: one-time alignment with v0.9.16

Owner: Codex runs; Claude Code reviews; the owner disposes. Board:
[1.1.12 board](../1.1.12.md), checklist C-3, WS-GOV decision 7b.
Protocol: [handoff README](../README.md).

## Dispatch envelope 2026-09-25

**Outcome.** A complete difference table between the 1.1.x candidate and the
v0.9.16 predecessor, produced with the existing ADR 0057 mechanism
(`scripts/v0916_parity_certification.py`, plan
`docs/contracts/v0916-parity-certification-v1.json`). One row per published
route and case: route, IC, workflow, equal or different, differing byte
ranges, and a proposed explanation citing the intended change it matches
(for example the DP Replace retirement, the NT51920/25/30/31 retirement, or a
CHANGELOG entry), or "unexplained". The owner confirms each difference;
an unexplained difference becomes a bug.

Non-goals: changing expected bytes, profiles or product code; certifying
support.

**Authority.** R0 evidence work. Local commits of this log and bug files only.
Building the predecessor executor from its pinned tag is allowed in the test
area; private firmware and outputs stay there.

**Model.** `gpt-6-sol` at xhigh. Headless runs pass
`--sandbox workspace-write`.

**Branch and worktree.** `feature/1.1.12/v0916-alignment`, worktree
`<worktrees>/v0916-alignment`, rebased onto the
`1.1.x` trunk at the base refresh.

**Write lock.** This log and new files under `docs/handoff/bugs/`.

**Read first.** `docs/adr/0057-v0916-black-box-parity-certification.md`;
`docs/contracts/v0916-parity-certification-v1.md`; the parity jobs in
`.github/workflows/release.yml` (gated on `2.0.0`); `CHANGELOG.md` from 0.9.16
on; `AGENTS.md`.

**Acceptance.** Every route and case in the plan is covered or listed as not
runnable with the reason; the table is in this log with commands, source SHA
and output hashes; the evidence can be regenerated from committed tooling.

**Stop and ask.** When the script cannot run for a 1.x candidate without
changing `scripts/` or the release workflow (governed; the commander decides
with checklist B-7); when the predecessor cannot be rebuilt as pinned.

**Bugs.** Record every unexplained difference and every other bug as a new
file under `docs/handoff/bugs/` per the bug ledger; cite IDs here.

**Start.** Early, right after the base refresh (checklist A-7), so
differences surface long before freeze.

## Checkpoints
