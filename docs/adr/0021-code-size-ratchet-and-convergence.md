# ADR 0021: Code-Size Ratchet and Convergence

- Status: Accepted
- Date: 2026-07-15
- Owners: Architecture owner

## Context

Production C# and XAML grew from 56,257 nonblank lines at `v0.9.2` to 60,237
at the `v0.9.7` merge. Most package bytes are the required self-contained .NET
and Avalonia runtime, while source growth is concentrated in V2 definitions
stacked beside compatibility paths, repeated built-in registration tables, and
large partial service aggregates. Deleting tests, evidence, comments, or
formatting would hide the problem without reducing runtime responsibility.

## Decision

The canonical repository validator owns exact, reproducible ratchets for:

- nonblank `.cs` and `.axaml` lines under `src/`, excluding generated output;
- nonblank lines in byte-identical JSON copies across `profiles/` and
  `docs/contracts/`; and
- the two existing partial aggregates above the general 2,500-line ceiling.

The initial ratchets are 60,237 production lines, 10,781 redundant exact JSON
lines, 6,033 lines for `WorkbenchCompositionService`, and 3,035 lines for
`MainWindowViewModel`. Growth fails validation. A reduction also requires its
ratchet to be lowered in the same commit, so later changes cannot reclaim the
removed budget. New partial aggregates may not exceed 2,500 lines.

This is a convergence control, not permission to delete safety. Tests, golden
vectors, evidence manifests, documentation, firmware-owner gates, and useful
comments are outside the production-source metric. A change must not weaken
validation, byte parity, immutable-input handling, self-contained packaging,
or human review merely to satisfy a number.

`scripts/verify.py` remains the only canonical verification entry point. The
measurement module has no command-line entry point and is invoked by the
existing repository structure validator.

## Consequences

- `v0.9.8` must remove duplicate ownership and unused compatibility code before
  adding production abstractions, and must exit at or below 56,000 production
  lines. `v0.9.9` must exit at or below 54,000 production lines.
- The source ratchet and release-package byte budget are separate. Package
  changes require a reproducible package artifact and release-risk review. The
  owner-approved package maximum is 1% above the 57,501,699-byte baseline.
- Legacy Combiner 1.13 remains an approved constrained external tool. Its
  executable and runner are not code-size retirement targets.
- Non-Combiner legacy paths may be retired only through the Legacy Retirement
  Matrix with equivalent runtime tests and the required R2/R3 evidence.

## Verification

- `python -m unittest discover -s tests/scripts -p test_code_size_policy.py`
- `python scripts/verify.py --structure-only`
- `python scripts/verify.py --all` before the `v0.9.8` handoff
