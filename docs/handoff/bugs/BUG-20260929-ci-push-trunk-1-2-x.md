# BUG-20260929-ci-push-trunk-1-2-x: merges into the `1.2.x` trunk get no post-merge CI run

Status: open
Severity: P2
Found: 2026-09-29, Claude Code commander (Claude Opus 5.5), while opening the first pull request into `1.2.x`
(#485), at `1.2.x`@`09d2a6ca2`
Where: `.github/workflows/ci.yml:3-7` and its mirrored template `docs/ci/workflow-templates/ci.yml:3-7`; the
contract test `tests/scripts/test_ci_structure_contract.py` (`test_ci_push_checks_main_and_integration_trunk`)
pins both lists to `[main, 1.1.x]`
Observed: `ci` runs on every `pull_request`, so pull requests into `1.2.x` are checked. Its `push` trigger lists only
`main` and `1.1.x`, so a merge into the `1.2.x` trunk (cut by decision 184) starts no `ci` run on the resulting
trunk commit.
Expected: every trunk gets the same post-merge `ci` run that `1.1.x` gets, so a trunk-only integration failure
(for example between two pull requests that each passed on their own base) is found on the trunk commit itself,
per WS-GOV decisions 4 and 10 (integration through pull requests into a trunk with tiered CI), as recorded in
the related, fixed [BUG-20260925-version-branch-ci-gap](BUG-20260925-version-branch-ci-gap.md), whose fix
covered only `1.1.x`.
Evidence: `.github/workflows/ci.yml:6-7` and `docs/ci/workflow-templates/ci.yml:6-7`
(`push: branches: [main, 1.1.x]`), pinned by the contract test above; decision 184 on the
[`1.1.x` board](../1.1.12.md) cuts `1.2.x`; the trunk ruleset pattern `*.*.x` covers `1.2.x`.
Owner: Claude Code, `feature/1.2.1/post-1.2.0-sync`. The fix changes a CI workflow; decision 200 (2026-09-30)
places it in the post-`1.2.0` back-merge pull request, which is R3 (`release`: `VERSION` and
`.github/workflows/**`) in any case, instead of the separate CI-governance pull request first planned. Decision
215 chooses `[main, 1.2.x]` in both workflow files, the contract test and `.github/workflows/README.md`; `1.1.x`
is frozen and takes no pull requests.
Resolution: changed on `feature/1.2.1/post-1.2.0-sync`, pending review and merge; the first push `ci` run on the
resulting `1.2.x` merge commit is the verification.
