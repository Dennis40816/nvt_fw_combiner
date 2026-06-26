# Domain Instructions

- Pure deterministic code only; no filesystem, JSON, process, UI, logging framework or mutable global state.
- Use immutable value objects and checked arithmetic for all byte offsets/lengths/ranges.
- Keep composition, initialization and experience variables orthogonal.
- Domain must not know Display/TP HW/TP FW UI labels beyond typed policy values.
- Every invariant needs direct boundary/property tests.
