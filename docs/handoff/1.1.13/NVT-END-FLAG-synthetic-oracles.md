# NVT-END-FLAG-1113-01: synthetic-oracle provenance

Record: `NVT-END-FLAG-1113-01` (R3, design-active); protocol:
[handoff README](../README.md). This log is provenance evidence for final
review finding F-1 and owner decision 48. It changes no record, program,
profile, fixture or owner-certified Golden expectation.

- Base `04369b738`; implementation commits `28e77d7a6` (synthetic TP fixture
  moved to the declared end flag) and `1de0e2928` (the four manifest
  `routeEvidence` expectations below). Evidence in this log was produced at
  `1de0e2928` with a clean tree; the commit adding this log is documentation
  only.
- Owner decision 48 (owner as firmware owner, 2026-09-26; recorded in
  `docs/handoff/1.1.12.md`, commit `affc576ea` on `feature/1.1.13/wave2`):
  the admitted promise that no expected Golden bytes change refers to
  owner-certified Golden expectations. The four synthetic-oracle expectations
  below are rebuilt as an explicit, recorded exception. The admitted
  `terminalContract` stays as committed.

## Why the synthetic expectations change

The old synthetic Standard Merge TP input ended its FWConfig Backup at a
marker at `0x1FFC`, outside the TP overlay `[0xA000, 0x37000)`. Under the
admitted rule an NT51950/NT51951 TP input is read only at the declared end
flag `[0x36FFC, 0x37000)`, so that input is now rejected with
`firmware-config.chip-count-unreadable` before any output exists. The end
flag lies inside the TP overlay, so no admissible TP input can reproduce the
old outputs. The fixture now ends its Backup at the declared end flag; every
other input byte and the oracle are unchanged.

## The four route evidence entries

All four are `kind: synthetic-oracle` with `oracleReference`
`tests/NvtFwCombiner.Bootstrap.Tests/Nt51950Nt51951V2StandardMergeGoldenTests.cs`
and `testReference`
`...#SyntheticInputsMatchIndependentlyConstructedDpPerspectiveOutputAcrossCapacities`.
A synthetic-oracle entry has no `caseId`: it is not an owner-certified case.

| Index | evidenceId | routeId | Capacity |
|---|---|---|---|
| 63 | `nt51950-standard-merge-selector-free-nt51950-standard-merge-1024k-evidence-v3` | `route-7-nt51950-14-standard-merge-13-selector-free-28-nt51950-standard-merge-1024k` | `0x100000` (1 MiB) |
| 65 | `nt51950-standard-merge-selector-free-nt51950-standard-merge-512k-evidence-v3` | `route-7-nt51950-14-standard-merge-13-selector-free-27-nt51950-standard-merge-512k` | `0x80000` (512 KiB) |
| 72 | `nt51951-standard-merge-selector-free-nt51951-standard-merge-1024k-evidence-v3` | `route-7-nt51951-14-standard-merge-13-selector-free-28-nt51951-standard-merge-1024k` | `0x100000` (1 MiB) |
| 73 | `nt51951-standard-merge-selector-free-nt51951-standard-merge-256k-evidence-v3` | `route-7-nt51951-14-standard-merge-13-selector-free-27-nt51951-standard-merge-256k` | `0x40000` (256 KiB) |

Expected outputs (size equals the capacity):

| Index | Size (bytes) | Old `expectedSha256` | New `expectedSha256` |
|---|---|---|---|
| 63 | 1048576 | `4266203f6d5e949dc6633f9dbca69d700d0cda73de0c4894d7438343c638d19a` | `7426263628547e127463f6e408dc869281cac90131dcb5d26ca4889158c9b24f` |
| 65 | 524288 | `51292dd980ad34ed5123b51d59447b99b43c7fcdd4638c8e4705f57e33729f63` | `32d6f68d4796d4253d0ca6f3fab398deda0abf8b2853de0117888635f14c6670` |
| 72 | 1048576 | `4266203f6d5e949dc6633f9dbca69d700d0cda73de0c4894d7438343c638d19a` | `7426263628547e127463f6e408dc869281cac90131dcb5d26ca4889158c9b24f` |
| 73 | 262144 | `983046ff9bb50f89905064429449958bd665b0a9442fdd10f9b8f8c4cd33eee5` | `10047d065c9f0ad0d7450787cce779768d686132a21c199feda13d021bba3ce6` |

