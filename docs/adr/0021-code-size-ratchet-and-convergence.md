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

### v0.9.10 owner amendment

Owner decision, 2026-07-19: `v0.9.10` uses 60,000 nonblank production C#/AXAML
lines as a hard ceiling rather than an exact descending total-source ratchet.
Measured reductions below 60,000 do not require lowering that ceiling during
the performance release. The stable-reconciled candidate at `6f3698dd` measures
59,429 production lines. Its named partial aggregates are frozen as exact
ratchets at 4,405 lines for `WorkbenchCompositionService` and 4,069 lines for
`MainWindowViewModel`; the exact duplicate-JSON ratchet is 1,055 lines, and the
general 2,500-line partial ceiling also remains in force.

The exception is time-bounded. `v0.9.10` added measured progress,
accessibility, report/Hex Diff, cancellation, persistence, inspection, and
performance evidence while preserving firmware semantics. A dedicated
code-size convergence phase starts only after `v0.9.11`; it will establish a
new measured baseline and lower descending ratchets instead of treating unused
space below 60,000 as a permanent budget. The final reviewed `v0.9.10` tree must
remain at or below 60,000 and must not exceed either named partial ratchet.

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
  adding production abstractions. On 2026-07-16 the owner accepted its exact
  56,742-line final ratchet, superseding the original 56,000-line stretch gate.
  `v0.9.9` must still exit at or below 54,000 production lines.
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
