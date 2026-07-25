# Domain instructions

- Remain pure: no filesystem, process, UI, Avalonia, JSON adapter, or
  Infrastructure references.
- Use checked half-open ranges with named address spaces.
- Domain types own invariants; callers must not reproduce validation.
- Prefer one deep interface over workflow-specific wrappers.
- Keep values deterministic, immutable where practical, and independent of
  filenames, environment, and current time.
- First test: `NvtFwCombiner.Domain.Tests`.
