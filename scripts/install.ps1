<#
.SYNOPSIS
    Builds and installs the ThimbleBerry screensaver for the current user (no admin needed).

.DESCRIPTION
    - Publishes the screensaver and the idle watcher to %LOCALAPPDATA%\ThimbleBerry\Screensavers
    - Registers ThimbleBerry.scr as the Windows screensaver for this user
    - Starts the idle watcher now and at every sign-in, so the screensaver still starts while
      Caffeine, PowerToys Awake or similar tools keep the PC awake

.PARAMETER IdleMinutes
    Minutes of real inactivity before the screensaver starts. Default 10.

.PARAMETER SelfContained
    Bundle the .NET runtime (bigger files, but runs on PCs without .NET 10 installed).

.PARAMETER NoWatcher
    Skip the idle watcher and rely on Windows' own screensaver timer only.
#>
[CmdletBinding()]
param(
    [int]$IdleMinutes = 10,
    [switch]$SelfContained,
    [switch]$NoWatcher
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$installDir = Join-Path $env:LOCALAPPDATA 'ThimbleBerry\Screensavers'
$publishDir = Join-Path $repo 'artifacts\publish'
$rid = if ([Environment]::Is64BitOperatingSystem) { if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'win-arm64' } else { 'win-x64' } } else { 'win-x86' }

Write-Host "Publishing ($rid)..."
$sc = if ($SelfContained) { 'true' } else { 'false' }
foreach ($proj in 'src\Saver\ThimbleBerry.Saver.csproj', 'src\Watcher\ThimbleBerry.IdleWatcher.csproj') {
    dotnet publish (Join-Path $repo $proj) -c Release -r $rid "-p:SelfContained=$sc" `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishDir --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $proj" }
}

Write-Host "Installing to $installDir"
Get-Process -Name 'ThimbleBerry.IdleWatcher' -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name 'ThimbleBerry' -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item (Join-Path $publishDir 'ThimbleBerry.exe') (Join-Path $installDir 'ThimbleBerry.scr') -Force
Copy-Item (Join-Path $publishDir 'ThimbleBerry.IdleWatcher.exe') $installDir -Force

$scr = Join-Path $installDir 'ThimbleBerry.scr'
$watcher = Join-Path $installDir 'ThimbleBerry.IdleWatcher.exe'

# Save the idle time into the shared settings file (keeps other settings if it already exists).
$settingsDir = Join-Path $env:APPDATA 'ThimbleBerry\Screensavers'
$settingsPath = Join-Path $settingsDir 'settings.json'
New-Item -ItemType Directory -Force -Path $settingsDir | Out-Null
$settings = if (Test-Path $settingsPath) { Get-Content $settingsPath -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
$settings | Add-Member -NotePropertyName IdleMinutes -NotePropertyValue $IdleMinutes -Force
$settings | ConvertTo-Json | Set-Content -Path $settingsPath -Encoding utf8

# Register as the Windows screensaver for this user.
$desktop = 'HKCU:\Control Panel\Desktop'
Set-ItemProperty -Path $desktop -Name 'SCRNSAVE.EXE' -Value $scr
Set-ItemProperty -Path $desktop -Name 'ScreenSaveActive' -Value '1'
Set-ItemProperty -Path $desktop -Name 'ScreenSaveTimeOut' -Value ([string]($IdleMinutes * 60))

Add-Type -Namespace TB -Name Spi -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool SystemParametersInfo(uint action, uint param, System.IntPtr pv, uint winIni);
'@
[TB.Spi]::SystemParametersInfo(0x0011, 1, [IntPtr]::Zero, 3) | Out-Null                    # SPI_SETSCREENSAVEACTIVE
[TB.Spi]::SystemParametersInfo(0x000F, [uint32]($IdleMinutes * 60), [IntPtr]::Zero, 3) | Out-Null # SPI_SETSCREENSAVETIMEOUT

$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if ($NoWatcher) {
    Remove-ItemProperty -Path $run -Name 'ThimbleBerryIdleWatcher' -ErrorAction SilentlyContinue
} else {
    Set-ItemProperty -Path $run -Name 'ThimbleBerryIdleWatcher' -Value "`"$watcher`""
    Start-Process $watcher
}

Write-Host ""
Write-Host "Installed. Screensaver: $scr"
if (-not $NoWatcher) { Write-Host "Idle watcher is running (berry icon in the tray) and starts at sign-in." }
Write-Host "Change settings: right-click the tray icon > Settings, or Windows Screen Saver Settings."
