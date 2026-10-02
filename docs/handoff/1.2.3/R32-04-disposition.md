# R32-04 Markdown assertion disposition proposal for NFC 1.2.3

## 1. Status and purpose

Status: Proposal for owner decision O22; not approved or implemented.
Date: 2026-10-02. Source read: `092641516`.
Purpose: Give the owner a bounded, test-by-test KEEP / MOVE / DELETE-PROPOSED decision list. This document changes no
test, code, accepted decision, CI selection, or permission.

The acceptance boundary is R32-04 and O22 in `docs/handoff/1.1.14/1.2.x-inventory.md:348,552`: protected facts stay;
only pure frozen wording may be deleted, with owner confirmation. Decision 71 (`docs/handoff/1.1.12.md:534`) and
`docs/adr/0079-test-architecture.md:378` require individual disposition. A topic test's document remains a semantic
input until its assertion is actually migrated or removed.

## 2. Method

1. Pin the source checkout at `092641516`; search both requested test directories for Markdown paths, `ReadAllText`,
   `ReadLines`, `read_text`, and `read_bytes`.
2. Include assertions on repository Markdown content, generated Markdown summaries (file-backed or rendered in
   memory), projections, byte integrity, and canonical skill frontmatter acceptance/rejection. Follow helpers and
   fixtures; exclude metadata-only YAML/JSON checks and inline Markdown parser inputs. List named exclusions in
   Limits; keep two reviewed path-only methods as explicitly marked non-document assertions. Count methods once; split
   wording from protected checks and mark uncertain purposes unconfirmed.
3. KEEP: the assertion protects a fact that code, a contract, or a decision depends on, and the document owns that
   fact; retain mapped semantic verification.
4. MOVE: the assertion protects document structure, such as a link, heading, section, or existing path; preserve it in
   the structure lane of `scripts/verify.py` / `validate_repository`.
5. DELETE-PROPOSED: only prose wording is frozen, with no identified code, contract, or decision dependency; deletion
   requires the owner's confirmation; doubt defaults to KEEP.

## 3. Disposition tables

Each Architecture row is identified by topic filename and method, independent of class names. Its evidence uses the
topic filename relative to the Architecture directory below; at the pinned source, prepend `RepositoryBoundaryTests.`
to resolve the physical file. Pull request #519 later moved most of these files to feature classes (for example
`RepositoryDocumentTests.` and `HostInfrastructureBoundaryTests.`) and renamed `TestSupport.cs` to
`RepositoryBoundaryTestSupport.cs`; the topic and method names are unchanged. Other evidence paths are
repository-relative. Line numbers refer to the source read; counts group assertions or cases, not expanded runner
invocations.

A disposition applies to the named assertion group, not unrelated assertions in the same method. Mixed methods retain
their protected assertions. Additional Roadmap and StartupDiagnostics rows isolate wording subgroups; no whole method
is proposed for deletion.

Generated Markdown summaries, skill projections, and explicit document-byte integrity checks are included
conservatively. Their KEEP rows protect executable output or integrity contracts, rather than author prose; O22 must
not erase those behavioral gates. MOVE preserves the complete check before any prose skip, and does not move firmware
or permission semantics into a generic link checker.

### Architecture project

Directory: `tests/NvtFwCombiner.Architecture.Tests/`. Eight topic files, 16 methods.

