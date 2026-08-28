# Canonical Golden Evidence

This closed inventory stores owner-approved direct firmware goldens by IC, workflow, variant or version, topology, and case. `provenance/case.json` pins every physical artifact by size, SHA-256, source facts, approval, and legacy path.

Fact-scoped aliases contain no copied firmware payload. One direct case may bind one immutable physical input to multiple logical command roles, while different direct cases retain separate case inventories. Diagnostic controls and owner handoff material are classified by `testdata/diagnostics/golden-evidence/manifest.json` and are excluded from canonical regression and release reference payloads. After physical migration was frozen, the record keeps hash-pinned references to the existing repository-only quarantine paths instead of copying payloads.

Every physical `.bin` filename identifies its case IC. Generic names such as `tp_bin.bin`, `dp-input.bin`, `flash.bin`, and `expected-output.bin` are forbidden even when the directory path supplies the missing context. When an owner filename does not already identify the same IC, canonical storage uses the short `<ic>-<artifact-id>.bin` form while preserving the original filename and legacy path in provenance.
