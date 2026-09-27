# ADR 0081: Bounded, observed cleanup for external tool processes

Status: Accepted (owner decision 97, 2026-09-27)

It was accepted after six rounds of independent design review: earlier rounds
rejected it or accepted it with changes, and round 6 granted design approval.
Its product choices come from owner decisions 85, 86 and 92.

Amends: [ADR 0006](0006-external-combiner-tool-runner.md): its common host's
"timeout/cancellation, process-tree cleanup" duty and its fail-closed list.

## Context

`SystemExternalProcessRunner` is the only production runner for approved
external tools (Legacy Combiner staged processors and the runtime trust probe).
ADR 0006 gives the common host timeout, cancellation and process-tree cleanup,
and requires every timeout or crash to fail closed. It does not say what the
host observes, how long cleanup may take, or which outcome wins when faults and
cancellation coincide. Audit findings F03 and F06 (1.1.13 roadmap row) showed,
by deterministic local reproduction at `557a9ee6a`:

- a descendant that denies `PROCESS_TERMINATE` makes the caller's
  `CancellationTokenSource.Cancel()` throw `AggregateException`, because the
  cancellation callback kills the process tree synchronously, and makes
  `RunAsync` fault with it on cancellation and on timeout;
- a background process holding a redirected stream keeps the run waiting after
  the tool exits, past the manifest timeout and past cancellation, and a
  cancellation during that wait returns a success-shaped result.

A synchronous `Process.Kill(entireProcessTree: true)` enumerates processes and
has no time bound, so any design that waits for it on the caller's or the
host's thread cannot promise a bounded completion.

## Decision

1. **One owner, a signal-only callback.** The runner owns the terminal phase of
   each invocation. The caller's cancellation callback only completes a signal;
   it never touches the process. `Cancel()` therefore returns at once, and
   disposing the registration never waits for a termination.
2. **Terminal signal.** The first of natural exit, manifest timeout, caller
   cancellation or a failed exit observation is the terminal signal. A
   cancellation already requested when the signal is chosen wins over an exit or
   timeout that completed together.
3. **One termination work item.** At most one tree termination runs per
   invocation, on a background task started by the runner: at once after a
   timeout, cancellation or failed exit observation; after a natural exit only
   when a stream stays open past the held-output grace (the exited child's open
   handle keeps its process ID from being reused, so the tree walk still reaches
   its direct descendants). The exceptions `AggregateException`,
   `Win32Exception`, `InvalidOperationException` and `NotSupportedException`
   mean the termination is unconfirmed; any other fault of that task is also
   unconfirmed. No termination exception reaches the caller.
4. **One total deadline (owner decision 86).** From the terminal signal, every
   host wait — termination, exit confirmation, output drain and reader stop —
   shares one deadline of 5 seconds. After a natural exit the streams get the
   first 2 seconds as held-output grace. Readers are asked to stop at 4 seconds
   (the last second is reserved for them to return). At 5 seconds the runner
   decides and returns; it never waits again for work that has not settled.
   The deadline is a host constant, not a manifest or profile timeout.
5. **Custody of detached work.** Work still running at the deadline (a blocked
   termination, a reader that ignores its stop) is detached, not cancelled by
   force. The invocation keeps ownership of it: the process handle, both stream
   readers and the cancellation sources are disposed only after every
   background task has settled, and every late fault is observed then, so none
   can surface later as an unobserved exception or be attributed to another
   run. The runner's `finally` path always performs this release, and requests a
   termination if the direct child's exit was never observed. The final step
   runs once: it disposes each handle independently (a disposal that throws
   never skips the others), observes every disposal fault, always returns the
   capacity slot (no cleanup work is still running under it), and only then
   publishes exactly one final signal — `ResourcesReleased` when every disposal
   succeeded, otherwise `ResourcesReleaseFailed`. A failed disposal never
   publishes the success signal, and the detached continuation that runs this
   step is itself observed so no fault on it goes unobserved. Both final
   signals are internal phases published only to the test observer seam; the
   production runner has no observer, writes no log for them and shows the user
   nothing, so they are not a public notification contract.
