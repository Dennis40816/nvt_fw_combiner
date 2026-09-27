# ADR 0082: Window close, recovery, and publication lifetime

Status: Accepted design (owner decisions 87, 88, and 136–139, 2026-09-27).
Implementation and UI reference evidence remain separate gates.

## Context

The desktop window owns asynchronous startup, composition, preload, inspection,
Settings, Report, and local-state work. A failed stable-launcher handoff leaves
that window open. Closing used to seal its save queues permanently, reuse a
cancelled startup token, and release preload resources after a bounded wait
rather than actual settlement. READY could escape an `async void` event handler;
late composition and view-owned work could publish to a dying window.

## Decision

1. `MainWindow` owns one close attempt and one session generation at a time.
   Its phases are Open, Draining, Sealing, HandingOff, Recovering, Closing, and
   Closed. Only Open admits a new attempt. Every external Close is cancelled
   until the single internally posted final Close in Closing. Reentrant Close
   neither stacks work nor changes an explicit Close anyway decision.
2. The attempt closes admission and cancels session work first. It observes all
   admitted run, startup, READY, discovery, Report JSON, and preload work for
   one host-owned five-second work deadline. An operation accepted during drain
   remains owned. The active run's committed Build receipt may publish before
   revocation; a receipt after revocation is discarded without deleting the
   committed output. Completion and resource custody continue after a timeout.
3. Accepted, noncancelled user operations remain computation-valid. Their
   existing feature owners retain typed terminal results or faults while
   publication is suspended in Sealing, HandingOff, or Recovering. On resume
   they recheck their own operation identity, generation, and captured inputs
   at the delivery site; supersession or final revocation wakes and drops the
   result. Intermediate progress is dropped. The window never interprets an
   Application result in place of its owner. Final Close revokes all window and
   session publication, including Report Save notifications.
4. At the terminal decision, a valid activation accepted during drain upgrades
   ordinary exit to launcher handoff. Activation arriving before final
   revocation is reconciled on the dispatcher. Activation during recovery is
   deferred to a fresh attempt after Open. Explicit Close anyway takes
   precedence. The stable launcher starts before final Close; `OnClosed` never
   starts it.
5. Both local-state coordinators seal and flush under one further five-second
   deadline. A failed handoff reopens their existing serialized tails and
   generations, then requeues the latest dirty immutable preference and
   history snapshots. It creates a fresh session token; old callbacks retain
   invalid tickets. Preload retry/skip from a closed session is not presented
   as live work.
6. Failed handoff starts a separately bounded pending-clear attempt. A settled
   clear is followed by the existing Application durable reload. Only a usable
   saved state confirms cleared or kept pending activation. Timeout, fault, or
   unsettled clear means unknown. New version mutations and Retry remain fenced
   while a clear is unsettled or status is unknown. Retry requires settlement
   and a fresh durable read confirming pending activation. A previously
   admitted clear may still commit after Close anyway; that choice starts no
   new clear or launcher. The decision-88 confirmation presents Cancel, Close
   anyway, and Retry with state-specific English and Traditional Chinese copy.
7. Preload admission closes before its accepted task set is frozen.
   `AllUsersSettled`, rather than a bounded drain return, controls release of
   its cancellation sources and session. All late faults and release faults
   are observed. The external process runner retains its separate custody
   contract under ADR 0081.

## Consequences

Presentation owns the window lease and composition of existing owners.
Application retains terminal use-case decisions and durable version-state
mutation. Domain and Profiles retain firmware semantics. UI reference approval
for decision 88 and its English Light and Traditional Chinese Dark renders is
required before the corresponding visual production change. Release and
integration evidence remain subject to the admitted change record and the
normal R2 gates.
