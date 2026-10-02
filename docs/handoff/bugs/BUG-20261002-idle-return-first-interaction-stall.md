# BUG-20261002-idle-return-first-interaction-stall: the application seems to stall briefly after a long idle period

Status: open; reported; not reproduced so far on a development build (a forced-trim probe and a 75-minute run)
Severity: P3 until measured (a brief stall; no effect on firmware output)
Found: 2026-10-02, the owner, in chat: "若閒置很久 回去操控好像會卡住一下，這是否屬實?". The owner asked whether this is
real. The build (installed package or development build), the idle time, whether the display slept, what the owner
did first on return and the location of the input and output files are not known yet; the commander asked.
Where: the Desktop application as a whole; no single code location is established.
Observed: the owner reports that after a long idle period, going back to operate the application, it seems to stall
for a moment. Whether only the first interaction is affected, and for how long, is not known.
Expected: no noticeable stall, or a known and bounded one with a stated cause.
Findings from reading the code (independent read-only diagnosis, Claude Opus 5.5, at `3c7843f82`; nothing was run):
- The application does no work of its own when the window becomes active again. Presentation, Desktop and the
  launcher projects have no handler for activation, window state or DPI changes, power or session events. Two
  related handlers exist: Memory Layout popups close on deactivation, and the Hex Editor repaints when the theme
  really changes (`src/NvtFwCombiner.Presentation.Avalonia/Views/HexViewportControl.Theme.cs:73`). No timer, polling
  loop or lease renewal keeps running while idle: the toast, version-check indicator, Hex Editor feedback and popup
  timers are bounded and stop.
- Selected inputs are not revalidated on return: re-inspection starts only when the catalog's resolution token
  changes or during the AB format evaluation; Preview and Build re-read and hash in the background; there is no file
  watcher and no last-write-time check in Presentation.
- The update source is checked once after startup and otherwise only from Settings; there is no periodic check.
Candidate causes, in the order the diagnosis rates them (none is confirmed):
1. The platform trims the working set of a long-idle process, and the first input pages it back in (inferred). The
   packaged build is a compressed single file with composite ReadyToRun code; measured startup peaks are about 332
   to 335 MB of private bytes and up to about 336 MB of working set with a managed heap of at most about 35 MB
   (`docs/handoff/1.1.12.md:187-204`), so most pages are private and come back from the page file or compressed
   memory. As rough estimates, not measurements: soft faults cost tens of milliseconds in total, hard faults
   hundreds of milliseconds to seconds.
2. The display or GPU waking up and the composition surface being recreated (inferred; the application uses
   Avalonia's default rendering options, `src/NvtFwCombiner.Presentation.Avalonia/DesktopApplication.cs:70-73`).
3. Focus being restored to a Memory Layout slice on return, which reopens its card (conditional and unverified:
   `src/NvtFwCombiner.Presentation.Avalonia/Views/MemoryCoverageBar.cs:410`, `:466-469`; the headless P95 of
   opening a card is 45 to 207 ms, `docs/handoff/1.1.15/WS-MEMLAYOUT.md:274`).
4. The first action itself doing synchronous work on the UI thread, made slower by cause 1: opening Build Settings
   computes SHA-256 over the retained input bytes at least twice, three passes by the code
   (`src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.Execution.cs:24-33`;
   `src/NvtFwCombiner.Application/Composition/CompositionExecutionBundleDelivery.cs:255-262`, `:289`); with Bundle
   delivery enabled the destination is checked synchronously on each edit
   (`src/NvtFwCombiner.Presentation.Avalonia/ViewModels/OutputDeliveryConfirmationViewModel.cs:342-396`), which can
   block for seconds on a network share or a sleeping disk; a file picker enumerating an offline drive is platform
   behavior.
First measurements (the commander, 2026-10-02, development build with the Golden example `51950-dp-256k` loaded,
on a machine that was also running builds; evidence in the test area under `evidence/1.2.x/idle-return`):
- Baseline: a synchronous pointer move is handled in about 0.3 ms; a UI Automation walk of the whole window tree
  (it makes the UI thread touch every element) takes 127 to 283 ms.
- After emptying the process's working set (from about 257 MB to under 10 MB; this causes soft faults only, the best
  case): the first pointer move takes 1 to 2 ms with about 5,000 to 8,000 page faults for a sweep of 24 moves; the
  tree walk takes 150 to 235 ms. No measurable delay in these two probes; neither measures what is painted.
- So in these probes trimming alone, while the pages are still in memory, adds no delay a person would notice.
- A real idle run: the window stayed minimized for 75 minutes (15:29 to 16:44) while the machine kept working
  and the display stayed on. The process was not trimmed: its working set was 76.3 MB when it was minimized and
  114.7 MB before the restore (private bytes 215.2 and 211.5 MB).
  Restoring the window and the first round trip took 53.8 ms; the first pointer move 0.27 ms; the first tree
  walk 227 ms against 140 and 154 ms for the next two, inside the baseline's range. No stall was reproduced.
- Still open, none measured yet: the display or the machine asleep (cause 2), memory pressure that forces hard
  faults, the packaged build, a longer idle time, and a heavy first action (cause 4).
Existing records: none for this symptom. R52 (native first-readable latency of the Memory Layout card, `1.2.12`),
R30 (CtrlRAM cold first-open) and R28 (repeated notification, inspection and compile work) are related measurements
but none covers the return from idle; the memory gates of 1.1.12 measure only the startup peak.
What the owner was told (2026-10-02): plausible but not confirmed; the application does nothing of its own on
return; the platform paging the idle process back in fits best; the commander measures it and records the result.
Next step (the commander): repeat with the packaged build, whose code pages are not
backed by the image file and which therefore pages differently; compare "minimized only" with "after the display
was off" (cause 2); check whether a card reopens after switching away and back (cause 3); time opening Build
Settings first after a long idle (cause 4). The built-in startup trace stops after startup and cannot measure this.
Owner: the commander (measurement); the disposition follows the measurement.
Resolution: pending. If paging is confirmed, the options are to accept it as platform behavior with a note, or to
reduce the private memory that has to be paged back; neither is chosen.
