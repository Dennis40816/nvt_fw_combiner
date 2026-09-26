# BUG-20260926-release-rerun-history-conflict: a failed release cannot be re-proposed from its release branch

Status: open (worked around for 1.1.12)
Severity: P2
Found: 2026-09-26, Claude Code (commander), on release pull requests #449 and #455
Where: CI job `policy / polytail` ("Require PR head to contain the exact reviewed base"),
`scripts/validate_repository.py` finalized-record history audit
(`_record_changed_in_commits_after` with `diff-tree -m` and
`_is_tree_transparent_containment_merge`), and the release branch model.
Observed: the first 1.1.12 release run stopped after its release pull request #449 had
merged into `main` as `405603dbe`. Fixes were then finalized on `1.1.12` (#454). The next
release pull request from `1.1.12` (#455) failed polytail because its head did not contain
`405603dbe`; merging `main` back into `1.1.12` then failed the history audit, because that
merge touches finalized records against a parent (`405603dbe`) that is not an ancestor of
the tree-equivalent side.
Expected: a release that stops before publication can be corrected and re-proposed from its
release branch without restructuring history.
Workaround (1.1.12): `feature/1.1.12/release-candidate` merges the reviewed pre-finalization
head `ee634de0c` onto `main` (tree-identical), re-finalizes the three review-fix records
there with a new owner attestation, and opens release pull request #456; `1.1.12` is
unchanged.
Owner: 1.1.13 WS-GOV (development and release flow reset). Options to assess: sync `main`
into the release branch before finalizing fixes after a stopped release, or let the audit
accept a merge whose other parent is a tree-equivalent release merge of an ancestor.
Resolution: not fixed.
