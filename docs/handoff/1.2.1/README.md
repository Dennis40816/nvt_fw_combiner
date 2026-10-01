# `1.2.1` wave-1 reports

Status: evaluation reports for `1.2.1`'s first inventory wave (board decision
185 in [`1.1.12.md`](../1.1.12.md)), entered into the repository sanitized to
test-area-relative paths. These are the accepted evaluation record, not a
second product-behavior contract; `SPEC.md`, ADRs and the
[1.2.x allocation](../1.1.14/1.2.x-allocation.md) stay the canonical owners of
any behavior or schedule the reports recommend.

Every evaluation follows the `1.2.1` model (board decision 185): one
evaluator, Claude Fable 5.1 or Codex `gpt-6-astra` at its highest available
reasoning effort, then an independent cross-review by the other (decision
169). Unreviewed and reviewer-only files (`*.review.md`) stay in the test
area under `evidence/1.2.1/`; only the accepted, revised reports below are
committed here.

| Report | Evaluator -> reviewer | Verdict | Feeds |
| --- | --- | --- | --- |
| [R09-01.md](R09-01.md) | Claude Fable 5.1 -> Codex `gpt-6-astra` | accept-with-changes, revised | Decision 185 (residual semantic-owner map, migration boundary) |
| [R39.md](R39.md) | Codex `gpt-6-astra` -> Claude Fable 5.1 | accept-with-changes, revised; the O-R39-05 shared-function addendum is final (Fable confirmed the 2026-09-29 revision: 36-46 fewer non-blank lines) | Decision 185 (cross-page behavior audit landing) |
| [R03-01.md](R03-01.md) | Codex `gpt-6-astra` -> Claude Fable 5.1 | accept-with-changes, revised | Decision 186 (Launcher completes within `1.2.x`; input to the necessity plan) |
| [R07-01.md](R07-01.md) | Claude Fable 5.1 -> Codex `gpt-6-astra` | accept-with-changes, revised | Decision 187 (Combiner: no switch yet; behavior comparison first) |
| [R03-launcher-plan.md](R03-launcher-plan.md) | Codex `gpt-6-astra` -> Claude Fable 5.1 | accept-with-changes (2 P2, 6 P3), revised; Fable confirmed the revision | Decision 188 (Launcher necessity plan and `1.2.x` completion schedule) |
| [V200-redefinition.md](V200-redefinition.md) | Codex `gpt-6-astra` -> Claude Fable 5.1 | accept-with-changes, revised; Fable confirmed | Decision 190 (`2.0.0` redefinition) |

Wave 2 (2026-10-01; fixed source `<worktrees>/e121b` at `d5770e52e`, the `1.2.x` trunk after #503):

| Report | Evaluator -> reviewer | Verdict | Feeds |
| --- | --- | --- | --- |
| [R09-02-03.md](R09-02-03.md) | Claude Fable 5.1 -> Codex `gpt-6-astra` | accept-with-changes, revised | Decision 227 (remaining migration boundaries; no window-lifetime residual) |
| [R11.md](R11.md) | Codex `gpt-6-astra` -> Claude Fable 5.1 | accept-with-changes, revised; Fable confirmed accept | Decision 228 (overdesign dispositions; new-IC admission table) |
| [R02-01.md](R02-01.md) | Codex `gpt-6-astra` -> Claude Fable 5.1 | accept-with-changes, revised; Fable confirmed accept | Decision 229 (General input lifecycle contract; Customized Merge reopens in `1.2.4`) |
| [R05-R06-C01.md](R05-R06-C01.md) | Codex `gpt-6-astra` -> Claude Fable 5.1 | accept-with-changes, revised; Fable confirmed accept | Decision 231 (Launcher self-update, source/auth and cold-health contracts; O2 waits for A1a) |
| [R04-01.md](R04-01.md) | Codex `gpt-6-astra` -> Claude Fable 5.1 | accept-with-changes, revised; Fable confirmed accept | Decision 232 (no delta update; threshold) |
| [R10-01.md](R10-01.md) | Claude Fable 5.1 -> Codex `gpt-6-astra` | revisions 1 and 2 rejected; revision 3 accept-with-changes; revisions 4-5 confirmed (one P3 fixed) | Decision 235 (shared-definition reference contract; parallel loading kept) |
| [R12-01.md](R12-01.md) | Codex `gpt-6-astra` -> Claude Fable 5.1 | accept-with-changes, revised; Fable confirmed accept | Decision 237 (UI interaction review scope) |
| [R13-01.md](R13-01.md) | Codex `gpt-6-astra` -> Claude Fable 5.1 | accept-with-changes, revised; Fable confirmed accept; appendix A adds the UI and core timing (Codex) | Decision 236 (.NET lane plan input) |
| [R31-01.md](R31-01.md) | Codex `gpt-6-astra` -> Claude Fable 5.1 | accept-with-changes, revised; Fable confirmed accept | Decision 238 (Header-copy facts mostly unknown; asked again before `1.2.5`) |
| [R50.md](R50.md) | Claude Code commander (retrospective, not cross-reviewed; data `evidence/1.2.1/R50-data.md`) | — | Decisions 225 and 233 (workflow changes; only R3 needs the owner's approval) |

The wave-2 reports for A1a and R12-02 are added when accepted. The owner's A1a deployment facts are in
`evidence/1.2.1/A1a-facts.md` (test area).

None of these reports is itself a specification approval, an implementation
ticket, a test result or a release gate; each states its own evaluation
boundary. Sanitization performed on entry: every test-area absolute path was
rewritten to its test-area-relative form under `evidence/` (for example
`evidence/1.2.1/R39.md`), and the read-only source worktree was rewritten to
the repository's `<worktrees>/<name>` convention. No other content was
changed.

R07-01 was written against the earlier private intake of the Python version
(commit `d7b08b92…`, 2.0.0.1). The public
[`nvt_combiner`](https://github.com/Dennis40816/nvt_combiner) repository
holds only the Python version as a new single-commit snapshot, so that
intake commit is not part of it (decision 187 follow-up on the
[`1.2.x` board](../1.2.x.md)).