| File | Test method | Document read | Assertion, grouped | Protected fact | Proposal | Reason | Evidence path:line |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `LauncherBootstrap.cs` | `LauncherReleaseGuidanceMatchesCanonicalAdmissionBudget` | `docs/contracts/managed-setup-v1.md`; `docs/ci/release-package.md` | 5 document string checks | Five-second operation cutoff, 0.5-second cleanup reserve, 5.5-second admission deadline | KEEP | Timing is a managed-setup contract, not editorial wording. | `LauncherBootstrap.cs:22`; `docs/contracts/managed-setup-v1.md:95` |
| `PostbuildStructure.cs` | `DynamicDiffDlmSummariesPreserveInactiveRecords` | `docs/architecture/integrity-processing-matrix.md`; `docs/architecture/ctrlram-postbuild-command-matrix.md` | 2 documents x 3 normalized strings | Only active DLM prefixes enter read/write sets; Diff NF tails and inactive records remain unchanged | KEEP | These are exact firmware mutation bounds. | `PostbuildStructure.cs:44`; `docs/architecture/integrity-processing-matrix.md:10` |
| `PresentationStructure.cs` | `ExternalCombinerVersionsAreDocumentedAsStringTokens` | `docs/adr/0006-external-combiner-tool-runner.md` | 2 exact substrings from one ADR sentence | Documented string-token contract; exact wording requirement unconfirmed | KEEP (unconfirmed) | This pins the ADR sentence, not runtime protocol representation. The constructor contract test and manifest schema already require a string. Because the sentence states an accepted contract, keep it pending confirmation of whether equivalent wording would suffice. | `PresentationStructure.cs:7,11`; `docs/adr/0006-external-combiner-tool-runner.md:33`; `tests/NvtFwCombiner.Application.Tests/ExternalTools/ExternalCombinerToolContractTests.cs:21`; `docs/contracts/external-combiner-tool-manifest-v1.schema.json:33` |
| `PresentationStructure.cs` | `UiDocumentsForbidFirmwareSemanticsInViewModels` | `docs/ui/viewmodel-boundaries.md` | 3 document strings; source check retained | ViewModels cannot own range arithmetic, integrity calculation, file reads, or tool execution | KEEP | Protects the architecture and firmware authority boundary. | `PresentationStructure.cs:29`; `docs/ui/viewmodel-boundaries.md:18` |
| `ProfileStructure.cs` | `Nt51950AndNt51951AdrMatchesCanonicalExecutionOwnership` | `docs/adr/0035-ab-topology-operator-selection.md`; `docs/architecture/nt51950-nt51951-ab-code-contract.md` | 7 positive/negative strings | Host relocates TPB DIFF only; checked Combiner output imports exactly three four-byte fields | KEEP | Protects authoritative writers and permitted firmware writes. | `ProfileStructure.cs:371`; `docs/architecture/nt51950-nt51951-ab-code-contract.md:83` |
| `RepositoryShape.cs` | `RepositoryTextFilesStayBelowEmergencyCeiling` | Markdown under `src/`, `tests/`, `docs/`, `eng/` | One scan; 2,500-line ceiling; bin/obj excluded | Generic text-file size ceiling | MOVE | Preserve the exact ceiling, roots, extensions, and exclusions in the always-run structure lane; preserve non-Markdown coverage too. | `RepositoryShape.cs:11`; `docs/adr/0079-test-architecture.md:380` |
| `RetiredIc.cs` | `RetiredIcCapabilitiesStayOutsideProductionOwners` | Markdown in four evidence roots listed by the method | 3 retired IDs checked across matching files; mixed source/JSON checks | Retired ICs cannot regain active evidence or production/publication authority | KEEP | Retirement is a support and release rule; preserve historical exceptions and non-Markdown checks. | `RetiredIc.cs:62`; `RetiredIc.cs:119` |
| `Roadmap.cs` | `NfcRoadmapHasOneOrderedVersionAllocationEntryPoint` | `README.md`; `docs/architecture/nfc_roadmap.md`; `VERSION` | 6 checks: link, headings, nonempty/unique/ordered versions, current-version membership | One reachable, structurally valid allocation index and unallocated section | MOVE | This checks routing and table structure, not milestone prose; move all six checks together. | `Roadmap.cs:12`; `docs/architecture/nfc_roadmap.md:20` |
| `Roadmap.cs` | `AbFunctionAvailabilityDoesNotHideGoldenDebt` | `docs/architecture/supported-ic-matrix.md` | 7 strings, including coverage/debt percentages | Function availability, direct Golden debt, support, and release certification are distinct | KEEP | Numbers and open human gates are declared evidence facts; do not delete them as frozen prose. | `Roadmap.cs:34`; `docs/architecture/supported-ic-matrix.md:156` |
| `Roadmap.cs` | `V0914OwnsAbCodeProductionReadmissionWithoutV0913Exposure` | `docs/architecture/0.9.x-completion-roadmap.md`; `docs/architecture/v0.9.14-roadmap-and-release-gates.md`; `docs/adr/0032-ab-code-production-readmission.md`; `SPEC.md` | 22 strings spanning pilot, ranges, routing, metadata, naming, and deferred work | Approved AB family/layout, fail-closed admission, informational metadata, and release scope | KEEP | Contains firmware ranges and admission/release rules; heading checks are not grounds to remove the group. | `Roadmap.cs:52`; `docs/adr/0032-ab-code-production-readmission.md:63` |
| `Roadmap.cs` | `V10VisibilityDecisionSupersedesPostV0911Schedule` | `SPEC.md`; `CHANGELOG.md`; `docs/governance/v0.9.12-public-visibility-review.md` | 8 strings | Owner-approved visibility schedule and public disclosure/asset boundary | KEEP | Protects permission and release rules, including historical public exposure. | `Roadmap.cs:84`; `docs/governance/v0.9.12-public-visibility-review.md:4` |
| `Roadmap.cs` | `OwnerPriorityTargetsNormalMergeReplaceBeforeAb` (protected group) | `docs/governance/development-tags.md`; `docs/architecture/supported-ic-matrix.md` | 2 IC rows x 5 checks; milestone sequencing/scope and evidence/approval conditions retained | NT51950/NT51951 route/evidence facts; no new byte behavior without evidence; owner reactivation/Golden gates | KEEP | The matrix rows hold firmware facts. The milestone titles below freeze wording; other historical sequencing/scope checks stay pending further disposition. The milestone conditions also explicitly protect byte-evidence and owner-approval rules and must stay. | `Roadmap.cs:96,108,118,120,127`; `docs/governance/development-tags.md:270` |
| `Roadmap.cs` | `OwnerPriorityTargetsNormalMergeReplaceBeforeAb` (milestone-title subgroup) | `docs/governance/development-tags.md` | Exact titles `Normal Replace priority`, `Workflow data-model convergence`, and title substring `AB merge` for the `0.5.0-dev.N`, `0.6.0-dev.N`, `0.7.0-dev.N` rows | Historical title wording only | DELETE-PROPOSED | These labels freeze how old milestones are named. Delete only the three title checks with owner confirmation; retain row sequencing, scope, byte-evidence, reactivation/Golden conditions, and both IC matrix rows. | `Roadmap.cs:110,115,119`; `docs/governance/development-tags.md:270` |
| `Roadmap.cs` | `IcWorkflowFlowchartReferenceCoversBuiltInIcLists` | `docs/architecture/ic-workflow-flowcharts.md` | 9 fixed document checks plus one row check per built-in IC | Reference stays aligned with declared IC routes, NB exclusion, and customer-info boundary | KEEP | Mixed routing structure and firmware/support facts; retain the complete group. | `Roadmap.cs:153`; `docs/architecture/ic-workflow-flowcharts.md:25` |
| `Roadmap.cs` | `ReplacePlanningRequiresIcNumAndCombinerPostProcessing` (protected group) | `docs/architecture/integrity-processing-matrix.md`; planning resources | 5 matrix checks; resource checks retained | Recorded post-replace CRC/header/tool evidence and shared Number context | KEEP | The integrity row holds recorded firmware/tool facts and explicitly limits its historical characterization to support-neutral evidence; it cannot authorize current admission. Resource checks stay intact. The historical demo-plan bullet checks are isolated below rather than treated as current behavior authority. | `Roadmap.cs:171,191,201`; `docs/architecture/integrity-processing-matrix.md:76` |
| `Roadmap.cs` | `ReplacePlanningRequiresIcNumAndCombinerPostProcessing` (demo-plan wording subgroup) | `docs/ui/0.1.1-demo-interface-plan.md` | 5 checks on Shared Number/selection and Processor/tool readiness bullets | Historical UI-plan wording only | DELETE-PROPOSED | The historical demo plan routes current UI ownership elsewhere. These checks freeze its bullet text; owner confirmation may remove only these five checks. Preserve resource checks and every integrity-matrix assertion. | `Roadmap.cs:173,176,179,185,188,189`; `docs/ui/0.1.1-demo-interface-plan.md:3,5,58,63` |
| `Roadmap.cs` | `HistoricalPlanningDocumentsPointToCanonicalRoadmapAndCurrentUiArchitecture` | `docs/architecture/0.7.0-refactor-and-evidence-plan.md`; `docs/architecture/adding-ic-merge-replace-workflow.md`; `docs/ui/0.1.1-demo-interface-plan.md` | 6 strings: historical status, owner routing, superseded-checklist rejection | Old plans cannot become competing active planning/UI owners | KEEP | Links are structural, but active versus historical authority is semantic; keep that boundary. | `Roadmap.cs:220`; `docs/architecture/0.7.0-refactor-and-evidence-plan.md:8` |
| `StartupDiagnostics.cs` | `V0105PreloadBaselineAndLifecycleLedgerStayFrozen` (protected group) | `docs/governance/v0.10.5-preload-baseline-and-ticket-ledger.md`; `docs/specs/v0.10.5-unified-preload-lifecycle.md`; `docs/adr/0049-unified-preload-lifecycle.md`; `docs/adr/0021-code-size-ratchet-and-convergence.md`; `CHANGELOG.md` | 5 predecessor rows, 13 identities, 6 size rows, 8 ticket rows, lifecycle/release strings; mixed source checks | Frozen release identity/accounting, non-transferable amendments, bounded preload lifecycle, remaining release gates | KEEP | The many strings bind source/package identities, exact gross/add/net and slice accounting, named non-transferable allowances, and lifecycle/release conditions. Exactly-once checks prevent duplicate evidence entries; ticket cells and arithmetic retain the accounting boundary. The ledger requires this baseline to remain intact. Only the heading subgroup below is proposed for deletion. | `StartupDiagnostics.cs:159,199,205,261,274,289`; `docs/governance/v0.10.5-preload-baseline-and-ticket-ledger.md:5,13,173` |
| `StartupDiagnostics.cs` | `V0105PreloadBaselineAndLifecycleLedgerStayFrozen` (title-only subgroup) | `CHANGELOG.md` | Require `#### Message Center, report, and System Information readability` exactly once; reject `#### Message Center report and System Information readability` and `#### Message Center and report readability` | Heading wording; also prevents duplication of that exact heading | DELETE-PROPOSED | These three checks pin one feature heading's words, not the generic heading level or required release sections/fields. The exactly-once count also guards against a duplicated heading; deleting it loses that specific guard. The inspected parser accepts generic feature titles. Owner confirmation covers only these three checks; retain every other assertion in this method. | `StartupDiagnostics.cs:239,242,243`; `scripts/render_release_notes.py:98`; `CHANGELOG.md:3030` |

