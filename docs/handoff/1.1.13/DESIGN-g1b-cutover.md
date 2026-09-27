# G1-B cutover: implementation plan (ADR 0080)

Status: **draft plan, revision 5**, 2026-09-27, after four design reviews by
`codex/gpt-6-astra` (each ACCEPT-WITH-CHANGES; round 4's one P2 is answered in
2.7, closure pending re-review and the implemented controls); revisions 2 to 4
are kept beside this file. Owner decisions 115-127
(section 7) bind it. Not accepted; changes no rule. Drafted by Claude Code
(Opus 5.5) for WS-GOV, read-only.

Evidence: `<worktrees>/g1a` at `17794107df99bc1b076583e0c997ae0e18e0ce1f`;
every `file:line` is at that commit (the worktree has since moved, for example
`scripts/verify.py` and ADR 0079, so lines are re-anchored at the real base).
Branch facts: local refs of 2026-09-27 (`origin/1.1.x` = `c250ed8a5`).
Authority: ADR 0080 items 1-3, 15, 17, 18, owners table, Consequences,
Verification, migration steps 0-6; board decisions 100, 101, 115-127; 1.1.12
WS-AI decisions 1-5 (`feature/1.1.12/agent-docs:docs/handoff/1.1.12/WS-AI.md:49-76`).

## 0. Shape and prerequisites

One branch, one pull request, two reviewable stages: **Stage A**, required for
the retirement (migration step 1), and **Stage B**, WS-AI item 18 and size
item 17 (decision 101). Stage A's head passes its own gates, so its fixed-head
review can start early, but it never authorizes a cutover of A alone; its
guarantees are re-verified at the final head (6.2). Prerequisites (step 2,
item 9): G1-A merged and in force; D4-D6 accepted;
`GOV-AUTHORITY-CHECK-1113-01` sealed on the base. Authority block: R3;
`governance-owner` (`authority-policy.json:123-147`), `release-owner` (step
2), `firmware-owner` (decision 115; section 3 contracts), firmware evidence "no
byte, range, profile or Golden input change; Golden executed by `verify.py
--all` at the head".

## 1. Stage A: validator and tests

### 1.1 `scripts/validate_repository.py`: delete

