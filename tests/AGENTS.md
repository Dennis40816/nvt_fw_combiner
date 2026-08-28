# Test Instructions

These rules apply to `tests/`; `testdata/` has separate nearest instructions.

- Test names describe behavior and invariant, not implementation method names only.
- Domain range math needs boundary and property coverage.
- C#/Python protocol tests must validate success, every stable error category, timeout, oversized output, and request-id correlation.
- Golden tests compare complete bytes and SHA-256; do not weaken them to partial ranges without an approved rationale.
- Never commit real firmware bytes to normal Git except owner-approved golden fixtures under `testdata/golden/` with manifest paths, sizes, hashes, source provenance, and human approval recorded.
- A bug fix starts with a failing regression test whenever reproducible.
- Avoid mocks for pure domain code; prefer values and small fakes for ports.
- Tests must be deterministic: inject clock, paths, process runner, and randomness.
