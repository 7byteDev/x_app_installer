# RustDesk

RustDesk is the 29th enabled catalog application, under Network. It uses the official rustdesk/rustdesk stable Windows x64 MSI release, requires SHA-256 verification when published and Authenticode verification, and includes its official local logo. The MSI runs with interactive vendor setup and Windows administrator permission. Existing RustDesk installations are detected through uninstall registry entries and conventional application folders.

The client does not force a Rust toolchain, Docker or a self-hosted server. Remote connections, server settings and unattended-access configuration remain in RustDesk.

Validation: 68 checks passed including the actual official RustDesk 1.5.0 MSI download and integrity/signature verification. The vendor installer was not executed by these checks. VLC, mpv and OBS validation is documented in MEDIA-APPS.md.

Published 29-app UI: all 11 catalog checks and 8 status/selection checks passed.
