# OpenCode, application logos and WinSCP recovery

- The catalog now contains 25 enabled applications. OpenCode Desktop is under AI & LLM, uses the official anomalyco/opencode stable Windows x64 desktop release, and checks registry/standard folders for existing installations. The official Electron build bundles the CLI; no separate Node dependency is forced. Installation stays with the vendor per-user setup.
- All 25 catalog entries have bundled vendor icons in both list rows and the detail panel. See APP-ICON-SOURCES.md for provenance. Images require no runtime network access.
- WinSCP previously used exact HTML attribute matching and surfaced a generic format error when it could not parse a response. Its source now supports relative/absolute links, either quote style, whitespace in attributes and checksum markup. Incomplete pages trigger up to two fresh, uncached retries. The release and download details are validated together and a fresh short-lived download ticket is obtained for every installation attempt. Missing links/checksums still block installation, with a specific source-page error.

## Validation

- Release build: zero warnings/errors.
- 67 checks passed, including WinSCP incomplete-page recovery, HTML variants, fresh download tickets, and real official WinSCP 6.7/OpenCode Desktop 1.19.3 downloads with integrity verification. These checks do not execute vendor installers.
- Published application: 11 catalog/dependency/search UI checks and 8 status/selection checks passed. Real logos were visually checked in the app.
- OpenCode 2.0.32 was already present in the user's registry and was recognized. The app does not downgrade an installation newer than the stable release metadata returned by the official source.

Published Windows x64 app: artifacts/release-1.0/XAppInstaller.exe. Run Launch.cmd at the project root. Distribute the complete X-App-Installer-1.0-win-x64.zip, not the EXE alone.

- Observed the user's subsequent WinSCP installation through the published app: at 21:33:38 it reported 'WinSCP 6.7: installed and verified' and 'WinSCP · Succeeded'.
