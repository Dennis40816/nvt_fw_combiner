# Pull Request CI

The executable workflow is [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml). Every external action is pinned to a reviewed full commit SHA.

## Required public checks

1. **`policy / polytail`** validates repository structure, forbidden tracked
   files, schemas, Markdown links, layered AGENTS, the exact skill inventory,
   skill frontmatter and Codex invocation metadata, immutable reference hashes,
   action pins, version/license consistency, dependency direction, and
   canonical architecture fields.
2. **`python-worker / verify`** runs Ruff format/check, Pyright strict, Pylint, pytest, branch coverage, protocol/process tests, plus the structure gate.
3. **`dotnet / build-test`** is the stable final verdict over one complete
   Release-build producer and three closed test shards. The canonical
   verifier validates the exact eight-project/TRX/coverage manifest, reconciles
   every execution with compiled test discovery and the declared platform skips,
   and requires complete GoldenRegression execution, coverage policy, and
   CtrlRAM fixture evidence before the check passes. Test totals and the
   GoldenRegression summary come from the validated execution evidence.

Private firmware golden regression remains an approved-runner gate once private vectors exist. It must publish reports/hashes only, never firmware payloads.

## Security rules

- Default permission is `contents: read`.
- Checkout credentials are not persisted.
- `pull_request_target` is forbidden.
- Pull-request jobs receive no release/signing secrets.
- Action references use full 40-character SHAs; mutable tags are rejected by repository validation.
- The Polytail semantic review remains required in the PR record in addition to deterministic CI checks.
- Producer artifacts are short-lived logs/TRX/coverage only. The finalizer
  rejects missing, failed, duplicate, unknown, wrong-SHA, wrong-SDK,
  path-escaping, symlinked, hash-mismatched, counter-drifted, or extra evidence.
  Producers publish from a clean allowlisted staging root, and the finalizer
  preserves each artifact name/root until ownership and collision checks pass.
  Coverage paths are normalized to verified repository-relative identities
  before hashing so the finalizer never trusts runner roots;
  missing, outside, ambiguous, or normalization-colliding identities fail at
  the producer.
