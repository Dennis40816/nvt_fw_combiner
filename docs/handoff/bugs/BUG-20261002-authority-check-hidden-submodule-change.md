# BUG-20261002-authority-check-hidden-submodule-change: a submodule setting can hide a submodule's new commit from the check

Status: fixed on `feature/1.2.3/authority-check-gitlink-changes` (waits for the governance owner's approval)
Severity: P2 (the authority check can miss one changed path; no known pull request used it)
Found: 2026-10-02, an independent review (Claude Opus 5.5) of the test selector noticed that the selector's diff and
`scripts/authority_check.py` both ran `git diff` with no explicit submodule setting. The selector was corrected on
its branch; the checker is an approval-authority file and was left for this record.
Where: `Git.changes` in `scripts/authority_check.py`, the one place that lists a pull request's changed paths.
Observed (the commander, Git 2.53.0, a scratch repository): a commit that changes only a submodule's recorded commit
is listed as `M <path>` by `git diff --name-status <base> <head>`. With `diff.ignoreSubmodules=all` in the Git
configuration, or with `ignore = all` for that submodule in the working tree's `.gitmodules`, the same command
prints nothing. Adding `--ignore-submodules=none` lists the path again in both cases.
Effect: the check classifies the changed paths it is given. The repository has one submodule,
`third-party/nvt_combiner` (class `third-party/**`). A pull request that moves it to another commit and sets
`ignore = all` in `.gitmodules` would be checked as if only `.gitmodules` changed; the submodule path would be
missing from the classification and from the owned-path comparison. The `.gitmodules` change itself stays visible,
and it is classified, so the pull request is not unclassified; what is lost is the submodule path.
Expected: every changed path is listed whatever the submodule settings of the runner or of the head are.
Fix: `Git.changes` passes `--ignore-submodules=none`. A test moves a submodule entry, sets `ignore = all` in
`.gitmodules` and runs with `diff.ignoreSubmodules` set to `none` and to `all`; it fails without the flag.
Not changed: the classification of `third-party/**` and `.gitmodules`; other Git settings that could alter a
diff's output were not examined here.
Note: the checker that judges a pull request is the base branch's copy, so the fix takes effect for pull requests
opened after it merges.
