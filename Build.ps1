$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
dotnet build XAppInstaller.slnx -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
dotnet run --project src/AppInstaller.Checks -c Release -- $PSScriptRoot
if ($LASTEXITCODE -ne 0) { throw 'Checks failed' }
dotnet publish src/AppInstaller.UI -c Release -r win-x64 --self-contained true -o artifacts/release-1.0
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
