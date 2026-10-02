# Pilot split plan: `RepositoryBoundaryTests` (WS-TEST batches T2a and T2b)

- Status: **T2a implemented on 2026-10-02 (pull request #519; result and differences from this plan in the
  [log](WS-TEST.md)); T2b not started.** Board decision 74 (D-6) chose this
  class, with the class, fixture and shared-state review, the file list
  recomputed at the real branch point and active write locks respected;
  decision 73 puts T2a and T2b in 1.1.13. The commander decided on 2026-09-26
  that T2a starts after batch 2a is frozen, from the batch head, and that
  `Roadmap.cs` stays in the residual class. It becomes a dispatch envelope when
  the commander schedules it. Revised on 2026-09-26 after the independent
  design review (findings F-5 and F-6 in the [log](WS-TEST.md)).
- Rules: the split rules S1 to S10 of the
  [test architecture ADR draft](ADR-DRAFT-test-architecture.md) (decision
  item 7). Log: [WS-TEST](WS-TEST.md).
- Risk: R1, test files only (`tests/NvtFwCombiner.Architecture.Tests/`). No
  product code, profile, script, workflow or document changes.
- Two separately reviewed changes (rule S4): **T2a** moves the files and keeps
  every test in one serial collection, exactly as the single class runs today;
  **T2b** removes that serialization after its own evidence.
- Measured at `cf4e42697`, whose Architecture test files are identical to the
  trunk head `9b2a7369e` (static counts; no build or test run). Step 0
  recomputes everything at the real branch point.

## Why this class

1. **Largest test aggregate.** 78 partial files, 11,229 nonblank lines and 231
   test methods in one class. xUnit runs one class as one serial unit, so today
   these 231 tests run one after another.
2. **Lowest-risk place to prove the procedure.** The tests read repository
   text and assert on it; they reference no product assembly. The one
   exception is visible and contained: `PackageTrustMaterializationBatch`
   starts `dotnet msbuild` once per test process (23 materialization cases in
   temporary directories, a two-minute budget) for the five
   `PackageTrustIndex` tests.
3. **The split is mechanical.** Topics already live in separate files. Shared
   helpers are concentrated: one helper file without tests is used by 72 of the
   78 files, the root file's three helpers by 51, and six other files define
   helpers used by one to three other files. No partial declaration has an
   attribute, a base type, an interface, a constructor, `Dispose` or a
   fixture.
4. **It feeds the other WS-TEST and WS-GOV work.** It starts separating the
   document checks that WS-GOV G2 must move into the structure lane before
   prose-only pull requests may skip product tests: `RepositoryDocumentTests`
   gets the 2,500-line ceiling now, and the roadmap assertions when
   `Roadmap.cs` leaves the residual class. It also gives the process-heavy
   MSBuild batch its own class.