### Repository script tests

Directory: `tests/scripts/`. Fourteen files, 54 inventoried methods: 52 with Markdown assertions and two reviewed
methods with no document assertion. Twenty-one test files contain literal Markdown paths; seven non-qualifying hits
are explained in Limits.

| File | Test method | Document read | Assertion, grouped | Protected fact | Proposal | Reason | Evidence path:line |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `test_authority_check.py` | `test_rename_into_and_deletion_of_firmware_paths_are_both_checked` | Generated authority summary `*-summary.md` | Missing firmware-owner role reported | Rename/deletion cannot evade firmware authority | KEEP | Permission gate and its diagnostic, not author wording. | `tests/scripts/test_authority_check.py:811` |
| `test_authority_check.py` | `test_passing_run_records_the_authority_blob_ids` | Generated authority summary | 2 identity strings | Summary binds policy blobs and evaluated head/base | KEEP | Protects traceability of the authority verdict. | `tests/scripts/test_authority_check.py:819` |
| `test_authority_check.py` | `test_live_head_that_differs_from_the_checkout_fails` | Generated authority summary | Checkout/live-head mismatch reported | Approval applies to the actual evaluated source | KEEP | Exact-source authority failure must remain visible. | `tests/scripts/test_authority_check.py:826` |
| `test_authority_check.py` | `test_git_failure_fails` | Generated authority summary | Missing base object reported | Missing local authority source fails closed | KEEP | Protects the authority verdict and actionable failure. | `tests/scripts/test_authority_check.py:836` |
| `test_authority_check.py` | `test_copy_from_an_unchanged_firmware_file_needs_the_firmware_role` | Generated authority summary | 2 strings: required role and source path | Copy detection retains unchanged firmware-source authority | KEEP | Permission semantics must not be deleted as diagnostics wording. | `tests/scripts/test_authority_check.py:850` |
| `test_authority_check.py` | `test_summary_names_the_checker_that_ran_when_it_is_not_the_heads` | Generated authority summary | Executed/evaluated checker identities and usage markers | Verdict identifies the checker that actually ran | KEEP | Distinguishes authority provenance from evaluated content. | `tests/scripts/test_authority_check.py:870` |
| `test_authority_check.py` | `test_summary_names_the_heads_own_checker` | Generated authority summary, rendered in memory | Summary names the evaluated head's checker; identity/state checks retained | Verdict distinguishes the matching checker from a later edited checker | KEEP | Generated Markdown is included whether rendered in memory or saved to a file. Checker provenance is an authority contract, not author wording. | `tests/scripts/test_authority_check.py:878,888` |
| `test_authority_check.py` | `test_api_failure_fails_the_run` | Generated authority summary | HTTP failure reported | Unavailable authority evidence cannot pass | KEEP | Protects fail-closed permission handling. | `tests/scripts/test_authority_check.py:904` |
| `test_canonical_golden_validation.py` | `test_accepts_contract_only_route_evidence_with_contract_reference` | Synthetic fixture `docs/contracts/test-route-contract.md` | Contract-only Golden evidence accepted; repository-confined file reference supplied | Golden evidence-kind acceptance contract | KEEP (no document assertion; stays where it is) | The validator checks a nonempty reference/locator and reads an existing confined file, without asserting its Markdown text or resolving its heading. This is a Golden contract test; leave it in its current lane. | `tests/scripts/test_canonical_golden_validation.py:500,520`; `scripts/canonical_golden_validation.py:232,242,247` |
| `test_canonical_golden_validation.py` | `test_rejects_release_canonical_readme_exact_byte_drift` | Fixture `testdata/golden/canonical/README.md` | 1 changed-content rejection | Release allowlist pins the complete canonical README bytes | KEEP | Integrity contract; arbitrary replacement prose is a mutation probe, not wording being frozen. | `tests/scripts/test_canonical_golden_validation.py:1276` |
| `test_ci_dotnet_retry.py` | `test_message_and_stack_are_bounded_escaped_and_summary_sanitized` | Generated `summary.md` | Bounded messages/stacks, truncation, fences, failed-only content | Failure evidence cannot inject commands or unbounded Markdown | KEEP | Executable diagnostic safety contract. | `tests/scripts/test_ci_dotnet_retry.py:92`; `docs/adr/0079-test-architecture.md:24` |
| `test_ci_dotnet_retry.py` | `test_twenty_identity_cap_keeps_both_attempts_of_each_failure` | Generated `summary.md` | 6 output checks; 21 input identities, 20 displayed | Bounded display retains both attempts for included failures | KEEP | Report limits and attempt evidence are behavioral facts. | `tests/scripts/test_ci_dotnet_retry.py:129` |
| `test_ci_dotnet_retry.py` | `test_failed_tests_only_retry_once_and_preserve_both_attempts` | Generated `summary.md` | Identity, bug pointer, failure/stack, passed-content exclusion | Retry evidence preserves the original failure and excludes passed details | KEEP | CI evidence/retry contract survives wording changes. | `tests/scripts/test_ci_dotnet_retry.py:619`; `docs/ci/pull-request-ci.md:205` |
| `test_ci_dotnet_retry.py` | `test_second_failure_fails_without_a_third_attempt` | Generated `summary.md` | 2 attempt messages and stack | Terminal failure retains both attempts, without a third run | KEEP | The Markdown is output evidence of bounded execution. | `tests/scripts/test_ci_dotnet_retry.py:633` |
| `test_ci_dotnet_retry.py` | `test_recovered_method_is_recorded_even_when_another_method_fails_twice` | Generated `summary.md` | Recovered FQN present | Another terminal failure cannot hide a flaky observation | KEEP | Protects release-relevant CI evidence. | `tests/scripts/test_ci_dotnet_retry.py:661`; `docs/ci/pull-request-ci.md:205` |
| `test_ci_dotnet_retry.py` | `test_flaky_bug_gate_matches_exact_fqn_only` | Fixture `docs/handoff/bugs/BUG-probe.md` via shard helper | 11 missing/text/status cases | Only an exact FQN in qualifying open bug evidence admits flaky coverage | KEEP | Markdown is machine-consumed admission evidence; not prose. | `tests/scripts/test_ci_dotnet_retry.py:967`; `tests/scripts/test_ci_dotnet_retry.py:307`; `docs/ci/pull-request-ci.md:218` |
| `test_ci_dotnet_retry.py` | `test_aggregate_recomputes_and_reports_flaky_list` | Generated `summary.md` | Literal `aggregate`; mixed console/coverage checks retained | Aggregate report identity, if required | KEEP (unconfirmed) | The sole Markdown assertion may be only a label; an output-contract requirement for that label is unconfirmed. Retain the substantive flaky gate regardless. | `tests/scripts/test_ci_dotnet_retry.py:1085`; `docs/ci/pull-request-ci.md:205` |
| `test_ci_dotnet_retry.py` | `test_failed_aggregate_prints_both_attempts_without_changing_producer_failure` | Generated `aggregate-summary.md` | 2 failure messages and stack | Finalizer diagnostics cannot replace the producer failure | KEEP | Failure priority and complete attempt evidence are contractual. | `tests/scripts/test_ci_dotnet_retry.py:1153`; `docs/ci/pull-request-ci.md:213` |
| `test_ci_dotnet_retry.py` | `test_failed_aggregate_without_readable_trx_keeps_identity_only_report` | Generated `aggregate-summary.md` | 5 output checks across 3 TRX cases: missing, invalid, unknown encoding | Retain identities; invent no unavailable failure details | KEEP | Protects truthful failure evidence under missing artifacts. | `tests/scripts/test_ci_dotnet_retry.py:1203`; `docs/ci/pull-request-ci.md:213` |
| `test_diagnostic_golden_separation.py` | `test_rejects_owner_handoff_content_drift` | Fixture `testdata/golden/owner-handoff/CASE.md` | Changed content fails closed tree hash | Diagnostic handoff inventory is immutable and distinct from canonical expected evidence | KEEP | Exact-byte evidence integrity, not a prose freeze. | `tests/scripts/test_diagnostic_golden_separation.py:213` |
| `test_external_tool_policy.py` | `test_rejects_catalog_pinned_repository_file_drift` | Fixture `external-tools/README.md` | 2 failures: size and SHA-256 | Catalog-pinned tool-policy payload cannot drift | KEEP | Protects the external-tool packaging contract. | `tests/scripts/test_external_tool_policy.py:128` |
| `test_governance_retirement.py` | `test_no_live_retired_admission_instructions` | Tracked active instructions/contracts/ADRs selected by `live_path` | One scan; sentence-level exceptions bound to their paths | Retired admission procedures cannot regain live authority | KEEP | Permission/governance rule; exact exceptions are authority bounds, not dispensable prose. | `tests/scripts/test_governance_retirement.py:108`; `tests/scripts/test_governance_retirement.py:78`; `docs/adr/0080-governance-reset.md:410` |
| `test_private_user_profile_paths.py` | `test_rejects_named_windows_user_profile_paths` | Fixture `new.md` | 1 disclosure rejection | Tracked text cannot disclose a named user-profile path | KEEP | Protects the disclosure rule, not the diagnostic's prose. | `tests/scripts/test_private_user_profile_paths.py:13` |
| `test_private_user_profile_paths.py` | `test_allows_placeholder_accounts_and_other_path_roots` | Fixture `new.md` | 4 permitted path forms | Declared placeholder/path exceptions remain bounded | KEEP | Exception contract must remain intact. | `tests/scripts/test_private_user_profile_paths.py:23` |
| `test_private_user_profile_paths.py` | `test_rejects_near_miss_placeholder_accounts` | Fixture `new.md` | 4 near-miss accounts | Similar names cannot widen a disclosure exception | KEEP | Permission/disclosure boundary. | `tests/scripts/test_private_user_profile_paths.py:38` |
| `test_private_user_profile_paths.py` | `test_historical_evidence_allows_only_existing_occurrences` | Fixture copy of `docs/governance/waivers/REL-110-FULL-VERIFY-OWNER-WAIVER-01.md`; 3 JSON cases also retained | 4 fixture cases; original accepted, appended disclosure rejected | Historical exceptions permit existing occurrences only | KEEP | Historical text is an exact exception boundary, not permission for new disclosure. | `tests/scripts/test_private_user_profile_paths.py:84` |
| `test_release_package_policy.py` | `test_distribution_metadata_uses_non_personal_owner_identity` | `docs/references/verification-report.md` | One owner line, declared identities, bounded historical URL exceptions | Release distribution identity and public disclosure boundary | KEEP | Release metadata and exceptions are contractual. | `tests/scripts/test_release_package_policy.py:1331`; `tests/scripts/test_release_package_policy.py:1349`; `docs/references/verification-report.md:1248` |
| `test_release_package_policy.py` | `test_canonical_release_allowlist_is_the_exact_self_contained_scope` | `testdata/golden/canonical/README.md` | One complete README hash; mixed scope checks retained | Exact canonical evidence included in the release allowlist | KEEP | Release/evidence closure must not be weakened as document wording. | `tests/scripts/test_release_package_policy.py:3847` |
| `test_release_package_policy.py` | `test_release_smoke_rejects_self_consistent_canonical_readme_replacement` | Packaged fixture `reference/golden/README.md` | Replacement rejected even after local metadata rehash | Package cannot self-authorize replacement canonical evidence | KEEP | Protects independently pinned release authority. | `tests/scripts/test_release_package_policy.py:3997` |
| `test_render_release_notes.py` | `test_current_version_release_section_renders` | `CHANGELOG.md` | Render succeeds; generated title names VERSION | Current stable release has complete, renderable release notes | KEEP | Rendered content is consumed for release; required sections/fields and disclosure rules are code contracts. | `tests/scripts/test_render_release_notes.py:55`; `scripts/render_release_notes.py:75` |
| `test_skill_inventory_validation.py` | `test_accepts_exact_inventory_and_invocation_metadata` | Fixture `.agents/skills/*/SKILL.md` via validator | 2 skill forms accepted | Canonical skill syntax and invocation metadata agree | KEEP | Parsed agent instructions and invocation contract, not ordinary prose. | `tests/scripts/test_skill_inventory_validation.py:104`; `scripts/validate_repository.py:700` |
| `test_skill_inventory_validation.py` | `test_rejects_unsupported_frontmatter_key` | Fixture `.agents/skills/explicit-skill/SKILL.md` via validator | One extra key rejected | Canonical frontmatter permits only declared fields | KEEP | Closed executable metadata contract; do not reduce it to a generic heading check. | `tests/scripts/test_skill_inventory_validation.py:110`; `scripts/validate_repository.py:721` |
| `test_skill_inventory_validation.py` | `test_to_spec_produces_draft_without_ready_label` | `.agents/skills/nfc-to-spec/SKILL.md` | 3 strings | Drafts and unresolved decisions cannot acquire approved implementation status | KEEP | Owner approval/permission rule. | `tests/scripts/test_skill_inventory_validation.py:298`; `.agents/skills/nfc-to-spec/SKILL.md:12` |
| `test_skill_inventory_validation.py` | `test_to_tickets_accepts_headless_vertical_paths` | `.agents/skills/nfc-to-tickets/SKILL.md` | 3 strings | Headless use cases are valid; ticket readiness requires explicit owner approval | KEEP | Accepted workflow and approval contract. | `tests/scripts/test_skill_inventory_validation.py:304`; `.agents/skills/nfc-to-tickets/SKILL.md:13` |
| `test_skill_inventory_validation.py` | `test_implement_owns_red_green_refactor_loop` | `.agents/skills/nfc-implement/SKILL.md` | 4 phase labels: `**Red:**`, `**Green:**`, `**Refactor:**`, `**Repeat:**`; manifest assertion retained | Required implementation feedback loop; exact label interface unconfirmed | KEEP (unconfirmed) | As with the three review-lens headings, duties are protected but an exact-label requirement is unconfirmed. Keep the labels for now; any approved rewrite must verify all four duties and retain the manifest check. | `tests/scripts/test_skill_inventory_validation.py:310`; `.agents/skills/nfc-implement/SKILL.md:20` |
| `test_skill_inventory_validation.py` | `test_polytail_expands_only_touched_authority` | `AGENTS.md`; `.agents/skills/nfc-review/SKILL.md` | 2 strings | Audit expansion follows affected authority rather than unrelated firmware scope | KEEP | Governance authority and review-scope rule. | `tests/scripts/test_skill_inventory_validation.py:316`; `.agents/skills/nfc-review/SKILL.md:72` |
| `test_skill_inventory_validation.py` | `test_code_review_uses_three_lenses_without_forced_subagents` | `.agents/skills/nfc-review/SKILL.md` | 3 lens labels and delegation condition | Review covers correctness, safety/architecture, evidence; delegation is conditional | KEEP (unconfirmed) | The obligations are clear; whether the exact lens labels themselves are a required interface is unconfirmed. Never remove the review/delegation obligations. | `tests/scripts/test_skill_inventory_validation.py:324`; `.agents/skills/nfc-review/SKILL.md:17` |
| `test_skill_inventory_validation.py` | `test_inventory_is_rendered_from_manifest` | `docs/governance/agent-skill-inventory.md` | One full generated-text comparison | Published index exactly reflects the manifest | KEEP | Already in the structure lane; removing the duplicate needs the owner's confirmation. `validate_skills` makes the same full-text comparison, so this is not a migration candidate. | `tests/scripts/test_skill_inventory_validation.py:350`; `scripts/validate_repository.py:765,768,1929` |
| `test_sync_derived.py` | `test_empty_projection_check_is_read_only_then_creation_converges` | Fixture canonical/projected `SKILL.md`; generated `.claude/agents/*.md` | Exact skill bodies plus invocation/tool/model fields; convergence | Projection preserves canonical instructions and declared runtime permissions | KEEP | Behavior and permission contract; complete output equality is intentional. | `tests/scripts/test_sync_derived.py:46` |
| `test_sync_derived.py` | `test_missing_canonical_source_is_not_creatable` | Fixture missing canonical `SKILL.md`, read by projection provider | Missing source rejected before projection | Writer cannot fabricate a canonical instruction source | KEEP | Protects authority direction and write permission, beyond mere path existence. | `tests/scripts/test_sync_derived.py:79` |
| `test_sync_derived.py` | `test_concurrent_creation_is_not_overwritten` | Generated projection at `provider.outputs[0]` | Concurrent bytes preserved | Projection writer cannot overwrite another writer's file | KEEP | Mutation/write-permission contract. | `tests/scripts/test_sync_derived.py:93`; `scripts/sync_derived.py:94` |
| `test_sync_derived.py` | `test_publication_time_creator_keeps_its_file` | Fixture `.claude/agents/implementer.md` | One exact-content comparison after creation race | Late concurrent creation is preserved | KEEP | Exact bytes prove no unauthorized overwrite, not a required prose sentence. | `tests/scripts/test_sync_derived.py:106` |
| `test_sync_derived.py` | `test_projection_set_rejects_stale_or_untracked_expected_files` | Generated `.claude/` Markdown paths; no content read by this check | 3 tracked-path-set cases | Projection paths match the expected tracked inventory | KEEP (no document assertion; stays where it is) | Only tracked path sets are compared; no Markdown text is asserted. The structure lane already invokes this validator. Retain the behavioral test in its current lane. | `tests/scripts/test_sync_derived.py:147,157`; `scripts/validate_repository.py:1897,1906,1914` |
| `test_sync_derived.py` | `test_stale_check_explicit_write_preserves_all_other_bytes_and_is_idempotent` | Fixture `SPEC.md`; `docs/references/verification-report.md` | 2 Markdown outputs; exact bytes except one version token | Version writer changes only declared headers, retaining historical approval/status | KEEP | Exact mutation contract for release documents. | `tests/scripts/test_sync_derived.py:355` |
| `test_sync_derived.py` | `test_invalid_versions_and_headers_fail_before_any_write` | Same 2 fixture Markdown outputs | 4 invalid replacements per input; no writes | Malformed/duplicate version headers cannot cause partial document mutations | KEEP | Parser and atomic writer behavior, not frozen prose. | `tests/scripts/test_sync_derived.py:373` |
| `test_sync_derived.py` | `test_version_provider_remains_read_only_in_ci` | Same 2 fixture Markdown outputs | Original bytes preserved under CI | CI cannot write version headers | KEEP | Explicit write-permission rule. | `tests/scripts/test_sync_derived.py:399` |
| `test_sync_derived.py` | `test_current_version_does_not_require_a_fictitious_historical_tag_node` | Fixture `SPEC.md`, verification report, `CHANGELOG.md`; historical tags are setup only | Current version accepted without a matching historical-tag entry | VERSION consistency does not fabricate release history | KEEP | Protects version/release contract; no historical tag wording is asserted. | `tests/scripts/test_sync_derived.py:431`; `scripts/validate_repository.py:928` |
| `test_sync_derived.py` | `test_removing_tag_text_gate_preserves_real_version_and_sdk_gates` | Fixture `SPEC.md`, verification report, `CHANGELOG.md` | 3 Markdown mutations within 6 input cases | Current document versions and changelog section still agree with VERSION | KEEP | Removing an old tag requirement must retain real release/bootstrap gates. | `tests/scripts/test_sync_derived.py:438`; `scripts/validate_repository.py:935` |
| `test_v0916_parity_1x_amendment.py` | `test_rows_cite_only_their_own_decisions_and_scope` | `docs/handoff/1.1.12.md`, decision 12 | One approval phrase; JSON scope checks retained | Diff NF semantic correction cites its actual owner authorization | KEEP | Firmware-owner decision is normative authority, even in a dated handoff. | `tests/scripts/test_v0916_parity_1x_amendment.py:327`; `docs/handoff/1.1.12.md:154` |
| `test_v0916_parity_artifacts.py` | `test_normative_parity_documents_name_the_exact_candidate_head` | `docs/adr/0057-v0916-black-box-parity-certification.md`; `docs/contracts/v0916-parity-certification-v1.md` | 2 documents x current-head inclusion / old-head exclusion | Normative parity guidance points to the exact authorized candidate source | KEEP | Protects source identity and certification contract. | `tests/scripts/test_v0916_parity_artifacts.py:71`; `docs/contracts/v0916-parity-certification-v1.md:393` |
| `test_verify_orchestration.py` | `test_ci_dotnet_shard_uploads_normalized_failed_evidence_and_names_failed_tests` | Generated `step-summary.md` | Shard heading, FQN, attempt, bounded failure details | Failed shard emits attributable failure evidence | KEEP | Required executable reporting contract; no producer is made passing. | `tests/scripts/test_verify_orchestration.py:7765`; `docs/ci/pull-request-ci.md:40` |
| `test_verify_orchestration.py` | `test_ci_dotnet_shard_keeps_its_failure_when_failed_evidence_has_a_reparse_point` | Generated `step-summary.md` | 2 exclusions: raw path sentinel and raw rejection detail | Unsafe evidence stays rejected; public summary omits raw path details | KEEP | Artifact safety and disclosure contract. | `tests/scripts/test_verify_orchestration.py:7888`; `docs/ci/pull-request-ci.md:45` |
| `test_verify_orchestration.py` | `test_ci_dotnet_shard_keeps_raw_coverage_paths_out_of_the_log_and_summary` | Generated `step-summary.md` | Fixed reason and FQN; multiple path sentinels excluded | Coverage rejection remains actionable without leaking raw paths | KEEP | Safety and reporting behavior. | `tests/scripts/test_verify_orchestration.py:7999`; `docs/ci/pull-request-ci.md:45` |
| `test_verify_orchestration.py` | `test_ci_failed_test_report_is_bounded_and_sanitized` | Generated `summary.md` | 50 of 55 identities; 7 repeated output checks and 3 fences | Bounded failed-test reporting, safe Markdown, truthful missing-TRX reasons | KEEP | Executable output limits and sanitization are intentional, not stylistic prose. | `tests/scripts/test_verify_orchestration.py:8162` |

