using System.Runtime.InteropServices;

namespace ThimbleBerry.Screensavers;

public static class PowerUtil
{
    /// <summary>
    /// Opt this process into Windows 11 "efficiency mode" (EcoQoS): Windows schedules it on
    /// efficient cores at lower clock speeds. Harmless no-op on older Windows.
    /// </summary>
    public static void EnableEfficiencyMode()
    {
        try
        {
            var state = new Native.PROCESS_POWER_THROTTLING_STATE
            {
                Version = Native.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = Native.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = Native.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            };
            Native.SetProcessInformation(Native.GetCurrentProcess(), Native.ProcessPowerThrottling,
                ref state, Marshal.SizeOf<Native.PROCESS_POWER_THROTTLING_STATE>());
            System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal;
        }
        catch
        {
            // Best effort only.
        }
    }

    public static bool OnBattery =>
        SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;

    /// <summary>
    /// True when the foreground window belongs to a process we can't inspect, usually an
    /// elevated (admin) app. Low-level hooks don't see input going to elevated windows,
    /// so the watcher must not treat that silence as idleness.
    /// </summary>
    public static bool ForegroundIsElevatedOrInaccessible()
    {
        var hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0 || pid == (uint)Environment.ProcessId) return false;

        var process = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return true;
        try
        {
            if (!Native.OpenProcessToken(process, Native.TOKEN_QUERY, out var token)) return true;
            try
            {
                return Native.GetTokenInformation(token, Native.TokenElevation, out int elevated, sizeof(int), out _)
                    ? elevated != 0
                    : true;
            }
            finally { Native.CloseHandle(token); }
        }
        finally { Native.CloseHandle(process); }
    }

    /// <summary>Milliseconds since any input, real or synthetic, as Windows sees it.</summary>
    public static long SystemIdleMilliseconds()
    {
        var info = new Native.LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<Native.LASTINPUTINFO>() };
        if (!Native.GetLastInputInfo(ref info)) return 0;
        return unchecked((uint)Environment.TickCount - info.dwTime);
    }
}
