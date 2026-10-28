param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.2.0',
    [string]$CompilerPath = '',
    [string]$VcRedistDirectory = '',
    [switch]$SkipAppBuild,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$jerryRoot = Split-Path $PSScriptRoot -Parent
$jerryDist = Join-Path $jerryRoot 'dist'
$jerryApp = Join-Path $jerryDist 'windows-x64'
$jerryMaintenance = Join-Path $jerryDist 'maintenance-windows-x64'
$jerryDotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'

function Invoke-JerryNative([string]$Executable, [string[]]$Arguments) {
    # UseShellExecute=false also works when invoked from WSL with a restricted
    # PATHEXT, without changing any machine or process security setting.
    $jerryInfo = New-Object Diagnostics.ProcessStartInfo
    $jerryInfo.FileName = $Executable
    $jerryInfo.Arguments = ($Arguments | ForEach-Object {
        '"' + ([regex]::Replace($_, '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
    }) -join ' '
    $jerryInfo.UseShellExecute = $false
    $jerryInfo.CreateNoWindow = $true
    $jerryInfo.RedirectStandardOutput = $true
    $jerryInfo.RedirectStandardError = $true
    $jerryProcess = [Diagnostics.Process]::Start($jerryInfo)
    try {
        $jerryStdout = $jerryProcess.StandardOutput.ReadToEndAsync()
        $jerryStderr = $jerryProcess.StandardError.ReadToEndAsync()
        $jerryProcess.WaitForExit()
        if ($jerryStderr.Result) { Write-Host $jerryStderr.Result.TrimEnd() }
        if ($jerryProcess.ExitCode -ne 0) { throw "Echec de $([IO.Path]::GetFileName($Executable)) (code $($jerryProcess.ExitCode)). $($jerryStdout.Result)" }
        if ($jerryStdout.Result) { Write-Output $jerryStdout.Result.TrimEnd() }
    } finally { $jerryProcess.Dispose() }
}

function Find-JerryInnoCompiler {
    if ($CompilerPath) { return (Resolve-Path -LiteralPath $CompilerPath).Path }
    $jerryCommand = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($jerryCommand) { return $jerryCommand.Source }
    foreach ($jerryCandidate in @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe"
    )) {
        if (Test-Path -LiteralPath $jerryCandidate -PathType Leaf) { return $jerryCandidate }
    }
    throw 'Installez Inno Setup 6.7 ou plus recent, ou indiquez -CompilerPath. Aucun parametre de securite Windows ne sera modifie.'
}

function Find-JerryVcRedist {
    if ($VcRedistDirectory) { return (Resolve-Path -LiteralPath $VcRedistDirectory).Path }
    $jerryCandidates = @()
    if ($env:VCToolsRedistDir) { $jerryCandidates += Get-ChildItem (Join-Path $env:VCToolsRedistDir 'x64\Microsoft.VC*.CRT') -Directory -ErrorAction SilentlyContinue }
    $jerryVsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $jerryVsWhere) {
        $jerryVsRoots = @((Invoke-JerryNative $jerryVsWhere @('-products', '*', '-requires', 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64', '-property', 'installationPath')) -split '\r?\n' | Where-Object { $_ })
        foreach ($jerryVsRoot in $jerryVsRoots) {
            $jerryCandidates += Get-ChildItem (Join-Path $jerryVsRoot 'VC\Redist\MSVC\*\x64\Microsoft.VC*.CRT') -Directory -ErrorAction SilentlyContinue
        }
    }
    $jerryCandidate = $jerryCandidates | Where-Object { Test-Path (Join-Path $_.FullName 'msvcp140.dll') } | Sort-Object { (Get-Item (Join-Path $_.FullName 'msvcp140.dll')).VersionInfo.FileVersionRaw } -Descending | Select-Object -First 1
    if ($jerryCandidate) { return $jerryCandidate.FullName }
    throw 'Runtime Visual C++ x64 redistribuable introuvable. Installez les outils C++ Visual Studio pour la construction ou indiquez -VcRedistDirectory vers le dossier officiel Microsoft.VC*.CRT. Aucune installation globale n''est faite chez l''utilisateur.'
}

function Copy-JerryVcRuntime([string]$SourceDirectory) {
    $jerryRequired = @('msvcp140.dll', 'vcruntime140.dll', 'vcruntime140_1.dll')
    foreach ($jerryName in $jerryRequired) {
        if (-not (Test-Path -LiteralPath (Join-Path $SourceDirectory $jerryName))) { throw "Runtime Visual C++ incomplet : $jerryName absent." }
    }
    $jerryRuntimeFiles = @(Get-ChildItem -LiteralPath $SourceDirectory -Filter '*.dll' -File)
    if (-not ($jerryRuntimeFiles | Where-Object { $_.Name -ieq 'vcomp140.dll' })) {
        $jerryOpenMp = Get-ChildItem (Join-Path (Split-Path $SourceDirectory -Parent) 'Microsoft.VC*.OpenMP\vcomp140.dll') -File -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $jerryOpenMp) { throw 'Runtime OpenMP x64 vcomp140.dll absent. Fournissez le CRT et son dossier officiel frere Microsoft.VC*.OpenMP, ou ajoutez la DLL redistribuable officielle au dossier fourni.' }
        $jerryRuntimeFiles += $jerryOpenMp
    }
    $jerryManifest = @()
    foreach ($jerryFile in $jerryRuntimeFiles) {
        if ($jerryFile.Name -notmatch '^(concrt140|msvcp140(?:_[a-z0-9_]+)?|vccorlib140|vcomp140|vcruntime140(?:_[a-z0-9_]+)?)\.dll$') { throw "Fichier CRT inattendu : $($jerryFile.Name). Fournissez exclusivement les DLL x64 redistribuables officielles." }
        $jerrySignature = Get-AuthenticodeSignature -LiteralPath $jerryFile.FullName
        if ($jerrySignature.Status -ne 'Valid' -or $jerrySignature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') { throw "Signature Microsoft invalide : $($jerryFile.Name)." }
        Copy-Item -LiteralPath $jerryFile.FullName -Destination $jerryApp -Force
        $jerryManifest += [ordered]@{ file = $jerryFile.Name; version = $jerryFile.VersionInfo.FileVersion; sha256 = (Get-FileHash -LiteralPath $jerryFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    $jerryNotices = Join-Path $jerryApp 'licenses'
    New-Item -ItemType Directory -Path $jerryNotices -Force | Out-Null
    [ordered]@{
        component = 'Microsoft Visual C++ x64 Runtime'
        distribution = 'Official Visual Studio VC/Redist/MSVC x64 CRT and OpenMP; app-local redistribution'
        license = 'https://visualstudio.microsoft.com/license-terms/'
        redistribution = 'https://learn.microsoft.com/visualstudio/releases/2022/redistribution'
        files = $jerryManifest
    } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $jerryNotices 'visual-cpp-runtime.json') -Encoding UTF8
}

function Copy-JerryDependencyNotices {
    $jerryAssetsPath = Join-Path $jerryRoot 'src\Jerry.App\obj\project.assets.json'
    if (-not (Test-Path -LiteralPath $jerryAssetsPath)) { throw 'Metadonnees NuGet absentes : construisez l''application avant de l''empaqueter.' }
    $jerryAssets = Get-Content -LiteralPath $jerryAssetsPath -Raw | ConvertFrom-Json
    $jerryPackages = @{}
    foreach ($jerryLibrary in $jerryAssets.libraries.PSObject.Properties) {
        if ($jerryLibrary.Value.type -eq 'package') { $jerryPackages[$jerryLibrary.Value.path] = $true }
    }
    foreach ($jerryFramework in $jerryAssets.project.frameworks.PSObject.Properties) {
        foreach ($jerryDependency in $jerryFramework.Value.downloadDependencies) {
            if ($jerryDependency.name -match '^Microsoft\.(NETCore|WindowsDesktop)\.App\.Runtime\.win-x64$') {
                $jerryPackageVersion = $jerryDependency.version.Trim([char[]]'[]').Split(',')[0].Trim()
                $jerryPackages[$jerryDependency.name.ToLowerInvariant() + '/' + $jerryPackageVersion] = $true
            }
        }
    }
    foreach ($jerryPackage in $jerryPackages.Keys) {
        $jerryPackageRoot = $null
        foreach ($jerryFolder in $jerryAssets.packageFolders.PSObject.Properties.Name) {
            $jerryPath = Join-Path $jerryFolder $jerryPackage
            if (Test-Path -LiteralPath $jerryPath -PathType Container) { $jerryPackageRoot = $jerryPath; break }
        }
        if (-not $jerryPackageRoot) { throw "Source des licences NuGet introuvable : $jerryPackage." }
        $jerryTarget = Join-Path (Join-Path $jerryApp 'licenses') ($jerryPackage.Replace('/', '-'))
        New-Item -ItemType Directory -Path $jerryTarget -Force | Out-Null
        Get-ChildItem -LiteralPath $jerryPackageRoot -File | Where-Object { $_.Name -match '(?i)license|notice|\.nuspec$' } | Copy-Item -Destination $jerryTarget -Force
    }
    # Desktop's runtime pack omits the repository's third-party notice file.
    $jerrySdkVersion = (Invoke-JerryNative $jerryDotnet @('--version')).Trim()
    $jerryDesktopNotices = Join-Path (Split-Path $jerryDotnet -Parent) "sdk\$jerrySdkVersion\Sdks\Microsoft.NET.Sdk.WindowsDesktop\THIRD-PARTY-NOTICES.TXT"
    if (-not (Test-Path -LiteralPath $jerryDesktopNotices)) { throw 'Notices tierces WindowsDesktop introuvables dans le SDK .NET.' }
    Copy-Item -LiteralPath $jerryDesktopNotices -Destination (Join-Path $jerryApp 'licenses\WindowsDesktop-THIRD-PARTY-NOTICES.TXT') -Force
    foreach ($jerryRequired in @('Microsoft.NETCore.App.Runtime.win-x64', 'Microsoft.WindowsDesktop.App.Runtime.win-x64')) {
        if (-not (Get-ChildItem (Join-Path $jerryApp 'licenses') -Directory | Where-Object { $_.Name.StartsWith($jerryRequired, [StringComparison]::OrdinalIgnoreCase) })) { throw "Licence du runtime absente : $jerryRequired." }
    }
}

Push-Location $jerryRoot
try {
    if (-not (Test-Path -LiteralPath $jerryDotnet)) { throw 'Le SDK .NET 10 est necessaire uniquement sur la machine de construction.' }
    $jerryCompiler = Find-JerryInnoCompiler
    $jerryVcSource = Find-JerryVcRedist
    if (-not $SkipAppBuild) {
        # These two directories contain only generated publish output.
        foreach ($jerryOutput in @($jerryApp, $jerryMaintenance)) {
            if (Test-Path -LiteralPath $jerryOutput) { Remove-Item -LiteralPath $jerryOutput -Recurse -Force }
        }
        & (Join-Path $PSScriptRoot 'build-windows.ps1') -OutputDirectory $jerryApp -Version $Version -SkipTests:$SkipTests
        Invoke-JerryNative $jerryDotnet @('publish', (Join-Path $jerryRoot 'src\Jerry.Maintenance\Jerry.Maintenance.csproj'), '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=None', "-p:Version=$Version", '-o', $jerryMaintenance, '--nologo', '--verbosity', 'minimal')
    }
    foreach ($jerryExpected in @('Jerry.exe', 'Jerry.dll', 'Jerry.runtimeconfig.json', 'coreclr.dll', 'PresentationFramework.dll', 'Whisper.net.dll', 'runtimes\win-x64\whisper.dll', 'Assets\Fonts\Montserrat-OFL.txt', 'Assets\Fonts\Oswald-OFL.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $jerryApp $jerryExpected) -PathType Leaf)) { throw "Publication autonome incomplete : $jerryExpected." }
    }
    $jerryRuntimeConfig = Get-Content (Join-Path $jerryApp 'Jerry.runtimeconfig.json') -Raw | ConvertFrom-Json
    if (-not $jerryRuntimeConfig.runtimeOptions.includedFrameworks) { throw 'La publication doit etre autonome (.NET inclus).' }
    $jerryActualVersion = (Get-Item -LiteralPath (Join-Path $jerryApp 'Jerry.exe')).VersionInfo.ProductVersion.Split('+')[0]
    if ($jerryActualVersion -ne $Version) { throw "Version publiee $jerryActualVersion differente de la livraison $Version." }
    $jerryHelper = Join-Path $jerryMaintenance 'Jerry.Maintenance.exe'
    if (-not (Test-Path -LiteralPath $jerryHelper -PathType Leaf)) { throw 'Auxiliaire de maintenance autonome absent.' }
    Copy-Item -LiteralPath $jerryHelper -Destination $jerryApp -Force
    Copy-Item -LiteralPath (Join-Path $jerryRoot 'README.md'), (Join-Path $jerryRoot 'THIRD_PARTY_NOTICES.md') -Destination $jerryApp -Force
    Copy-JerryDependencyNotices
    Copy-JerryVcRuntime $jerryVcSource
    $jerryPayloadFiles = @(Get-ChildItem -LiteralPath $jerryApp -Recurse -File | Where-Object { $_.FullName -ne (Join-Path $jerryApp 'payload-manifest.json') } | Sort-Object FullName | ForEach-Object {
        [ordered]@{
            path = $_.FullName.Substring($jerryApp.Length + 1).Replace('\', '/')
            bytes = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
    [ordered]@{ version = $Version; runtime = 'win-x64'; files = $jerryPayloadFiles } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $jerryApp 'payload-manifest.json') -Encoding UTF8
    # The entire publish directory is the only payload shared by ZIP and Setup.
    Invoke-JerryNative $jerryCompiler @("/DAppVersion=$Version", "/DAppPublishDir=$jerryApp", "/DMaintenancePublishDir=$jerryMaintenance", "/O$jerryDist", (Join-Path $jerryRoot 'installer\Jerry.iss'))
    $jerryInstaller = Join-Path $jerryDist "Jerry-Setup-$Version.exe"
    if (-not (Test-Path -LiteralPath $jerryInstaller)) { throw 'Inno Setup n''a pas produit l''installeur attendu.' }
    $jerryArchive = Join-Path $jerryDist "Jerry-Windows-x64-$Version.zip"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path -LiteralPath $jerryArchive) { Remove-Item -LiteralPath $jerryArchive -Force }
    [IO.Compression.ZipFile]::CreateFromDirectory($jerryApp, $jerryArchive, [IO.Compression.CompressionLevel]::Optimal, $false)
    $jerrySums = foreach ($jerryArtifact in @($jerryInstaller, $jerryArchive)) {
        $jerryHash = (Get-FileHash -LiteralPath $jerryArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
        $jerryHash + '  ' + [IO.Path]::GetFileName($jerryArtifact)
    }
    [IO.File]::WriteAllLines((Join-Path $jerryDist 'SHA256SUMS.txt'), [string[]]$jerrySums, (New-Object System.Text.UTF8Encoding($false)))
    Write-Output "Installeur : $jerryInstaller"
    Write-Output "Archive portable : $jerryArchive"
    Write-Output ('Empreintes : ' + (Join-Path $jerryDist 'SHA256SUMS.txt'))
} finally { Pop-Location }