## 4. Totals and least-certain dispositions

Method totals count each method once. Mixed methods with protected groups count as KEEP; their wording subgroups are
counted separately below. Unconfirmed rows, the two no-document-assertion rows, and the existing structure-lane
duplicate count as KEEP: their tests stay where they are. Parameterized cases and loop iterations are not additional
methods.

| Project | Methods classified | KEEP | MOVE | DELETE-PROPOSED |
| --- | ---: | ---: | ---: | ---: |
| Architecture | 16 | 14 | 2 | 0 |
| scripts | 54 | 54 | 0 | 0 |
| Total | 70 | 68 | 2 | 0 |

Table-group totals: Architecture **19 groups: 14 KEEP, 2 MOVE, 3 DELETE-PROPOSED**; scripts **54 entries: 54 KEEP, 0
MOVE, 0 DELETE-PROPOSED**, including the two no-document-assertion entries. The three deletion subgroups cover three
milestone-title checks, five demo-plan bullet checks, and three changelog-heading checks; no whole method or protected
fact is proposed for deletion. The original 53-script-method inventory grows by one generated-summary method.

The five least-certain decisions, in priority order:

| Test / group | Current proposal | What would settle it |
| --- | --- | --- |
| `V0105PreloadBaselineAndLifecycleLedgerStayFrozen`, title-only subgroup | DELETE-PROPOSED | Owner confirms that the exact feature title/forbidden variants carry no historical decision or consumer requirement; preserve the release-note fields and protected group. |
| `NfcRoadmapHasOneOrderedVersionAllocationEntryPoint` | MOVE | Owner accepts all six checks as structural and names the always-run lane owner; preserve current-version membership and ordering rather than migrating only the link. |
| `test_aggregate_recomputes_and_reports_flaky_list` | KEEP, unconfirmed | Identify an accepted requirement for the literal `aggregate` in Markdown, or approve a rewrite that checks the reporting duty while retaining flaky evidence/coverage checks. |
| `test_code_review_uses_three_lenses_without_forced_subagents` | KEEP, unconfirmed | Owner identifies whether exact lens labels are an interface, or approves equivalent fact-based verification of all three obligations and the delegation condition. |
| `ExternalCombinerVersionsAreDocumentedAsStringTokens` | KEEP, unconfirmed | Confirm whether the ADR's exact sentence is required, or allow equivalent contract wording; retain the string-token contract and its code/schema checks. |

