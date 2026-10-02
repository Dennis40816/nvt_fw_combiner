# BUG-20261002-authority-check-hidden-submodule-change: a submodule setting can hide a submodule change from the check

Status: fixed on `feature/1.2.3/authority-check-gitlink-changes` (waits for the governance owner's approval)
Severity: P2 (the authority check can miss changed submodule paths; no known pull request used it)
Found: 2026-10-02, an independent review (Claude Opus 5.5) of the test selector noticed that the selector's diff and
`scripts/authority_check.py` both ran `git diff` with no explicit submodule setting. The selector was corrected on
its branch; the checker is an approval-authority file and was left for this record.
Where: `Git.changes` in `scripts/authority_check.py`, the one place that lists a pull request's changed paths.
Observed (the commander and an independent reviewer, Git 2.53.0 on Windows, scratch repositories, the checker's own
arguments): `git diff --name-status <base> <head>` lists a submodule whose recorded commit changed as `M <path>`,
an added one as `A` and a removed one as `D`. Each of these settings makes the command omit them:
- `diff.ignoreSubmodules=all` in the Git configuration;
- `ignore = all` for the submodule in `.gitmodules`: Git reads the work tree's file, then the index's, then
  `HEAD`'s, never the two commits being compared;
- `submodule.<name>.ignore=all` in `.git/config`.
`diff.ignoreSubmodules=none` in the configuration does not override the `.gitmodules` entry. With
`--ignore-submodules=none` on the command line the output is identical to an unconfigured run in every case,
including renames, copies and type changes. A submodule replaced by a file, or the reverse, was never hidden.
Effect: the check classifies the changed paths it is given, and in CI it runs in a checkout of the pull request's
head, so the head's `.gitmodules` decides.
- A pull request that moves a submodule and sets `ignore = all` in `.gitmodules` was checked as if only
  `.gitmodules` changed. Today both `third-party/**` and `.gitmodules` are R2 without roles, so the floor and the
  roles of such a pull request would not have differed; the submodule path was missing from the listing.
- If the base's `.gitmodules` already carried `ignore = all`, a pull request that only moved the submodule would
  list no path at all and its floor would fall to R0.
- An added, removed or renamed submodule was hidden the same way at any path, including a path under a pattern
  that requires a role.
Expected: every changed path is listed whatever the submodule settings of the runner or of the checkout are.
Fix: `Git.changes` passes `--ignore-submodules=none`. Two tests use submodule entries made with
`git update-index --cacheinfo` (no real submodule checkout): one moves an entry with `ignore = all` in
`.gitmodules` under `diff.ignoreSubmodules` `none` and `all`; one adds and removes entries under
`diff.ignoreSubmodules=all`. Both fail without the flag (`('third-party/vendored',) not found in []`).
Verified at the fixing commit: `tests/scripts/test_authority_check.py` passes; `python scripts/verify.py
--structure-only` and `--ci-python-shard repository-scripts-a-g` pass.
When it takes effect: the check runs the checker of the pull request's own head (ADR 0080, P1), so a head that
contains this commit is checked with the fix and an older open head keeps the old checker until it is rebased.
The fixing pull request changes the check it runs: it needs the base checker's result on its head and the
owner's self-change statement (ADR 0080, "Changes to the check itself").
Not changed, for separate follow-ups:
- Other diffs with no submodule setting: `scripts/collect_review_handoff.py:166` (a hidden submodule change would
  drop out of the review handoff's file list) and `scripts/v0916_parity_certification.py:1073`, `:1081` (exact
  changed-path comparisons over historical commits). `scripts/coverage_policy.py:1052` is limited to C# paths.
- `git status --porcelain` cleanliness checks in the handoff, parity, packaging and publishing scripts may miss an
  unstaged submodule move under `ignore = all` (not tested).
- Other Git state that can alter this diff: a `refs/replace` entry for a compared commit emptied the diff in the
  reviewer's test (`--no-replace-objects` would pin it; the CI checkout is not expected to fetch such refs, not
  checked); `diff.relative=true` matters only when Git runs in a subdirectory, which the check does not do.
- The classification of `third-party/**` and `.gitmodules`.
