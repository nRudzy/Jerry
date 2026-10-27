param([switch]$Background)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $projectRoot 'dist\windows-x64'
$executable = Join-Path $destination 'Jerry.exe'
if (!(Test-Path $executable)) {
    & (Join-Path $PSScriptRoot 'build-windows.ps1')
}
$launch = @{ FilePath = $executable; WorkingDirectory = $destination }
if ($Background) { $launch.ArgumentList = '--background' }
Start-Process @launch
