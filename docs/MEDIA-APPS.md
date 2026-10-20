# VLC, mpv and OBS Studio

The catalog contains 28 enabled apps. VLC media player, mpv Player and OBS Studio are under Multimedia with bundled official icons and conventional-folder/registry discovery.

VLC: latest version from VideoLAN's Windows x64 update feed, SHA-256 from VideoLAN's canonical download server, installer from a fixed HTTPS VideoLAN mirror. The fixed mirror avoids differing regional redirect hosts on other PCs. Authenticode verification is also required. Vendor setup controls file associations.

mpv: standard x86_64 Windows builds by shinchiro, explicitly linked by https://mpv.io/installation/. These are third-party development builds. The catalog explains this and uses the build date as the distribution version. ARM64, development libraries and x86_64-v3 CPU builds are excluded. GitHub's published SHA-256 is mandatory. The portable archive is extracted using bundled SharpCompress 0.50.7, with path confinement, link/encryption rejection, no overwrites, cancellation and an 8 GiB expanded-size cap. No separately installed 7-Zip or FFmpeg is required. The player is checked with --version before its build date receipt is recorded. Current-user PATH is configured; file associations are not changed. An externally found mpv engine version does not establish which shinchiro build date it came from, so a known managed build may still be offered.

OBS Studio: official obsproject/obs-studio Windows x64 installer, published SHA-256 and Authenticode verification. Uses interactive vendor setup. Graphics compatibility and recording/streaming setup remain in OBS.

All three have locally bundled vendor logos. SharpCompress's MIT license is bundled in Licenses/SharpCompress-LICENSE.txt in the release folder.

## Validation

- Release build/publish: zero warnings or errors.
- 75 checks passed, including real VLC 3.0.24, shinchiro mpv 2026.10.20 and OBS Studio 32.2.2 downloads and integrity verification.
- The real mpv 7z archive extracted successfully and its executable reported engine version 0.41.0 through --version, without changing PATH or registering an installation during the test.
- Published UI: 11 catalog/dependency/search checks and 8 status/selection checks passed. The published application detected the already installed VLC 3.0.24 and OBS Studio 32.2.2.
- Vendor installers were not executed by these automated checks.
