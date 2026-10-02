# BUG-20261002-predecessor-cli-finds-no-external-tools: the comparator's staged CLI could not find the legacy Combiner

Status: fixed
Severity: P1 (no CtrlRAM Replace scenario could run)
Found: 2026-10-02, Claude Code (Opus 5.5), in the R35-09 real-execution rehearsal against `v1.2.1`, at
`feature/1.2.2/executor-contract`@`2b90ffd73`
Where: `scripts/predecessor_comparison.py`, `execute_cli_stage`
Observed: a CtrlRAM Replace Preview exited 1 without a report; stderr named
`capability.readiness.runtime-dependency-blocked` and an unregistered tool binding `legacy-combiner-1.13.0`. The
comparator staged only the runtime closure. The CLI looks for a directory named `external-tools` in its base
directory and then in each parent (`ExternalProcessorEnvironmentLoader.FindExternalToolsRoot`; `v0.9.16` has the
same search in `ExternalProcessorFactory`), so it found none, and a directory of that name above the temporary root
would have been used instead.
Expected: each executor runs with the external tools of its own commit, whose tree the report identity already
records, per `docs/contracts/predecessor-comparison-v1.md` ("Executors").
Evidence: rehearsal runs `rolling-5` and `rolling-6` in the test area (`evidence\1.2.2\p2c\rehearsal`): 12 scenarios
`PREDECESSOR_PROCESS_FAILED` at `preview`. In `rolling-7` the same Previews run the tool and write a report.
Owner: Claude Code, `feature/1.2.2/executor-contract`
Resolution: fixed in `2b277c0b6`: each CLI process gets a read-only copy of the `external-tools` tree of its
executor's commit beside its runtime closure, from the Git blobs, under the closure's custody and hash checks
(`executor.externalToolStaging` in the contract). Three tests in `tests/scripts/test_predecessor_comparison.py`.