Every name defined in `:1513-3939` is used outside that block only by the call
at `:3960` (also confirmed by the review's AST cross-search).

| Lines | Code | ADR item 1 term |
| --- | --- | --- |
| `:7`, `:12` | `import io`, `import tempfile` (used only at `:2041`, `:1892`, `:2139`) | import cleanup |
| `:1513-1598` | record, checkpoint, legacy-record and attestation field sets and roots; canonical document paths; auxiliary delivery path | record parsing, checkpoint, legacy records, path coverage |
| `:1599-1603` | `CAPABILITY_REUSE_CLASSIFICATION_CUTOVER`, `CAPABILITY_REUSE_VERSION_CUTOVER` | the two cutover constants |
| `:1604-1672` | finalized fields, reconcilable auxiliary, `CAPABILITY_REUSE_R3_SCRIPTS`, four dataclasses | finalization, history |
| `:1675-1880` | record path classifiers (`_is_capability_reuse_governed_path`, `_capability_reuse_minimum_risk`, two helpers); record Git helpers | record classification |
| `:1883-2225` | `_git_capability_history_inventory` (`git log --reverse`), blob-batch readers, `_capability_path_state_digest`, `_historical_final_records` | history replay, `pathStateDigest` |
| `:2228-2335` | `_GitIndexSnapshot`, `_git_index_snapshot`, `_git_index_blob` | staged-blob admission |
| `:2338-2495` | `_read_commit_path_batch` (`diff-tree --stdin -m`), `_record_changed_in_commits_after` (`rev-list --ancestry-path`), `_is_tree_transparent_containment_merge` | history audit |
| `:2498-2883` | `_load_trusted_capability_checkpoint`, `_validate_external_authority_attestations` | checkpoint, legacy records, attestations |
| `:2886-3244` | `_validate_capability_reuse_record` (reviewer-name comparison `:3000-3004`, `:3023-3031`), checkpoint and auxiliary reconciliation | record validation, reviewer comparison |
| `:3247-3939` | `validate_capability_reuse_governance` (checkpoint derivation `:3538-3560`, path coverage `:3891-3939`) | all of item 1 |
| `:3960` | the call in `validate_agent_files` | caller |

2,431 lines removed; about 1,670 remain before the new code.

### 1.2 What survives

Everything outside `:1513-3941` (its only Git call is `git ls-files -z`,
`:357-371`); `validate_agent_files` (`:3942-3999`) without `:3960`;
`validate_historical_parity_authority` (`:4002-4031`), which reads the frozen
record `RELEASE-111-PARITY-AUTHORITY-TRANSFER-09.json` after the pin check (its
comment `:4005` now credits the pins); `REQUIRED_FILES` (`:113`, `:148`, plus
the pin file). Path classification lives on in the authority policy (G1-A);
the PR base ancestry check is `.github/workflows/ci.yml:28-48`, unchanged, and
the structure job keeps `fetch-depth: 0`.

### 1.3 Other code that reaches the deleted code

`scripts/v0916_parity_certification.py:1196-1211` imports the deleted function
as the default `governance_validator` (`:1305`; callers `:6058`, `:6376`,
`:6471`, `release.yml:420-428`); every test injects one
(`test_v0916_parity_contracts.py:2070`, `:2166`, `:2178`), so the break would be
silent. Decision 115: the default runs `validate_frozen_evidence_pins`; two
tests cover pass and `PARITY_AUTHORITY_MISMATCH`. Nothing else changes.

### 1.4 Tests

| File (tests collected) | Action |
| --- | --- |
| `test_agent_governance.py` (179) | Delete; move `:2811-2815`, `:2817-2881` (PR base check contract), `:2883-2951` (`_validate_windows_only_ci_topology`) to a new `test_ci_structure_contract.py`. |
| `test_authority_policy.py` (9) | Delete `:168-177` (seam test); drop the subset assertion `:180-183`, import `:32`, docstring clause `:3-5`. |
| `test_code_size_policy.py` (19) | Delete `:50-64` (reads a frozen record); Stage B rewrites the file (5.3). |
| `test_skill_inventory_validation.py` (23) | `:361` follows the skill text (section 3). |
| `test_v0916_parity_contracts.py` (46) | Keep; add the two default-hook tests (1.3). |
| New | `test_frozen_evidence_pins.py` (2.5); `governance_topology.py`, `test_governance_topology.py` (2.6); `structure_entry_audit.py`, `test_structure_entry_audit.py` (2.7); `test_governance_retirement.py` (2.8). |

## 2. Frozen evidence pins and regression guards

### 2.1 Pin file

`docs/governance/frozen-evidence-pins.json`, already governance R3
(`authority-policy.json:144`, `test_authority_policy.py:138-150`):

```json
{
  "schemaVersion": 1,
  "authority": "docs/adr/0080-governance-reset.md#evidence-and-admission",
  "frozenAtBase": "<40-hex base commit named in the cutover authorization>",
  "pins": [
    {"path": "docs/governance/change-records", "type": "tree", "id": "<40-hex>"},
    {"path": "docs/governance/external-authority-attestations", "type": "tree", "id": "<40-hex>"},
    {"path": "docs/governance/waivers", "type": "tree", "id": "<40-hex>"},
    {"path": "docs/governance/trusted-initial-capability-checkpoint.v1.json", "type": "blob", "id": "<40-hex>"}
  ]
}
```

The four path/type pairs are a closed validator constant; `frozenAtBase` is
format-checked only. The checkpoint mode is a second constant, `100644` (a blob
ID does not cover mode; a tree ID covers only its children).

### 2.2 The three READMEs

One `README.md` per frozen directory, nothing else: historical, frozen by item
2 at G1-B, evidence only, pinned with the README; how to read records (the
Historical contract), that approvals replace attestations (item 7) and that
waivers are pull request statements (item 15). No counts, SHAs or dates, so a
re-pin never edits them.

### 2.3 Pin check: three layers

`validate_frozen_evidence_pins(root, errors)`, called by `validate()` before
`validate_historical_parity_authority`, proves that `HEAD`, the index and the
working tree each equal the pinned snapshot.

**Git environment for every proof call.** `cwd=root`; `os.environ` without
every `GIT_*` variable (so `GIT_DIR`, `GIT_WORK_TREE`, `GIT_INDEX_FILE`,
`GIT_OBJECT_DIRECTORY`, `GIT_CONFIG_PARAMETERS`, `GIT_CONFIG_COUNT` cannot
redirect or reconfigure it), plus `GIT_NO_REPLACE_OBJECTS=1`,
`GIT_OPTIONAL_LOCKS=0`, `GIT_LITERAL_PATHSPECS=1`; argv begins
`git --no-replace-objects`.

1. **Pin file**: duplicate-key rejection, exact keys, `schemaVersion` 1, the
   closed four pairs in order, each ID 40 lowercase hex.
2. **Repository**: `git rev-parse --show-toplevel --is-inside-work-tree
   --show-object-format`: top level is the same directory as `root`
   (`os.path.samefile`), `true`, `sha1`.
3. **Tree layer**: `git ls-tree -r -t -z --full-tree HEAD`: each pinned
   directory is a `040000 tree` with its pinned ID; the checkpoint is
   `100644 blob` with its pinned ID. Every entry below a pinned directory is
   `100644 blob` (no `120000` symlink, `160000` gitlink or other mode); these
   entries form the expected set E (path, blob ID), authentic because the tree
   IDs match with replacement disabled. No other tree path equals or lies below
   a frozen path after `casefold()`.
4. **Index layer**: `git ls-files -z -s -v` (whole index): entries under the
   frozen paths equal E exactly (case-sensitive path set, mode `100644`, stage
   0, OID) with tag `H`. Rejected: `h`/`s` (assume-unchanged), `S`
   (skip-worktree, sparse), mode `040000` (sparse directory), `160000`,
   `120000`, stage 1-3, an extra path (intent-to-add, staged addition), a
   missing path, a case alias.
5. **Working-tree layer**, in Python, reached only through enumerated
   directory entries (`lstat` or `scandir` on a spelled path succeeds on a
   case-insensitive file system after a case-only rename):
   - *Path components.* From `root`, for each component of each frozen path
     (`docs`, `governance`, then `change-records`,
     `external-authority-attestations`, `waivers`,
     `trusted-initial-capability-checkpoint.v1.json`), list the parent with
     `os.scandir`; require exactly one entry named exactly as the component
     (case-sensitive) and no other entry with an equal `casefold()`. The
     matched `DirEntry` is no symlink (`is_symlink()`) and no Windows reparse
     point (`stat(follow_symlinks=False).st_file_attributes &
     FILE_ATTRIBUTE_REPARSE_POINT`, as `scripts/sync_derived.py:118-127`
     does); directories are directories, the checkpoint a regular file. The
     next step starts from the matched entry's own path.
   - *Frozen directories.* Walk each from its entry with `os.scandir`, links
     not followed: only regular files and directories; the file set, by
     enumerated name, equals E's paths, and no two siblings share a
     `casefold()`; an extra file (untracked or ignored, decision 126), extra
     directory or missing file fails.
   - *Checkpoint.* Checked through its own entry: exact basename, regular
     file, no reparse point, no execute bit on POSIX, hash equal to its pin.
   - *Content.* Each file's raw bytes hash as a Git blob,
     `sha1(b"blob %d\0" % len + bytes)`, to E's ID. Raw bytes, not
     `git hash-object`: `.gitattributes:1,10-11` gives `*.json` and `*.md`
     `text eol=lf`, so a correct checkout holds the blob bytes even with
     `core.autocrlf=true` (the round-2 review found no CRLF in the 478 frozen
     files), and no filter, `ident` or encoding attribute can make different
     bytes pass; a CRLF-only change fails. On POSIX an execute bit fails. Only
     enumerated long names are opened (no 8.3 names or trailing-dot aliases);
     NTFS alternate data streams are not Git content.
6. Any failed or malformed Git reply, non-UTF-8 path or unreadable file fails.

`git status` is not used (it trusts assume-unchanged and skip-worktree bits and
can refresh the index). Cost: three Git processes and about 480 file reads,
independent of history. Not covered (item 2): a change later restored, and
atomicity under concurrent edits.

### 2.4 Pin-diff proof (authorization evidence)

```text
git --no-replace-objects diff --name-status <base> <head> -- \
  docs/governance/change-records docs/governance/external-authority-attestations \
  docs/governance/waivers docs/governance/trusted-initial-capability-checkpoint.v1.json
# exactly three lines: A <dir>/README.md for the three directories
git --no-replace-objects ls-tree <base> docs/governance/trusted-initial-capability-checkpoint.v1.json
git --no-replace-objects ls-tree <head> docs/governance/trusted-initial-capability-checkpoint.v1.json
git --no-replace-objects rev-parse <head>:docs/governance/change-records \
  <head>:docs/governance/external-authority-attestations <head>:docs/governance/waivers
# checkpoint: same mode 100644 and ID at base and head; all IDs equal the pin file
```

Plus the base validator at the base in a detached worktree (old rules, 250-310 s
locally) and `verify.py --structure-only` timed at base and head on one machine.

### 2.5 `test_frozen_evidence_pins.py`

Scratch repositories under the test-area `TEMP`. Positive: clean checkout;
`core.autocrlf=true` checkout (Windows); a frozen file changed and restored in
the next commit. Each negative fails naming the path:

- committed: add, change, delete, rename out of or into a frozen directory;
  record mode change; checkpoint `100644 -> 100755`; checkpoint blob;
- index: staged change; `git add -N`; `--assume-unchanged` then edit;
  `--skip-worktree` then delete; sparse checkout of a frozen directory;
  unmerged stage; gitlink (`--cacheinfo 160000,...`); `120000` entry;
- working tree: unstaged, CRLF-only, untracked and ignored changes; extra empty
  directory; missing file; case-only rename on disk of a record, each frozen
  directory, `docs/governance`, `docs` and the checkpoint basename with HEAD
  and index unchanged; case-alias sibling and `core.ignorecase=true` on
  case-sensitive systems (skips reported); symlink, and on Windows junction,
  for a file, a frozen directory, `docs/governance`, `docs`; execute bit;
- environment: `git replace` of `HEAD` by a commit matching the pins;
  `GIT_INDEX_FILE`, `GIT_DIR` at crafted copies; every proof argv starts with
  `git --no-replace-objects` under `GIT_NO_REPLACE_OBJECTS=1`;
- pin file: duplicate or extra key, missing or retyped path, reordered entries,
  short ID; not a Git work tree; `sha256` object format.

### 2.6 Topology builders

`tests/scripts/governance_topology.py` builds each shape; `seal(task)` makes an
implementation commit `I` with a `design-active` record and its direct child
`E` with the `final-complete` record. Under the head's tests `seal` writes
placeholder records (valid JSON, parsed by `validate_structured_files`,
`validate_repository.py:449`); under the legacy run it uses the base harness
(`test_agent_governance.py:46-54` cutover patch, `:138-186` helpers, trusted
base `C0`). The builder asserts the relations itself.

| Shape | Commits and parents | Expected relations | Before (base validator at final merge) |
| --- | --- | --- | --- |
| (a) 1.1.12 re-run | `C0`; `rel` seals `REL-01` (`I1`, `E1`); `main`: `M1 = merge --no-ff rel` (`C0`, `E1`); `rel` seals `FIX-01` (`I2`, `E2`); final `R5 = merge --no-ff main` on `rel` (`E2`, `M1`); PR base `M1` | `tree(M1) = tree(E1)`; `tree(R5) = tree(E2) != tree(M1)`; `M1` not an ancestor of `E2`; `M1` ancestor of `R5` | `final-complete capability-reuse record changed in commit history: docs/governance/change-records/FIX-01.json` |
| (b) trunk merge-back | `C0`; `rel` from `C0` seals `REL-01`; `M1 = merge --no-ff rel` on `main` (`C0`, `E1`); `trunk` from `C0` seals `TRUNK-01` (`I2`, `E2`); final `T3 = merge --no-ff main` on `trunk` (`E2`, `M1`); PR base `E2` | `tree(T3)` differs from both parents; neither parent an ancestor of the other | the same error for `REL-01.json` and for `TRUNK-01.json` |
| (c) trunk into a working branch after a seal (`beb32b930`) | `C0`; `wave2` from `C0` adds `docs/notes.md` (`W1`); `trunk` seals `SEAL-01` (`I1`, `E1`); final `W2 = merge --no-ff trunk` on `wave2` (`W1`, `E1`); PR base `E1` | `tree(W2)` differs from both parents; `E1` ancestor of `W2` | the same error for `SEAL-01.json` |

- **Before** (run once by the commander in a detached base worktree; the
  40-line driver stays in the WS-GOV log because it imports deleted code): at
  every pre-merge head (`E2` in a; `E1`, `E2` in b; `W1`, `E1` in c) the base
  `validate_capability_reuse_governance` returns no error; at the final merge
  it returns the named error (`validate_repository.py:3512-3514`). Output
  recorded in the pull request and the WS-GOV log.
- **After, unit level**: shape, freeze commit `F` (READMEs, pin file from
  `F`'s tree, a product change), post-freeze replay on both sides (a: `M2`,
  `P`, `R6 = merge main`; b: `F`, `P1`, `M2`, `T4 = merge main`; c: `F`, `P1`,
  `W3`, `W4 = merge trunk`); at the final `HEAD` the pin check passes and
  `git merge-base --is-ancestor <PR base> HEAD` is 0.
- **After, entry level**: the same shapes, built in the 2.7 fixture as
  `topo-a`, `topo-b`, `topo-c`; each final head runs the real entry under the
  harness: exit 0, no violation, calls equal to the multiset.
- **Negative controls**: a frozen file changed after `F` and kept to the final
  `HEAD` fails naming the pinned path and its pinned/actual mode, type and ID;
  the index and physical layers name changed entries. Restored before the final
  head, it passes; the 2.7 controls
  fail on the topology heads too.

### 2.7 Structure-entry audit harness (decision 125)

An isolated test harness observes every process the real structure entry
launches; no production code is added.

**The structure lane, reproduced.** `scripts/verify.py:997-998` runs
`[sys.executable, "scripts/validate_repository.py"]` through `run()`
(`:825-866`): `cwd=ROOT`, inherited environment, no interpreter flags. The
child has `sys.argv == ["scripts/validate_repository.py"]` and `sys.path[0]`
= the checkout's absolute `scripts` directory, which the sibling imports need
(`validate_repository.py:22`); it leaves through `raise SystemExit(main())`
(`:4101-4102`); an extra argument ends in exit 2 (`:4072-4078`).

**Launch**, with `cwd=<checkout>`, the reference run's environment dictionary
and `stdin=DEVNULL`:

```text
<sys.executable> -S <checkout>/tests/scripts/structure_entry_audit.py \
  --expected <expected-calls.json> -- scripts/validate_repository.py
```

`-S`, the only flag and the only intended difference, defers `site` so the
hook precedes all site and audited code; revision 3's `-I` is dropped (it
removes `PYTHONPATH` and the user site, which the lane keeps).

