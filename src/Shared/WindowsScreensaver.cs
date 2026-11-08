using Microsoft.Win32;

namespace ThimbleBerry.Screensavers;

/// <summary>Helpers for Windows' own screensaver configuration (per user).</summary>
public static class WindowsScreensaver
{
    /// <summary>True when Windows is set to use ThimbleBerry.scr as this user's screensaver.</summary>
    public static bool IsThimbleBerrySelected()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            return key?.GetValue("SCRNSAVE.EXE") is string current
                   && current.EndsWith(SaverInstance.SaverFileName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>If Windows uses this screensaver, set its own timeout to match ours, effective immediately.</summary>
    public static void SyncTimeout(int idleMinutes)
    {
        if (!IsThimbleBerrySelected()) return;
        Native.SystemParametersInfo(Native.SPI_SETSCREENSAVETIMEOUT, (uint)(idleMinutes * 60), IntPtr.Zero,
            Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE);
    }
}
