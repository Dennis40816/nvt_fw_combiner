# NT51950 CtrlRAM Replace Handoff

The owner-approved AUTO_PRJ-676/PID `0x4A06` Common FW 2.0.0 single package is
committed under
`testdata/golden/canonical/NT51950/ctrlram-replace/fw2.0.0/single/nt51950-fw200-single-auto-prj-676-20260717/`.
It contains the Initial DP, TP, final FlashCode, Normal, VN, and the official
short 2,816-byte NF source with original filenames. The final FlashCode is the
canonical Standard Merge result for the supplied DP/TP pair. Its provenance
manifest preserves every former `fixtures/20260717/...` path for audit only;
tests and future verification must read the canonical case.

No additional owner input is requested. Exact Legacy/V2 parity, route closure,
and independent R3 review are agent-owned. NT51950 cascade has no product case
and is excluded from the v0.9.9 release scope; do not create a synthetic folder
or infer cascade support.
