# BUG-20261028-nt51925-declared-metadata-admission: Declared metadata bypassed its route compilation contract

Status: fixing
Severity: P3
Found: 2026-10-28, Codex (gpt-6.1-sol, xhigh), PR4b static inspection, at feature/1.2.2/nt51925-pr4b-open-authoring@eac8ff16a
Where: src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCompiler.StandardMerge.Routing.cs
Observed: The PR4a declared-map metadata branch accepted any non-null plan without checking its allowed map, profile id/version or trusted bundle hash against the published route.
Expected: Read-only metadata must retain the exact route compilation authority; PR4b item F.1 requires fail-closed admission on mismatch.
Evidence: The base branch checked only plan non-null and publication currency. DeclaredMetadataMustMatchPublishedCompilationContract now covers valid plans and mismatched map/profile/version/bundle, missing source and empty plans.
Owner: Codex, feature/1.2.2/nt51925-pr4b-open-authoring
Resolution: Local patch extends CanonicalCapabilityCompilationContract as the admission owner. Regression tests run on the host build of the PR4b head; the exact-head review is recorded in the pull request.
