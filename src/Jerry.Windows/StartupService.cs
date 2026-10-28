using Jerry.Core.Settings;
using Microsoft.Win32;

namespace Jerry.Windows;

public static class StartupService
{
    public const string AppPathsKey = @"Software\Microsoft\Windows\CurrentVersion\App Paths\Jerry.exe";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovalKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public static bool IsEnabled()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKey);
        if (run?.GetValue("Jerry") is not string command || string.IsNullOrWhiteSpace(command)) return false;
        using var approval = Registry.CurrentUser.OpenSubKey(ApprovalKey);
        // Windows keeps the Run command when startup is disabled in Task Manager.
        return approval?.GetValue("Jerry") is not byte[] { Length: > 0 } flags || flags[0] is not (3 or 7);
    }

    public static string ChooseExecutable(string current, string? registered)
    {
        if (IsLocalExecutable(registered) && File.Exists(registered)) return Path.GetFullPath(registered!);
        if (IsLocalExecutable(current) && File.Exists(current)) return Path.GetFullPath(current);
        throw new InvalidOperationException("Installez Jerry ou placez-le dans un dossier Windows local pour démarrer avec Windows.");
    }

    public static string CreateCommand(string executable) => "\"" + executable + "\" --background";

    private static bool IsLocalExecutable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('"')) return false;
        var local = path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..] : path;
        if (local.Length < 3 || !char.IsAsciiLetter(local[0]) || local[1] != ':' || local[2] != '\\') return false;
        try
        {
            if (!Path.IsPathFullyQualified(local)) return false;
            return new DriveInfo(local[..3]).DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Ram;
        }
        catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException) { return false; }
    }

    public static void SetEnabled(bool enabled)
    {
        string? command = null;
        if (enabled)
        {
            using var registered = Registry.CurrentUser.OpenSubKey(AppPathsKey);
            var executable = ChooseExecutable(Path.Combine(AppContext.BaseDirectory, "Jerry.exe"), registered?.GetValue("") as string);
            command = CreateCommand(executable);
        }
        if (enabled)
        {
            using var run = Registry.CurrentUser.CreateSubKey(RunKey);
            run.SetValue("Jerry", command!, RegistryValueKind.String);
        }
        else
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            run?.DeleteValue("Jerry", throwOnMissingValue: false);
        }
        // An explicit choice in Jerry supersedes a previous Task Manager disable.
        using var approval = Registry.CurrentUser.OpenSubKey(ApprovalKey, writable: true);
        approval?.DeleteValue("Jerry", throwOnMissingValue: false);
    }

    public static void Configure(bool enabled, string settingsPath)
    {
        var previousRun = Snapshot(RunKey); var previousApproval = Snapshot(ApprovalKey);
        try
        {
            SetEnabled(enabled);
            StartupPreferences.SetEnabled(settingsPath, enabled);
        }
        catch
        {
            Restore(RunKey, previousRun); Restore(ApprovalKey, previousApproval);
            throw;
        }
    }

    private sealed record ValueSnapshot(object? Value, RegistryValueKind Kind);
    private static ValueSnapshot Snapshot(string path)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        var value = key?.GetValue("Jerry", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return new(value, value == null ? RegistryValueKind.String : key!.GetValueKind("Jerry"));
    }
    private static void Restore(string path, ValueSnapshot previous)
    {
        if (previous.Value == null)
        {
            using var key = Registry.CurrentUser.OpenSubKey(path, writable: true);
            key?.DeleteValue("Jerry", false);
        }
        else
        {
            using var key = Registry.CurrentUser.CreateSubKey(path);
            key.SetValue("Jerry", previous.Value, previous.Kind);
        }
    }
}
