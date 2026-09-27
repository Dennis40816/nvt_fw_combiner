# BUG-20260927-trust-probe-start-failure-escapes: a start failure of the runtime trust probe escapes as an exception

Status: open
Severity: P3
Found: 2026-09-27, Codex (gpt-6-sol) review of W6-B (PROCESS-START-TYPED-1113-01) at `d75a4b585`
Where: `src/NvtFwCombiner.Infrastructure/ExternalTools/RuntimeTrustProbeProcess.cs` (catches only the cleanup-capacity
exception around `IExternalProcessRunner.RunAsync`)
Observed: after W6-B the runner turns an OS start failure into `ExternalProcessStartFailedException`; the trust probe does
not catch it, so a probe host that cannot start still escapes the `runtime.trust.*` typed results.
Expected: the probe maps the typed start failure into its existing typed failure result, like the two external processors.
Evidence: the W6-B review; the same pattern as BUG-20260926-process-start-failure-escapes-typed-result.
Owner: a follow-up R1 change to the trust probe (toolchain runtime owner).
Resolution:
