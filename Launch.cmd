@echo off
cd /d "%~dp0"
if exist "artifacts\release-1.0\XAppInstaller.exe" (start "" "artifacts\release-1.0\XAppInstaller.exe") else (dotnet run --project src\AppInstaller.UI)
