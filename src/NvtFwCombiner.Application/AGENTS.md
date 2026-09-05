# Application instructions

- Own use-case policy through ports; request file/process work through those
  ports, without directly performing adapter I/O or rendering UI.
- Consume canonical Domain/profile resolution rather than copy firmware facts.
- Expose typed requests, results, stable issue codes, readiness, and immutable
  snapshots for UI and CLI.
- Keep Merge and Replace on the same composition planner/executor.
- Temporary compatibility adapters require a named replacement and deletion
  criterion.
