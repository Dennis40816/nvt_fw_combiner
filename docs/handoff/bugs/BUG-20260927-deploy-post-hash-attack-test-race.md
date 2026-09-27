# BUG-20260927-deploy-post-hash-attack-test-race: the post-hash attack test can mutate the package before the hash is taken

Status: open (observed once in seven full-verifier runs; the script rejected the mutated package in every run)
Severity: P3
Found: 2026-09-27, Claude Code (Opus 5.5), in `python scripts/verify.py --all` at batch 2b head `3b8d46693`
Where: `tests/scripts/test_deploy_update_source.py`
(`test_download_changed_after_initial_hash_is_not_admitted` and its generated `post_hash_attacker.py`); not
`scripts/deploy-update-source.ps1`, which the batch did not change
Observed: the test expects the mutation to happen after the script's first hash stream has closed, so that the later
`verified downloaded package` check rejects it. In this run the script rejected earlier, at its first hash check, with
`Downloaded package bytes do not match published Release metadata.`; the assertion on the rejection message failed while
the admitted package correctly did not exist. The attacker counts any failed exclusive open of the package as the hash lock.
Other holders of the file, such as the fake `gh` copy that writes it or an on-access scanner, can cause the same failure, so the
attacker can mutate the file before the script opens its first hash stream. The run was slower than usual (lane 57.9 s against
35–40 s in the six earlier runs) while another full test suite ran on the same machine.
Expected: the test proves the post-hash case deterministically, for example by having the script signal when its first hash
stream is open, or by accepting either rejection stage while still requiring that nothing is admitted.
Evidence: the verifier log of that run (local test area); three reruns of the test file at the same head passed (17/17 each);
the same lane passed in the six earlier full runs of batches 2a and 2b.
Owner: WS-TEST (test architecture work under ADR 0079); an R1 test-only correction.
Resolution:
