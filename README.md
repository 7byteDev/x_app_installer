# X App Installer

Windows application installer and dependency manager, built in C# / .NET 10 / WinUI 3 with MVVM.

## Launch and build

Double-click `Launch.cmd`. The published application lives in `artifacts/app`; keep all runtime files beside its EXE.

```powershell
.\Build.ps1
```

Alternatively open `XAppInstaller.slnx` in Visual Studio with Windows app development tools. `Build.ps1` builds the Release solution, runs deterministic checks and publishes an unpackaged, x64, self-contained application. The publish target includes the application PRI resource file.

## Application support

All 14 catalog applications are selectable and have live release sources: Node.js, Git, Java/Temurin JDK, Python, Hermes Agent Desktop, LM Studio, LM Studio Bionic, REA, Ghidra, Docker Desktop, FFmpeg, 7-Zip, VS Code and Notepad++.

MSI and documented EXE silent installations are used where supported. Hermes, LM Studio, Bionic and VS Code use their vendor setup window; complete that window when prompted. No silent switches were guessed. Interactive installers are still followed by installed-version verification; a cancelled wizard does not count as a successful installation.

Ghidra and FFmpeg use verified, versioned portable deployments in `%LOCALAPPDATA%\XAppInstaller\Tools`. FFmpeg adds its bin directory to User PATH without duplication. Ghidra sets `GHIDRA_INSTALL_DIR`. Existing portable Ghidra and JDK installs are also discovered by the bounded common-folder scan; Settings provides optional overrides for uncommon locations. The REA npm package installs under the application's current-user npm prefix, with SHA-512 verification and npm lifecycle scripts disabled; MCP client configuration remains a separate reviewed action in REA setup.

## Dependencies

Every catalog entry includes verified requirement notes and authoritative source links. Required application edges, optional feature dependencies, bundled/managed runtimes and Windows prerequisites are distinct.

The default REA profile includes its Windows Ghidra backend: Node.js 26+, full Java JDK 21+, and Ghidra 12.1.x. These are compatibility bounds, not fixed download versions. Selecting REA adds missing/incompatible prerequisites; Java installs before Ghidra and Ghidra before REA. Ghidra's JDK requirement is refreshed from the exact release's `application.properties`. Python is optional for Ghidra's Python/debugger features and has a separate supported range. Bionic does not require the separate LM Studio application; Hermes manages its runtime rather than forcing unrelated system installations.

The resolver checks transitive dependencies even when an intermediate application is already installed, merges version ranges, rejects conflicts/cycles and does not accept too-new incompatible versions. Automatic selections explain their owner and cannot be unchecked while required. The queue rechecks prerequisites before running a dependent application.

Docker uses the WSL 2 backend. It checks supported Windows build, client OS, SLAT, firmware virtualization, RAM, Windows Server service configuration and WSL 2.1.5+ readiness. It reports remediation without silently enabling Windows features, accepting Docker terms or rebooting the PC.

## Interface and security

Dark native three-column layout, category/search filtering, real installed/latest versions, dependency details, installation notes, current/overall progress, activity logging and Retry/Skip/Cancel decisions. Window controls occupy a separate native caption row, preventing overlap with Settings/Refresh. Development milestone messages do not appear in the product UI.

Downloads enforce HTTPS and trusted redirect hosts, support retry/resume, and stop an idle transfer after 45 seconds before retrying. SHA-256 or npm SHA-512 integrity is mandatory for archives. Native Windows Authenticode and configured publisher checks protect executable installers; mandatory policies cannot be disabled through Settings. Unsigned official 7-Zip requires its authoritative SHA-256. The package remains locked against replacement through installer completion. The UI stays unelevated; only installers requiring administrator rights trigger UAC.

Local detection finishes before online release checks, and detected paths are visible in application details. A missing environment variable or uninstall registry entry no longer hides an existing portable application.

Settings/logs are in `%LOCALAPPDATA%\XAppInstaller`. An active system installer is allowed to finish on cancellation, then the remaining queue stops. Optional shutdown runs only if every queue result succeeds or is already current, with a 60-second Windows countdown.

## Validation and limits

See `docs/VALIDATION.md` for the exact checks and download evidence. No real vendor software was installed, upgraded or uninstalled during development. A synthetic portable fixture tests archive deployment without installing or executing an application. Real silent installation/UAC/reboot acceptance still requires a disposable Windows VM. Full Ghidra archive download stalled in the development network; release metadata, its SHA-256 and release-specific Java requirements resolved successfully. LM Studio/Bionic/Docker full downloads were not included in the download test set.

Restore-point creation is not supported in this build and its setting remains disabled. The temporary-directory field is reserved; portable staging stays on the destination volume for atomic publication. Preview release support depends on the upstream feed. Detection checks registry, runtime environment, managed installations, configured overrides and a shared common-folder index. The index is bounded to two seconds, 2,500 directory entries and depth three, skips junctions and network drives, and checks conventional folders on fixed drives. Portable candidates are inspected through files/version metadata; discovery does not execute them. A full disk scan is intentionally avoided. External MCP registration, model downloads, accounts/licenses and Windows feature setup remain vendor/user actions.

See `docs/DEPENDENCY-AUDIT.md`, `docs/SOURCES.md`, `docs/ARCHITECTURE.md` and `catalog/apps.schema.json`.
