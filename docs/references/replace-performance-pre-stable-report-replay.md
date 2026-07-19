# v0.9.10 Historical Pre-stable Report Replay

Status: Historical same-source evidence only. This is not the stable v0.9.9
predecessor or the authoritative v0.9.10 release comparison.

Return to the [Replace Performance Baseline](replace-performance-baseline.md)
for the stable Node B/C capture and current release gates.

On 2026-07-19, the unchanged initial
`FragmentedReplaceReportPreservesTenThousandDifferenceFacts` harness from
`fbd7ee7c374458ecba84dd9b34493f28ee1adb5a` was applied as a test-only overlay
to two detached worktrees. Node B's production source was the then-current
pre-stable 0.9.9 code milestone,
`270e803e1f043ffd56d8568c7e80c7f771a35d7e`; Node C's production source was
`4a2b6c0a04a8aac302830cabf60ba8080a39a8d8`. Both nodes compiled the same test
without a production adapter or physical fixture/tool path, and used separate
project-local `bin`/`obj` trees.

This capture is retained as historical same-source performance evidence only.
It is not the final stable predecessor and does not describe the current
annotated `v0.9.9` tag. The authoritative stable Node B/C capture is recorded
in the main baseline at `32c37e25` and `6f3698dd`.

The machine/runtime/power settings match the component replay in the main
baseline. Each node ran one exact-filter validation/warm-up sample that was
discarded. Ten recorded B/C pairs then alternated without concurrency; every
`--no-build --no-restore` invocation created a fresh testhost. The p50 is the
median and p95 is the nearest-rank value, which is the observed maximum for ten
samples. These are local comparison values, not portable CI thresholds.

Every recorded sample preserved 10,000 exact one-byte differences, output
SHA-256
`e7b39a736b02c1793f1c22ab4c21e29bc478bd94465614c27bd70c4ac42c25b4`,
`11,720,520` JSON characters, and JSON SHA-256
`16d46159b46bcb3acdd27783321b21504a721f01e4dddef43f10fc336a49c937`.
The 20,000-byte reference and replacement inputs therefore remain the same
deterministic values recorded for the component replay; no expected output was
regenerated.

| Historical report observation | Pre-stable 0.9.9 milestone | Contemporary `v0.9.10` node | Observation |
| --- | ---: | ---: | --- |
| Application run through complete report, p50 / p95 | `118.581 / 145.196 ms` | `88.037 / 96.553 ms` | p50 `25.76%` lower; p95 `33.50%` lower |
| Run-through current-thread allocation, p50 / p95 | `20,181,656 / 20,181,656` bytes | `7,541,912 / 7,591,112` bytes | p50 `62.63%` lower |
| First indented serialization, p50 / p95 | `160.803 / 200.909 ms` | `166.777 / 190.699 ms` | p50 `3.72%` higher; p95 `5.08%` lower; no serialization improvement claim |

The ten run-through samples were B
`127.298, 113.255, 121.888, 145.196, 132.099, 115.889, 109.565, 108.087,
121.273, 108.250` ms and C
`77.594, 88.305, 92.144, 96.553, 87.769, 95.060, 91.600, 80.551, 87.006,
86.659` ms. The ten first-serialization samples were B
`200.909, 147.008, 164.270, 192.806, 186.073, 157.336, 155.165, 154.841,
165.134, 154.059` ms and C
`159.708, 165.109, 176.550, 190.699, 164.368, 168.445, 173.832, 154.325,
156.183, 177.772` ms.

Node B reported `20,181,656` run-through allocated bytes in every recorded
sample. Node C reported `7,541,912` bytes in nine samples and `7,591,112` bytes
in one sample. The exact output/report digests make this valid historical
same-source evidence for upstream report creation. It does not create a stable
`v0.9.9` Hex Diff baseline, because that UI did not exist at the predecessor;
Hex Diff before/after remains the same-source component comparison in the main
baseline. Stable physical Legacy Combiner parity is closed by the authoritative
capture there. Packaged UI timings remain a separate gate.

Commit `64a45efd` also reuses the UTF-8 byte count already produced by a
successful JSON projection when the active report is captured or relocalized
in history. This removes the prior second full-string UTF-8 sizing scan from
the dispatcher after report publication. Older persisted metadata entries and
parse-error reports retain the original fallback scan, and the small artifact
path is still counted. The history schema, JSON payload, warning threshold,
entry order, and reopen behavior are unchanged. This is a source-audited
whole-report scan removal; packaged click-to-history timing remains open.

Commit `7185c0e2` bounds retained best-effort local history by both 12 entries
and a 16 MiB UTF-8 soft budget. The newest report is never dropped; older
entries are evicted from the tail until the budget is met. A single newest
report may exceed the soft budget so active review is not lost. Persisted
history files above 64 MiB are rejected from the opened file before BOM
inspection or JSON deserialization, bounding legacy/corrupt startup input. The
machine-readable run report and local history schema are unchanged. This is a
deterministic retention/I/O bound; packaged startup working set remains open.
