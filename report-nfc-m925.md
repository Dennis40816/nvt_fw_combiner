# NFC m925 local implementation report

State: local uncommitted patch; completion blocked by the sandbox verification environment and remaining derived pins on `feature/1.2.2/nt51925-flash-map`, base `fef89147fc0ee8b8aa67cebc9607bfd12087916e`, target `1.2.2`. Host owns commits, integration and private BIN checks.

## Admission and ownership

Risk: R3 firmware semantics. Authority: m925 owner brief and the owner's postbuild flow. Primary agent is the sole writer of the NT51925 catalog entries, family locators and required identity projections, catalog/display contracts, scoped tests, ADR 0084 and this requested report. No GitHub mutation or publication is authorized.

Owner search: `BuiltInTpFlashMapCatalog.Loader/Selection` owns version slots and diagnostics; `BuiltInCommonFwSelector` is shared by authoring, inspection and discovery (`reuse`). `TpFlashMapProfile` owns catalog ranges and IC-count visibility (`extend-owner`): an explicit declared-range option keeps provisional command plans from replacing authoritative map ranges. `FirmwareNvtEndFlag` projects family locators; `BuiltInFirmwareInspection.ResolveCtrlRamBaseNvtEndFlag` combines the declarations (`reuse`). `CtrlRamInspectionDisplay` carries discovery projections (`extend-owner`). No second selection or version-reading path is added.

Acceptance: two version slots, four layouts, exact visible catalog ranges, end-flag version reads, marker issues, blocked candidates and other IC identities preserved. Local gates: Infrastructure flash-map/selector; Application; whole Bootstrap; ProfileContract; Architecture; build and structure. Host firmware-owner BIN/write-range evidence, independent review, protected checks and exact-source R3 approval remain integration gates.

## Decisions and deviations

- Profiles use the loader's `effectiveCommonFwVersion`; `fromCommonFwVersion` belongs to pending markers.
- TP prefix is the zero-based TP work extent; the declared TP size and separate full-Flash capacity follow the owner's postbuild flow.
- `useDeclaredRegionRanges` prevents unchanged provisional NT51925 command plans from overriding authoritative catalog ranges. Authoritative source groups also require exact declared destinations for every staged block; unconfirmed provisional groups produce no slot. Other IC overlap/override behavior stays unchanged.
- CtrlRAM_S rows use stable `normal-slave` and `mp-slave` IDs, CtrlRAM kind, slave tag and null filenames. No filename is guessed. Single-only VN/NF IDs have a `-single` suffix because duplicate IDs within a profile are forbidden. Cascade IDs remain `vn` and `nf`.
- Header Copy uses the existing `header-backup` Other/backup/postbuild convention and a null filename.
- Confidentiality conflict: the brief forbids addresses in the report and also requests an address table here. Pending clarification, addresses remain only in JSON and tests. Exact independent region tuples are in `BuiltInTpFlashMapCatalogNt51925Tests`; stable IDs are listed below.
- User-level test-root declaration is absent in this sandbox; the inherited fixed `NFC_TEST_AREA_ROOT` and its existing temp child are used after loading the user-level declaration. No new test root is created.

## Evidence scope

| Layout | Verification level |
| --- | --- |
| Common FW 1.x single | The owner's postbuild flow only; no real BIN. |
| Common FW 1.x cascade | The owner's postbuild flow plus two real BINs agree, as relayed by the owner. |
| Common FW 2.0.0 single | The owner's postbuild flow only; no real BIN; DIFF is absent. |
| Common FW 2.0.0 cascade | The owner's postbuild flow plus three real BINs agree, as relayed by the owner. |

Private BIN checks were not executed here. No support, approval or Golden claim is made. Known unknowns: the 1.x single `.data` position, full DIFF use by a 2.0.0 two-chip cascade, and the two unused gaps around the 2.0.0 Header Copy.

| Layout | Stable visible region IDs |
| --- | --- |
| 1.x single | normal, mp, vn-single, nf-single, fw-config-backup, customer-info, dp |
| 1.x cascade (2 or 3) | normal, mp, normal-slave, mp-slave, vn, nf, fw-config-backup, customer-info, dp |
| 2.0.0 single | normal, mp, vn, nf, header-backup, fw-config-backup, customer-info, dp |
| 2.0.0 cascade (2 or 3) | normal, mp, diff, vn, nf, header-backup, fw-config-backup, customer-info, dp |