6. **Observation scope (owner decision 85).** The runner observes only the
   direct child's exit and the end of its two redirected streams.
   `ExternalProcessResult.Cleanup` reports what it observed:

   | Value | Meaning |
   | --- | --- |
   | `Complete` | The direct child's exit was observed and both streams reported their end. |
   | `TerminationUnconfirmed` | The termination or exit was not confirmed within the cleanup deadline. |
   | `OutputStreamHeldOpen` | A redirected output stream did not reach its end within the allowed drain period. |
   | `OutputReadFailed` | A redirected output stream could not be read to its end. |

   Priority when several apply: `TerminationUnconfirmed`, then
   `OutputStreamHeldOpen`, then `OutputReadFailed`, then `Complete`.
   `Complete` is an observation, not a proof that every descendant stopped. Each
   value's diagnostic text states only the observation; it never names or blames
   a holder the runner cannot identify. A consumer may treat the captured output
   as a successful result or as protocol input only when the value is `Complete`;
   the captured text may still be quoted as error diagnostics for a timeout or a
   non-zero exit whatever the value.
7. **Terminal outcome priority.**

   | Situation at the decision point | Outcome of `RunAsync` |
   | --- | --- |
   | Caller cancellation requested (at any time before the decision) | `OperationCanceledException` bound to the caller's token; its message names the observed cleanup value. Termination, exit-observation and reader faults are classified, not thrown. |
   | Manifest timeout | Result, exit code -1, `TimedOut` true, with the observed `Cleanup`. |
   | Failed exit observation | Result, exit code -1, `TimedOut` false, `Cleanup` `TerminationUnconfirmed`. |
   | Natural exit | Result with the exit code, `TimedOut` false, and the observed `Cleanup`. |

   Cancellation requested after the decision point does not change the
   returned result. A start failure is outside this ADR.
8. **Consumers fail closed.** A timeout keeps `external-tool.process.timeout`
   and a non-zero exit keeps `external-tool.process.failed`; both add one
   sentence describing the observed incomplete cleanup. A zero exit with any
   `Cleanup` other than `Complete` fails as
   `external-tool.process.cleanup-incomplete` before any staged file or captured
   output is read as a result, and a multi-command postbuild stops before its
   next command. The runtime trust probe applies the same timeout priority as
   the other outcomes: a timed-out probe is `runtime.trust.timeout`, and only a
   non-timed-out probe with an incomplete cleanup is `runtime.trust.probe-failed`.
   Presentation and CLI code and their existing classification channels do not
   change; the new message text, the zero-exit failure and the bounded wait are
   observable.
9. **Invocation capacity (owner decision 92).** Detached work keeps its
   resources until it settles, so the runner enforces a hard cap on how much may
   accumulate. A process-wide capacity with the fixed limit 8 counts every
   invocation that is **running or still cleaning up**; it is a true cap, not a
   pre-check: before starting a process the runner atomically reserves one slot
   (a compare-and-set that only takes a slot while the count is below the
   limit), so the in-use count can never exceed the limit however many runs
   race. The reservation is held for the whole invocation, a detached invocation
   keeps its original slot, and the slot is returned exactly once — on a start
   failure, on completion, or when detached work settles (including after a
   disposal failure). When no slot is free the runner throws
   `ExternalProcessCleanupCapacityException` before `ProcessLaunchGate.Start`,
   so the refused run starts no process. The exception reports the in-use count
   that the atomic reservation observed when it refused (at or above the
   limit), never a later re-read; its user-facing message states only that
   external-tool runs that are still running or still cleaning up have filled
   the capacity, keeps the restart guidance, and gives no count. The staged
   processors map the refusal to `external-tool.process.cleanup-capacity` and
   the trust probe keeps `runtime.trust.probe-failed` while carrying the same
   reason and restart guidance to the caller. The limit of 8 is chosen because
   external-tool runs are effectively serial per workflow (a UI run or a CLI
   invocation at a time), so more than a handful of simultaneously running or
   stuck invocations indicates a host-level fault rather than normal load; 8
   leaves generous headroom for rare transient slow terminations while still
   bounding resources to a small constant.
10. **Presentation of cancellation.** A cancelled run keeps the existing
    cancelled presentation (UI: no result; CLI: `error: operation canceled`, exit
    70). No surface may state that every process was confirmed stopped. A
    separate warning for incomplete cleanup after cancellation is not part of
    this ADR.

## Accepted limitation

A descendant that holds neither redirected stream (for example one started
through `ShellExecute`) is invisible to this observation: the run can report
`Complete` while it still runs and could still write the staging directory. An
orphan whose parent exited before the tree walk is not reachable by the
termination either; if it holds a stream, the run reports
`OutputStreamHeldOpen` and fails closed, but the orphan keeps running. The
owner accepted both limits (decision 85). A process that refuses termination
may also outlive the run. Staging cleanup keeps its existing best-effort
behavior when such a process holds the directory.

