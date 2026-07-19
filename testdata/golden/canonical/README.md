# Canonical Golden Evidence

This closed inventory stores owner-approved direct firmware goldens by IC, workflow, variant or version, topology, and case. `provenance/case.json` pins every physical artifact by size, SHA-256, source facts, approval, and legacy path.

Fact-scoped aliases contain no copied firmware payload. Diagnostic controls and owner handoff material live under `testdata/diagnostics/golden-evidence` and are excluded from canonical regression and release reference payloads.