## Unmodeled rows

FW Header, FW Code/ILM, Overlay ILM, primary FW Config, FW Register and blank Reserved are not catalog replacement/preservation rows; the primary FWConfig start is retained as a profile fact. End flag is a family locator declaration rather than a catalog row. Vec Table and `.data` have no confirmed catalog Replace-write role and are omitted instead of inventing one. Unknown gaps and an unprovided Project ID are not modeled. Header Copy follows the existing catalog backup convention.

## Family identities

Both NT51925 NVT marker-relative search ranges now cover exactly the declared marker. Unique selection, result offset, allowed-result regions, every map's metadata-set references, provisional candidate geometry, blockers and migration inventory stay unchanged. Their allowed-result regions already contain the Backup envelope; no conflicting region changes are required. Only NT51925 family/profile/bundle identities and required downstream pins may move.

## Display issue

`CtrlRamInspectionDisplay.Issue` is optional. Discovery carries the existing slot refusal reason through the shared display selector; a selected map leaves it null. Marker tests use a synthetic catalog. The UI pending-layout path needs coordinated ViewModel state beyond a small binding, so the authorized Application record plus tests boundary is used. No UI/CLI change is claimed.

## Verification and review

No .NET build/test pass or zero-warning result is claimed. The existing fixed test area is outside the sandbox writable scope. `dotnet test` failed before MSBuild could build, with UnauthorizedAccessException creating its temporary directory. `python scripts/verify.py --structure-only` failed with WinError 5 at the test-area boundary. The environment was unchanged, so expensive .NET gates were not repeated. The host's `nfc-lanes/test-m925.ps1` has not been run here.

`python scripts/sync_derived.py --write --only reviewed-source-pins` failed because PyYAML is absent in the configured interpreter. No dependency/package change was attempted. The existing `scripts.release_source_pins.plan_reviewed_source_pins` provider was invoked directly for the authorized trust-index change; it changed only the named digest in `scripts/smoke-release.ps1`, and its second check was idempotent.

`git diff origin/1.2.2 -- profiles/built-in/ctrlram-postbuild-v2/flash-map.json` shows one tail hunk replacing the two NT51925 markers with two NT51925 profiles. Independent raw/parsed comparisons against the base confirm every other IC profile is unchanged. `git diff --check` passed. Stdlib checks of both old and new canonical entry arrays matched the repository's existing PackageTrustIndex/materializer hashing contract; all new entry bytes, family references, bundle hashes and index hashes agree. Every other trust-index entry is unchanged. These checks do not substitute for MSBuild materialization, ProfileContract or Bootstrap execution.

The unchanged code-size policy's `validate_code_size_policy` returned PASS after the final discovery correction, and the final `git diff --check` passed. Its baselines and CI were not edited. `CHANGELOG.md` is unchanged: PR #602 made the preceding candidate catalog change without an entry, and this change makes no supported-route promotion.


The independent read-only gpt-6-astra/high reviewer found a P2 discovery-source mismatch: a provisional staged group could have no matching region and be indexed as if it did. The primary corrected the canonical source producer to require complete exact matches for authoritative maps, and added shipped-plan discovery coverage. Actual .NET reproduction is blocked by the test environment.

Final independent correction review passed static inspection of the uncommitted source: every authoritative staged block requires the same exact filename/range predicate used by source projection, and the shipped-plan tests cover both slots with all supported IC counts. No additional concrete P0-P3 finding was reported. The earlier P2 finding is statically corrected but remains unconfirmed by execution. Overall local Polytail remains FAIL because required tests and downstream pins are incomplete.

Required downstream capability policy fingerprints and the two Bootstrap catalog snapshot digest pin sets have not been re-pinned: their authoritative values must come from the runtime inventory/probe after a build can run. Updating family/bundle identities invalidates these prior pins, so the patch is not ready to commit or integrate. Do not mistake hash consistency for a passing catalog load. No commit, push, merge or publication was performed.