## 5. Decisions for the owner (O22)

1. **Approve tests one by one, or in named batches?** Options: one by one; named batches with every method, subgroup,
   and protected check listed. Recommendation: named batches. Each named check still gets an explicit disposition; a
   batch gives no blanket permission to remove document tests.
2. **Move the document checks into the always-run structure checks in 1.2.3, or approve the list now and move them in
   1.2.11?** Options: migrate the two MOVE groups (the text-file line ceiling and roadmap allocation structure) in
   1.2.3; approve this list now and migrate them in 1.2.11. Recommendation: migrate in 1.2.3. R32-04 includes
   structure-lane migration (`docs/handoff/1.1.14/1.2.x-inventory.md:348`), O22 requires classification and migration
   together (`docs/handoff/1.1.14/1.2.x-inventory.md:552`), and R32-04 is allocated to 1.2.3
   (`docs/handoff/1.1.14/1.2.x-allocation.md:186,190`). The 1.2.11 option changes that allocation. Preserve current
   checks/mappings until the migration is verified; no prose skipping is authorized here.
3. **May we delete three checks that only pin one changelog heading's exact words? Everything else in that test
   stays.** In `V0105PreloadBaselineAndLifecycleLedgerStayFrozen`, they require `#### Message Center, report, and
   System Information readability` exactly once and reject `#### Message Center report and System Information
   readability` and `#### Message Center and report readability`. Options: delete only these three checks; keep them.
   Recommendation: delete only these three checks. The exactly-once check also prevents duplication of this particular
   heading; deleting it removes that specific guard. Keep every other assertion in the test.
