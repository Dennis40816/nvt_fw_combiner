# BUG-20260929-f16-capacity-retired-text-evidence: F16 wording probes need a visible failing case

Status: suspected
Severity: P2
Found: 2026-09-29, Codex (gpt-6-sol), while checking F16 evidence on `feature/1.1.14/bounded-diagnostics` at `e3994fdbe`
Where: `docs/architecture/nfc_roadmap.md:424`; `docs/architecture/post-v1.1.8-audit-handoff.md:266,274`
Observed: the accepted planning text calls for capacity wording to follow actual typed limits and for obsolete current-runtime DP Replace wording to be corrected, but those lines do not identify an input, displayed message, or failing result. The bounded probes below did not reproduce a user-visible capacity mismatch or obsolete DP message.
Expected: a failing input/display pair must establish the affected current-runtime text and its typed owner before a product edit is admitted.
Evidence:

- Capacity probe: `dotnet test tests/NvtFwCombiner.Application.Tests/NvtFwCombiner.Application.Tests.csproj --no-restore -m:1 -p:BuildInParallel=false --filter 'FullyQualifiedName~GeneralAuthoringAdmissionTests.ResolvesTechnicalParentAndSavedRuleIntersection|FullyQualifiedName~GeneralMappingDraftStateTests.InitializerInputResolvesCanonicalTypedValue' --verbosity quiet` passed 4/4 on 2026-09-29. The first test resolves the effective total-write limit to `0x40` and whole-file limit to `0x70`; the second accepts three typed initializer capacities. These checks establish the typed values, but do not render a UI/CLI message and therefore do not prove or disprove the reported text defect.
- Retired-text probe: `dotnet test tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~ShellViewModelTests.SettingsNavigation.ProductSettingsProjectRetiredDpReplaceAbsence|FullyQualifiedName~ShellScreenInventoryTests.HomeAndSettingsSectionsRemainReachableWithoutWorkflowMutation' --verbosity quiet` passed 4/4 on 2026-09-29. Settings has no DP Replace authoring row; Home and Settings remain reachable. `ShellTextResources.Localized.cs:171,181` still stores DP Replace wording in `ReplacePreview.Facts`, but the inspected current Home/Replace XAML binds its title/subtitle rather than those facts. This is source-text residue, not an observed visible-message failure.
- The probes used no firmware input or private fixture. They did not cover every runtime message or language state. A current user-visible screenshot or exact command/input with the mismatched text is still needed to promote either suspicion into a verified F16 defect.

Owner: unassigned; F16 current-runtime text owner after a visible reproduction is identified.
Resolution: pending a reproducible displayed message and the actual typed limit or retirement state.