The same entries' `capabilityFingerprint` values move with the family
re-pin of the record (63 `2121ebd2...` to `89a5c7d2...`, 65 `f815f5b7...` to
`3d21e6b3...`, 72 `1b7070c9...` to `9700825f...`, 73 `7549c64b...` to
`f14a37bd...`), like every other NT51950/NT51951 route; that re-pin is not
part of this exception.

## Synthetic inputs

Both fixtures come from the test named above; `pattern(length, salt)` is
byte `i = (salt + 37 * i) mod 256`.

| Input | Construction | SHA-256 |
|---|---|---|
| DP, 256 KiB (unchanged) | `pattern(0x40000, 0x31)` | `277c587922b9d5a4a2f0c9c4f5f08029bccbc2678c9c180172aa570e1dfe3c1f` |
| DP, 512 KiB (unchanged) | `pattern(0x80000, 0x31)` | `afbab09500ec4f5518e82c152569382b769436a03ec1db4696804569cee41ea0` |
| DP, 1 MiB (unchanged) | `pattern(0x100000, 0x31)` | `1b6ba4852290a70cb91b203efc34abed10f60b02e77bce5f1fd42bef20ab5775` |
| TP, old | `pattern(0x37000, 0xC7)`; `0x1000=81`, `0x1001=7E`, `0x1017=01`, `[0x1FFC,0x2000)=00 4E 56 54` | `31aa35d33498b666114172ffa9e2976d3e184fe8caefdd16ef16ac36e0de42a2` |
| TP, new | `pattern(0x37000, 0xC7)`; `0x36000=81`, `0x36001=7E`, `0x36017=01`, `[0x36FFC,0x37000)=00 4E 56 54` | `774d86015903c5cf6b1559503553fe31751fcdc721a80da05bf94890174cdd29` |

Both TP inputs are 225280 bytes (`0x37000`). They differ in 14 bytes: the
old Backup bytes at `0x1000`, `0x1001`, `0x1017` and `[0x1FFC, 0x2000)`
return to the pattern (`C7 EC 1A` and `33 58 7D A2`), and the same seven
Backup values move to `0x36000`, `0x36001`, `0x36017` and
`[0x36FFC, 0x37000)`.

## Independent oracle

`ConstructDpPerspectiveOutput(dp, tp)` clones the complete DP input and copies
the TP bytes `[0xA000, 0x37000)` to the same offsets (DP clone plus fixed TP
overlay). It uses no product code or output. The test asserts that the
oracle's SHA-256 equals the pinned literal, then that the Preview output
bytes and SHA-256 equal the oracle, and separately asserts the declared plan
(DP copy, TP overlay, blank initialization of the capacity).

## Complete output difference

Old against new expected output, identical for all three capacities: exactly
seven bytes, all inside the TP overlay `[0xA000, 0x37000)`; every other byte
is equal.

| Offset | Old | New | Meaning |
|---|---|---|---|
| `0x36000` | `C7` | `81` | TP FW version |
| `0x36001` | `EC` | `7E` | TP FW version complement |
| `0x36017` | `1A` | `01` | IC Count |
| `0x36FFC` | `33` | `00` | end flag |
| `0x36FFD` | `58` | `4E` | end flag |
| `0x36FFE` | `7D` | `56` | end flag |
| `0x36FFF` | `A2` | `54` | end flag |

The old values are the TP pattern at those offsets; the old Backup bytes at
`0x1000..0x1FFF` never reached either output, because the overlay starts at
`0xA000`.

## Rebuild

