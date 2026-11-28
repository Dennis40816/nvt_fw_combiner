# IC file import: local admission and evidence

## Dispatch and admission

- Outcome: import the owner-delivered structure C sources, formatter and
  structural validation into the repository. No loader binding or support promotion.
- Authority: local Python/data edits only; no commit, push, PR or dotnet execution.
- Base: `c3cc65875`, branch `feature/1.3.0/ic-files-in`, target `1.3.0`.
- Risk: R3 because `profiles/**` retains the output-byte path gate.
- Writer: primary Codex session. No delegated writes.
- Owned paths: `profiles/ic-files/**`, `scripts/ic_files*`,
  `scripts/check_ic_files.py`, `scripts/verify.py`, `tests/scripts/test_ic_files*`,
  `tests/scripts/fixtures/ic-files/**`, and this workstream's evidence/bug records.
- Semantic owner: the delivered structure C schemas and `check_c.py` own
  authoring structure, reference resolution and range validation. The delivered
  `ic_files.py` owns formatting and lock generation. Existing Profiles IC loader
  parts remain unchanged; this task adds no executable firmware path.
- Search evidence: inspected `scripts/verify.py:verify_structure_postchecks`,
  the delivered `check_c.py`, its `c_model.py`/`check_examples.py` dependencies,
  the formatter/config/tests, `profiles/AGENTS.md` and the conversion report.
- Disposition: `reuse` the authored files and formatter; `extend-owner` to make
  the existing structural checker self-contained and register it with the verifier.
- Non-goals: C#, existing profile JSON, firmware bytes, Golden expectations,
  loader integration, hash-policy acceptance, release or publication.
- ADR 0085 is Proposed. Importing an authoring tool does not accept its open
  bundle, runtime identity, override approval or Saved Rule design decisions.
- Narrow gates: formatter pytest; structural checker pytest on the real imported
  tree, including both schema engines and malformed-input rejection.
- Final local gate: `python scripts/verify.py --structure-only`.
- Environment: owner-authorized process-only `.tmp/temp` and explicit pytest
  `--basetemp` under `.tmp`; no persistent environment changes or PYTEST_ADDOPTS.
- Residual integration gates: independent review of the frozen local patch,
  protected CI, exact-head review after any future commit, and firmware-owner
  approval for the final R3 push. No Golden execution is claimed here.

## Checkpoints

### 2026-11-28 import

State: local; uncommitted patch.
Evidence: probe write/delete passed; 65 files / 2,943,736 bytes copied and
compared byte for byte before formatting. A manifest is retained in ignored
local scratch for final source/semantic comparison.
Open: the normal verifier requires an external test-area root, while this
sandbox permits writes only in the worktree. Do not change that guard.
Next: run formatting, port the structural checker and execute the scoped gates.

### 2026-11-28 formatter and clean-clone validation

State: local, verified by the narrow commands below; canonical gate remains blocked.
Evidence:

- First `python scripts/ic_files.py fmt --root profiles/ic-files`: exit 0,
  3.957870 seconds; formatted 60 source documents and created the lock.
- Second command with `--check`: exit 0, 3.763725 seconds, no drift.
- The tree now has 66 files / 2,903,528 bytes. The lock has 60 entries,
  14,445 bytes, SHA-256
  `e6307f7846c58dda0ccfbaa55006173d879fc410447e1c4e3f417e0f15b4f271`.
- The original conversion reader independently compared all 60 source documents
  after formatting: values and list order are unchanged after hexadecimal
  spelling normalization. All five schema files remain byte-identical.
- Initial imported formatter module: 81 passed. Added real-tree lock/check
  coverage and structural/clone/registration tests: combined 90 passed before
  the final nested YAML coverage; final structural module: 10 passed.
- Ruff passed for the imported/new Python tool and test modules.

The port is `scripts/check_ic_files.py`, based on the delivered `check_c.py`.
It retains schemas, semantic IDs, historical alias restrictions, typed tool
parameters, all reference/range/metadata/variant checks, exact 94 canonical
routes and 38 expected identities, ten General Merge baseline comparisons,
and all 22 original negative controls. It reads both `.yaml` and `.yml`
recursively, matching the formatter's source inventory. The formatter's
restricted YAML reader is reused instead of adding another parser.

Only the needed helper functions were carried from `c_model.py`,
`check_examples.py` and `authoring.py`; no `tools-src`, generator or drill tree
was copied. The unchanged route selection inventory is carried as
`scripts/ic_files_routes.json` to retain the route/selection assertions.
`scripts/ic_files_structure_refs.json` carries 858 immutable historical source
IDs, the 38 expected route IDs, existing General Merge profile paths and one
unchanged negative-control context. These inventories are necessary for the
original checks and contain no outside paths.

Conversion-only checks were deliberately not admitted as ongoing validation:
`source_hashes` (historical A source/provenance and external planning-authority
hashes, including the cause of `Authority source drift`) and the full A/C
`equivalence` comparison. Their immutable inputs are not repository sources.
The source report remains dated conversion evidence. No schema, reference,
range, baseline assertion or negative control was removed. No firmware parity
or production support is inferred from this import.

