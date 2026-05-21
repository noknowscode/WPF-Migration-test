using System;
using System.Diagnostics;
using System.Security.Principal;

namespace EventLookUno.Models;

/// <summary>
/// Shell-launch and elevation helpers. Windows-only calls are guarded by
/// <c>OperatingSystem.IsWindows()</c> (Slot 1 §6 Level 1 pattern) so non-Windows
/// heads silently no-op rather than throwing.
/// </summary>
public static class ProcessHelper
{
    public static bool IsElevated
    {
        get
        {
            if (!OperatingSystem.IsWindows())
                return false;

            try
            {
                return WindowsIdentity.GetCurrent().Owner
                    ?.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) == true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Launches Windows Event Viewer (eventvwr.msc) focused on the supplied log source.
    /// No-op on non-Windows heads.
    /// </summary>
    public static void LaunchEventViewer(LogSource? logSource)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var arg = logSource?.PathType switch
        {
            PathType.FilePath when !string.IsNullOrEmpty(logSource.Path) => $"/l:\"{logSource.Path}\"",
            PathType.LogName when !string.IsNullOrEmpty(logSource!.Path) => $"/c:\"{logSource.Path}\"",
            _ => string.Empty,
        };

        try
        {
            Process.Start(new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = "eventvwr.msc",
                Arguments = arg,
            });
        }
        catch { /* silent — feature is best-effort */ }
    }

    /// <summary>
    /// Opens an http(s) URI via the OS shell. On WASM this falls through to the browser's
    /// default navigation (called from code that should instead use <c>Launcher.LaunchUriAsync</c>).
    /// </summary>
    public static void OpenUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch { /* silent */ }
    }
}
