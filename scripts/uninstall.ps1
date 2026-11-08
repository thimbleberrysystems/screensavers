<#
.SYNOPSIS
    Removes the ThimbleBerry screensaver and idle watcher for the current user.

.PARAMETER KeepSettings
    Leave %APPDATA%\ThimbleBerry\Screensavers\settings.json in place.
#>
[CmdletBinding()]
param([switch]$KeepSettings)

$ErrorActionPreference = 'Stop'
$installDir = Join-Path $env:LOCALAPPDATA 'ThimbleBerry\Screensavers'

Get-Process -Name 'ThimbleBerry.IdleWatcher', 'ThimbleBerry' -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'ThimbleBerryIdleWatcher' -ErrorAction SilentlyContinue

$desktop = 'HKCU:\Control Panel\Desktop'
$current = (Get-ItemProperty -Path $desktop -Name 'SCRNSAVE.EXE' -ErrorAction SilentlyContinue).'SCRNSAVE.EXE'
if ($current -and $current -like '*ThimbleBerry.scr') {
    Remove-ItemProperty -Path $desktop -Name 'SCRNSAVE.EXE'
    Set-ItemProperty -Path $desktop -Name 'ScreenSaveActive' -Value '0'
}

if (Test-Path $installDir) { Remove-Item -Recurse -Force $installDir }
if (-not $KeepSettings) {
    Remove-Item -Recurse -Force (Join-Path $env:APPDATA 'ThimbleBerry\Screensavers') -ErrorAction SilentlyContinue
}
Write-Host "ThimbleBerry screensaver removed."
