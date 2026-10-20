# Validation — dependency and discovery fixes

## Passed

- Release solution build: zero warnings/errors; self-contained publish succeeds and includes its WinUI PRI resource.
- 57 checks in the combined deterministic, live-source and local-discovery run. This includes all 14 live release sources, exact-release Ghidra Java metadata, Python HTML SHA-256 extraction, constraint merging/conflicts, cyclic graphs, too-new dependencies, Java/Ghidra/REA ordering, optional/managed runtime distinctions, npm SHA-512, path deduplication, ZIP traversal/ADS rejection, synthetic portable extraction/receipt/detection, queue failure/cancellation and redirect/resume behavior.
- Automatic discovery on this PC: JDK 21.0.12.1 at `C:\Program Files\Java\jdk-21.0.12.1+1` and Ghidra 12.1.4 at `C:\ghidra\ghidra_12.1.4_PUBLIC`, without configured-location input. The discovery test completed in 41 ms. This is a measured sample, not a universal timing guarantee.
- Eleven in-app ViewModel/UI smoke checks: all 14 selections, direct Java selection, REA automatic prerequisites, dependency locks and release, detail constraints, categories/search and all official release metadata. Native window inspected for caption/action separation.
- Real package download and integrity verification in this fix session: Java, Python, Hermes, REA, FFmpeg, VS Code and Notepad++. Previous milestone additionally verified Node.js, Git and 7-Zip downloads. No vendor installer was executed.

## Explicit limits

Ghidra's full archive transfer stalled on this development network, although its official release URL, SHA-256 and exact-release Java requirement resolved. The downloader now times out an idle read after 45 seconds and retries, instead of hanging indefinitely. Full LM Studio, Bionic and Docker downloads were not part of the completed package download set.

The real vendor installation/UAC/reboot matrix remains pending on a disposable Windows VM. A passing version feed or downloaded installer is not proof that every vendor installation succeeds. Interactive vendor setup windows are used where unattended parameters have not been verified. No account/model/MCP/Windows-feature configuration was automatically applied.

## VM acceptance matrix

| Case | Expected |
| --- | --- |
| Clean supported install / upgrade | Real version verified; no unsupported silent switches |
| Compatible existing JDK/Ghidra | Discovered and reused as dependencies |
| Incompatible too-old/too-new dependency | Compatible plan or actionable error; no unsafe substitution |
| Denied UAC / cancelled vendor wizard | Failure decision; no false success |
| Exit 3010 | Restart-required result; no forced reboot |
| Corrupt hash / wrong publisher / invalid signature | No execution |
| ZIP traversal / link / oversized payload | No publication outside the owned staging root |
| Cancel mid-download | Partial file retained for resume |
| Cancel during native installer | Active installer finishes; remaining queue cancelled |
| Failed prerequisite | Dependents skipped; independent entries can continue |
| Missing Docker Windows prerequisite | Report remediation before download; no automatic system setting changes |
| Several window sizes / DPI values | Native caption stays separate from header actions |

The common-folder discovery index is limited to two seconds, 2,500 directory entries and depth three. It skips reparse points and network drives and does not execute candidates. Uncommon folders can be supplied through Settings.

## Header and launch correction

See `LAUNCH-FIX.md`. The current deterministic suite has 42 passing checks, including a real harmless locked-file EXE launch from a Unicode/spaced path. Python/vendor installation was not rerun.
