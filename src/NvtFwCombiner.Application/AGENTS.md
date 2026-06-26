# Application Instructions

- Orchestrate use cases through ports declared here; do not instantiate infrastructure adapters.
- Compile all experiences into one plan/executor model.
- Return structured issue/result types for user/profile failures.
- Preview and Build must share semantics; Build adds output commitment only.
- Never access UI state, environment paths or arbitrary worker commands directly.
