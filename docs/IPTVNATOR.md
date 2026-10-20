# IPTVnator

IPTVnator is included under Multimedia as the 30th enabled catalog entry. It uses stable Windows x64 setup releases from the official 4gray/iptvnator repository and bundles the upstream icon locally. Installed copies are detected through the uninstall registry and conventional IPTVnator folders.

The Electron runtime and built-in web playback are part of the desktop package. MPV and VLC are optional external engines rather than mandatory installation dependencies. Playlists and playback settings are configured in IPTVnator.

The provider uses interactive vendor setup and verifies the published SHA-256 before installation. Upstream Windows setup is unsigned (Windows trust result 0x800B0100); the existing allow-unsigned-with-authoritative-hash policy applies to this provider. Invalid signed certificates remain rejected, and unsigned packages without an authoritative SHA-256 remain blocked. No vendor installer is executed by the automated download checks.
