param(
    [switch]$SkipTests,
    [switch]$TestOnly,
    [switch]$FrameworkDependent
)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$projectRoot = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (!(Test-Path $dotnet)) { throw 'Installez le SDK .NET 10 pour construire Jerry.' }
function Invoke-JerryDotnet([string[]]$BuildArguments) {
    $commandLine = ($BuildArguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }) -join ' '
    $buildProcess = Start-Process -FilePath $dotnet -ArgumentList $commandLine -Wait -NoNewWindow -PassThru
    if ($buildProcess.ExitCode -ne 0) { throw "La commande .NET a échoué (code $($buildProcess.ExitCode))." }
}
Push-Location $projectRoot
try {
    if (!$SkipTests) {
        Invoke-JerryDotnet @('test', (Join-Path $projectRoot 'tests\Jerry.Core.Tests\Jerry.Core.Tests.csproj'), '-c', 'Release', '--nologo', '--verbosity', 'minimal')
    }
    if ($TestOnly) { exit 0 }
    $destination = Join-Path $projectRoot 'dist\windows-x64'
    $selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
    Invoke-JerryDotnet @('publish', (Join-Path $projectRoot 'src\Jerry.App\Jerry.App.csproj'), '-c', 'Release', '-r', 'win-x64', '--self-contained', $selfContained, '-p:PublishSingleFile=false', '-o', $destination, '--nologo', '--verbosity', 'minimal')
    Write-Host ('Application prête : ' + (Join-Path $destination 'Jerry.exe'))
} finally { Pop-Location }
