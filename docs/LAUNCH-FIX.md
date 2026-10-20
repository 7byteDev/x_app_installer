# App Installer — header and installer launch fixes

Project: `C:\Users\Win11\Desktop\X App Installer Codex`
Run `Launch.cmd` for the published application.

## Header

The blue X is a vector shape, vertically centered and right-aligned in its header column. The header and native window title now read **App Installer**. Internal project/folder identifiers remain unchanged.

## Installer launch

The reported Python package existed and had passed integrity verification; the recorded failure occurred when Windows attempted to start it. The launch code now uses the default Windows EXE action, letting vendor bootstrappers and executable manifests handle elevation. MSI execution retains explicit administrator elevation. The launch working directory is the stable Windows system directory, with a fallback to the default directory if the Windows shell rejects the explicit one. A legacy EXE reporting elevation-required gets an explicit UAC retry. Hash/signature verification and package locking remain in place.

Missing packages are rejected before execution with a redownload/retry explanation. Launch failures include the Windows native error code and relevant resolution. UAC cancellation, access denial, invalid executable/architecture, policy blocks and common MSI exit failures receive specific guidance rather than a generic connectivity suggestion. No Windows security mechanism is bypassed.

## Validation

- Release build: zero warnings/errors; self-contained publish completed.
- 42 deterministic checks passed, including a real harmless child EXE launched from a Unicode/spaced path while its package remained locked, MSI quoting/elevation, stable EXE launch configuration and actionable missing-file errors.
- No real Python/vendor installation was rerun. The original Python elevation failure was not reproduced end-to-end; retrying the real installer on the target machine remains the final confirmation.
