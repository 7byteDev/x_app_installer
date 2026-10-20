# Versioning and local source control

The project uses a local Git repository on branch `main`. No remote is configured and nothing is uploaded. Source, catalog, scripts, documentation and assets are tracked; generated binaries and ZIPs are excluded.

`v1.0.0` preserves the previous application. `v1.1.0` introduces version control. Initial commits use `Codex <codex@localhost>` as a local automation identity; no global Git identity was changed. Configure your own name and email before making personal commits.

## Versions

Version.props is the single source of truth. Use major.minor.patch: major for incompatible changes, minor for features, patch for fixes. AssemblyVersion stays at 1.0.0.0 for compatibility; file/product versions follow the release.

```powershell
./Set-Version.ps1 -Bump Patch -WhatIf # preview only
./Set-Version.ps1 -Bump Patch
./Set-Version.ps1 -Bump Minor
./Set-Version.ps1 -Version 2.0.0
```

These are alternative ways to increment the version; do not run them all. Update CHANGELOG.md and commit before building. Set-Version never commits or tags automatically.

## Release workflow

Run PowerShell in the project folder:

```powershell
git status
git diff
git add .
git commit -m "Describe the release"
./Build.ps1
git tag -a v1.1.0 -m "Release 1.1.0" # substitute the actual version
```

Build.ps1 builds, checks and publishes the self-contained x64 app. It produces artifacts/release-VERSION/, a versioned ZIP and its SHA-256 checksum. release.json records the version, Git revision and UTC build time. A .dirty suffix identifies uncommitted changes. Launch.cmd starts the latest built release using artifacts/current-release.txt; release-1.0 remains as a fallback.

Git is needed on the development PC only. The released app shows its version beside the heading and complete build details in Settings.

## History and backup

```powershell
git log --oneline --decorate
git diff v1.0.0..v1.1.0
git show v1.0.0:catalog/apps.json
```

To inspect old sources without changing current files, create a separate checkout with git worktree. Back up the project including the hidden .git directory to preserve history. A source ZIP alone does not preserve Git history.