Run the pinned oracle test after the test-area setup in
[CONTRIBUTING.md](../../../CONTRIBUTING.md#fixed-test-area):

```text
dotnet test tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj --filter "FullyQualifiedName~Nt51950Nt51951V2StandardMergeGoldenTests.SyntheticInputsMatchIndependentlyConstructedDpPerspectiveOutputAcrossCapacities"
```

The independent reconstruction below uses the Python 3 standard library only
and reads nothing from the repository. It prints the input and expected
hashes of both fixtures and the byte differences; all values match the tables
above, including the three old pinned hashes.

```python
import hashlib

def pattern(length, salt):
    return bytearray((salt + 37 * i) & 0xFF for i in range(length))

def tp_input(backup_start):
    tp = pattern(0x37000, 0xC7)
    tp[backup_start], tp[backup_start + 1], tp[backup_start + 0x17] = 0x81, 0x7E, 0x01
    tp[backup_start + 0xFFC:backup_start + 0x1000] = b"\x00NVT"
    return bytes(tp)

def oracle(dp, tp):
    out = bytearray(dp)
    out[0xA000:0x37000] = tp[0xA000:0x37000]
    return bytes(out)

sha = lambda data: hashlib.sha256(data).hexdigest()
old_tp, new_tp = tp_input(0x1000), tp_input(0x36000)
print("tp", sha(old_tp), sha(new_tp))
for capacity in (0x40000, 0x80000, 0x100000):
    dp = bytes(pattern(capacity, 0x31))
    old, new = oracle(dp, old_tp), oracle(dp, new_tp)
    changed = [(hex(i), f"{old[i]:02X}", f"{new[i]:02X}") for i in range(capacity) if old[i] != new[i]]
    print(hex(capacity), sha(dp), sha(old), sha(new), changed)
```

## The 11 certified NT51950/NT51951 cases

These are not 11 direct strict-parity cases. The canonical manifest declares
5 `direct-full-output`, 3 `allowed-byte-difference` and 3
`fact-scoped-alias` dispositions. `git diff 04369b738 1de0e2928 --
testdata/golden` touches only `manifest.json`: its `cases` index is unchanged,
and its `routeEvidence` changes are 23 `capabilityFingerprint` and 2
`sourceCapabilityFingerprint` re-pins plus the four synthetic expectations
above. No certified expected artifact, size or SHA-256 changed.

| Case | Disposition | Executed test | Result at `1de0e2928` |
|---|---|---|---|
| `51950-dp-256k` | direct-full-output: complete bytes and SHA-256 | `StandardMergeWorkbenchGoldenTests.WorkbenchBuildStandardMergeMatchesGoldenBytes(caseId: "51950-dp-256k")` (GoldenRegression) | passed |
| `51951-dp-512k` | direct-full-output | `StandardMergeWorkbenchGoldenTests.WorkbenchBuildStandardMergeMatchesGoldenBytes(caseId: "51951-dp-512k")` (GoldenRegression) | passed |
| `nt51950-ab-boe-d82t80` | direct-full-output, with the pinned Combiner | `AbMergeGoldenRegressionTests.Nt51950CandidateMatchesOwnerApprovedAbGoldenWithCombinerAsync(caseId: "nt51950-ab-boe-d82t80")` (Bootstrap) | passed |
| `nt51950-ab-hiway-d82t80` | direct-full-output, with the pinned Combiner | `AbMergeGoldenRegressionTests.Nt51950CandidateMatchesOwnerApprovedAbGoldenWithCombinerAsync(caseId: "nt51950-ab-hiway-d82t80")` (Bootstrap) | passed |
| `nt51950-ab-osd-d03t02-20260924` | direct-full-output through the public CLI host | `AbMergeGoldenRegressionTests.Nt51950OsdPublicHostMatchesOwnerCertifiedGoldenAsync` (Bootstrap) | passed |
| `nt51951-fw200-cascade2-auto-prj-599-20260731` | allowed-byte-difference: complete output within `allowedByteDifferenceContract`, 16 approved CRC bytes | `Nt51950Nt51951DiffDlmMaskCascade2GoldenTests.RegisteredCombinerDiffersOnlyInApprovedCrcWordsAsync` (Bootstrap) | passed |
| `nt51951-fw200-single-auto-prj-695-20260718` | allowed-byte-difference: complete output within `phaseBResult`, 16 approved CRC bytes | `Nt51951CtrlRamFw200EvidenceTests.V2ProducesLockedOutputWithCrcOnlyCombinerVersionDeviationAsync` (Bootstrap) | passed |
| `nt51950-fw200-single-auto-prj-676-20260717` | allowed-byte-difference: complete output within `phaseBResult`, 16 approved CRC bytes | `Nt51950CtrlRamFw200EvidenceTests.V2ProducesLockedOutputWithCrcOnlyOwnerDeviationAsync` (Bootstrap) | passed |
| `nt51951-ab-boe-d82t80-workflow-alias` | fact-scoped-alias of `nt51950-ab-boe-d82t80`; no separate payload | `AbMergeGoldenRegressionTests.EveryAbFactScopedAliasResolvesItsDeclaredDirectCase` (Bootstrap) | passed |
| `nt51951-ab-hiway-d82t80-workflow-alias` | fact-scoped-alias of `nt51950-ab-hiway-d82t80`; no separate payload | `AbMergeGoldenRegressionTests.EveryAbFactScopedAliasResolvesItsDeclaredDirectCase` (Bootstrap) | passed |
| `nt51950-cascade2-geometry-nt51951-auto-prj-599-alias` | fact-scoped-alias of `nt51951-fw200-cascade2-auto-prj-599-20260731`; no separate payload | `tests/scripts/test_ctrlram_canonical_final_intake.py::CtrlRamCanonicalFinalIntakeTests::test_nt51950_geometry_alias_is_explicitly_bound` | passed |

The two AB direct-case tests also assert the NT51951 workflow alias binding of
their case. The Combiner-backed tests run on Windows only; they ran and
passed here, not skipped.

Evidence commands, all at `1de0e2928` with a clean tree and the test area set
(`<test-area-temp>` is a fresh directory under the test area's `temp`):

```text
dotnet build tests/NvtFwCombiner.GoldenRegression.Tests/NvtFwCombiner.GoldenRegression.Tests.csproj
dotnet test tests/NvtFwCombiner.GoldenRegression.Tests/NvtFwCombiner.GoldenRegression.Tests.csproj --no-build
  -> 14 passed, 0 failed, 0 skipped
dotnet build tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj
dotnet test tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj --no-build --filter "FullyQualifiedName~AbMergeGoldenRegressionTests.Nt51950CandidateMatchesOwnerApprovedAbGoldenWithCombinerAsync|FullyQualifiedName~AbMergeGoldenRegressionTests.Nt51950OsdPublicHostMatchesOwnerCertifiedGoldenAsync|FullyQualifiedName~AbMergeGoldenRegressionTests.EveryAbFactScopedAliasResolvesItsDeclaredDirectCase|FullyQualifiedName~Nt51950Nt51951DiffDlmMaskCascade2GoldenTests.RegisteredCombinerDiffersOnlyInApprovedCrcWordsAsync|FullyQualifiedName~Nt51951CtrlRamFw200EvidenceTests.V2ProducesLockedOutputWithCrcOnlyCombinerVersionDeviationAsync|FullyQualifiedName~Nt51950CtrlRamFw200EvidenceTests.V2ProducesLockedOutputWithCrcOnlyOwnerDeviationAsync|FullyQualifiedName~Nt51950Nt51951V2StandardMergeGoldenTests.SyntheticInputsMatchIndependentlyConstructedDpPerspectiveOutputAcrossCapacities"
  -> 13 passed (7 certified-case results and the 6 synthetic-oracle rows), 0 failed, 0 skipped
python -m pytest -p no:cacheprovider --confcutdir=tests/scripts --basetemp=<test-area-temp> tests/scripts/test_ctrlram_canonical_final_intake.py -k geometry_alias
  -> 1 passed, 6 deselected
```
