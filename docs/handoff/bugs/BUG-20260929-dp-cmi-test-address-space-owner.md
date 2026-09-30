# BUG-20260929-dp-cmi-test-address-space-owner: regression assertion used the region instead of its map

Status: fixed
Severity: P3
Found: 2026-09-29, Codex gpt-6-astra, during decision 195 implementation on `feature/1.1.15/nt51950-dp-regions` after `cb321efeb`.
Where: `tests/NvtFwCombiner.Bootstrap.Tests/Nt51950AbDpRegionTests.cs`, new B CMI assertion.
Observed: the first test build failed with CS1061 because `FirmwareRegion` has no `AddressSpaceId` property.
Expected: assert `FirmwareImageMap.AddressSpaceId`, which owns the physical address space.
Evidence: `red-cmi.log` records the compiler error; `red-ranges.trx` then built and reproduced only the two old B CMI ranges; `bootstrap.trx` passes all 217 selected tests.
Owner: Codex gpt-6-astra, `feature/1.1.15/nt51950-dp-regions`.
Resolution: corrected the assertion to `map.AddressSpaceId` in the decision 195 commit containing this record. No production code changed for this test-authoring defect.
