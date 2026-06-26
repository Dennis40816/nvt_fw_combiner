---
name: crc-worker-contract
description: Change or verify the external Python CRC/header worker, pure CRC calculation protocol, staged BIN transform protocol, C# host adapter, processor registry, packaging, or checksum/header test vectors. Do not use for unrelated composition logic or to invent undocumented firmware fields.
---

# CRC / Header Worker Contract

1. Read ADR 0002, `docs/contracts/crc-worker-v1.md`, `docs/contracts/crc-worker-transform-v2-draft.md`, the integrity matrix, and `tools/crc-worker/AGENTS.md`.
2. Classify the change as Protocol 1 calculate or Protocol 2 staged transform. Never silently expand 1.x authority to filesystem mutation.
3. Preserve original input/final output immutability. Transform mode may modify only a host-created staging copy referenced by a plain relative filename.
4. Require an approved `processorId`, contract version, exact read/write ranges, typed parameters, operation order, preconditions, postconditions, and owner evidence.
5. Keep host authority: independently diff before/after bytes and reject any changed byte outside declared ranges, regardless of worker claims.
6. Keep stdout to exactly one JSON response; bound stdin/stdout/stderr/time/files and reject unknown fields.
7. Preserve stable CRC-32/MPEG-2 facts unless a new algorithm id is introduced:
   - polynomial `0x04C11DB7`
   - initial `0xFFFFFFFF`
   - refin/refout false
   - xorout `0x00000000`
8. Add success and negative tests for protocol, traversal, absolute path, symlink/reparse escape, extra file, length change, timeout/crash, incorrect claimed diff, and one-byte out-of-range mutation.
9. Verify `123456789 -> 0x0376E6E7` plus approved IC vectors/golden outputs.
10. Run Python tests, C#/Python contract tests, package smoke, and `$polytail`. State protocol/version/release impact.
11. Do not implement the reserved header transform until the owner supplies exact command and field semantics.
