# NT51926 CtrlRAM Replace 1.4.1 Cascade

Required payloads:

- `base.bin` (262,144 bytes)
- `inputs/normal.bin` (11,264 bytes)
- `inputs/diff.bin` (10,240 bytes)
- `inputs/mp.bin` (9,216 bytes)
- `inputs/vn.bin` (5,728 bytes)
- `inputs/nf.bin` (11,728 bytes)
- `expected.bin` (262,144 bytes)
- `combiner-command.txt` or an unedited postbuild log
- `tool.json` with Combiner 1.13 identity and SHA-256
- `allowed-diffs.json`
- `owner-approval.md`
- `case.json`

The repository already has a direct base and sliced inputs. If those are the
official originals, the missing essentials are the independent expected
output, command/tool trace, provenance, allowed differences, and owner review.

Do not infer runtime support from the presence of these files.
