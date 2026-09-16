---
name: diagnosing-bugs
description: Diagnose NFC failures, crashes, incorrect behavior or performance regressions using reproducible evidence and falsifiable hypotheses. Diagnosis alone does not authorize a fix.
---

# Diagnosing Bugs

Apply [Agent Skill Routing](../../../docs/governance/agent-skill-routing.md)
and root/nearest `AGENTS.md`. Inspect the affected contracts, current owner,
caller path and relevant tests; expand for a concrete dependency or contradiction.

## Establish the symptom

Record expected versus observed behavior, source/build, input identity,
environment and the trigger sequence. Preserve original logs, crash traces,
timings and failing evidence. Separate user reports, direct observations and
inferences; an adjacent failure is not proof of the reported defect.

Use code inspection and existing evidence to form falsifiable hypotheses while
developing a reproduction. Start with the smallest check that can distinguish
them, not a fixed number of guesses or a mandatory tool sequence. Prefer an
existing test or CLI/fixture path. For binding, layout, focus or input-event
bugs, exercise the actual control or packaged app when a lower seam misses the
symptom. Use the available platform tools, not an assumed browser or shell.

If human interaction is necessary, give precise steps and request the observed
result/artifact. The [Bash feedback template](scripts/hitl-loop.template.sh) is
optional when Bash and an interactive terminal are available; it is not a
prerequisite for Windows UI diagnosis.

## Test the cause

- Associate each probe with a prediction; change one relevant variable at a
  time and capture the actual result. Reduce the scenario only while it still
  reproduces the user's symptom and the reduction helps distinguish causes.
- For intermittent failures, choose a bounded sampling or stress experiment
  from the suspected timing/state issue. Record attempts, failures and relevant
  conditions; a clean sample does not establish absence of the bug. Apply the
  [retry policy](../../../docs/governance/development-execution-workflow.md#retry-policy)
  rather than repeating an unchanged expensive command without a hypothesis.
- For performance, measure the relevant boundary and compare like-for-like
  inputs/environment. Separate startup, first render and ready state when
  relevant. Use a profiler or targeted timing before proposing an optimization;
  avoid attributing a noisy end-to-end number to an unmeasured component.
- Prefer debugger/trace inspection; add only authorized, targeted temporary
  instrumentation with a recognizable tag. Use the configured test area for
  probes, preserve immutable firmware inputs and avoid logging private bytes
  or credentials. Production instrumentation requires separate authority.

When the environment or evidence cannot reproduce the symptom, continue useful
read-only analysis and label hypotheses as unconfirmed. Report what was tried,
what the evidence supports and the smallest missing artifact/access needed.
Do not invent a passing reproduction or a confirmed root cause.

## Close diagnosis or verify an authorized fix

For diagnosis-only work, report the supported cause (or ranked unresolved
hypotheses), decisive evidence, limitations and a focused fix proposal. Stop
before implementing a product change.

When a fix is authorized, follow `$implement` and its admission/test rules.
Reuse or add a regression that reaches the real bug pattern, preserve its
failing evidence, then verify the corrected behavior and original scenario.
If the seam cannot reproduce the interaction, record that gap rather than
substituting a constant/source-text assertion or claiming the original bug is
covered. Route a necessary ownership/seam change to `$nfc-architecture-change`.

Remove this task's temporary instrumentation; retain evidence needed to explain
the finding and identify any remaining diagnostic artifacts. Report actual
checks and residual uncertainty separately from integration/release status.
