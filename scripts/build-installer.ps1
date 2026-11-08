<#
.SYNOPSIS
    Builds the ThimbleBerry Screensavers installer (Setup .exe).

.DESCRIPTION
    Publishes the screensaver and idle watcher as self-contained x64 apps (no .NET install
    needed on target PCs), sharing one copy of the runtime, then compiles
    installer\ThimbleBerry.Screensavers.iss with Inno Setup.

    Output: artifacts\installer\ThimbleBerry-Screensavers-Setup-<version>.exe

    Needs the .NET 10 SDK and Inno Setup 6 (winget install JRSoftware.InnoSetup).
#>
[CmdletBinding()]
param([string]$Iscc)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$staging = Join-Path $repo 'artifacts\installer-staging'
$output = Join-Path $repo 'artifacts\installer'

# Version comes from Directory.Build.props.
[xml]$props = Get-Content (Join-Path $repo 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { $version = '0.1.0' }

if (-not $Iscc) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    $Iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $Iscc) { throw 'Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup' }
}

Write-Host "Publishing $version (self-contained, win-x64)..."
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
foreach ($proj in 'src\Saver\ThimbleBerry.Saver.csproj', 'src\Watcher\ThimbleBerry.IdleWatcher.csproj') {
    dotnet publish (Join-Path $repo $proj) -c Release -r win-x64 -p:SelfContained=true `
        -p:DebugType=none -o $staging --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $proj" }
}

# Windows only lists screensavers with the .scr extension.
Move-Item (Join-Path $staging 'ThimbleBerry.exe') (Join-Path $staging 'ThimbleBerry.scr') -Force

Write-Host "Compiling installer..."
& $Iscc "/DAppVersion=$version" "/DStagingDir=$staging" "/DOutputDir=$output" `
    (Join-Path $repo 'installer\ThimbleBerry.Screensavers.iss') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compile failed' }

$setup = Join-Path $output "ThimbleBerry-Screensavers-Setup-$version.exe"
Write-Host ""
Write-Host ("Built {0} ({1:N1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB))
