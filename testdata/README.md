# Test Data Policy

This repository normally excludes real firmware and expected output BIN files.

The `testdata/golden/` tree is a narrow exception for owner-approved regression fixtures. Every tracked BIN in that tree must be listed by a manifest with source provenance, file size, and SHA-256 so CI can detect drift.

`testdata/golden/ctrlram-replace/` contains manifest-pinned, owner-approved
CtrlRAM Replace evidence. Dated committed intakes include their approved payloads
and expected outputs; remaining parity and review work is repository-owned.

Public synthetic fixtures may still be used for behavior tests that do not need firmware parity evidence.
