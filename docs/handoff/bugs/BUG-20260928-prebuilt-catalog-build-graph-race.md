# BUG-20260928-prebuilt-catalog-build-graph-race: generator dependencies race with the host build

Status: fixed locally (independent review pending)
Severity: P1
Found: 2026-09-28, Codex (codex/gpt-6-astra), during the ADR 0077 B2 build-graph fix,
at feature/1.1.13/integration-b@79abc874c.
Where: src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj;
eng/prebuilt-profile-catalog/NvtFwCombiner.PrebuiltProfileCatalog.targets.
Observed: Desktop Release publish with the package flags builds shared dependencies
twice and fails with CS2012 while writing Application's intermediate assembly.
Expected: One normal MSBuild graph owns each shared dependency output; catalog
generation and failure cleanup retain ADR 0077's admission and freshness contract.
Evidence: The lifecycle regression against the baseline production files failed
with CS2012 after 259.00 seconds, without disabling parallel builds. Domain,
Platform and Contracts each appeared twice in the publish build log.
Owner: Codex, B2 implementer, feature/1.1.13/integration-b.
Resolution: Fixed in the commit carrying this entry; all required local gates passed.

Owner search: Bootstrap's build-only ProjectReference performs the generator
build. Its publish-property overrides fork the generator's Infrastructure closure
into different global-property instances that still write the host's obj/bin
paths. The target's separate MSBuild call only requests GetTargetPath; it is not
the build invocation at this baseline. Disposition: extend-owner. Keep the normal
ResolveProjectReferences graph, remove the reference's property overrides, and
consume its exact tool output through OutputItemType. Retain ReferenceOutputAssembly,
PrivateAssets and Private settings that exclude the tool from host outputs.
No runtime admission, build identity, codec, firmware bytes/ranges/order/integrity,
support, project inventory, package lock or package-script contract changes.
This is implementation and self-check evidence, not independent review.

Verification on the fix (Windows, SDK 10.0.303 selected by the existing SDK pin):

- `python -m pytest tests/scripts/test_prebuilt_profile_catalog_build.py
  -k CatalogOutputLifecycleTests -q`, three consecutive runs: 2/2 each, in
  486.02, 480.80 and 341.84 seconds. The added assertions require exactly one
  build of every shared dependency during Debug build and Release publish.
  All existing byte equality, admission, stale-output cleanup, process-failure,
  timeout and tool-exclusion assertions remain unchanged and passed.
- `dotnet test tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj
  --no-restore --filter FullyQualifiedName~PrebuiltProfileCatalog -nologo`: 95 passed,
  zero failed or skipped.
- The same command for `tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj`:
  77 passed, zero failed or skipped.
- `python -m pytest tests/scripts/test_verify_orchestration.py
  tests/scripts/test_release_package_policy.py -q`: 329 passed (250 orchestration,
  79 package policy).
- `python scripts/verify.py --structure-only`: passed; separate
  `python scripts/polytail_check.py`: passed.
- Two additional consecutive Desktop publishes from fresh copies without obj/bin,
  with all package.ps1 publish flags: passed. Each shared dependency built once;
  the tool was absent from publish output. Complete pack bytes matched, SHA-256
  `9892baad30593f5965324c8c89988cf22ebd06f09d201416f6e39b1bd09c6876`.
  Per-project offline locked restores passed. An initial diagnostic additionally
  imposed locked mode on the final Windows RID restore and stopped with NU1004:
  the unchanged Desktop and Presentation locks do not contain that RID projection.
  The successful checks used the existing package.ps1 final RID restore behavior;
  no repository locks or packaging behavior changed.

All build/test shells loaded the user-level NFC_TEST_AREA_ROOT and set TEMP, TMP
and TMPDIR to its existing temp child. Logs are retained there as `b2-race-*.log`.
The build-race regression and the extra publishes did not disable parallel builds.
Independent review and the commander's full verification remain outstanding;
these local checks do not certify integration or release readiness.
