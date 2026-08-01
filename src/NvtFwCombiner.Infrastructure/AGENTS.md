# Infrastructure instructions

- Implement filesystem, JSON, profile, staging, process, and report ports;
  never redefine firmware semantics.
- Preserve caller inputs and promote outputs atomically.
- External tools operate only on host-created staging copies with pinned tool
  identity, timeout, bounded output, and host-side changed-range validation.
- Fail closed on malformed data, missing tools, process errors, or undeclared
  writes; return stable Application-facing issues.
- First test: `NvtFwCombiner.Infrastructure.Tests`.
