[CmdletBinding(DefaultParameterSetName='Bump', SupportsShouldProcess=$true)]
param(
    [Parameter(ParameterSetName='Bump')][ValidateSet('Major','Minor','Patch')][string]$Bump='Patch',
    [Parameter(Mandatory=$true,ParameterSetName='Exact')][ValidatePattern('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$')][string]$Version
)
$ErrorActionPreference='Stop'
$path=Join-Path $PSScriptRoot 'Version.props'
[xml]$document=Get-Content -LiteralPath $path -Raw
$current=[version]$document.Project.PropertyGroup.AppVersion
if($PSCmdlet.ParameterSetName -eq 'Bump'){
    $Version=switch($Bump){
        'Major' { '{0}.0.0' -f ($current.Major+1) }
        'Minor' { '{0}.{1}.0' -f $current.Major,($current.Minor+1) }
        'Patch' { '{0}.{1}.{2}' -f $current.Major,$current.Minor,($current.Build+1) }
    }
}
if([version]$Version -le $current){throw 'The new version must be greater than the current version.'}
if($PSCmdlet.ShouldProcess($path,"Change version from $current to $Version")){
    $document.Project.PropertyGroup.AppVersion=$Version
    $document.Save($path)
    Write-Host "Version: $current -> $Version. Update CHANGELOG.md, commit, then run Build.ps1."
}
