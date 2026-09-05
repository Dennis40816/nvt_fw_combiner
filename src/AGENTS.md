# C# Source Instructions

These rules apply to `src/`.

- Keep dependency direction inward: Presentation/CLI/Bootstrap -> Application -> Domain; Infrastructure implements ports declared inward.
- Domain types must be immutable where practical and use checked arithmetic for addresses and lengths.
- Do not pass raw `int`/`long` for semantically different offsets when a dedicated type improves safety.
- Enable nullable reference types and treat warnings as errors.
- Prefer explicit result/issue models for user-data failures; reserve exceptions for violated invariants or unrecoverable infrastructure failures.
- Do not use service location or static mutable state outside adapters. Read time through an injected clock/TimeProvider; composition code may bind the system implementation. Direct filesystem/process access belongs in adapters, reached through inward-declared ports.
- Use `System.Text.Json` source generation for runtime contracts.
- Keep public API surfaces minimal and document invariants on public domain and port types.
- Tests must compare complete values/reports where practical, not hand-pick only convenient fields.
- UI ViewModels expose state and commands; they do not calculate ranges, patch bytes, or choose firmware semantics.