**Why not `runpy`** (round 4). In the installed CPython 3.13.5 (CI also uses
3.13, `ci.yml` setup-python), `runpy.run_path` runs the code inside
`_ModifiedArgv0(path_name)`, which sets `sys.argv[0]` to the path passed for
the whole run (`Lib/runpy.py:49-62`, `:91-102`, `:262-288`); `_run_code` sets
`__loader__ = None`, `__cached__ = None` and `__package__` from `run_name`
(`:71-87`), unlike a script started by the interpreter; and an exception
propagates through the harness and `runpy` frames. Revision 4's bootstrap
therefore could not pass its own control. The bootstrap now executes the
target the way the interpreter executes a script, and relies on one fact: the
harness is itself a script started by the same interpreter, so its own initial
`__main__` namespace was built by the same code path as the lane's.

**Bootstrap**, stdlib only before the hook:

1. Take and remove its own arguments; `target = "scripts/validate_repository.py"`,
   relative, exactly as the lane passes it; `target_abs =
   os.path.abspath(target)`. Before that, as the bootstrap's first statement
   (before any import or definition), record its own initial `__main__`
   attribute names and the values of `__package__`, `__spec__`, `__cached__`
   and the class of `__loader__`.
2. `sys.addaudithook`: each process-launch event (`subprocess.Popen`,
   `_winapi.CreateProcess`, `os.system`, `os.exec`, `os.spawn`,
   `os.posix_spawn`, `os.startfile`, `os.fork`, `os.forkpty`) and
   `ctypes.dlopen` is written at once, after `sys.stderr.flush()`, as one
   whole line `NFC-AUDIT\t<json>\n` by `os.write(2, ...)`, before any check;
   then a violation raises.
3. `import site; site.main()`: the processing of a flagless start (`.pth`,
   `sitecustomize`, `usercustomize`), now observed.
