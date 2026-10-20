$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
[xml]$versionDocument=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Version.props') -Raw
$appVersion=[string]$versionDocument.Project.PropertyGroup.AppVersion
if($appVersion -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid AppVersion in Version.props'}
$revision=''
if((Get-Command git -ErrorAction SilentlyContinue) -and (Test-Path -LiteralPath (Join-Path $PSScriptRoot '.git'))){
    $revision=(git rev-parse --short=12 HEAD).Trim()
    if($LASTEXITCODE -ne 0){throw 'Cannot read Git revision'}
    if(git status --porcelain --untracked-files=normal){$revision+='.dirty'}
}
$versionArgument="-p:SourceRevisionId=$revision"
dotnet build XAppInstaller.slnx -c Release $versionArgument
if($LASTEXITCODE -ne 0){throw 'Build failed'}
dotnet run --project src/AppInstaller.Checks -c Release --no-build -- $PSScriptRoot
if($LASTEXITCODE -ne 0){throw 'Checks failed'}
$releaseName="release-$appVersion"
$releaseDirectory=Join-Path $PSScriptRoot "artifacts/$releaseName"
dotnet publish src/AppInstaller.UI -c Release -r win-x64 --self-contained true $versionArgument -o $releaseDirectory
if($LASTEXITCODE -ne 0){throw 'Publish failed'}
$manifest=[ordered]@{
    Application='X App Installer';Version=$appVersion;Revision=$revision
    Runtime='win-x64';BuiltAtUtc=[DateTimeOffset]::UtcNow.ToString('o')
    AssemblySha256=(Get-FileHash -LiteralPath (Join-Path $releaseDirectory 'XAppInstaller.dll') -Algorithm SHA256).Hash
    ExecutableSha256=(Get-FileHash -LiteralPath (Join-Path $releaseDirectory 'XAppInstaller.exe') -Algorithm SHA256).Hash
}
$manifest|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $releaseDirectory 'release.json') -Encoding utf8
$zip=Join-Path $PSScriptRoot "X-App-Installer-$appVersion-win-x64.zip"
Compress-Archive -Path (Join-Path $releaseDirectory '*') -DestinationPath $zip -Force
"$((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash)  $([IO.Path]::GetFileName($zip))"|Set-Content -LiteralPath "$zip.sha256" -Encoding ascii
$releaseName|Set-Content -LiteralPath (Join-Path $PSScriptRoot 'artifacts/current-release.txt') -Encoding ascii
Write-Host "Release $appVersion ($revision): $zip"