- `nt51925-ctrlram-replace-candidate`: family SHA-256 `a96dec1f0b04a5cf4901860245d64798468f4dc4e282789c44546c9fb486e9a4` -> `17d21801f5ec555c6186fc480a7e4edeb4156e929b278fe3b6fe8dc67bb29e49`; bundle entry-array SHA-256 `441ebb5c3969e5dc5573222bfabcd90ac4a81e4a7fc1464a72c99cf01fa1113f` -> `9ee5920cbc59ef3f2528fab6193838b05aa40aad47d4822743c3257c2d9352ff`.

- `nt51925-standard-merge-candidate`: family SHA-256 `9d4510fb1b62cc4d78ae4a672cec44132dd36a6660044cf86492852cde6b7275` -> `79ed1b031e1272bf3cd50f62a79160063e77910e75a5596b0fdad0ffd2ba86f2`; bundle entry-array SHA-256 `59c8f935946672a52e7e3c539a4bc053da2180c75d73892e739e721f442d272c` -> `9b60c06673134f4c2919bebcc7ff02bb65df2a4169c55d2324f10cfea8cc885b`.

## Tests added or changed

New test methods (Theory cases cover each stated boundary):

- `GetRegions_Common1Single_ReturnsExactOwnerRegions`
- `GetRegions_Common1Cascade_ReturnsExactOwnerRegions`
- `GetRegions_Common2Single_ReturnsExactOwnerRegions`
- `GetRegions_Common2Cascade_ReturnsExactOwnerRegions`
- `TrySelect_Nt51925_DeclaresSeparateTpAndFullFlashShapes`
- `TrySelect_BelowMinimum_RefusesMap`
- `GetRegions_ChipVisibility_ShowsOnlyDeclaredTopology`
- `GetRegions_ProvisionalPostbuildPlan_KeepsAuthoritativeCatalogRanges`
- `CreateDisplay_ShippedProvisionalPlan_ProjectsDeclaredRegions`
- `CreateDisplay_UnconfirmedProvisionalSources_ProducesNoInputSlots`
- `CreateDisplay_MarkerSlot_CarriesMarkerReason`
- `CreateDisplay_MapSlot_LeavesIssueNull`
- `TrySelect_DeclaredNt51925EndFlag_SelectsCommonFwSlot`
- `TrySelect_MarkerOutsideDeclaredEndFlag_RefusesUnreadableVersion`
- `ReadBackup_ExtraMarkerOutsideDeclaredRange_UsesDeclaredEndFlag`
- `ResolveNvtEndFlag_EveryCandidateMap_DeclaresSameMarker`

Existing NT51925 marker expectations now assert the two maps. The shared source-model test cross-checks the NT51925 Primary start. Existing postbuild command parity tests keep their prior exclusion of NT51925 provisional geometry, replacing the marker check with an authoritative-map check; new discovery tests exercise the selected shipped plans. Candidate publication, authoring/session and execution rejection tests remain in `Nt51925RestorationProfileTests`; only its synthetic NT51925 marker/Backup placement changes, preserving the NT51926 synthetic placement. No output Golden expectations were changed. No real BIN was read. Red/green execution was unavailable, so no red/green claim is made.

## Actual commands

| Command | Result |
| --- | --- |
| `git status --short`, `git branch --show-current`, `git rev-parse HEAD` | Requested clean branch/base before edits. |
| `git show HEAD --stat` | PR #602 contains no changelog entry. |
| `git diff origin/1.2.2 -- profiles/built-in/ctrlram-postbuild-v2/flash-map.json` | Only NT51925 tail additions and marker removal. |
| `git diff --check` | PASS. |
| `python scripts/sync_derived.py --write --only reviewed-source-pins` | BLOCKED: missing PyYAML. |
| Direct existing reviewed-source provider invocation and idempotence check | PASS; only the trust-index smoke digest moved. |
| Stdlib old/new entry-array, family/profile, index and other-IC preservation checks | PASS; matches existing hash contract; not a product test. |
| `dotnet test tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj --no-restore -c Release --filter "FullyQualifiedName~FlashMaps\|FullyQualifiedName~BuiltInCommonFwSelectorTests\|FullyQualifiedName~CtrlRamDiscoveryIssueTests" --logger "console;verbosity=minimal"` | BLOCKED before build: fixed external temporary directory denied. |
| `python scripts/verify.py --structure-only` | BLOCKED: WinError 5 at fixed test-area access. |
| Existing `validate_code_size_policy` with the fixed temp preamble | PASS after final discovery correction. |
| Independent reviewer: affected diff/test inspection and `git diff --check` | Static correction review PASS; no additional finding. Required execution and pins remain blocked. |

