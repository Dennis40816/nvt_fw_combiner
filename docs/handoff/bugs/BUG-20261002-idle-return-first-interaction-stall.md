# BUG-20261002-idle-return-first-interaction-stall: the first interaction after a long idle period seems to stall briefly

Status: open; reported, not yet measured or reproduced
Severity: P3 until measured (a brief stall on the first interaction; no effect on firmware output)
Found: 2026-10-02, the owner, in chat: "若閒置很久 回去操控好像會卡住一下". The owner asked whether this is real. The
build (installed package or development build), the idle time, whether the display slept, the first action and the
location of the input and output files are not known yet.
Where: the Desktop application as a whole; no single code location is established.
Observed: after the window has been idle for a long time, the first interaction stalls for a moment; then the
application behaves normally.
Expected: no noticeable stall, or a known and bounded one with a stated cause.
Findings from reading the code (independent read-only diagnosis, Claude Opus 5.5, at `3c7843f82`; nothing was run):
- The application does no work of its own when the window becomes active again. Presentation, Desktop and the
  launcher projects have no handler for activation, window state, theme or DPI changes, power or session events; the
  only related handler closes the Memory Layout popups on deactivation. No timer, polling loop or lease renewal keeps
  running while idle: the toast, version-check indicator, Hex Editor feedback and popup timers are bounded and stop.
- Selected inputs are not revalidated on return: re-inspection starts only when the catalog's resolution token
  changes or during the AB format evaluation; Preview and Build re-read and hash in the background; there is no file
  watcher and no last-write-time check in Presentation.
- The update source is checked once after startup and otherwise only from Settings; there is no periodic check.
Candidate causes, in the order the diagnosis rates them:
1. The platform trims the working set of a long-idle process, and the first input pages it back in (inferred; it
   fits the report best). The packaged build is a compressed single file with composite ReadyToRun code; measured
   startup peaks are about 331 to 336 MB private memory with a managed heap of at most about 35 MB
   (`docs/handoff/1.1.12.md:146-149`, `:186-206`), so most pages are private and come back from the page file or
   compressed memory. Soft faults would cost tens of milliseconds; hard faults hundreds of milliseconds to seconds.
2. The display or GPU waking up and the composition surface being recreated (inferred; the application uses
   Avalonia's default rendering options, `DesktopApplication.cs:70-73`).
3. Focus being restored to a Memory Layout slice on return, which reopens its card (conditional and unverified:
   `MemoryCoverageBar.cs:410`, `:466-469`; opening a card costs 45 to 207 ms in headless measurements).
4. The first action itself doing synchronous work on the UI thread, made slower by cause 1: opening Build Settings
   computes SHA-256 twice over the retained input bytes (`MergePresentationViewModel.Execution.cs:24-33`,
   `CompositionExecutionBundleDelivery.cs:255-262`, `:289`); with Bundle delivery enabled the destination is
   checked synchronously on each edit (`OutputDeliveryConfirmationViewModel.cs:342-396`), which can block for
   seconds on a network share or a sleeping disk; a file picker enumerating an offline drive is platform behavior.
Existing records: none for this symptom. R52 (native first-readable latency of the Memory Layout card, `1.2.12`),
R30 (CtrlRAM cold first-open) and R28 (repeated notification, inspection and compile work) are related measurements
but none covers the return from idle; the memory gates of 1.1.12 measure only the startup peak.
Next step (the commander, without the owner): measure it. With a Golden example loaded, time a forced repaint and a
few first actions as a baseline; then empty the process's working set (the `EmptyWorkingSet` call on the
application's own process; this gives the best case, soft faults only) and time the same actions again, recording
the working set and the page-fault count before and after; compare "minimized only" with "after the display was
off" to separate cause 2; check whether a card reopens after switching away and back (cause 3); time opening Build
Settings first after trimming (cause 4). A result where the first action after trimming is clearly slower than the
baseline with page faults in the tens of thousands, and the second action is normal again, is consistent with
cause 1. The built-in startup trace stops after startup and cannot measure this.
Owner: the commander (measurement); the disposition follows the measurement.
Resolution: pending the measurement. If cause 1 is confirmed, the options are to accept it as platform behavior with
a note, or to reduce the private memory that has to be paged back; neither is chosen yet.
