# Dependency audit — 20 October 2026

Required, optional, vendor-managed and Windows-system prerequisites are distinguished. Download versions are resolved live.

## Node.js

No separately installed application from this catalog is required. Installer includes its runtime components.


Installation: Uses documented vendor installation defaults.

Source: https://nodejs.org/en/download

## Git

No separately installed application from this catalog is required. Installer includes its runtime components.


Installation: Uses documented vendor installation defaults.

Source: https://gitforwindows.org/silent-or-unattended-installation

## Java (JDK)

No separately installed application from this catalog is required. Installer includes its runtime components.


Installation: Machine-wide Temurin JDK; vendor INSTALLLEVEL=1 configures PATH. JAVA_HOME is not overwritten.

Source: https://adoptium.net/installation/windows/

## Python

No separately installed application from this catalog is required. Installer includes its runtime components.


Installation: Official Windows x64 full installer; installs for all users and adds Python to PATH.

Source: https://docs.python.org/3/using/windows.html
Source: https://www.python.org/downloads/windows/

## Hermes Agent Desktop

Official desktop bootstrap manages its own Git, Python, Node.js, npm and other runtime dependencies. They are not separate system-app prerequisites. Optional integrations may need extra tools.


Installation: Signed official desktop bootstrap. Follow the vendor setup window; it prepares its managed runtime and builds Desktop. Network access is required.

Source: https://hermes-agent.nousresearch.com/desktop
Source: https://hermes-agent.nousresearch.com/docs/user-guide/windows-native

## LM Studio

No separate Node.js, Python, Java or Docker installation is required. Local inference depends on supported CPU/GPU and available RAM; model downloads are separate.


Installation: Official signed installer. Follow the vendor setup window; no undocumented silent switches are used.

Source: https://lmstudio.ai/download
Source: https://lmstudio.ai/docs/app/system-requirements

## LM Studio Bionic

Runs its own local models, cloud models or remote models. A separate LM Studio installation is not required. Local models need suitable hardware and a model download.


Installation: Official signed installer. Follow the vendor setup window; no undocumented silent switches are used.

Source: https://lmstudio.ai/download
Source: https://lmstudio.ai/docs/bionic/models

## REA MCP Server

Installs REA with its Windows Ghidra backend: Node.js, Ghidra and a compatible full JDK. MCP client registration remains a separate reviewed setup step. Other REA analysis modes can have different prerequisites.

- Required: node ≥ 26.0 — REA package runtime; conservative supported Node.js 26+ range.
- Required: java ≥ 21.0 — Full 64-bit JDK for the Ghidra backend.
- Required: ghidra ≥ 12.1 and < 12.2 — REA Windows Ghidra backend supports the 12.1 release series.

Installation: Verified npm tarball; global npm installation for the current user. Does not modify agent/MCP configuration automatically.

Source: https://github.com/morluto/rea/blob/main/docs/installation.md
Source: https://registry.npmjs.org/rea-agents/latest

## Ghidra

Java is required. Its version range is read from the selected Ghidra release. Python is optional for PyGhidra/debugger workflows; a newer unsupported Python is not substituted.

- Required: java ≥ 21.0 — Full JDK; refreshed from the selected release application.properties.
- Optional: python — PyGhidra and debugger workflows; release-specific Python support applies.

Installation: Versioned portable installation for the current user. Sets GHIDRA_INSTALL_DIR; preserves existing installations.

Source: https://github.com/NationalSecurityAgency/ghidra/blob/Ghidra_12.1.4_build/Ghidra/application.properties
Source: https://github.com/NationalSecurityAgency/ghidra/blob/Ghidra_12.1.4_build/GhidraDocs/GettingStarted.md

## Docker Desktop

Requires a supported Windows edition/build, hardware virtualization and the WSL 2 or Hyper-V backend. These Windows features are checked before installation; they are not Java/Python/Node catalog dependencies. Docker license acceptance remains with the user.


Installation: Documented quiet installer with Linux containers. Missing Windows prerequisites are reported; no automatic feature enabling or license acceptance.

Source: https://docs.docker.com/desktop/setup/install/windows-install/

## FFmpeg

Gyan essentials builds are linked by FFmpeg upstream and include their codec libraries. No separate Python, Node.js or Java is required.


Installation: Verified Gyan stable essentials ZIP; versioned current-user installation; adds its bin directory to User PATH without duplicate entries.

Source: https://ffmpeg.org/download.html
Source: https://www.gyan.dev/ffmpeg/builds/

## 7-Zip

No separately installed application from this catalog is required. Installer includes its runtime components.


Installation: Uses documented vendor installation defaults.

Source: https://www.7-zip.org/faq.html

## Visual Studio Code

Standalone editor. Language runtimes and Git are optional for the corresponding development features.

- Optional: git — Source-control features
- Optional: node — JavaScript/Node.js development
- Optional: python — Python development

Installation: Official system installer. Follow the vendor setup window to choose PATH and context-menu options.

Source: https://code.visualstudio.com/docs/setup/windows
Source: https://code.visualstudio.com/docs/supporting/requirements

## Notepad++

No separately installed application from this catalog is required. Installer includes its runtime components.


Installation: Official NSIS silent installer (/S). Close running Notepad++ windows before installing.

Source: https://github.com/notepad-plus-plus/notepad-plus-plus/blob/master/PowerEditor/installer/nppSetup.nsi
