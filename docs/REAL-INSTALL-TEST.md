# App Installer: real installation test

Tested through the published WinUI application's Install All Available button on 2026-10-20. Vendor installation processes were launched by the application, not by separate installation commands.

| Application | Result |
|---|---|
| Node.js | Upgraded to 26.12.0; registry and command verification passed |
| Git | Upgraded to 2.56.0.2; verification passed |
| Java JDK | Installed Temurin 27.0.0.35; verification passed |
| Python | Installed 3.15.0; verification passed |
| FFmpeg | Installed 9.0.2; managed version detection and PATH corrected and retested |
| 7-Zip | Upgraded to 26.04; command version parser corrected and retested |
| Visual Studio Code | Installed 1.142.0 using vendor setup wizard; detection passed |
| Hermes Agent Desktop | Already current at 0.22.2; no reinstall |
| LM Studio | Already current at 0.4.26+4; no reinstall |
| LM Studio Bionic | Already current at 1.1.8+10; no reinstall |
| REA MCP Server | Already current at 6.7.0; CLI verification passed on repeat run |
| Ghidra | Already current at 12.1.4; no reinstall |
| Docker Desktop | Blocked before download because WSL is missing; skipped |
| Notepad++ | Vendor installer returned 5 with editor open. Added a preflight check directing the user to save and close documents; skipped to preserve unsaved work |

Fixes:
- Resolve the actual filesystem path from the downloaded file handle before installer launch. A packaged parent process can redirect LocalAppData into its package cache; elevated Windows Installer otherwise sees a nonexistent path (1619 / MSI storage error 0x80030003). This also resolves the reproduced Python launch path failure.
- Add MSI diagnostic logging and specific advice for errors 1619 and 1622.
- Inspect the version of the managed FFmpeg installation instead of accepting Unknown from Windows version metadata. Arbitrary discovered executables are still not run during scanning.
- Write real portable installation paths to the environment so external applications can use them; migrate the old FFmpeg PATH entry.
- Prefer dotted version numbers over the digit in the 7-Zip product name, retaining feature-only Java version handling.
- Recheck declared commands when an application is already current.
- Count an explicit Skip as Skipped, and keep dependent applications blocked.
- Bound command mismatch text to keep failure dialogs usable.

Validation: Release publish succeeded. 47 deterministic checks passed. The second application-driven queue confirmed 12 current installations, with Docker and Notepad++ skipped for the prerequisites above. Full Docker installation and the Notepad++ update remain unverified until those conditions are resolved. No restart or shutdown was initiated.