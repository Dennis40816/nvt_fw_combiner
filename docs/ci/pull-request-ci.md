# Pull Request CI

The executable workflow is [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml). Every external action is pinned to a reviewed full commit SHA.

## Required public checks

1. **`policy / polytail`** validates repository structure, forbidden tracked files, schemas, Markdown links, layered AGENTS, skill frontmatter, immutable reference hashes, action pins, version/license consistency, dependency direction, and canonical architecture fields.
2. **`python-worker / verify`** runs Ruff format/check, Pyright strict, Pylint, pytest, branch coverage, protocol/process tests, plus the structure gate.
3. **`dotnet / build-test`** installs the SDK pinned by `global.json` through the repository installer, restores, checks formatting, builds Release with warnings as errors, and runs the public .NET tests.

Accepted firmware golden replay artifacts are tracked under `testdata/golden/`
after owner approval, manifest/hash anchoring, and personal-information review.
CI may execute those tracked fixtures but must not publish firmware payloads as
logs or artifacts. Unreviewed intake evidence remains excluded from CI.

## Security rules

- Default permission is `contents: read`.
- Checkout credentials are not persisted.
- `pull_request_target` is forbidden.
- Pull-request jobs receive no release/signing secrets.
- Action references use full 40-character SHAs; mutable tags are rejected by repository validation.
- The Polytail semantic review remains required in the PR record in addition to deterministic CI checks.
