# Golden Evidence Diagnostics

This root is the authoritative classification record for repository-only golden
diagnostics. It is not a canonical golden inventory and is never an expected
output source.

The physical payload migration was frozen after commit `9e15bc0f`. Therefore,
the remaining 2026-07-17 CtrlRAM diagnostic and cross-workflow duplicate files
stay in their hash-pinned legacy quarantine path. `manifest.json` references the
closed inventory without copying or moving those payloads. Owner handoff files
remain in their existing documentation tree and contain no binary payloads.

Canonical regression reads only `testdata/golden/canonical/manifest.json`.
Release packaging reads only the human-gated Standard Merge allowlist. Neither
consumer may read files classified here.
