# Infrastructure Instructions

- Implement ports for files, staging, processes, serialization, clocks and atomic output.
- Constrain paths to configured roots and fail closed on symlinks/reparse/traversal escapes.
- Python worker staging is disposable, isolated and independently diff-verified.
- Do not place range, persona, merge or replace business rules in adapters.