4. **Two checks pin a label word and three review headings; keep them for now (recommended), or allow a rewrite that
   checks the duty instead?** The methods are `test_aggregate_recomputes_and_reports_flaky_list` (the word
   `aggregate`) and `test_code_review_uses_three_lenses_without_forced_subagents` (the headings `Spec correctness`,
   `Runtime, safety, and architecture`, `Tests and evidence`). Options: keep both as written for now; allow a rewrite
   that verifies the reporting/review duties instead. Recommendation: keep both for now. Any rewrite must preserve
   flaky evidence, all three review duties, and the delegation condition.

## 6. Limits

- This is static inspection of the pinned source, not a fresh test result, runtime-reader census, migration
  implementation, or owner approval. No build, verifier, test, network operation, commit, or push was run.
- Search established eight Architecture topic files plus the non-test Markdown helpers in `TestSupport.cs:199,269`,
  and 21 script test files with literal Markdown paths. Helper/fixture tracing supplies the additional indirect reads
  in the table.
- Seven script hits do not directly assert Markdown text: `test_authority_policy.py` classifies document paths and
  inspects non-Markdown consumer source; `test_ci_structure_contract.py` checks tracked file inventory;
  `test_frozen_evidence_pins.py` checks JSON/Git objects; `test_governance_topology.py` checks topology/entry
  outcomes; `test_prebuilt_profile_catalog_build.py` uses an ignore pattern; `test_predecessor_rolling.py` supplies
  in-memory changelog input; `test_release_promotion_policy.py` uses release notes as hashed assets rather than
  asserting their prose. Their protected behavioral gates remain outside any deletion proposal.
