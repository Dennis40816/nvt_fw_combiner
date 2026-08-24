# Application instructions

- Own use-case policy through ports; do not start processes, access files, or
  render UI.
- Consume canonical Domain/profile resolution rather than copy firmware facts.
- Expose typed requests, results, stable issue codes, readiness, and immutable
  snapshots for UI and CLI.
- Keep Merge and Replace on the same composition planner/executor.
- Temporary compatibility adapters require a named replacement and deletion
  criterion.
