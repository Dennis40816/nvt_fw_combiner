---
name: nfc-onboard-new-ic
description: Onboard a new IC, or a new layout or firmware version of an existing IC, from an owner evidence drop; use when the owner delivers IC files or asks to add, extend, or promote an IC.
---

# Onboard A New IC

This skill orders the work. The runbook [`adding-ic-merge-replace-workflow.md`](../../../docs/architecture/adding-ic-merge-replace-workflow.md) owns the file list, and skill `nfc-firmware-profile-authoring` owns the profile facts. An owner data drop is evidence, not support.

1. **Protect the drop first.** Store the original files in the private asset repository and record each SHA-256 and size. The public repository gets only the IC name, program support, address settings the owner approved for publication, and manifest identities. It gets no BIN, flash-map sheet, postbuild script, source file, or customer file name. Do not paste private content into a PR, issue, comment, or CI log.
2. **Inventory by role.** Sort the files into map or header, postbuild flow, source headers, sample images, Golden outputs, and tools. List what the runbook's evidence table still lacks.
3. **Stage, do not promote.** For a public drop, run `scripts/intake_ic_reference.py`. For a confidential drop, use `--dry-run` or point `--output-root` at the private asset repository. The script copies files and writes names, sizes, and hashes, so its output must never land in this repository for confidential files. Ignore its proposed tracked destinations for them. The script changes no profile and no code.
4. **Resolve layouts with the owner.** When the drop holds more than one layout or firmware version, do not pick one. Ask the owner which one is production. Compare the facts with the nearest family. Do not join a family without an official map that proves identical facts and an owner approval.
5. **Check the Combiner.** Run the delivered Combiner and the pinned one on host-created staging copies of every sample, in the test area. Compare complete outputs byte by byte and list each difference range. Matching output is not support evidence, because the legacy Combiner does not check the chip.
6. **Declare a candidate, when the owner decides it.** An owner decision or an ADR must approve an isolated map, family, and bundle first. Mark copied numbers as provisional. Register each route as `Candidate`, `ContractOnly`, with authoring unavailable. Hash-pin every changed fact, and keep the existing IC identities stable.
7. **Collect promotion evidence.** Standard Merge needs a single-chip direct Golden. CtrlRAM Replace needs one Golden for each declared FW contract and each Single or Cascade topology. A missing case is unexecuted, never a pass. Use skill `nfc-golden-regression`.
8. **Write docs, tests, and the PR.** Update `supported-ic-matrix.md` and `ic-workflow-flowcharts.md`. Update the other matrices the runbook names when they apply. Use the runbook's PR notes checklist. For a confidential drop, file names and hashes stay in the private repository, and the PR names only the IC and the evidence kind. Ranges, command order, processor write ranges, and Golden promotion are R3 and need firmware-owner review.

Ask the owner about each risky decision on its own. Group related questions, at most four per round, and continue the independent work meanwhile.