## Remaining host actions

1. Provide writable access to the existing fixed test area, or execute checks in the host console under its established test-area preamble.
2. Build with `--no-restore`, run the focused regressions, and run the production catalog probe. Re-pin only the nine NT51925 capability definition fingerprints from the inventory diagnostics; preserve their Unavailable/Candidate/ContractOnly decisions. Use the reviewed-source provider to synchronize changed source digests.
3. Capture the production snapshot with the existing CatalogProbe/CanonicalCatalogSnapshotDigest utility and update the complete/section pins in `CanonicalCatalogSnapshotDigestTests` and `PrebuiltProfileCatalogEquivalenceTests`. Confirm all unrelated IC definition identities remain unchanged.
4. Run the host m925 lane, including whole Bootstrap, ProfileContract, Architecture and structure, with zero warnings/errors. Existing golden, review, protected CI and firmware-owner R3 gates remain in force.
5. Compare the stable JSON region IDs and independent exact tuples with the private BINs, then perform the required final firmware-owner/write-range review before committing or integrating.

No commit, push, merge, publication, package dependency change, CI edit, hotspot baseline edit, firmware payload or new bug/plan/handoff/status file was produced.

## Mechanical identity changes

- `profiles/built-in/ctrlram-postbuild-v2/flash-map.json`: raw SHA-256 `1ba6eee0a139266121cda96e95f5e1f3dc1f0d317508a92ea71d9fc1970e67ed` -> `2c880f6786a611654b84d888d7ac6590856a0109dbb184c6b22b14d58d158c01`.
- `profiles/built-in/package-trust-index.json`: raw SHA-256 `f1978b0e03c976f714d2a625171b6c5c8be1a061a39d0dbef51ca3cb893adde7` -> `36e7a3d320fc881436d93c40ca9daeb6b0370ad4ff4d28474ff092d47b4e9c1a`.
- `profiles/built-in/nt51925-ctrlram-replace-candidate/profiles/nt51925-ctrlram-replace-fw141-runtime-cascade.json`: raw SHA-256 `2545ee150bd26d675ed85644aeff37288478f68d4d3cbed6552c29513f5b459e` -> `b14b4fd9dba261ace25bad0e5663a1b08a3433919be9f61f74272054b4f00e36`.
- `profiles/built-in/nt51925-ctrlram-replace-candidate/profiles/nt51925-ctrlram-replace-fw141-runtime-single.json`: raw SHA-256 `710bf1d7e7d031e9a721e287f00604131c459a83ea1361c835b8795de2219e5c` -> `827e1038e7545e86c4f4b2535fbd9aabdefe897764059bc91f4c3fd4d8c268c5`.
- `profiles/built-in/nt51925-ctrlram-replace-candidate/profiles/nt51925-ctrlram-replace-fw200-runtime-cascade.json`: raw SHA-256 `35927b5c1824e5c0d167e7a408e302f35345aea203cf1cdfdbaf5f9cdf640803` -> `471349686106f4910cf0c85b3755e856b83a812967079a46759d3e7d8def5af4`.
- `profiles/built-in/nt51925-ctrlram-replace-candidate/profiles/nt51925-ctrlram-replace-fw200-runtime-single.json`: raw SHA-256 `de8bf3897567cb221a7180ff47ddf5e4ecad5a558c75bf1aae43e38c2fa0474c` -> `c64bd238588a4ba79ed51c03c1bd8bbf0477f96a408b12f7820fdc31cfb6edc6`.
- `profiles/built-in/nt51925-standard-merge-candidate/profiles/nt51925-standard-merge.json`: raw SHA-256 `751ba4a1c0eaac2c9dd4a2672a4dde8e8f5883d4b91581edd72302ee61a5afb1` -> `319eb8486ffc4282014b55248892f14ce2694c1b69cb7816364bfbb2690b9fe9`.

The flash-map raw SHA pin moved in its loader and `BuiltInTpFlashMapCatalogQueryTests`. The trust-index source digest moved only in the existing smoke-release pin. No package payload was generated.