4. Reproduce the launch: `sys.argv = [target]` (never changed again by the
   harness); `sys.path[0]` (the harness's directory) replaced by
   `os.path.dirname(target_abs)`; `os.getcwd()` required to be the checkout.
5. Build the script module: a new `types.ModuleType("__main__")` with the
   attributes recorded in step 1, `__file__ = target_abs`, `__loader__` a new
   instance of the recorded loader class for `("__main__", target_abs)` and
   `__builtins__`; install it as `sys.modules["__main__"]`; compile
   `compile(<file bytes>, target_abs, "exec", dont_inherit=True)`, so tracebacks
   and `__file__` name the absolute path as the lane's do.
6. Run and keep the result:

```python
try:
    exec(code, module.__dict__)   # the only harness frame above the target
    status = 0
except SystemExit as stop:      # the entry leaves this way (:4101-4102)
    status = exit_status(stop)   # None -> 0; int -> int; other -> str to stderr, 1
except BaseException as error:
    tail = drop_leading_harness_frames(error.__traceback__)
    error = error.with_traceback(tail)   # the display reads __traceback__
    sys.excepthook(type(error), error, tail)
    status = 1
finally:
    sys.stdout.flush(); sys.stderr.flush()
    os.write(2, f"NFC-AUDIT-END\t{events}\t{status}\n".encode())
raise SystemExit(status)          # normal shutdown; atexit runs as in the lane
```

`drop_leading_harness_frames` removes traceback entries only from the head and
only while `tb.tb_frame.f_code` is one of the harness's own code objects (a
closed set captured at start by identity, never by file name or text); it
stops at the first other entry and never removes or rewrites anything else.
`exec` is a builtin and adds no frame, so the remaining traceback starts at
the target's `<module>` frame, as in the lane. The same `sys.excepthook` the
interpreter uses for an uncaught exception prints it, including chained
causes, whose tracebacks hold no harness frame. `KeyboardInterrupt` and signal
exits are out of scope.

**Verdict, in the parent.** The exit status equals the sentinel's code; a
missing sentinel, a count mismatch or any `NFC-AUDIT` line after the sentinel
(an `atexit` launch) fails. **Stderr rule** (exact, tested): the harness's
stderr minus every whole line matching `^NFC-AUDIT(-END)?\t[^\n]*\n` must
equal the reference stderr byte for byte; a reference line with that prefix,
or a prefix in mid-line, is an error, not a filter case. `subprocess.Popen`
is allowed only for `git`; on Windows `_winapi.CreateProcess` only for the
exact application, command line and cwd of the `subprocess.Popen` just before
it, recorded by a `_winapi.CreateProcess/arguments` boundary event forwarding
the same native-call tuple. The native `_winapi.CreateProcess` event must
follow that boundary once, with the same application and cwd. On CPython
3.13.5 only, its command field may be a single nonzero control character
(the known audit-format defect); all other runtimes require the exact command
line there too. A missing, duplicate or unrecognized event fails. Any other
launch or `ctypes.dlopen` is a violation, so a child interpreter's launches never need observing, and an
exception caught in the entry cannot remove a written line. After normalizing
`-C <checkout>` and `--no-replace-objects`, the Git calls must equal this
multiset (derived from the code at `17794107d`, confirmed by the first run;
later changes are reviewed edits):

| Git call | Count | Source |
| --- | --- | --- |
| `ls-files -z` | 1 | `validate_repository.py:357-371` |
| `rev-parse --show-toplevel --is-inside-work-tree --show-object-format`; `ls-tree -r -t -z --full-tree HEAD`; `ls-files -z -s -v` | 1 each | pin check (2.3) |
| `log -1 --format=%H B -- docs/contracts/v0916-parity-certification-v1.json` | 1 | `last_change`, `v0916_parity_certification.py:1081-1101` (decision 125) |
| `rev-list --parents -n 1 B` | 1 | `parent`, `:1035-1039` (decision 125) |
| `rev-parse --verify B^{commit}` | 1 | `resolve_commit`, `:1117-1118` |
| `show B:docs/contracts/v0916-parity-certification-v1.json`; `show B:docs/contracts/v100-candidate-source-executor-v1.json`; `show I:docs/contracts/v0916-parity-certification-v1.json` | 1 each | `file_bytes`, `:1130-1140` |
| `diff --name-only --no-renames I B` | 1 | `parent` and `changed_paths` calls, `:1152-1155` |
| `rev-parse C:<p>` for C in {I, B} and each `p` of the source contract's `authorityTrees`; `show C:docs/contracts/canonical-capability-policy-v1.json` for C in {I, B} | 1 each | `tree_for_path`, `file_bytes`, `:1159-1165` |

**Frozen binding.** `B` is the `reviewedHead` of the pinned record
`RELEASE-111-PARITY-AUTHORITY-TRANSFER-09.json` (today `0deb3aad4d58...`); `I`
is `source.implementationHead` of
`docs/contracts/v100-candidate-source-executor-v1.json` at `B`
(`v0916_parity_certification.py:1137-1139`). The parent derives `B`, `I` and
the `authorityTrees` paths outside the audit and writes `expected-calls.json`,
so the decision-125 calls are bound to the actual SHAs, path and count; a walk
by repeating an allowed shape fails. Named failures: `diff-tree`,
`--ancestry-path`, any other `rev-list` or `log` form, a symbolic ref or
range, a SHA outside `{B, I}`, a count above the expected one.

**Tests** (`test_structure_entry_audit.py`) run in an entry fixture, a
shared clone at the tested head (`git clone --shared --no-checkout` into the
test-area `TEMP`, one checkout of about 132 MB) with every canonical file, the
parity binding history and the real validator, nothing mocked; both launches
of every comparison run with `PYTHONPATH` removed.

- **Launch positive control** (rounds 3-4): an untracked probe
  `scripts/_launch_probe.py` imports the sibling `ab_merge_fixture_validation`
  (as `:22` does) and prints `sys.argv` (at module level and again from an
  `atexit` handler), `os.getcwd()`, `sys.path`, `sys.executable`,
  `sys.prefix`, `sys.flags` without `no_site`, and its `__main__` namespace:
  attribute names and `__name__`, `__file__`, `__package__`, `__spec__`,
  `__cached__`, the loader's class and path. It leaves by `SystemExit(7)`,
  `SystemExit("message")`, a `ValueError` raised two calls deep, a
  `ValueError` raised `from KeyError`, or a normal return. The lane form
  `[sys.executable, "scripts/_launch_probe.py"]` and the harness launch must
  give identical stdout, identical stderr under the stderr rule (for the
  exceptions: the same traceback frames, source lines and message), exit
  status 7, 1, 1, 1, 0, and sentinel `NFC-AUDIT-END\t0\t<status>` with
  nothing after it.
- **Real-entry equivalence**: `scripts/validate_repository.py` on the tested
  head, lane form and harness form: exit 0, identical stdout, stderr equal
  under the stderr rule, sentinel code 0, calls equal to the multiset. The
  topology heads of 2.6 run in harness form.
- **Negative controls** (one-file fixture edits; each yields its named
  violation although the edited code catches exceptions): (1)
  `subprocess.run(["git", "rev-list", "HEAD"])` in `validate_code_size_policy`,
  called first by `validate()` (`validate_repository.py:4036`), inside
  `try/except Exception: pass`; (2) `os.system("git rev-list HEAD")`; (3) a
  Python child running `git log --oneline`; (4) call (1) at import time; (5)
  `rev-list --parents -n 1 <parent of B>`; (6) one extra
  `rev-list --parents -n 1 B`; (7) three broken bootstraps, each of which must
  fail the launch positive control: (7a) `runpy.run_path(target_abs)` in place
  of steps 5-6 (round 4: absolute `argv[0]`, `runpy` namespace, extra frames);
  (7b) step 4 without the `sys.path[0]` replacement (round 3: the import of
  `:22` fails); (7c) harness arguments left in `sys.argv` (round 3: exit 2 from
  `:4072-4078`).
