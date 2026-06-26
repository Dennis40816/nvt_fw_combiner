# CRC / Header Worker Instructions

These rules apply to `tools/crc-worker/`.

- The worker is a one-shot versioned JSON process. Protocol 1.x calculates from bytes; Protocol 2.x may transform only a host-created staging `work.bin`.
- Never accept or modify the user's original BIN, final output path, arbitrary absolute path, shell command, executable path, or plugin.
- Transform paths are plain relative filenames under the current isolated staging directory. Reject separators, traversal, drives, UNC, symlinks, junctions, and reparse-point escape.
- `stdout` contains exactly one protocol response. Diagnostics go to bounded `stderr`; no banner or traceback on stdout.
- Runtime is stdlib-only unless an ADR approves a dependency. No network, subprocess, dynamic plugin loading, or directory enumeration outside the staging root.
- CRC functions are pure and deterministic. Protocol parsing, algorithm logic, staged file I/O, and processor dispatch remain separate modules.
- Reject unknown fields, unsupported versions/algorithms/processors, invalid base64, oversized payloads, unexpected files, length changes, and malformed ranges.
- Keep stable error codes synchronized with schemas and C# contract tests.
- Any processor must declare minimum read/write ranges, typed parameters, pre/postconditions, and reference vectors.
- Run format, Ruff, Pyright strict, Pylint, pytest, and the repository `$polytail` review before release.
- Current transform details are reserved; do not invent header fields or command semantics before owner evidence.
