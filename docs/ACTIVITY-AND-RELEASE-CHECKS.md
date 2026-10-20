# Activity log and release checks

Activity messages are now selectable and colored by severity: SUCCESS green, WARNING yellow, ERROR red, INFO/DEBUG neutral. Copy all copies the complete current activity log as plain text. Select text in an individual message and use Ctrl+C to copy just that text.

Git, Ghidra, Hermes, 7-Zip and Notepad++ reported failures because GitHub's release API returned HTTP 403 with rate limit exceeded. This does not mean the installed applications are missing or broken.

GitHub release metadata is persisted for 15 minutes so refreshes, application restarts and installation checks reuse recent results. Rate-limited responses expose the retry time from GitHub's headers and suspend further GitHub API calls until then. Other HTTP 403 errors remain distinct access errors.

If a previous successful cached release is available, the app can display it for up to seven days during an HTTP outage. It labels this metadata as cached/unconfirmed and blocks new installations from stale metadata. With no previous cache, the latest-version column shows Rate limited and the installed version remains visible. Details and status tooltips explain the failure.

Validation: Release publish succeeded, 56 deterministic checks passed, three UI severity/color checks passed, and the Copy all button was clicked in the running application and its clipboard content verified. The live request reproduced GitHub's rate limit and displayed its actual retry time. No new app installations were performed for this UI/network-handling change.

GitHub guidance: https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api

Build and release location: artifacts/release-1.0/XAppInstaller.exe. Launch.cmd starts this build.