- **Units**: `drop_leading_harness_frames` on a harness-rooted traceback,
  on a traceback without harness frames (unchanged) and on one whose harness
  code object appears only below the head (unchanged); `exit_status` for
  `None`, `0`, `7`, `"message"` and a tuple; the stderr rule on a clean
  stream, a stream with audit lines, a mid-line prefix and a reference line
  with the prefix; each multiset row accepted; `rev-list HEAD`, `rev-list
  --ancestry-path a..b`, `log --format=%H HEAD -- x`, `log -1 HEAD`,
  `diff-tree -r a`, a symbolic or foreign SHA rejected; a mock test proves the
  pin check runs before the parity check.

**Limits.** One interpreter lifetime from before `site`: a regression proof for
reviewed validator code, not a sandbox (a native extension calling the OS or
tampering with harness state escapes it). Launch equivalence is proven for the
compared, controlled environment (same interpreter and environment,
`PYTHONPATH` removed, startup customization limited to what `site.main()`
runs); `sys.flags.no_site` stays 1 and is the one excluded field. Cost: one checkout, five full entry
runs, seven early-failing controls; partition per ADR 0079; always run at the
final head (6.2).

**ADR sync.** ADR 0080 Verification says "The validator runs no `rev-list` or
`diff-tree` (today every such call belongs to the record code)"
(`0080:1173-1174`), but the parity calls already run from
`validate_historical_parity_authority`. Stage A replaces it with: "No gate
walks history: no `diff-tree`, no `--ancestry-path`, no unbounded `rev-list`
or `log`; the only `rev-list` and `log` calls are the parity reader's two calls
anchored at the frozen binding head (decision 125); the structure-entry audit
test fails otherwise."

### 2.8 Regression scans

- **Symbols.** A test scans `scripts/**/*.py` and `tests/**/*.py` for
  `capability_reuse`, `CAPABILITY_REUSE`, `_record_changed_in_commits_after`,
  `_read_commit_path_batch`, `_is_tree_transparent_containment_merge`,
  `trusted_initial_base`, excluding only itself; the manual command uses the
  same exclusion: `rg -n -e capability_reuse -e CAPABILITY_REUSE -e
  _record_changed_in_commits_after -e _read_commit_path_batch -e
  _is_tree_transparent_containment_merge -e trusted_initial_base -g
  "!tests/scripts/test_governance_retirement.py" scripts tests` (exit 1
  passes, 0 fails, 2 is a search error).
- **Live instructions.** A test scans every `AGENTS.md`, `CONTRIBUTING.md`,
  the pull request template, `docs/policies/*.md`, `docs/ci/pull-request-ci.md`,
  the governance rule owners (execution workflow, branch governance, skill and
  model routing, issue tracker), `.agents/skills/**`, `.claude/**`,
  `.codex/agents/*.toml`, `docs/contracts/*.md` and every `Proposed` or
  `Accepted` ADR for (case-insensitive) `design-active`, `final-complete`,
  `evidence commit`, `capability-reuse record`, `capability record`,
  `external-authority attestation`, `trusted (initial )?(capability )?checkpoint`,
  `latest (evidence )?checkpoint`, `R[0-3] (capability )?record`,
  `(own|separate|executor) record`, `finaliz\w* .{0,40}record`,
  `change-records/`. Exceptions are the exact (file, sentence) pairs of section
  3 "Found and left", so a new hit in those files fails. Negative fixture: an
  appended "Finalize the design-active record before the evidence commit." and
  a moved exception sentence are both reported.

## 3. Live instructions to replace (Stage A)

| File (map class) | Lines now | Replacement |
| --- | --- | --- |
| `AGENTS.md` (gov R3; 15,519 of 16,384 bytes, `validate_repository.py:3944`) | `:122-133` gate pointer, bounded local R1 path | "Complete owner search and record the semantic owner and disposition (`reuse`, `extend-owner`, `reject-duplicate`) in the pull request admission fields"; keep `:131-133` |
| | `:153-158`; `:164-171`; `:190-192`; `:67-70` | R1-R3 review record on the exact head (item 6), R3 last-push approval per role; R0 path kept, "classifier" means the authority policy, record wording dropped; governance owner added (decision 102); one writer per surface, procedure in the workflow |
| `docs/AGENTS.md:12` (R2) | "owns verification and record applicability" | "owns verification and review applicability" |
| `development-execution-workflow.md` (gov R3) | `:13-16`; `:21-78`; `:82-93`; `:179-189`; `:247-252` | fields in the pull request; new Admission, Single writer and Waivers (item 15, fields moved from `polytail.md:23-26`); no record gate; G1-A bootstrap wording past tense; evidence-checkpoint paragraph deleted |
| `branch-version-and-release-governance.md` (gov R3) | `:53-69`, `:71-88` | items 10-12 (decision 118); PR statement points to the template |
| `docs/policies/polytail.md` (gov R3) | `:17-21`, `:23-26` | "Name the reviewed stage; a local verdict never certifies CI or release readiness"; waivers point to the workflow |
| `docs/ci/pull-request-ci.md` (R1) | `:7-11` | add "frozen evidence pins" |
| `CONTRIBUTING.md` (R2) | `:3-10`, `:18-20`, `:25`, `:30`, `:123-132` | branch model points to branch governance; floor and roles from the policy; PR evidence points to the template |
| `.github/pull_request_template.md` (R2) | lacks item 3 fields | add "Owner search" and "Waiver" |
| `capability-reuse-record.md:3`; ADRs 0054, 0059, 0061, 0070, 0071 `:3` | status | Historical; "Superseded by ADR 0080 (G1-B)" with the former status |
| `implement/SKILL.md:13-15`, `nfc-architecture-change/SKILL.md:16-17`, `polytail/SKILL.md:17-21`, `supervised-branch-development/SKILL.md:42` | gate steps; `capability-reuse disposition` | admission evidence in the pull request; `owner-search disposition` (test `:361`) |
| ADR 0080 `:1173-1174` | Verification bullet | 2.7 "ADR sync" wording |
| `G0-owner-checklist.md:979` (prose) | decision 66 procedure | ended with G1-B |

**Contracts and ADRs.** Firmware-owner authority, exact-head approval and
Golden evidence stay; only the mechanism changes. Contract values
(`pending-reader-record`, `pending-executor-record`) keep their names.

