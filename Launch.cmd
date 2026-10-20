@echo off
cd /d "%~dp0"
set "APP_RELEASE="
if exist "artifacts\current-release.txt" set /p APP_RELEASE=<"artifacts\current-release.txt"
if defined APP_RELEASE if exist "artifacts\%APP_RELEASE%\XAppInstaller.exe" (
    start "" "artifacts\%APP_RELEASE%\XAppInstaller.exe"
    exit /b
)
if exist "artifacts\release-1.0\XAppInstaller.exe" (
    start "" "artifacts\release-1.0\XAppInstaller.exe"
) else (
    dotnet run --project src\AppInstaller.UI
)
