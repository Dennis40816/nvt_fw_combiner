# Test Instructions

These rules apply to `tests/`; `testdata/` has separate nearest instructions.

- Test names describe behavior and invariant, not implementation method names only.
- Domain range math needs boundary and property coverage.
- When changing a C#/Python protocol or its adapter, retain coverage of success, stable error categories, timeout, oversized output, and request-id correlation. Run affected cases during development; unrelated changes do not require recreating that entire matrix.
- Golden tests compare complete bytes and SHA-256; do not weaken them to partial ranges without an approved rationale.
- Never commit real firmware bytes to normal Git except owner-approved golden fixtures under `testdata/golden/` with manifest paths, sizes, hashes, source provenance, and human approval recorded.
- A bug fix starts with a failing regression test whenever reproducible.
- Reuse an existing regression if it already demonstrates the defect; do not add a duplicate just to perform a red/green ceremony. If reproduction is unavailable, report that limitation and use the strongest relevant check available without claiming the original failure was reproduced.
- Avoid mocks for pure domain code; prefer values and small fakes for ports.
- Tests must be deterministic: inject clock, paths, process runner, and randomness.
- Select local tests by affected behavior under root AGENTS.md. Inspection and prose-only changes do not need new product tests. Every release still executes all applicable certified Golden output cases, including those outside the project named GoldenRegression.
- Include shared consumers when the changed behavior is shared. Prefer deterministic headless checks; drive the actual control or packaged app when the defect depends on bindings, layout, focus, startup, or delivery. Do not replace behavior evidence with source-text assertions or require manual interaction for every case.
- Keep original failing evidence. Investigate isolation, lifetime, shared state, and environment before changing a timeout or seed; a retry alone does not establish a fix. Root AGENTS.md controls rerun and escalation scope.