| File (map class) | Now | Exact replacement |
| --- | --- | --- |
| `docs/contracts/v0916-parity-1x-amendment-v1.md:16-23` (firmware, release) | "...the amendment is admitted under its own R3 capability record, separate from the R2 record of the predecessor comparison contract, and that record is finalized only with an exact-head firmware-owner attestation. ... it does not replace the R3 record's finalization contract." | "That is firmware-owner authority: a change to the amendment is an R3 pull request of its own, separate from changes to the predecessor comparison contract, and needs the owner's approval of its last push naming the `firmware-owner` role, with the byte and Golden evidence and the exact write-range audit that role requires (ADR 0080 items 4 and 7). Board decision 64's rule that a release declaration cites the board decision instead of a separate approval is the approval form of release declarations; it does not replace that exact-head firmware-owner approval." |
| same file `:139-140` | "The contract itself is admitted by a separate executor record; until then" | "The contract itself is admitted by its own R3 pull request with the owner's exact-head firmware-owner approval; until then" |
| `docs/contracts/predecessor-comparison-v1.md:184-186` | "It does not replace the exact-head owner attestation that an R3 capability record needs, such as the record of the 1.x amendment." | "It does not replace the owner's exact-head firmware-owner approval that an R3 pull request needs, such as a change to the 1.x amendment (ADR 0080 item 7)." |
| same file `:382-384` | "its rules are admitted by a separate record after the P-0.5 spike, and until then" | "its rules are admitted by their own pull request after the P-0.5 spike, with the review and approvals their paths require, and until then" |
| `docs/contracts/predecessor-comparison-v1.md:290-292` | "together with the second v0.9.16 executor contract, is admitted by a separate executor record; until it is" | "together with the second v0.9.16 executor contract, is admitted by its own R3 pull request with the owner's exact-head approval of its last push naming the `firmware-owner` role, with byte and Golden evidence and the exact write-range audit (ADR 0080 items 4 and 7); until it is" |
| same file `:469`, report-schema row | "revised by the reader record; no report of record" | "revised by the reader pull request, with the review and approvals its paths require; any R3 approval binds the last push and names each required role (ADR 0080 item 7); no report of record" |
| ADR 0078 `:127-130` (Proposed; R2) | "they are admitted by their own R3 record and finalized with an exact-head firmware-owner attestation, which the declaration's citation form does not replace." | "they are admitted by their own R3 pull request with the owner's exact-head firmware-owner approval (ADR 0080 item 7), which the declaration's citation form does not replace." |
| ADR 0078 `:212-215` Open items | "(a separate record after the P-0.5 spike)"; "(a separate executor record after the P-0.5 spike)" | "(a separate pull request after the P-0.5 spike)"; "(a separate executor pull request after the P-0.5 spike)" |
| ADR 0079 `:781`, `:783` (Accepted; R2) | T2a "no capability-reuse record (the validator rejects a record whose only path is a test file)"; T3 "its own record or pull request fields, review" | T2a "pull request fields only"; T3 "pull request admission fields, review record" |

**Found and left** (`git grep` over every `Proposed`/`Accepted` ADR, all
contracts, `docs/specs/`, `SPEC.md`, the canonical architecture documents,
`docs/ci/`, every `AGENTS.md`), the only 2.8 exceptions: ADR 0057 `:282-289`
(terminal v1.0.0 H1-H4 chain; left, as the review asks), plus the exact
terminal sentence pairs below; ADR 0072 `:872`,
`:992` (dated; records 11-13 `final-complete`, section 4); ADR 0077 `:547-550`
(defers to "the governance in force"; B2b is an owner approval under item 7);
ADR 0079 `:780` (T1, sealed); ADR 0021 artifact amendment `:541` (dated); ADR
0080. No action: `sourceReference` entries of the capability policy cite
frozen records; `docs/specs/v1.0.8-update-catalog-v2.md:20` is an owner table;
dated handoff, UI, reference and ledger documents are history
(`docs/AGENTS.md:8`).

The terminal exceptions retain substantive parity execution and verification
records, not governance admission records; their text stays unchanged:

| File | Exact allowed sentence | Reason |
| --- | --- | --- |
| `docs/adr/0057-v0916-black-box-parity-certification.md:320` | "Finalization verifies that external record." | Terminal firmware-owner verification evidence; retain the v1.0.0 certification requirements. |
| `docs/contracts/v0916-parity-certification-v1.md:241` | "The same hash-pinned comparator script prepares and finalizes the canonical invocation record." | Terminal executor invocation evidence; retain the hash-pinned parity certification requirements. |

## 4. Transition inventory (step 3)

Five sources merged by head SHA, each head keeping every source that names it:

| Source | Command | Finds |
| --- | --- | --- |
| Open pull requests, every base | `gh api --paginate "repos/<owner>/<repo>/pulls?state=open&per_page=100"` (not `gh pr list`, which stops at 30) | open pull request heads, forks included |
| Remote branches | `git ls-remote --heads origin` | pushed branches without a pull request |
| Local refs | `git for-each-ref --format="%(refname) %(objectname)" refs/heads/ refs/remotes/` | unpushed branches with or without a worktree; stale tracking refs |
| Worktrees | `git worktree list --porcelain` | detached worktree heads |
| Board | the workstream rows | heads held in another clone, by name and SHA |

Each source's command, time and count is recorded; a head that cannot be
fetched or classified is an **unresolved row** cited by the authorization,
never "nothing owed"; a head in no source (an unreported clone) is a stated
limit; patch-equivalent heads come from `git cherry <BASE> <head>`.
**Classification** at `BASE`: trunk `design-active` and `blocked` records
(`git grep -l '"state": "design-active"' BASE -- docs/governance/change-records`)
and each checkpoint `openR3Authorities` entry against its attestation; per head
`H` and record or attestation path in `git diff --name-only $(git merge-base
BASE H) H`: blob equal to `BASE` = merged; `BASE` final, `H` different = stale
copy, dropped on rebase; `H` final, not final at `BASE` = sealed, not merged;
`H` `design-active` = active; `H` `blocked` = blocked; owed evidence named in
live text ("Found and left") gets a row with its closure. Row fields: head,
provenance, path, states, handling (step 3 table), owed owner or Golden
evidence, carrying pull request or board row; recomputed at every base move and
just before the authorization (6.1).

**Example** at `c250ed8a5` (local refs and worktrees only): 363 records, 112
attestations; 0 `design-active`, 0 `blocked`; the six inherited authorities
carry approving attestations; `GOV-AUTHORITY-CHECK-1113-01` (G1-A) is active
and must be sealed and merged before G1-B's base; stale copies: 28 files on
`feature/1.1.12/review-fixes` and 20 `feature/1.1.13/*` branches; unmerged
commits without records on seven branches; ADR 0072's records 11-13
(`AB-116-*-11`, `-12`, `-13`) are `final-complete`, with attestations for 11
and 13; sealed-unmerged, blocked, unresolved: none.

## 5. Stage B: decision 101

### 5.1 Skills (decisions 120-122)

| Today | Verdict | After |
| --- | --- | --- |
| `assess-refactor-progress` (and its script), `supervised-branch-development`, `grilling` | delete | - |
| `code-review` + `polytail` | merge | `nfc-review` |
| `composition-experience-change`, `firmware-profile-authoring` | rewrite | `nfc-` prefix |
| `golden-regression`, `ui-experience-change`, `release-readiness` | trim | `nfc-` prefix |
| `implement`, `nfc-architecture-change` | updated in Stage A | `nfc-implement`; `nfc-architecture-change` unchanged |
| the other 11 (`diagnosing-bugs` drops `scripts/hitl-loop.template.sh`; `locate-golden-evidence` uses `NFC_TEST_AREA_ROOT`; `grill-with-docs` gets the hybrid style) | keep | `nfc-` prefix |

19 skills (decision 120; ADR 0080 item 18 `:798` records the count). Rename
sites: skill directories, `manifest.json`, `agent-skill-inventory.md`,
`agent-skill-routing.md` (runtime-neutral names), `AGENTS.md:92-94,221-235`,
`.codex/agents/{implementer,reviewer}.toml`, each `agents/openai.yaml`,
`SPEC.md:574`, `src/NvtFwCombiner.Presentation.Avalonia/AGENTS.md:10`,
`agent-model-routing.md:7,54`, `polytail.md:7`, `github-review-polling`
(`SKILL.md:51`, its test `:16-18`), `test_skill_inventory_validation.py`
(`:279`, `:285`, `:317-323`, `:332-379`). Supervised rules go per decision
121; `nfc-grill-with-docs` inlines its interview instead of composing the
deleted `grilling` (`agent-skill-routing.md:41-46`). Root `AGENTS.md` gets
runtime-neutral skill names, the handoff and bug-ledger pointer and the
single-runtime rule; with 865 bytes left under the 16 KiB cap
(`validate_repository.py:3944-3945`), text is replaced, never added.

