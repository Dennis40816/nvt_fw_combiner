# BUG-20261028-nt51925-readonly-bundle-failures: Trusted-bundle failures escaped read-only classification and metadata

Status: fixing
Severity: P3
Found: 2026-10-28, Codex (gpt-6.1-sol, xhigh), PR4b static inspection, at feature/1.2.2/nt51925-pr4b-open-authoring@eac8ff16a
Where: src/NvtFwCombiner.Application/InputInspection/CompiledFirmwareArtifactClassifier.cs; src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCompiler.StandardMerge.Routing.cs
Observed: GetStandardMapSelectionPrerequisite was outside failure handling on both read-only paths. The metadata builder caught only InvalidDataException; classification compilation also left IOException and UnauthorizedAccessException unhandled.
Expected: A failed trusted-bundle read cannot publish classification consensus or declared metadata and must use the existing typed failure result, per PR4b item F.2.
Evidence: Base source call/catch inspection. DeclaredMetadataBundleFailuresReturnTypedIssues and ClassificationBundleFailuresReturnNoConsensus inject invalid-data, I/O and access failures at prerequisite, metadata and compilation boundaries.
Owner: Codex, feature/1.2.2/nt51925-pr4b-open-authoring
Resolution: Local patch returns a typed metadata issue or the existing null classification tuple; the prerequisite getter's contract for other callers is unchanged. Regression tests run on the host build of the PR4b head; the exact-head review is recorded in the pull request.
