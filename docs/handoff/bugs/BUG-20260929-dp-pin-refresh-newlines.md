# BUG-20260929-dp-pin-refresh-newlines: scratch pin refresh wrote Windows newlines

Status: fixed
Severity: P3
Found: 2026-09-29, Codex gpt-6-astra, during decision 195 implementation on `feature/1.1.15/nt51950-dp-regions` after `cb321efeb`.
Where: uncommitted scratch refresh of AB family bindings, bundle pins and C# test pins.
Observed: Python's default text writer emitted CRLF into LF-governed files, producing temporary entry hashes that would differ from committed bytes; a C# build also reported IDE0055 in the rewritten Golden test.
Expected: `.gitattributes` and `.editorconfig` require LF for JSON/C#; hash the same bytes that enter Git.
Evidence: the intermediate `derive-bundle-source.trx` differs from `derive-bundle-lf.trx`; the final `hash.trx` passes both bundle checks, and `bootstrap.trx` builds cleanly and passes 217 tests.
Owner: Codex gpt-6-astra, `feature/1.1.15/nt51950-dp-regions`.
Resolution: normalized owned JSON/C# files to LF, made scratch writes explicit with `newline='\n'`, and regenerated every dependent identity through the existing producers. Intermediate hashes were never committed; no hash algorithm or expected firmware output changed.
