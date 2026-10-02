# BUG-20261002-nt51927-fw132-evidence-build-flake: the NT51927 FW 1.3.2 two-chip evidence Build failed once on a pull-request CI run

Status: open (cause not yet known)
Severity: P2 until the cause is known (a CtrlRAM Replace Build of an owner-certified case reported failure once)
Found: 2026-10-02, Claude Code commander (Claude Opus 5.5): pull request #535 (records only: handoff documents),
CI run `37013137906`, job `dotnet / build-test` (`110861530924`). The test failed on attempt 1 and passed on the
in-job retry, so the decision 193 gate marked it flaky and the job failed with "flaky test has no bug record".
The pull request changes no product, test or profile file.
Where: `NvtFwCombiner.Bootstrap.Tests.Nt51927CtrlRamFw132TwoChipEvidenceTests.ExactExpectedDerivedCaseProducesLockedV2EvidenceAsync`
(`icId: "NT51927"`, `expectedProfileId: "nt51927-ctrlram-replace-fw132-twochip"`,
`expectedProcessorId: "nfc.nt51927.ctrlram-postbuild-v1"`),
`tests/NvtFwCombiner.Bootstrap.Tests/Nt51927CtrlRamFw132TwoChipEvidenceTests.cs` line 87 at `2ea530361`.
Observed: `Assert.True(v2.Succeeded, CompositionRunReportJson.Serialize(v2))` failed: the CtrlRAM Replace Build of
the case returned a result that did not succeed. The run report in the message starts at 13:32:51 UTC and
completes at 13:32:54 UTC (3.5 seconds); the job log prints only the first lines of the message (the report's
header and inputs), so the issue that made the run fail is not visible there. The attempt-1 evidence
(artifact `dotnet-test-bootstrap-evidence-attempt-1` of that run) holds the full message; it has not been
downloaded yet (a download needs the owner's approval).
Expected: the Build succeeds on every run and its output has the locked hash; a failure of this test must be
explained, because it compares a certified case's complete output.
Evidence: the job log of run `37013137906`. Not yet established: which issue the report carries (an external
postbuild processor failure or timeout, a staging or output file conflict between parallel tests, or something
else), and whether the output bytes were affected. The retry in the same job passed, including the locked output
hash.
Owner: Claude Code commander.
Next: download the attempt-1 evidence with the owner's approval, read the report's issues and operations, and
decide between a test-isolation fix and a product investigation. This record stays open until then.