5. **Few collisions.** Two existing files are edited by open branches or
   worktrees, a third is held back while an old branch is unconfirmed, two are
   linked from the roadmap, and open branches add two new files; all of them
   stay in a residual class (see [files to avoid](#files-to-avoid)).
6. **Cheap to measure.** Architecture.Tests runs last in the `core` CI shard
   and takes seconds to minutes locally, so before and after runs can be
   repeated on a quiet machine.

What it does not prove: this pilot validates the split procedure. It is no
evidence that UI tests can run in parallel; that needs its own S9 review (ADR
option U2a).

Why not a UI class now: `XamlControlStyleContractTests` and the large shell
group classes all sit in the serialized `UiAvaloniaRuntime` collection, so a
UI split gains time only by taking files that build no control out of that
collection (ADR option U2a). That removes serialization, which needs the
indirect-use review of S9; F08 and the navigation focus work edit those
classes; and UiSmoke timing needs the U0 measurement first. U2a is the planned second
split, using the procedure this pilot proves. Why not the ProfileContract or
Application aggregates: the pre-built catalog and VersionManagement work may
edit them.

## Target classes

Every file keeps its topic part: `RepositoryBoundaryTests.<Topic>.cs` becomes
`<NewClass>.<Topic>.cs`. Counts are nonblank lines and test methods at
`cf4e42697`.

| New class | Files | Lines | Methods | Topics |
| --- | ---: | ---: | ---: | --- |
| `PresentationBoundaryTests` | 11 | 1,657 | 34 | PresentationFirmwareSlotStructure, PresentationOrchestration, PresentationRunnerStructure, PresentationTokenStructure, PresentationViewModelStructure, ShellSurface, SettingsModal, LocalUiFileStores, Localization, MemoryLayoutStructure, SupportMatrixStructure |
| `ApplicationBoundaryTests` | 19 | 1,331 | 33 | AcceptedExecutionConvergence, AdditionalDeliveryConvergence, ApplicationCompositionConvergence, ApplicationConvergence, ApplicationStructure, CompilationExecutionPhases, CompiledCompositionIdentity, CompiledCompositionProjection, V2CompiledCompositionStructure, ProcessorPlanConvergence, SingleExecutionPort, GeneralWorkflowConvergence, GeneralInspectionStructure, InputInspectionStructure, PageInspectionIsolation, DomainPreparationOutcome, FirmwareMapResolutionStructure, FamilyValidationStructure, FullImageMetadata |
| `CanonicalCatalogBoundaryTests` | 15 | 1,568 | 37 | CanonicalAdmission, CanonicalCapabilityCatalog, CanonicalCatalogSelection, CanonicalIcNumberMode, CanonicalInputs, CanonicalOperations, CanonicalProfileDefinition, CanonicalProfileHeader, CanonicalValidations, CapabilitySelectors, CatalogTokenStructure, ClosedVocabulary, CtrlRamDiscovery, CtrlRamSnapshot, Nt51928CtrlRam |
| `ProfileBoundaryTests` | 6 | 1,053 | 23 | ProfileStructure, ProfileSchemaTrust, ProfileMaterializerBuildTool, PostbuildStructure, ContractsStructure, RetiredIc |
| `PackageTrustBoundaryTests` | 2 | 902 | 5 | PackageTrustIndex, PackageTrustMaterializationBatch; category `process`; the two files share the driver's once-per-process guard and lazy result, so they stay in one class |
| `BootstrapCliBoundaryTests` | 3 | 1,005 | 25 | BootstrapStructure, BootstrapCliConvergence, CliStructure |
| `HostInfrastructureBoundaryTests` | 6 | 1,532 | 28 | InfrastructureConvergence, InfrastructureStructure, LauncherBootstrap, FirstInstallationProgress, VersionRegistry, StartupDiagnostics |
| `RetirementBoundaryTests` | 7 | 546 | 19 | DpReplaceRetirement, LarCatalogRetirement, LarTerminalStructure, LegacyRetirementStructure, WorkbenchRetirement, WorkbenchRetirement.DpIdentity, MemoryNamingConvergence |
| `RepositoryDocumentTests` | 1 | 38 | 1 | RepositoryShape (the ceiling also covers source files; G2 moves it); Roadmap joins when it leaves the residual class |
| `RepositoryBoundaryTestSupport` (static helpers) | 2 | 367 | 0 | TestSupport and TestParallelism (both without tests), plus the helpers moved under rule S3 |
| `RepositoryBoundaryTests` (residual) | 6 | 1,230 | 26 | the root file (class summary and `ArchitectureTestsRemainDependencyFree`), PresentationStructure and DesktopHostConvergence (linked from the roadmap), WorkbenchStructure (edited on nvt-marker and in the C-7 worktree), JsonSchemaConcurrency (edited on VersionManagement JSON), Roadmap (held back by the commander while `feature/1.1.x/roadmap-and-agent-workflow` is unconfirmed) |
| **Total** | 78 | 11,229 | 231 | |

The mapping covers every file exactly once (checked by a script over the file
list at `cf4e42697`). Two files that open branches add,
`RepositoryBoundaryTests.NvtEndFlagCallers.cs` (nvt-marker) and
`RepositoryBoundaryTests.JsonContextOwnership.cs` (VersionManagement JSON),
join the residual class when they merge; they compile unchanged because the
helpers stay reachable through the `global using static` import. The
largest new class has 1,657 nonblank lines. The implementer may regroup
topics if a helper dependency requires it, within these limits: no new class
above 2,000 nonblank lines, document checks in `RepositoryDocumentTests`, the
MSBuild batch alone in its class.

Besides the two helper files, three files define private helpers that files in
other target classes use. Under rule S3 those helpers move verbatim, with only
`private` changed to `internal`, into `RepositoryBoundaryTestSupport`: `Root`,
`AssertContainsAll` and `AssertDoesNotContainAny` (root file; used by 51
files), `AssertNoProductionText` (LegacyRetirementStructure; also used by
LarTerminalStructure, MemoryNamingConvergence and, in another class,
PostbuildStructure) and `HasPathSegment` (RepositoryShape; also used by
CatalogTokenStructure). TestParallelism's two assertion helpers need no move:
its file becomes a partial file of the support class. Helpers shared only
inside one target class stay where they are: `Slice` (PageInspectionIsolation
and ApplicationStructure) and the package-trust helpers (the two
`PackageTrustBoundaryTests` files).

## Files to avoid

Write locks seen on 2026-09-26: committed branch changes against the trunk head
`9b2a7369e` and uncommitted worktree changes. The commander rechecks at the
branch point (step 0); a new hit moves that file to the residual class.

| Where | What it edits | Pilot handling |
| --- | --- | --- |
| `feature/1.1.13/nvt-marker` | `RepositoryBoundaryTests.WorkbenchStructure.cs`; adds `RepositoryBoundaryTests.NvtEndFlagCallers.cs` | both stay in the residual class |
| C-7 TP SVN proposal (`<worktrees>/wt-c7-stack`, uncommitted; `<worktrees>/wt-c7-base` adds tests only elsewhere) | `RepositoryBoundaryTests.WorkbenchStructure.cs`; the rest are Application, Bootstrap, Infrastructure, ProfileContract and UiSmoke tests | residual class; no other overlap |
| `feature/1.1.13/version-management-json` | `RepositoryBoundaryTests.JsonSchemaConcurrency.cs`; adds `RepositoryBoundaryTests.JsonContextOwnership.cs` | both stay in the residual class |
| The roadmap | links `RepositoryBoundaryTests.PresentationStructure.cs` and `RepositoryBoundaryTests.DesktopHostConvergence.cs` | residual class until the roadmap owner updates the links |
| F08 (`feature/1.1.13/f08-save-notice`) | UiSmoke: `LocalStateSaveNoticeTests.cs`, `ShellViewModelTests.Preferences.cs` | no overlap: the pilot touches only Architecture.Tests |
| CLI work (`feature/1.1.13/cli-hardening`; the execution-refusal item of decision 68) | Bootstrap CLI tests, for example `CliReportAtomicWriteTests.cs` | no overlap today; recheck `RepositoryBoundaryTests.CliStructure.cs` and `RepositoryBoundaryTests.BootstrapCliConvergence.cs` at the branch point, where CLI changes usually add structure checks |
| Rolling parity (`<worktrees>/parity`) | a new repository-script test module | no overlap |
| `feature/1.1.x/roadmap-and-agent-workflow` | `RepositoryBoundaryTests.Roadmap.cs` | last commit 2026-09-03, not merged, status unconfirmed: `Roadmap.cs` stays in the residual class (commander, 2026-09-26) |

## Static review (E7), expected content

The implementer confirms or corrects each row at the branch point; any
difference other than the class names must be explained.

| Aspect | Before (one class) | After T2a | After T2b |
| --- | --- | --- | --- |
| Class attributes | none | one shared collection attribute on each class, including the residual class | none, except `PackageTrustBoundaryTests` in a collection with parallelization disabled |
| Base types, interfaces | none | none | none |
| Constructors, `Dispose`, `IAsyncLifetime` | none | none | none |
| Fixtures | none | none | none |
| Static state | immutable arrays and generated regexes; `Root`; the materialization `Lazy` result and launch counter | same values; `Root` and the moved helpers in the support class (initialized once per process, as before); the `Lazy` and counter still in one class | unchanged from T2a |
| Initialization count | `Root` once per process; the MSBuild driver once per process (the counter throws on a second start) | same | same |
| Background work | the `dotnet msbuild` child, awaited, killed with its process tree on timeout | same | same, running alone after the parallel classes |
| Temporary files | unique directories per case, deleted in `finally` | same | same |
| Process-wide state (environment, culture, current directory) | none written | none | none |
| Order dependence | none found: no test writes state that another reads | to confirm | to confirm with repeated runs |
| Parallelism | the 231 tests serial; the class parallel with `ProjectDependencyTests` | identical: one serial collection, parallel with `ProjectDependencyTests` | nine classes in parallel; the MSBuild batch alone afterwards |

## Steps

0. **Preconditions.** Board decision 74. Batch 2a is frozen. The commander
   rechecks the [files to avoid](#files-to-avoid) (including
   `RepositoryBoundaryTests.CliStructure.cs` and
   `RepositoryBoundaryTests.BootstrapCliConvergence.cs`) and any new
   `RepositoryBoundaryTests.*.cs`, creates `feature/1.1.13/test-pilot-split`
   from the batch 2a head and records the dispatch envelope. The implementer
   recomputes
   the file list, the mapping table, the helper uses and the E7 table at that
   branch point B, applies the test-area setup of the root `AGENTS.md`, and
   times only on a quiet machine (no other build, test or verifier; recorded
   with each timing).
1. **Baseline at B**, Release build of the Architecture project: discovery
   (`dotnet vstest <assembly> --ListTests`), and three `dotnet test` runs with
   a TRX logger. Keep the discovery list, the TRX files, the wall-clock times
   and the materialization batch duration outside Git; cite their hashes.
2. **T2a commit 1, support class.** Rename `RepositoryBoundaryTests.TestSupport.cs`
   to `RepositoryBoundaryTestSupport.cs` and
   `RepositoryBoundaryTests.TestParallelism.cs` to
   `RepositoryBoundaryTestSupport.TestParallelism.cs`; declare both as
   `internal static partial class RepositoryBoundaryTestSupport`; move the
   listed helpers verbatim; add
   `global using static NvtFwCombiner.Architecture.Tests.RepositoryBoundaryTestSupport;`
   to `GlobalUsings.cs`. Build and run: discovery and outcomes are identical to
   B, because no test has moved yet.
3. **T2a commit 2, the moves.** Add one collection definition (a new file, no
   `DisableParallelization`), so that its member classes run one after another,
   as the single class did. For each target class: `git mv` its files, change
   each declaration line to `public sealed partial class <NewClass>`, and, in
   the class's first file, add one `/// <summary>` line (the build treats
   missing documentation comments as errors) and the shared collection
   attribute. The residual class gets the attribute on its root file. Build and
   run.
4. **T2a evidence** E1 to E7 (below), then review and pull request. E5 is
   expected to show no speed change: T2a changes names, not scheduling.
5. **T2b, after T2a merges.** Remove the shared collection attribute from
   every class, put `PackageTrustBoundaryTests` into a collection with
   `DisableParallelization = true` (category `process`), and delete the unused
   shared collection definition. Evidence: E1, E2, E5 and E7 again, E2 over at
   least five consecutive full runs of the project, and the MSBuild batch
   headroom. Separate review and pull request.

## Equivalence evidence

- **E1 discovery.** Same number of discovered cases at the head H and at B.
  After removing the class name, the multiset of test identities (method and
  arguments) is identical, and each identity's old and new class match the
  mapping table.
- **E2 outcomes.** Every case passes at H as at B; no new skip.
- **E3 mechanical diff.** `git diff --find-renames -U0 B H --
  tests/NvtFwCombiner.Architecture.Tests` contains only: renames; class
  declaration lines; one summary line per new class; `using` and
  `global using` lines; `private` changed to `internal` on members of the
  support class; helper members removed from one file and added to the support
  class byte-identical except for that change; collection attributes and
  collection definitions. Any other line fails E3.
- **E4 canary**, in a scratch worktree that is never committed: three
  deliberate violations, each failing exactly the expected moved test under
  its new class name. (a) A scratch Markdown file of 2,501 lines under
  `docs/` fails
  `RepositoryDocumentTests.RepositoryTextFilesStayBelowEmergencyCeiling`.
  (b) A text that a `BootstrapCliBoundaryTests` test requires to be absent,
  added to the CLI source file it reads, fails that test. (c) The same for one
  `PresentationBoundaryTests` or `ApplicationBoundaryTests` test. This proves
  the moved tests still execute and assert.
- **E5 timing.** Three runs at H against the three at B on the same quiet
  machine: median wall-clock, per-class durations from the TRX files, and the
  MSBuild batch duration with its headroom to the two-minute budget.
- **E6 CI.** The pull request's `core` shard passes; the finalizer's
  discovery reconciliation passes with an unchanged Architecture case count.
  If CI needs a second attempt, the evidence rules of the CI evidence change
  apply (a new workflow run, not "Re-run failed jobs", until a real re-run has
  been verified); a passing second run does not explain away the first
  failure, which gets a bug file.
- **E7 static review.** The table above, confirmed or corrected at B and at H
  (rule S9).

## Records and review

Before each implementation commit, the WS-TEST log records the admission:
authority (board decisions 73 and 74), base, exact paths, acceptance, narrow
tests, residual items.
No capability-reuse record: the validator rejects a record whose only path is a
test file, as it did for the H1 fix. Each of T2a and T2b gets an exact-head
review by the other runtime (or a fresh session) with its evidence attached,
then its own pull request into the trunk. Narrow tests: Architecture.Tests
only.

## Acceptance

- T2a: E1 to E7 hold at one exact head; scheduling is unchanged (E7).
- T2b: E1, E2, E5 and E7 hold at one exact head; five consecutive green runs;
  the MSBuild batch keeps at least twofold headroom.
- No test body, test name, assertion, input path or product file changed.
- Every new class is below 2,000 nonblank lines; the residual class holds only
  the six listed files (eight after nvt-marker and VersionManagement JSON
  merge).
- The log records the timing result, including a slower result if that is what
  E5 shows.

## Stop and ask

- A move would need a change inside a test method, a test rename or an edited
  assertion.
- A case fails, changes outcome, or discovery counts differ.
- A moved test turns out to be referenced by a contract or a document link.
- E7 finds a difference that is not a class name, or state the table does not
  list.
- E5 shows T2b slower than B, or the MSBuild batch with less than twofold
  headroom (more than 60 s).
- A file to move appears in another active write lock.

## Expected effect

- The largest Architecture class drops from 11,229 to at most 1,657 nonblank
  lines; each topic can be timed and reported as its own class (T2a).
- After T2b, nine classes (and the existing `ProjectDependencyTests`) run in
  parallel and the MSBuild batch runs alone afterwards, instead of 231 tests in
  one serial class. The saving is at most the serial sum minus the longest
  class and the batch; E5 gives the real number. The `core` CI shard gains the
  same seconds, because Architecture.Tests runs last in it.
- WS-GOV G2 receives `RepositoryDocumentTests` as the explicit set of generic
  document checks to move into the structure lane: the line ceiling now, the
  roadmap assertions after `Roadmap.cs` leaves the residual class. The WS-TEST
  log lists the other files that read documents as part of topic checks; those
  stay mapped to the `architecture` group.
- What any test asserts does not change.

## After the pilot

- Put the E5 result into the ADR; if the E1, E3 and E7 checks were run by hand,
  decide whether later splits justify turning them into one small script
  (governance class, R2).
- Move the residual files when their blockers clear: the roadmap owner updates
  the two links; nvt-marker and VersionManagement JSON merge; the old roadmap
  branch is confirmed abandoned.
- Next candidates, each scheduled by the commander under rule S7: U2a for the
  mixed UiSmoke classes after U0 and after F08 and the navigation focus work
  merge; the ProfileContract and Application aggregates after the pre-built
  catalog and VersionManagement work merge.
