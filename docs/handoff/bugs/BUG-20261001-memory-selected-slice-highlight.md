# BUG-20261001-memory-selected-slice-highlight: the selected small slice's highlight looks crude

Status: open (scheduled in `1.2.9` with Memory Layout design A, board decision 226)
Severity: P3 (visual)
Found: 2026-10-01, the owner, on a photo of the running application (in chat: "這個反白好醜 安排在 1.2.x 修改").
Where: Memory Layout coverage bar of an AB Code output (`0x00000`-`0x7FFFF`): the selected small DP slice
"DP AB #2" (`b-bank`, `0x40000`-`0x46FFF`, 28 KiB) between the TPA and TPB segments, with its card open.
Observed: the selected slice is drawn as a separate raised rounded block in a darker blue, taller than the bar
and overlapping the edges of the neighbouring TPA and TPB segments, with a `…` label, a thin black connector to
the card and a black diamond marker below the bar. The owner finds this highlight ugly.
Expected: a selected or focused slice is marked in a way that fits the bar and the neighbouring segments; the
new treatment is shown to the owner as a real-screen preview and the approved image is the reference. Keyboard
focus and the High Contrast cue (decisions 189 and 196, `1.2.10`) stay distinguishable.
Evidence: the owner's photo, `evidence/1.2.x/ui/20261001-memory-selected-slice-highlight.jpg` (test-area relative,
SHA-256 `429b6be97f2830eb…`).
Owner: unassigned; `1.2.9` (shared visuals and Memory Layout design A).
Resolution: pending.
