# Sample files

`sample-board.gbr` is a hand-written RS-274X copper layer, small enough to read but
complete enough to exercise the parser: flashed pads, a drawn track, a filled region
(`G36`/`G37`) and a cleared keep-out (`%LPC`).

Use it to try the import without a CAD tool: **Import Gerber…**, pick this file, and the
preview will report a 20 × 10 mm board with two contours — the outline of the pour, and the
hole the `%LPC` ring cut around the left pad.

It also shows why exposure order matters: the ring is cleared *before* the pad and track
are drawn, so the pad and its track sit back inside the hole and stay connected to the
pour. Draw them in the other order and the pad would be erased.
