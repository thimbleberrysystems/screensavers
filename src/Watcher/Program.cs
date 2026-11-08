namespace ThimbleBerry.Screensavers.Watcher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var single = new Mutex(true, SaverInstance.WatcherMutexName, out bool createdNew);
        if (!createdNew) return;

        ApplicationConfiguration.Initialize();
        PowerUtil.EnableEfficiencyMode();
        Application.Run(new WatcherContext());
    }
}
