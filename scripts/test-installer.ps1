param(
    [Parameter(Mandatory = $true)][string]$InstallerPath,
    [string]$PublishedAppPath = '',
    [string]$OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'

# This guard must precede every filesystem, registry, process or UI mutation.
# The lifecycle below deliberately uses Jerry's real, per-user data directory.
if ($env:GITHUB_ACTIONS -cne 'true' -or $env:RUNNER_OS -cne 'Windows' -or
    $env:RUNNER_ENVIRONMENT -cne 'github-hosted') {
    throw 'Ce test destructif est reserve exclusivement aux runners Windows GitHub heberges et jetables. Aucune modification effectuee.'
}

$jerryId = '{6712D2EC-8CDA-4B02-8D4A-4E61C3FE26C7}_is1'
$jerryUninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $jerryId
$jerryAppPathsKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\Jerry.exe'
$jerryRunKey = 'Software\Microsoft\Windows\CurrentVersion\Run'
$jerryApprovedKey = 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'
$jerryData = Join-Path $env:LOCALAPPDATA 'Jerry'
$jerryDefaultApp = Join-Path $env:LOCALAPPDATA 'Programs\Jerry'
$jerryStartMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'Jerry.lnk'
$jerryDesktop = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Jerry.lnk'

function Get-JerryRegistryValue([string]$Key, [string]$Name = 'Jerry') {
    $jerryRegistry = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($Key)
    if (-not $jerryRegistry) { return $null }
    try { return $jerryRegistry.GetValue($Name, $null) } finally { $jerryRegistry.Dispose() }
}

# A hosted runner is necessary but not sufficient: never replace pre-existing
# user state, even when environment variables have been set by a caller.
foreach ($jerryExisting in @($jerryData, $jerryDefaultApp, $jerryUninstallKey, $jerryAppPathsKey, $jerryStartMenu, $jerryDesktop,
    'HKLM:\Software\Microsoft\Windows\CurrentVersion\App Paths\Jerry.exe',
    ('HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $jerryId))) {
    if (Test-Path -LiteralPath $jerryExisting) { throw "Profil non vierge : $jerryExisting existe deja. Aucune modification effectuee." }
}
if ($null -ne (Get-JerryRegistryValue $jerryRunKey) -or $null -ne (Get-JerryRegistryValue $jerryApprovedKey)) {
    throw 'Une preference Windows Jerry existe deja. Aucune modification effectuee.'
}
if (Get-Process -Name 'Jerry', 'Jerry.Maintenance' -ErrorAction SilentlyContinue) {
    throw 'Un processus Jerry existe deja. Aucune modification effectuee.'
}
foreach ($jerryMutexName in @('Local\Jerry.CursorStudio', 'Local\Jerry.CursorStudio.Maintenance')) {
    try { $jerryExistingMutex = [Threading.Mutex]::OpenExisting($jerryMutexName) }
    catch [Threading.WaitHandleCannotBeOpenedException] { continue }
    $jerryExistingMutex.Dispose()
    throw "Un mutex Jerry existe deja : $jerryMutexName. Aucune modification effectuee."
}

$jerryRoot = Split-Path $PSScriptRoot -Parent
$InstallerPath = (Resolve-Path -LiteralPath $InstallerPath).Path
if (-not $PublishedAppPath) { $PublishedAppPath = Join-Path $jerryRoot 'dist\windows-x64' }
$PublishedAppPath = (Resolve-Path -LiteralPath $PublishedAppPath).Path
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $jerryRoot 'dist\installer-test' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$jerryManifest = Get-Content (Join-Path $PublishedAppPath 'payload-manifest.json') -Raw | ConvertFrom-Json
if ($jerryManifest.runtime -cne 'win-x64' -or $jerryManifest.files.Count -lt 30) { throw 'Manifeste de publication invalide.' }
$jerryScratch = Join-Path $env:RUNNER_TEMP ('jerry-installer-' + [guid]::NewGuid().ToString('N'))
$jerryApp = Join-Path $jerryScratch 'Application Jerry été'
$jerryCaptures = Join-Path $jerryScratch 'Captures conservées'
$jerryFixtureHashes = @{}
$jerryOwnedApp = $null
$jerryReport = New-Object 'System.Collections.Generic.List[string]'

# All mutations start here, after both the hosted-runner and clean-profile guards.
New-Item -ItemType Directory -Path $OutputDirectory, $jerryScratch, $jerryData, $jerryCaptures -Force | Out-Null
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)

function Assert-Jerry([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $jerryReport.Add('PASS: ' + $Message)
    Write-Output ('PASS: ' + $Message)
}
function Get-JerryStartup { return Get-JerryRegistryValue $jerryRunKey }
function Invoke-JerryHelper([string]$Argument, [int]$Expected = 0, [string]$Helper = '') {
    if (-not $Helper) { $Helper = Join-Path $jerryApp 'Jerry.Maintenance.exe' }
    $jerryProcess = Start-Process -FilePath $Helper -ArgumentList $Argument -Wait -PassThru
    Assert-Jerry ($jerryProcess.ExitCode -eq $Expected) ("Helper $Argument : code $Expected (obtenu $($jerryProcess.ExitCode))")
}
function Invoke-JerrySetup([string]$TaskArgument = '', [switch]$ExpectRefusal) {
    $jerryLog = Join-Path $OutputDirectory ('setup-' + [guid]::NewGuid().ToString('N') + '.log')
    $jerryArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR=`"$jerryApp`" /LOG=`"$jerryLog`" $TaskArgument"
    $jerryProcess = Start-Process -FilePath $InstallerPath -ArgumentList $jerryArguments -PassThru
    if (-not $jerryProcess.WaitForExit(120000)) { throw "Installation bloquee ; journaux : $jerryLog" }
    if ($ExpectRefusal) {
        Assert-Jerry ($jerryProcess.ExitCode -ne 0) 'Installation refusee pour une ancienne instance active'
    } elseif ($jerryProcess.ExitCode -ne 0) {
        Get-Content -LiteralPath $jerryLog -Tail 60
        throw "Installation echouee : code $($jerryProcess.ExitCode)."
    }
}
function Assert-JerryData {
    foreach ($jerryFile in $jerryFixtureHashes.Keys) {
        Assert-Jerry (Test-Path -LiteralPath $jerryFile) ("Donnee conservee : $jerryFile")
        Assert-Jerry ((Get-FileHash -LiteralPath $jerryFile -Algorithm SHA256).Hash -ceq $jerryFixtureHashes[$jerryFile]) ("Empreinte conservee : $jerryFile")
    }
    $jerrySettings = Get-Content (Join-Path $jerryData 'settings.json') -Raw | ConvertFrom-Json
    Assert-Jerry ($jerrySettings.UpdateRate -eq 47 -and -not $jerrySettings.Enabled -and $jerrySettings.MinimizeToTray) 'Preferences existantes conservees'
    Assert-Jerry ($jerrySettings.Recording.OutputDirectory -ceq $jerryCaptures) 'Dossier de capture conserve'
    Assert-Jerry ($jerrySettings.FutureSetting.marker -ceq 'préférence inconnue' -and ($jerrySettings.FutureSetting.values -join ',') -ceq '1,7,42') 'Preference JSON inconnue conservee'
}
function Assert-JerryStartup([bool]$Enabled) {
    $jerrySettings = Get-Content (Join-Path $jerryData 'settings.json') -Raw | ConvertFrom-Json
    Assert-Jerry ($jerrySettings.LaunchAtLogin -eq $Enabled) 'Preference LaunchAtLogin synchronisee'
    if ($Enabled) {
        Assert-Jerry ((Get-JerryStartup) -ceq ('"' + (Join-Path $jerryApp 'Jerry.exe') + '" --background')) 'Demarrage Windows vers la copie installee'
        Invoke-JerryHelper '--startup-status' 20
    } else {
        Assert-Jerry ($null -eq (Get-JerryStartup)) 'Aucune entree Run Jerry quand le demarrage est desactive'
        Invoke-JerryHelper '--startup-status' 0
    }
}
function Assert-JerryPayload {
    foreach ($jerryEntry in $jerryManifest.files) {
        $jerryInstalledFile = Join-Path $jerryApp $jerryEntry.path.Replace('/', '\')
        if (-not (Test-Path -LiteralPath $jerryInstalledFile -PathType Leaf) -or
            (Get-Item -LiteralPath $jerryInstalledFile).Length -ne $jerryEntry.bytes -or
            (Get-FileHash -LiteralPath $jerryInstalledFile -Algorithm SHA256).Hash.ToLowerInvariant() -cne $jerryEntry.sha256) {
            throw "Charge utile installee differente : $($jerryEntry.path)."
        }
    }
    Assert-Jerry $true ("Publication complete verifiee : $($jerryManifest.files.Count) fichiers")
}
function Start-JerryIdle {
    $jerryProcess = Start-Process -FilePath (Join-Path $jerryApp 'Jerry.exe') -ArgumentList '--background' -PassThru
    $jerryUntil = [DateTime]::UtcNow.AddSeconds(25)
    while ([DateTime]::UtcNow -lt $jerryUntil) {
        if ($jerryProcess.HasExited) { throw "Jerry a quitte pendant son lancement (code $($jerryProcess.ExitCode))." }
        try { $jerryInstance = [Threading.Mutex]::OpenExisting('Local\Jerry.CursorStudio'); $jerryInstance.Dispose(); return $jerryProcess }
        catch [Threading.WaitHandleCannotBeOpenedException] { Start-Sleep -Milliseconds 100 }
    }
    throw 'Jerry n''a pas publie son mutex d''instance.'
}

Add-Type @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
public static class JerryInstallerProbe {
    private delegate bool EnumProc(IntPtr window, IntPtr unused);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr unused);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern IntPtr GetDlgItem(IntPtr window, int id);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool SetDefaultDllDirectories(uint flags);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern IntPtr AddDllDirectory(string directory);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, uint flags);
    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool FreeLibrary(IntPtr module);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] private static extern uint GetModuleFileName(IntPtr module, StringBuilder path, int size);
    [DllImport("kernel32.dll", CharSet=CharSet.Ansi, ExactSpelling=true)] private static extern IntPtr GetProcAddress(IntPtr module, string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr WhisperVersion();
    public static bool CancelConfirmation(int[] allowedProcessIds) {
        var allowed = new HashSet<int>(allowedProcessIds);
        bool result = false;
        EnumWindows((window, unused) => {
            uint processId; GetWindowThreadProcessId(window, out processId);
            if (!allowed.Contains((int)processId)) return true;
            var title = new StringBuilder(256); var kind = new StringBuilder(64);
            GetWindowText(window, title, title.Capacity); GetClassName(window, kind, kind.Capacity);
            if (title.ToString().IndexOf("Jerry", StringComparison.OrdinalIgnoreCase) < 0 || kind.ToString() != "#32770") return true;
            var no = GetDlgItem(window, 7); if (no == IntPtr.Zero) return true;
            result = PostMessage(window, 0x111, (IntPtr)7, no); return false;
        }, IntPtr.Zero);
        return result;
    }
    public static string LoadWhisper(string applicationDirectory, string nativeDirectory) {
        // Search only explicit application/native locations and Windows system
        // libraries, so PATH/Visual Studio cannot conceal a missing dependency.
        if (!SetDefaultDllDirectories(0xC00) || AddDllDirectory(applicationDirectory) == IntPtr.Zero || AddDllDirectory(nativeDirectory) == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var modules = new List<IntPtr>();
        try {
            foreach (var name in new[] {"vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "vcomp140.dll"}) {
                string full = System.IO.Path.Combine(applicationDirectory, name);
                IntPtr module = LoadLibraryEx(full, IntPtr.Zero, 0xD00);
                if (module == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), full);
                modules.Add(module);
                var actual = new StringBuilder(32768); GetModuleFileName(module, actual, actual.Capacity);
                if (!String.Equals(full, actual.ToString(), StringComparison.OrdinalIgnoreCase)) throw new Exception("CRT loaded outside application: " + actual);
            }
            string whisper = System.IO.Path.Combine(nativeDirectory, "whisper.dll");
            IntPtr native = LoadLibraryEx(whisper, IntPtr.Zero, 0xD00);
            if (native == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), whisper);
            modules.Add(native);
            IntPtr symbol = GetProcAddress(native, "whisper_version");
            if (symbol == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "whisper_version");
            return Marshal.PtrToStringAnsi(((WhisperVersion)Marshal.GetDelegateForFunctionPointer(symbol, typeof(WhisperVersion)))());
        } finally { for (int i = modules.Count - 1; i >= 0; i--) FreeLibrary(modules[i]); }
    }
}
'@

try {
    $jerrySettingsFixture = [ordered]@{
        LaunchAtLogin = $false; Enabled = $false; MinimizeToTray = $true; UpdateRate = 47
        Recording = [ordered]@{ OutputDirectory = $jerryCaptures }
        FutureSetting = [ordered]@{ marker = 'préférence inconnue'; values = @(1, 7, 42) }
    }
    [IO.File]::WriteAllText((Join-Path $jerryData 'settings.json'), ($jerrySettingsFixture | ConvertTo-Json -Depth 5), (New-Object System.Text.UTF8Encoding($false)))
    foreach ($jerryRelative in @('sessions\recoverable-session.keep', 'models\whisper-model.keep', 'tools\ffmpeg\existing-tool.keep')) {
        $jerryFile = Join-Path $jerryData $jerryRelative
        New-Item -ItemType Directory -Path (Split-Path $jerryFile -Parent) -Force | Out-Null
        [IO.File]::WriteAllBytes($jerryFile, [byte[]](0, 7, 42, 128, 255))
        $jerryFixtureHashes[$jerryFile] = (Get-FileHash -LiteralPath $jerryFile -Algorithm SHA256).Hash
    }
    $jerryCapture = Join-Path $jerryCaptures 'capture à conserver.keep'
    [IO.File]::WriteAllBytes($jerryCapture, [byte[]](6, 5, 4, 3, 2, 1, 0))
    $jerryFixtureHashes[$jerryCapture] = (Get-FileHash -LiteralPath $jerryCapture -Algorithm SHA256).Hash

    # The temporary maintenance payload must run independently of the main app.
    $jerryHelperOnly = Join-Path $jerryScratch 'Helper indépendant'
    New-Item -ItemType Directory -Path $jerryHelperOnly | Out-Null
    Copy-Item -LiteralPath (Join-Path $PublishedAppPath 'Jerry.Maintenance.exe') -Destination $jerryHelperOnly
    Invoke-JerryHelper '--startup-status' 0 (Join-Path $jerryHelperOnly 'Jerry.Maintenance.exe')

    Invoke-JerrySetup
    Assert-JerryPayload
    Assert-JerryStartup $false
    Assert-JerryData
    Assert-Jerry ((Get-ItemProperty -LiteralPath $jerryUninstallKey).DisplayName -ceq 'Jerry') 'Jerry enregistre dans Applications installees'
    Assert-Jerry ((Get-Item -LiteralPath $jerryAppPathsKey).GetValue('') -ceq (Join-Path $jerryApp 'Jerry.exe')) 'App Paths vers la copie installee'
    Assert-Jerry (Test-Path -LiteralPath $jerryStartMenu) 'Raccourci menu Demarrer present'
    $jerryShell = New-Object -ComObject WScript.Shell
    $jerryShortcut = $jerryShell.CreateShortcut($jerryStartMenu)
    Assert-Jerry ($jerryShortcut.TargetPath -ceq (Join-Path $jerryApp 'Jerry.exe') -and $jerryShortcut.Arguments -ceq '') 'Raccourci menu Demarrer correct'
    $jerryWhisper = [JerryInstallerProbe]::LoadWhisper($jerryApp, (Join-Path $jerryApp 'runtimes\win-x64'))
    Assert-Jerry (-not [string]::IsNullOrWhiteSpace($jerryWhisper)) ("Whisper natif et CRT locaux charges : $jerryWhisper")

    Invoke-JerrySetup '/TASKS=startup'
    Assert-JerryStartup $true
    Invoke-JerrySetup
    Assert-JerryStartup $true
    Invoke-JerrySetup '/MERGETASKS=desktopicon'
    Assert-JerryStartup $true
    Assert-Jerry (Test-Path -LiteralPath $jerryDesktop) 'MERGETASKS conserve le demarrage et ajoute le Bureau'
    Invoke-JerrySetup '/MERGETASKS=!startup'
    Assert-JerryStartup $false
    Invoke-JerrySetup
    Assert-JerryStartup $false
    Invoke-JerrySetup '/MERGETASKS=startup'
    Assert-JerryStartup $true
    Invoke-JerrySetup '/TASKS=desktopicon'
    Assert-JerryStartup $false
    Invoke-JerrySetup '/TASKS=startup'
    Assert-JerryStartup $true
    Assert-JerryData

    # Task Manager may disable startup while keeping the Run entry itself.
    $jerryApproved = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($jerryApprovedKey)
    try { $jerryApproved.SetValue('Jerry', [byte[]](3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), [Microsoft.Win32.RegistryValueKind]::Binary) }
    finally { $jerryApproved.Dispose() }
    Invoke-JerryHelper '--startup-status' 0
    Invoke-JerrySetup
    Assert-JerryStartup $false
    Invoke-JerrySetup '/TASKS=startup'
    Assert-JerryStartup $true

    # Refuse an old instance that owns the established mutex but has no IPC.
    $jerryCreated = $false
    $jerryLegacy = [Threading.Mutex]::new($true, 'Local\Jerry.CursorStudio', [ref]$jerryCreated)
    try {
        Assert-Jerry $jerryCreated 'Ancienne instance de test isolee'
        $jerrySettingsHash = (Get-FileHash (Join-Path $jerryData 'settings.json')).Hash
        $jerryStartup = Get-JerryStartup
        $jerryBlockedLaunch = Start-Process -FilePath (Join-Path $jerryApp 'Jerry.exe') -ArgumentList '--background' -PassThru
        Assert-Jerry ($jerryBlockedLaunch.WaitForExit(15000) -and $jerryBlockedLaunch.ExitCode -eq 0) 'Lancement empeche pendant la reservation du mutex historique'
        Assert-Jerry ((Get-FileHash (Join-Path $jerryData 'settings.json')).Hash -ceq $jerrySettingsHash) 'Aucun service ni sauvegarde pendant un lancement reserve'
        Invoke-JerrySetup -ExpectRefusal
        Assert-JerryPayload
        Assert-Jerry ((Get-JerryStartup) -ceq $jerryStartup -and (Get-FileHash (Join-Path $jerryData 'settings.json')).Hash -ceq $jerrySettingsHash) 'Refus sans modification du demarrage ni des preferences'
        $jerryRefusedUninstall = Start-Process -FilePath (Join-Path $jerryApp 'unins000.exe') -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="' + (Join-Path $OutputDirectory 'uninstall-refused.log') + '"') -PassThru
        Assert-Jerry ($jerryRefusedUninstall.WaitForExit(30000) -and $jerryRefusedUninstall.ExitCode -ne 0) 'Desinstallation refusee si la preparation ne libere pas l ancienne instance'
        Assert-Jerry (Test-Path -LiteralPath $jerryUninstallKey) 'Refus de desinstallation avant suppression des inscriptions'
        Assert-JerryPayload
        Assert-Jerry ((Get-JerryStartup) -ceq $jerryStartup -and (Get-FileHash (Join-Path $jerryData 'settings.json')).Hash -ceq $jerrySettingsHash) 'Refus de desinstallation sans changement des preferences ni du demarrage'
    } finally { if ($jerryCreated) { $jerryLegacy.ReleaseMutex() }; $jerryLegacy.Dispose() }

    $jerryOwnedApp = Start-JerryIdle
    # Let the real application's IPC server finish initialization.
    Start-Sleep -Seconds 2
    Invoke-JerryHelper '--configure-startup=disable' 10
    Assert-Jerry (-not $jerryOwnedApp.HasExited) 'Configuration de demarrage refusee sans fermer une instance active'
    $jerryStartup = Get-JerryStartup
    $jerrySettingsHash = (Get-FileHash (Join-Path $jerryData 'settings.json')).Hash
    $jerryCancel = Start-Process -FilePath (Join-Path $jerryApp 'unins000.exe') -ArgumentList ('/NORESTART /LOG="' + (Join-Path $OutputDirectory 'uninstall-cancel.log') + '"') -PassThru
    $jerryAllowedIds = New-Object 'System.Collections.Generic.HashSet[int]'
    [void]$jerryAllowedIds.Add($jerryCancel.Id)
    $jerryCancelled = $false
    $jerryUntil = [DateTime]::UtcNow.AddSeconds(25)
    while ([DateTime]::UtcNow -lt $jerryUntil) {
        # Follow only children of the exact uninstaller launched above. Inno's
        # loader may move the confirmation into a temporary child executable.
        foreach ($jerryChild in Get-CimInstance Win32_Process) {
            if ($jerryAllowedIds.Contains([int]$jerryChild.ParentProcessId) -and
                $jerryChild.ExecutablePath -and $jerryChild.ExecutablePath.StartsWith($env:TEMP + '\', [StringComparison]::OrdinalIgnoreCase)) {
                [void]$jerryAllowedIds.Add([int]$jerryChild.ProcessId)
            }
        }
        if ([JerryInstallerProbe]::CancelConfirmation([int[]]@($jerryAllowedIds))) { $jerryCancelled = $true; break }
        Start-Sleep -Milliseconds 100
    }
    Assert-Jerry $jerryCancelled 'Annulation de la confirmation de notre desinstallateur'
    Assert-Jerry ($jerryCancel.WaitForExit(15000)) 'Desinstallateur annule termine'
    Start-Sleep -Milliseconds 500
    Assert-Jerry (-not $jerryOwnedApp.HasExited) 'Annuler la desinstallation laisse Jerry ouvert'
    Assert-Jerry ((Get-JerryStartup) -ceq $jerryStartup -and (Get-FileHash (Join-Path $jerryData 'settings.json')).Hash -ceq $jerrySettingsHash) 'Annulation sans changement de demarrage ni de preferences'
    Assert-JerryPayload
    Assert-JerryData

    Invoke-JerrySetup
    Assert-Jerry ($jerryOwnedApp.WaitForExit(15000) -and $jerryOwnedApp.ExitCode -eq 0) 'Mise a jour apres fermeture propre de Jerry inactif'
    $jerryOwnedApp.Dispose(); $jerryOwnedApp = $null
    Assert-JerryStartup $true
    Assert-JerryPayload
    Assert-JerryData

    # Confirmed uninstall also requests a graceful exit before removing files.
    $jerryOwnedApp = Start-JerryIdle
    Start-Sleep -Seconds 2
    $jerryUninstall = Start-Process -FilePath (Join-Path $jerryApp 'unins000.exe') -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="' + (Join-Path $OutputDirectory 'uninstall-confirmed.log') + '"') -PassThru
    Assert-Jerry ($jerryUninstall.WaitForExit(120000) -and $jerryUninstall.ExitCode -eq 0) 'Desinstallation confirmee reussie'
    Assert-Jerry ($jerryOwnedApp.WaitForExit(15000) -and $jerryOwnedApp.ExitCode -eq 0) 'Desinstallation apres fermeture propre de Jerry inactif'
    $jerryOwnedApp.Dispose(); $jerryOwnedApp = $null
    $jerryUntil = [DateTime]::UtcNow.AddSeconds(15)
    while ((Test-Path -LiteralPath (Join-Path $jerryApp 'unins000.exe')) -and [DateTime]::UtcNow -lt $jerryUntil) { Start-Sleep -Milliseconds 100 }
    foreach ($jerryRemoved in @($jerryUninstallKey, $jerryAppPathsKey, $jerryStartMenu, $jerryDesktop)) {
        Assert-Jerry (-not (Test-Path -LiteralPath $jerryRemoved)) ("Integration Windows supprimee : $jerryRemoved")
    }
    Assert-Jerry (-not (Test-Path -LiteralPath $jerryApp) -or @(Get-ChildItem -LiteralPath $jerryApp -Recurse -File).Count -eq 0) 'Tous les fichiers installes ont ete retires'
    Assert-Jerry ($null -eq (Get-JerryStartup)) 'Run Jerry retire apres desinstallation'
    Assert-Jerry (-not (Get-Content (Join-Path $jerryData 'settings.json') -Raw | ConvertFrom-Json).LaunchAtLogin) 'Demarrage desactive dans les donnees conservees'
    Assert-JerryData

    # Verify only the three intended release files; logs stay CI artifacts.
    $jerryReleaseRoot = Split-Path $InstallerPath -Parent
    $jerrySumLines = @(Get-Content (Join-Path $jerryReleaseRoot 'SHA256SUMS.txt'))
    Assert-Jerry ($jerrySumLines.Count -eq 2) 'Deux livrables dans SHA256SUMS'
    foreach ($jerryLine in $jerrySumLines) {
        if ($jerryLine -notmatch '^([0-9a-f]{64})  (Jerry-(?:Setup-[0-9.]+\.exe|Windows-x64-[0-9.]+\.zip))$') { throw 'Entree SHA256SUMS inattendue.' }
        $jerryReleaseFile = Join-Path $jerryReleaseRoot $Matches[2]
        Assert-Jerry ((Get-FileHash -LiteralPath $jerryReleaseFile -Algorithm SHA256).Hash.ToLowerInvariant() -ceq $Matches[1]) ("SHA256 verifie : $([IO.Path]::GetFileName($jerryReleaseFile))")
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $jerryZip = [IO.Compression.ZipFile]::OpenRead((Join-Path $jerryReleaseRoot ("Jerry-Windows-x64-$($jerryManifest.version).zip")))
    try {
        foreach ($jerryEntry in $jerryManifest.files) {
            $jerryZipEntry = $jerryZip.GetEntry($jerryEntry.path)
            if (-not $jerryZipEntry) { $jerryZipEntry = $jerryZip.GetEntry($jerryEntry.path.Replace('/', '\')) }
            if (-not $jerryZipEntry) { throw "Fichier absent du ZIP : $($jerryEntry.path)." }
            $jerryStream = $jerryZipEntry.Open(); $jerrySha = [Security.Cryptography.SHA256]::Create()
            try { $jerryZipHash = [BitConverter]::ToString($jerrySha.ComputeHash($jerryStream)).Replace('-', '').ToLowerInvariant() }
            finally { $jerryStream.Dispose(); $jerrySha.Dispose() }
            if ($jerryZipHash -cne $jerryEntry.sha256) { throw "Empreinte ZIP differente : $($jerryEntry.path)." }
        }
    } finally { $jerryZip.Dispose() }
    Assert-Jerry $true 'ZIP portable identique a la publication complete'
    [IO.File]::WriteAllLines((Join-Path $OutputDirectory 'results.txt'), $jerryReport, (New-Object System.Text.UTF8Encoding($false)))
} catch {
    [IO.File]::WriteAllLines((Join-Path $OutputDirectory 'results.txt'), $jerryReport, (New-Object System.Text.UTF8Encoding($false)))
    $_ | Out-String | Set-Content (Join-Path $OutputDirectory 'failure.txt') -Encoding UTF8
    throw
} finally {
    # Even on a failed disposable test, never kill an application or capture.
    if ($null -ne $jerryOwnedApp -and -not $jerryOwnedApp.HasExited -and (Test-Path -LiteralPath (Join-Path $jerryApp 'Jerry.Maintenance.exe'))) {
        $jerryCleanup = Start-Process -FilePath (Join-Path $jerryApp 'Jerry.Maintenance.exe') -ArgumentList '--prepare-update' -Wait -PassThru
        Write-Output ("Nettoyage gracieux du processus de test : code $($jerryCleanup.ExitCode).")
    }
}
