# Core source consumption

`UiEventRunner.cs` is byte-identical to Core commit
`f07d1568af2336229a488aca9b3e497c2531d7bb`. Its SHA-256 and raw byte length
are pinned in the accompanying `manifest.json`.

`NvtFwCombiner.Presentation.Avalonia` and `NvtFwCombiner.DistributionLauncher`
compile the single linked source with `NVT_CORE_SOURCE_CONSUMPTION`, retaining
its namespace and making the type internal. Neither consumer references the
`Nvt.Core` package.

To update, copy source and manifest together from a newly accepted Core commit,
verify the raw-byte hash, length and LF endings, and rerun the source-consumption,
assembly-scanner, adapter, handler and affected UI behavior tests. Record the new
accepted commit here.

When NFC adopts the Core package, remove both source links, the compile symbol,
this source copy and its manifest together, then switch the adapters to the
package and rerun the behavior tests. Do not compile both implementations.