### 5.2 `.claude/` projections (decisions 1, 127; extends ADR 0068)

Provider `claude-projections` in `sync_derived.py`: inputs are the manifest,
each `.agents/skills/<n>/SKILL.md` and `.codex/agents/*.toml`; outputs
`.claude/skills/<n>/SKILL.md` (same body; `disable-model-invocation: true` for
`explicit`) and `.claude/agents/<n>.md` (no model field; read-only roles
`tools: Read, Grep, Glob`). **First creation**: outputs must be inputs
(`:167`), `_snapshot` raises "missing synchronization input" (`:128-129`) and
writes reuse the existing mode (`:211`). The synchronizer gains **creatable
outputs**: absent ones snapshot as absent (check mode reports drift, exit 1);
write mode creates missing parents only below `.claude/` after the same
reparse checks, mode `0644`; CI-write, convergence and concurrent-edit checks
stay. It never deletes; the validator requires the tracked `.claude/skills/`
and `.claude/agents/` files to equal the expected set. Tests: creation from
empty, idempotent rerun, missing output in check mode, reparse parent, CI
write, stale file. ADR 0068 gets "Amended by ADR 0080 item 18"; `.gitignore`
adds `/.claude/settings.local.json`.

### 5.3 Size policy (item 17)

- **Aggregate**: every C# type in `src/` production sources by qualified name,
  summing the nonblank lines of each file declaring it (partial or not, one
  file or many); this drops the partial-only filter
  (`code_size_policy.py:32-34`) and the single-file exclusion (`:299-300`).
- **Enrollment table** `HOTSPOT_LINES`, generated at the G1-B head; each run
  re-measures. Errors: (1) 2,000 or more lines without an entry: enroll, with
  the owner's approval in that pull request; (2) an entry differing from the
  measurement: growth raises it with that approval, a reduction lowers it
  without (the entry is the measured baseline, never an allowance); (3) an
  entry below 1,500: remove it; a later crossing is case 1 again. Everything
  else: one advisory report. The slice and allowance machinery goes (`:38-59`,
  `:92-116`, `:181-254`, `:353-372`, `:490-531`).
- **Tests** (rewritten file): each error case, re-entry after removal, the
  1,500-2,000 band, single-file, one-file-partial, split and two-type-file
  aggregates, excluded `obj` and generated directories, one advisory block.
- `RepositoryBoundaryTests.StartupDiagnostics.cs:224-230` (ratchet constants)
  is updated; `:119`, `:221` read ADR 0021 text, which stays; the three ADR 0021
  files become "Superseded by ADR 0080 item 17" (decision 123).

## 6. Branch, commits, tests, size, risks

### 6.1 Base and commits

`feature/1.1.13/g1b-cutover`, developed on the G1-A head and rebased onto the
trunk tip after G1-A is in force (the authorized base). Every base move:
rebase (never merge), re-pin if the evidence trees changed, rerun inventory,
pin diff and base validator, renew review, approvals and authorization (step
4); decision 116 reduces record and attestation drift, not base moves.

Stage A: (1) remove record validation (1.1, 1.4; the parity default fails
closed until 2); (2) pins, pin check, parity default, their tests; (3)
topology, entry harness, scans, ADR sync; (4) governance documents (section 3,
first table); (5) contracts and ADRs 0078, 0079; (6) supersessions and the
Historical contract. Stage B: (7) delete three skills, merge `nfc-review`; (8)
pure `nfc-` rename; (9) rewrites, trims, fixes, item 18 count; (10) creatable
outputs and projections; (11) `AGENTS.md`; (12) hotspot rule; (13) re-pin.

### 6.2 Test plan (after test-area setup of `TEMP`, `TMP`, `TMPDIR`)

| After | Narrow gate |
| --- | --- |
| 1-2 | `python -m pytest tests/scripts/test_ci_structure_contract.py tests/scripts/test_authority_policy.py tests/scripts/test_frozen_evidence_pins.py tests/scripts/test_v0916_parity_contracts.py tests/scripts/test_verify_orchestration.py tests/scripts/test_production_source_ownership.py -q`; pin diff (2.4) |
| 3 | `pytest tests/scripts/test_governance_topology.py tests/scripts/test_structure_entry_audit.py tests/scripts/test_governance_retirement.py -q`; `python scripts/verify.py --structure-only` |
| 4-6 | structure-only; `pytest tests/scripts/test_skill_inventory_validation.py tests/scripts/test_authority_check.py tests/scripts/test_governance_retirement.py -q` |
| 7-11 | `pytest tests/scripts/test_skill_inventory_validation.py tests/scripts/test_sync_derived.py tests/scripts/test_github_review_polling.py tests/scripts/test_governance_retirement.py -q`; `python scripts/sync_derived.py`; structure-only |
| 12 | `pytest tests/scripts/test_code_size_policy.py -q`; `dotnet test tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj` |

**Final head** (no Stage A verdict carries over; the fixed-head review lists
these): 16 KiB cap (5.1); 2.4 proofs; the 2.6 before-run repeated if the base moved;
every test of 1.4 including 2.7 on the head and the topology heads; the 4
inventory; structure timing; `python scripts/verify.py --all` once at the exact
head; every required check and `governance / authority` green; the review
record; the pre-merge verification.

### 6.3 Size and risks

Size (counted at `17794107d`, others estimated from G1-A's tests of 400-700
lines and the old topology helpers, `test_agent_governance.py:306-325`): Stage
A removes 2,431 validator and about 2,840 test lines and adds about 200
validator, 1,150-1,500 test (pins 350-450, topology 250-350, harness 300-400,
scans 150-200, moved 141) and 400 document lines; Stage B removes about 1,000
and adds about 1,050 plus about 800 generated.

Risks: (1) base churn repeats review and authorization (6.1); (2) the pin
check is R2 code guarding R3 data (decision 124, procedural); (3) only the
final head is gated, and Stage B lengthens the review and reaches C# (5.3);
(4) single writer: while open, G1-B owns root `AGENTS.md`, the workflow,
branch governance, `docs/policies/`, `.agents/`, `.claude/`, the validator,
`code_size_policy.py`, the parity hook and the section 3 lines, assigned by
the board, with `docs/contracts/` and the parity script shared with the parity
workstream (WS-GOV log `:731-733`); (5) nothing a frozen record links may be
renamed or deleted; (6) the entry harness is slow and not a sandbox (2.7).

## 7. Owner decisions (2026-09-27, board decisions 115-127)

| Question | Decision | Effect |
| --- | --- | --- |
| Parity hook | 115 | Edit the parity script; G1-B declares `firmware-owner` |
| Seal freeze | 116 | From the fixed head sent to final review until merge, no trunk merge adds or changes records or attestations |
| Trunk merges | 117 | After G1-B, ordinary trunk merges into feature branches are allowed; reviewed heads default to rebase; G1-B only rebases |
| Branch governance | 118 | Items 10-12 there; `CONTRIBUTING.md` points to it |
| Inventory | 119 | WS-GOV log and the pull request; cited by the authorization |
| Skill count | 120 | 19, recorded as the WS-AI port's amendment of item 18 |
| Supervised rules | 121 | Disclosure in root `AGENTS.md`, tiers in `agent-model-routing.md`, envelope format in the handoff README |
| Polytail | 122 | Only the skill name retires in G1-B |
| ADR 0021 | 123 | Three files Superseded by item 17; none deleted or renamed |
| Pin-check class | 124 | No authority-policy change; review of any weakening is procedural |
| No-history test | 125 | Forbid `diff-tree`, `--ancestry-path`, unbounded `rev-list`/`log`; allow the two anchored parity calls |
| Ignored files | 126 | Staged, unstaged, untracked and ignored entries under frozen paths fail |
| `.claude/` | 127 | No model field; read-only roles read-only tools; ignore `.claude/settings.local.json` |

