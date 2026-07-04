# Test Data Policy

This repository normally excludes real firmware and expected output BIN files.

The `testdata/golden/` tree is a narrow exception for owner-approved regression fixtures. Every tracked BIN in that tree must be listed by a manifest with source provenance, file size, and SHA-256 so CI can detect drift.

`testdata/golden/ctrlram-replace/` is a private fixture handoff folder for CtrlRAM Replace byte evidence. It tracks metadata and a manifest template while owner-approved payloads, expected outputs, and firmware-owner sign-off remain outside Git.

Public synthetic fixtures may still be used for behavior tests that do not need firmware parity evidence.