- Inline Markdown used only as parser/classifier input is excluded, including release-note parser mutation cases,
  retirement-exception classifier probes, and distribution-report identity helper probes. Generated Markdown output is
  included even when rendered in memory, including `test_summary_names_the_heads_own_checker`. Markdown byte-drift
  tests are retained explicitly because their integrity facts could otherwise be mistaken for wording freezes.
- Skill fixture tests targeting only `agents/openai.yaml` are excluded: `test_rejects_missing_openai_metadata`,
  `test_rejects_invocation_policy_mismatch`, `test_rejects_default_prompt_for_another_skill`,
  `test_rejects_default_prompt_matching_only_a_skill_prefix`, `test_rejects_short_description_outside_codex_bounds`,
  `test_rejects_short_description_whose_raw_length_exceeds_bound`,
  `test_rejects_short_description_whose_trimmed_length_is_below_bound`,
  `test_rejects_malformed_openai_yaml_before_field_validation`, `test_rejects_unknown_interface_metadata_field`,
  `test_rejects_unknown_policy_metadata_field` (`tests/scripts/test_skill_inventory_validation.py:124-263`). Their
  synthetic `SKILL.md` is shared setup; assertions target YAML presence, fields, invocation policy, or errors, not
  Markdown content. Their permission/metadata gates stay intact.