## Consequences

- Each run returns within the 5-second deadline plus scheduling latency, keeps
  ownership of its still-running cleanup work, and reclaims that work's
  resources once it settles; `Cancel()` returns without waiting for any process
  work. The runner does not claim that no handle survives the 5 seconds: a
  detached termination or reader keeps its process handle and pipe handle open
  until it settles or the host process exits.
- Invocations that are running or still cleaning up never exceed the fixed
  limit (8), because a slot is reserved atomically before a process starts. At
  the cap a new external-tool run is refused with a typed error asking the user
  to restart, before it starts any process, so resources cannot accumulate
  without bound even if some cleanup work never settles.
- Because the capacity also counts running invocations, a ninth external-tool
  run started while eight are still running or cleaning up is refused, even
  when none is stuck. Normal use starts one external-tool run at a time per
  workflow, so this does not arise in ordinary operation.
- Approved tools that exit cleanly and leave no stream holder behave exactly as
  before — same bytes, ranges, order, CRC/Header behavior, naming and result —
  while fewer than eight external-tool runs are in use at once.
- A disposal failure after cleanup settles does not hold the slot. It is
  surfaced only as the internal `ResourcesReleaseFailed` phase to the test
  observer seam; there is no production log or user notification for it, and
  this ADR adds no public notification contract. The handle whose disposal
  failed is left to the runtime's finalization.
- A tool that exits with code 0 while leaving a stream holder now fails instead
  of succeeding late.

## Rejected options

- **Kill inside the cancellation callback** (first revision): runs an unbounded
  tree walk on the canceller's thread, so `Cancel()` and Close can block; also
  lets the callback and the cleanup operate on one `Process` concurrently.
- **Waiting synchronously for the tree kill within a budget** (first revision):
  the wait itself cannot be bounded once the kill call blocks.
- **A cancellation exception subtype carrying the cleanup value:** adds a type
  every caller must know; the value is kept in the exception message for
  diagnostics, and a typed warning can be decided separately.
- **Reusing `ManagedProcessTermination`:** VersionManagement-only, synchronous,
  no stream drain, and `NvtFwCombiner.Infrastructure` may not reference it.
- **A Windows Job Object for whole-tree containment:** the only way to reach
  every descendant, but a new platform capability; allocated to a later R2 item
  (owner decision 85).
- **Separate budgets for termination and reader stop** (for example 5 s plus
  1 s): rejected by owner decision 86 in favor of one total deadline.

## Verification

Infrastructure tests exercise the contract with real processes and with seams
for a slow (gated, then real) termination, a refused or an ineffective termination, a failed exit observation,
a failing or uncooperative reader, and phase handshakes for the timeout/exit/
cancellation races. `Cancel()` runs on its own task, bound-waited from before
the call, so a regression to a synchronous blocking callback fails promptly
rather than hanging; a guard test confirms that. A pure test on the schedule
pins the reader stop inside the single deadline (the reserve is not added after
it). The orphan test records the pipe-holder's PID and its direct parent's PID,
waits for the pipe-holder to be ready and confirms its direct parent has
actually exited, and asserts that confirmation precedes the timeout that starts
termination; real-OS tests keep a scheduling margin. The capacity is driven to
its limit to show a new run is refused with the typed capacity error and that
settling frees a slot. For the concurrency claim, each run starts on its own
dedicated thread and all of them are released together by one barrier, so their
reservations genuinely race; exactly the limit start a process, the rest are
refused before starting one, every admitted run — whose exit seam, after the
gate opens, still waits for the real process exit — returns exit code 0 with
complete cleanup, every started run then publishes `ResourcesReleased`, and the
capacity returns to zero. No test seam treats an opened gate as a completed
exit or termination. A separate test releases
many dedicated threads through one barrier to call the reservation directly and
confirms exactly the limit succeed and every refusal observed a count at or
above the limit. Both are repeated. The completion signal a test waits on is
published only after the handles are disposed and the reservation is returned,
so the count it then reads is already settled. Fault-injection tests make a
disposal throw, on the inline and on the detached path, and confirm the other
disposals still run, the slot is returned, `ResourcesReleaseFailed` is
published and `ResourcesReleased` is not. Mapping tests lock the staged output
against reading to prove that the cleanup check precedes every read, use a
two-command plan to prove that the next command does not start, cover the
timeout-plus-incomplete message, and map the capacity refusal to its typed
issue. The trust-probe timeout priority is covered, and a probe capacity refusal
is asserted to carry the restart guidance through to the inspection result.
