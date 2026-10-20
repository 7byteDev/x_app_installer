# Detection fixes and expanded catalog — 2026-10-20

Python 3.15.0 was already installed at Program Files/Python315. Its installer log ended with exit code 0, but detection could return an older registry result before comparing executable metadata from other installation folders. Detection now compares the registry, managed, and discovered versions and chooses the highest compatible version. A live diagnostic with the registry match deliberately restricted to Python 3.14.7 still detected Python 3.15.0.

FFmpeg 9.0.2 was installed in the packaged parent's redirected LocalAppData folder. Its old receipt contained a virtual path that was valid in that host's filesystem view. The installer now discovers its existing receipts in standard and package-local stores, rebases legacy receipt paths within the corresponding Tools directory, and validates the executable as before. New portable receipts record the resolved physical directory. Receipt path validation still rejects unrelated directories.

Ten enabled providers were added: WinSCP, PuTTY, Advanced Port Scanner, Wireshark, DBeaver Community, Flameshot, Lightshot, ShareX, Sniffnet, and Portmaster. The catalog has 24 apps. Network has its own colorful menu icon. DBeaver uses the bundled Java runtime. Wireshark uses interactive setup to offer Npcap. Sniffnet checks for Npcap in WinPcap API-compatible mode and explains the required vendor setup instead of falsely declaring Wireshark itself as a hard dependency. Portmaster uses interactive vendor setup for its background service/network components.

Sources: official vendor pages, official PuTTY SHA-256 feed, Wireshark stable appcast, Safing published installer checksums, and official GitHub release assets/digests. Lightshot's unversioned vendor installer is inspected for PE version metadata without executing it. All download redirects remain subject to exact HTTPS host allowlists. ShareX and Flameshot can accept unsigned vendor installers only with an authoritative SHA-256 digest; invalid signed-file trust checks are not bypassed.

Validation:
- 61 deterministic checks, including receipt migration and dependency cases, passed.
- All 10 new live release sources resolved successfully.
- All 10 actual downloads passed integrity verification in targeted runs. No new vendor installers were executed.
- 11 full UI smoke checks and 8 status/selection checks passed in the published 24-app build.
- Live diagnostics detected Python 3.15.0 and FFmpeg 9.0.2.
- Publish completed successfully; the normal application was reopened from this project's artifacts/release-1.0 folder.

Official documentation:
https://winscp.net/eng/docs/installation
https://www.chiark.greenend.org.uk/~sgtatham/putty/latest.html
https://www.advanced-port-scanner.com/
https://www.wireshark.org/docs/wsug_html_chunked/ChBuildInstallWinInstall.html
https://dbeaver.com/docs/dbeaver/Installation/
https://flameshot.org/docs/installation/installation-windows/
https://app.prntscr.com/en/download.html
https://getsharex.com/
https://github.com/GyulyVGC/sniffnet/wiki/Required-dependencies
https://safing.io/download/
https://github.com/safing/checksums/