- In that file, `test_manifest_routes_exactly_nineteen_active_skills` and `test_only_polling_are_explicit` assert JSON
  manifest data; `test_removed_meta_skills_are_not_repository_routes` asserts manifest membership and absent paths
  (`:279,285,331`). They do not assert Markdown text and remain excluded. The two listed fixture methods establish
  canonical Markdown/frontmatter acceptance and rejection; other mixed methods retain their non-Markdown assertions.
- The 150-method cap was not reached: 70 methods were inventoried; none were omitted because of that cap. Counts are
  static method counts, not collected test cases. Class renames in another change may alter filenames/lines; method
  names and the source SHA identify this proposal's basis.
- Four rows remain unconfirmed: the ADR sentence, aggregate label, implementation phase labels, and review-lens
  headings. All are KEEP. Question 4 asks about the aggregate label and the review-lens headings; the other two stay
  KEEP without a question. Three wording subgroups are DELETE-PROPOSED: question 3 asks about the changelog heading;
  the milestone-title and demo-plan subgroups are named in the batch list that question 1 approves. None is an
  accepted deletion.
- The changelog-title dependency search was limited to the requested tests and their relevant release-note/validation
  readers. It found one exact-title assertion and generic feature-title parsing; it cannot prove absence of consumers
  outside that scope.
- A MOVE must retain coverage and failure behavior in an always-run structure owner before old assertions/mappings are
  removed. Local-link validation at `scripts/validate_repository.py:578` does not establish heading, anchor,
  allocation, or line-ceiling coverage. Inventory consistency is already covered by the full-text comparison in
  `validate_skills` (`:765-771`), invoked by the structure validator (`:1929`); it is not a missing migration gate.
- This proposal preserves firmware facts, authority/permission rules, release rules, and contracts. It neither
  promotes support nor closes missing human or Golden evidence.
