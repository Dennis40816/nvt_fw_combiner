# Compiled profile instructions

- Compile only canonical declarations from `profiles/`; do not invent fallback
  ranges, family links, topology/count variants, processors, or support.
- Keep map selection independent of informational PID, filename, and golden
  identity.
- Reject unknown integrity, overlap, invalid bounds, and ambiguous processor
  authority.
- Preserve one CompiledComposition execution artifact and deterministic plan
  identity.