## 8. Review response (codex/gpt-6-astra)

Each finding is answered by the normative section named.

| Round | Findings | Answer |
| --- | --- | --- |
| 1 | [P1] working tree not proven; [P2] checkpoint mode | 2.1, 2.3, 2.5 |
| 1 | [P1] live contracts still require records | 3 |
| 1 | [P2] search, audit and topology evidence; [P2, B] fixed hotspot list; notes | 2.6-2.8, 5.3, 4, 0 |
| 2 | [P2] Windows path spelling; hook coverage and frozen binding; topology through the entry; inventory sources; [P3] scan exclusion; notes | 2.3 layer 5, 2.5, 2.6, 2.7, 4, 2.8, 5.3, 6.1 |
| 3 | [P2] harness launch (import path, argv, cwd, exit code, sentinel) | 2.7 "The structure lane, reproduced", "Launch", "Bootstrap", launch positive control, control 7 |
| 3 | note: length | each rule stated once and linked |
| 4 | [P2] `runpy.run_path()` makes `argv[0]` absolute and adds traceback frames | 2.7 "Why not `runpy`", "Bootstrap" steps 5-6, stderr rule, launch positive control, controls 7a-7c, units |
| Stage A implementation clarification 1 (commander, 2026-09-27) | Windows command-line pairing, 2.7 | The running Windows CPython 3.13.5 emits `subprocess.Popen(executable, command_line, cwd, env)` and `_winapi.CreateProcess(application_name, command_line, current_directory)`. Observed native `\x02` / `\x03` are reference-count fragments: CPython v3.13.5 `Modules/_winapi.c` passes a `PyObject*` through audit format `uuu`, whose second slot expects `wchar_t*`. The harness records the actual positional-only native arguments before forwarding the identical tuple, pairs Popen -> boundary -> native once, and checks completion. Only that known runtime accepts the native nonzero control-character fragment; it never supplies argv authority. Every real argv still matches the unchanged decision-125 multiset. This clarifies implementation, not design. |
| Stage A implementation clarification 2 (commander, 2026-09-27) | Pin error detail, 2.3 and 2.6 | HEAD-tree mismatches report the pinned path with pinned and actual mode, type and ID (or `missing`). Index and physical mismatches continue to name the changed file, since those layers compare entries. The three kept-mutation topology cases assert the complete HEAD diagnostic against independently read fixture IDs. No call is added to the three-call proof or the exact decision-125 allowlist. This clarifies implementation, not design. |
| Stage A implementation clarification 3 (commander, 2026-09-27) | Predecessor executor/reader admission, section 3 | Added both replacement rows. Executor admission uses its own R3 pull request and the owner's exact-head last-push approval naming `firmware-owner`, retaining byte/Golden evidence and the exact write-range audit. Reader schema admission uses its own pull request with path-required reviews and role-naming last-push approvals. `pending-executor-record`, `pending-reader-record` and all other contract values stay unchanged. This clarifies implementation, not design. |
| Stage A implementation clarification 4 (commander, 2026-09-27) | Terminal exceptions, 2.8 and section 3 | Added the two exact (file, sentence) pairs listed in section 3 to the retirement scan. They are terminal parity execution/verification evidence, not live governance admission instructions. Both source sentences stay unchanged; moved sentences and new admission instructions still fail. This clarifies implementation, not design. |

Stage A continuation admission: implementation owner `codex/gpt-6-astra`;
base `912ad70e602fa30873202d5eb996fdffdc3dd289`, plus the authorized G1-A
placeholder-data cherry-pick. The four commander clarifications own only this
plan, the structure audit harness and tests, the frozen-pin validator and
its topology assertions, the predecessor contract prose and retirement scan.
Owner search: `LaunchGuard` and `bootstrap` produce/consume the launch proof;
`validate_frozen_evidence_pins` owns HEAD/index/disk checks and is called by
`validate()` and the parity default; `live_findings` owns retirement exceptions.
Disposition: `extend-owner`, no second execution or authority path. No firmware
bytes, ranges, ordering, integrity, profile values or Golden inputs change.
The scope retains the plan's R3 roles and final integration review/approval
gates; this local Stage A continuation runs only section 6.2's Stage A gates.

Stage B implementation admission: `codex/gpt-6-astra`, base `acd88696941f6f7b86ac5ae9a8936ae071a893aa`.
The manifest and skill routing own invocation; `sync_derived.Provider` and
`synchronize` own derived-file custody; `validate_skills` owns inventory;
`code_size_policy` owns source aggregation. Disposition: `extend-owner`.
The plan's Stage B surfaces and necessary live rename consumers are mutable;
no production firmware behavior, inputs or expectations change. Local commits
are authorized; external integration, approval and full verification remain
with the commander.

| Stage B clarification | Resolution |
| --- | --- |
| Section numbers in dispatch | This revision's commit sequence is 6.1, narrow gates 6.2, decisions 7 and review responses 8. The named content governs. |
| Step 7 deletion consumers | Remove `test_assess_refactor_progress.py` with its deleted script; move supervision routing to the existing root, model routing and handoff owners. Update the CRC worker's old review-skill invocation too. Polytail policy, checker and CI names remain. |
| Step 8 live rename consumers | Also update the evidence-reviewer role and agent-issue-tracker invocations discovered by caller search; dated evidence retains historical names. |
| Step 10 projection discovery | The provider discovers names from the selected repository manifest and canonical role files, then binds that inventory to its snapshot. The validator reuses the existing tracked-path read, keeping Stage A's Git-call multiset unchanged. New files use atomic no-clobber publication; existing files retain replacement and mode preservation. |
| Step 12 single advisory owner | Remove the separate 800-line per-file warnings in `polytail_check.py`; `code_size_policy.review_code_size_policy` emits the one advisory block. Its former slice and allowance machinery is removed; source-ownership and worker-coverage helper contracts remain. Qualified type identities include nesting and generic arity; source measurements exclude generated directories/files without changing source-ownership validation. The nine initial entries are measured from unchanged Stage A production sources. |
| Stage B review 1 | Fixed the P2 live SPEC planning route: it now names `nfc-grill-with-docs` and its accepted hybrid interview. Added direct publication-time no-clobber, post-discovery inventory drift and real Windows junction regressions. Review found no firmware or projection-control defect. |
| Stage B review 2 | Fixed the P2 delegate aggregate parser: skip complete tuple/generic return types to locate the declared name and exclude anonymous delegates. Four regressions failed on the reviewed implementation, then all 32 size tests passed; the nine measured hotspot entries remain unchanged. |
| Stage B review 3 | Extend the same delegate fix to `ref readonly` tuple returns (two additional red cases; 36 size cases green), preserving complete return-type parsing before name selection. Final rename scan also updates the interview description and ticket draft directory, with their derived copies. The advisory names enrollment as well as exact-baseline checks. |
