# CtrlRAM Legacy Diagnostic Quarantine

This directory is not an executable golden source. All active CtrlRAM direct,
input-only, alias, and supporting derived regression evidence is owned by
`../canonical/` and validated by `scripts/canonical_golden_validation.py`.

The only retained payload tree is `fixtures/20260717`, indexed by
`manifest.20260717.json`. It contains diagnostic and cross-workflow duplicate
artifacts whose physical relocation is frozen. Its authoritative classification
lives in `../../diagnostics/golden-evidence/manifest.json`; canonical test
runners and release packaging must not consume it as expected evidence.

The former active `manifest.json`, manifest template, `fixtures/20260705`, and
`fixtures/derived` authorities are retired and must stay absent. Canonical case
metadata preserves applicable pre-migration artifact paths in `legacyPaths`
and legacy manifest provenance in `legacyManifest`; this is not a claim that
every retired path has a one-for-one canonical artifact entry.
Unapproved local payloads remain ignored under `private/` and must never be
promoted without the repository golden policy and explicit owner approval.
