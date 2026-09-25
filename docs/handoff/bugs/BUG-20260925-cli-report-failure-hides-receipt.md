# BUG-20260925-cli-report-failure-hides-receipt: CLI writes Report before printing committed BIN receipt

Status: suspected
Severity: P2
Found: 2026-09-25, Codex worker (GPT-6), while tracing F07 report failure, at `feature/1.1.12/io-persistence`@`807c294bc`
Where: `src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs:331-348`; `src/NvtFwCombiner.Cli/CliCompositionRunSupport.cs:49-62`
Observed: after `ExecuteAsync` returns a committed Build result, the CLI writes Report before `PrintRunResultAsync` and the bundle receipt. A Report write failure can therefore prevent the CLI from printing the already committed path, size and hash. This path has not been reproduced at runtime.
Expected: a Report failure must not hide the exact committed BIN receipt, per `docs/architecture/post-v1.1.8-audit-handoff.md` AUD-03.
Evidence: code order and unguarded `File.WriteAllTextAsync` await; the CLI source is outside WS-IO's Write lock.
Owner: unassigned; commander to allocate a CLI writer.
Resolution:
