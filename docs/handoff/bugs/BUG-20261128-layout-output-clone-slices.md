# BUG-20261128-layout-output-clone-slices: Partial scratch replacement drops remaining cloned input writes

Status: fixed (host build and narrow tests on the committed tree; see the host note)
Severity: P2
Found: 2026-11-28, Codex independent review (gpt-6-astra), during the local layout output contract review over `434920c6e82e095334d430445f344f0aa04d8e00`.
Where: `MemoryLayoutOutputWriteProjection.CopySource`, uncommitted projection snapshot SHA-256 `1F1FC7B9E14F4B734AA760D62A37FE12E2EE23118D6543C495231F237E228D9C`.
Observed: a prior content Copy/Fill overlapping any byte of an input-cloned workspace causes the later entire clone copy to be classified as transport. Unchanged selected-input slices disappear while output facts remain known.
Expected: the requested NFC 1.3.0 output-write contract omits only pure transport. Remaining selected-input slices retain the copying operation identity and matching input ranges; content overlays keep their original operation identities.
Evidence: static review of the all-or-nothing `replaced` predicate; regression and host execution are pending. No build or test pass is claimed.
Owner: Codex primary agent, `feature/1.3.0/layout-outputs`.
Resolution: the local patch partitions remaining clone-source slices through Domain `ByteRange.Subtract` and adds the public-snapshot `PartialInputCloneOverlayRetainsTheOtherSelectedSourceSlices` regression for both copy and fill overlays. Independent scoped re-review by Codex gpt-6-astra accepted the correction with no further findings (static-stage Polytail PASS), at projection SHA-256 `646B7D2BFDD5FF488715418BF5F760FB9A7E0A6CA5234F7A588D664F573BA819` and regression SHA-256 `DFEC4605BA2744D425E37AF145F549C2781B5CE456D86A03226179185A053CB6`. Host build/test execution remains pending; no test pass is claimed.

Host note (2026-11-28, Claude host review on the same patch): the review found three more P2 cases in
`MemoryLayoutOutputWriteProjection`, fixed in the same change.
- Every earlier write into the clone (copy, fill, patch, transform or processor) is subtracted from the
  clone slice, not only content copy/fill. `InputCloneCopyRetainsItsSelectedSourceAndSeparatePatchWrite`
  now expects the patched byte to be excluded from the input slices.
- A clone of a space that is not a bound input makes the set unavailable with `OutputLocationNotResolved`.
  Regression: `CloneOfAnUnboundSpaceMakesTheOutputWritesUnresolved`.
- Scratch bytes overwritten before they are published count as consumed, so the earlier write stays known.
  Regression: `ScratchByteOverwrittenBeforePublishKeepsTheWritesKnown`.
