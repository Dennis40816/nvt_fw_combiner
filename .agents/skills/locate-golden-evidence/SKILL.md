---
name: locate-golden-evidence
description: Locate an NVT FW Combiner canonical Golden case, its input/expected files, and exact evidence disposition; use before claiming byte parity.
---

# Locate Golden Evidence

The canonical inventory is `testdata/golden/canonical/manifest.json`; physical cases live under `testdata/golden/canonical/<IC>/<workflow>/<variant>/<topology>/<case-id>/`. Read the case's `provenance/case.json` first. Its `artifacts` array identifies `inputs/` and `expected/` files and SHA-256 values. `cmd /c scripts\open-golden-example.cmd --list` gives a concise case index.

Report `testDisposition.kind` accurately: `direct-full-output` carries a complete expected image; `allowed-byte-difference` applies only its declared bounds; `input-only-evidence` has no independently approved complete output. Aliases do not become direct Goldens. The local `D:\NvtFwCombiner-TestArea\evidence` tree contains run evidence and candidates, separate from the canonical Git fixtures. Follow `$golden-regression` for adding, changing, or promoting expected bytes.

For NT51929 AB CtrlRAM A-only/B-only/Both, set the required test-area environment, ensure dependencies have been restored through the repository's normal bootstrap, and run `python tests/scripts/nt51929_ab_ctrlram_oracle.py --fresh`. The recovered Combiner 1.13 `Combiner.c` at the path pinned by the script must be available locally. The command builds without restoring packages, executes all three real-input cases into a new private evidence directory, and compares every output byte with an independent exact-case CRC/header oracle. The resulting `oracle-report.json` binds the run ID and loaded assembly hashes; `--inspect-existing <evidence-directory>` only checks saved files and does not certify the current build. These candidates still await firmware-owner Golden certification.
