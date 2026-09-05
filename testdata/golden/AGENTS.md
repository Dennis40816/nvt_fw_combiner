# Golden evidence instructions

- Golden bytes and hashes are independent evidence, never production routing
  authority.
- Add or update payloads only with source provenance, exact sizes and SHA-256
  values, manifest scope, and owner approval. Output Goldens require complete
  output comparison under the case's approved contract; input-only evidence
  validates its declared observations and never invents an expected output.
- Never weaken expected bytes to make a change pass.
- Current parity uses the applicable owner-approved Golden reference. When
  newly certified evidence supersedes it, update the active case references
  under the same approval policy; do not freeze all future work to one old
  release or rewrite the historical runs that used it.
- Every release executes the complete applicable Golden output-case set.
  Report strict equality and approved bounded differences accurately; preserve
  the latter's whole-image comparison and rejection of undeclared changes.
- Fact-scoped aliases prove only the declared fact; they do not replace direct
  product evidence outside that scope.
- Keep private evidence and unapproved firmware outside Git.