`verify_structure_postchecks` now runs read-only fmt checking and the checker
with its normal and forced-fallback engines. No workflow changed. The exact
`python scripts/verify.py --structure-only` entry point exits 1 before checks:
see `BUG-20261128-ic-files-in-verifier-environment`. Its external test-area
guard remains unchanged. PyYAML is absent and the sandbox blocks installation;
Polytail and the existing sentinel dry run pass independently.

The pre-existing authority-policy test fixes the structure subprocess inventory
at four scripts. A read-only dry run confirms exactly the two new paths are
added, with no old step removed. The existing test is untouched under the
owner's no-expected-value-change instruction; the scoped clarification is
pending. See `BUG-20261128-ic-files-in-structure-step-expectation`.

## Disposition of the six source fmt bug records

The source worktree is read-only. None of its six records is copied as an open
issue: the four product fixes were already in the imported tool and are now
covered by the passing final tests; the other two are resolved for this task
as described below. This table records their updated disposition without
deleting or rewriting the source records.

| Source bug suffix (`BUG-20261128-ic-fmt-`) | Status here | Fix/evidence |
| --- | --- | --- |
| `diagnostic-boundary` | fixed | Invalid escape/float/surrogate inputs return located errors without writes; regression tests pass. |
| `optional-identities` | fixed | Required identity namespaces are explicit; empty optional invocation arrays pass. |
| `recovery-backups` | fixed | Fault-injection tests preserve original recovery bytes and report incomplete rollback. |
| `unicode-idempotence` | fixed | Unicode separators stay escaped; second fmt and check pass. |
| `draft-authority-drift` | fixed for the repository checker | External historical provenance/authority reads are absent; isolated clone passes. |
| `test-temp-fallback` | fixed for this task | Explicit owner-authorized `.tmp/temp` and `--basetemp` permit tests; no implicit fallback. The separate canonical verifier environment blocker remains open above. |

### 2026-11-28 independent frozen-patch review

State: locally reviewed; not integrated or published. No commit.
Reviewer: separate fresh Codex reviewer session `/root/ic_files_review`, without
the author's conversation; inherited model identifier not exposed, role effort
`high`. Author: primary Codex session `/root`; configured model identifier not
exposed. No model is inferred from a role or prose.

The frozen local scope is the base/HEAD
`c3cc65875552ffa6b869b8f92e14f47722bedab0` plus the 85 Python/data/test
paths and SHA-256 values in the ignored review manifest. The reviewer
recomputed all 85 hashes at completion with no drift. This is local patch
review evidence, not a future PR's exact-head record.

The independent reviewer found no new P0-P3 defect. Scoped Polytail verdict:
`PASS-WITH-HUMAN-GATE` for the local reviewed patch only. Byte, range, order,
integrity and support semantics are unchanged; no C# loader, executor,
existing profile JSON, firmware payload or existing test expectation changed.
The admitted authoring checker remains the semantic owner reused from the
conversion tool; the formatter remains byte-identical to its delivered source.

Final independent commands, with process-only `.tmp/temp` and no
`PYTEST_ADDOPTS`:

- `python -B -m pytest tests/scripts/test_ic_files_fmt.py tests/scripts/test_ic_files_structure.py --basetemp=.tmp/reviewer-pytest -q -p no:cacheprovider`:
  92 passed in 62.48 seconds.
- `python -B scripts/check_ic_files.py --root profiles/ic-files`: exit 0,
  jsonschema engine; 60 documents, 175 pairs, 94 canonical routes, 38 expected
  identities, all 22 original negative controls.
- The same checker with `--force-fallback`: exit 0 with the same structural
  assertions and controls.
- `python -B scripts/ic_files.py fmt --root profiles/ic-files --check`:
  exit 0, no drift.
- `git diff --check`: exit 0. Primary privacy scan: 88 new/changed files,
  zero drive/account/machine-path matches.

Residual gates remain the two open bugs above, the host's canonical verifier
rerun, future exact-head independent review/protected CI, and firmware-owner
approval of the final R3 push. No Golden execution, integration readiness,
commit, push, merge or publication is claimed.

The requested report destination outside the worktree rejected the write with
`PermissionError: [Errno 13] Permission denied`. The complete Traditional Chinese
report (under 400 words) is retained in ignored scratch as
`.tmp/report-nfc-ic-files-in.md`; the host must transfer it to the requested
destination. No sandbox bypass or permission/configuration change was attempted.

## Host integration note (2026-11-28 11:1x, PC-NFC)

- The `verify.py` structure registration was removed. CI enforces the IC files through the repository-script pytest shards instead. This keeps `tests/scripts/test_authority_policy.py` (governance-owner) unchanged.
- Host evidence in the external test area: `test_ic_files_fmt.py`, `test_ic_files_structure.py` and `test_authority_policy.py` passed (105 tests). `python scripts/verify.py --structure-only` passed.
- Both bug records of this workstream are now fixed.
