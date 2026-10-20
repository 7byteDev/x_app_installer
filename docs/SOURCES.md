# Release and requirement sources

All download versions resolve at runtime. `catalog/apps.json` holds provider metadata and `docs/DEPENDENCY-AUDIT.md` records required/optional/managed/system prerequisites.

| Application | Release source | Installation |
| --- | --- | --- |
| Node.js | https://nodejs.org/dist/index.json and release SHASUMS256.txt | Signed x64 MSI; /qn /norestart |
| Git | https://api.github.com/repos/git-for-windows/git/releases | Signed official x64 EXE; documented Inno switches |
| Java | https://api.adoptium.net/v3/info/available_releases and assets/latest/<feature>/hotspot | Latest GA Windows x64 Temurin MSI, checksum/signature, INSTALLLEVEL=1 |
| Python | https://www.python.org/downloads/windows/ and linked stable release page | Full Windows x64 EXE, page SHA-256 and signature, documented /quiet options |
| Hermes | https://hermes-agent.nousresearch.com/desktop and NousResearch/hermes-agent releases | Signed Nous bootstrap from the official page; vendor setup window |
| LM Studio | https://lmstudio.ai/download/latest/win32/x64 | Stable official redirect, signed Element Labs installer; vendor setup window |
| Bionic | https://lmstudio.ai/download/bionic/latest/win32/x64 | Stable official redirect, signed Element Labs installer; vendor setup window |
| REA | https://registry.npmjs.org/rea-agents/latest | Authoritative npm SHA-512, global current-user prefix; no automatic client registration |
| Ghidra | https://github.com/NationalSecurityAgency/ghidra/releases | Official ZIP and SHA-256; selected-tag application.properties supplies Java bounds |
| Docker | https://desktop.docker.com/win/main/amd64/appcast.xml | Signed stable vendor EXE; documented quiet WSL-2 Linux-container installation |
| FFmpeg | https://www.gyan.dev/ffmpeg/builds/release-version and release essentials ZIP/hash | Upstream-linked binary distributor; verified portable archive |
| 7-Zip | https://github.com/ip7z/7zip/releases | Official x64 EXE, SHA-256; /S; unsigned-with-hash policy |
| VS Code | https://update.code.visualstudio.com/api/update/win32-x64/stable/latest | Official system EXE, SHA-256/Microsoft signature; vendor setup window |
| Notepad++ | https://github.com/notepad-plus-plus/notepad-plus-plus/releases | Official x64 EXE, SHA-256/publisher signature; documented /S |

## Primary documentation

- REA: https://github.com/morluto/rea/blob/main/docs/installation.md
- Ghidra: selected release tag under https://github.com/NationalSecurityAgency/ghidra
- Adoptium MSI: https://adoptium.net/installation/windows/
- Python: https://docs.python.org/3/using/windows.html
- Hermes native Windows: https://hermes-agent.nousresearch.com/docs/user-guide/windows-native
- Bionic model/runtime options: https://lmstudio.ai/docs/bionic/models
- Docker flags and prerequisites: https://docs.docker.com/desktop/setup/install/windows-install/
- Git flags: https://gitforwindows.org/silent-or-unattended-installation
- 7-Zip flags: https://www.7-zip.org/faq.html
- Notepad++ NSIS source: https://github.com/notepad-plus-plus/notepad-plus-plus/blob/master/PowerEditor/installer/nppSetup.nsi
- VS Code setup: https://code.visualstudio.com/docs/setup/windows

No speculative production URLs or undocumented silent switches are used. Official feed failure is a visible per-app error. For feeds without documented prerelease support, the stable channel remains in use even if the global prerelease preference is enabled.
