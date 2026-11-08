namespace ThimbleBerry.Screensavers;

/// <summary>Names shared between the screensaver and the watcher so only one saver runs at a time.</summary>
public static class SaverInstance
{
    public const string RunningMutexName = @"Local\ThimbleBerry.Screensaver.Running";
    public const string WatcherMutexName = @"Local\ThimbleBerry.IdleWatcher";
    public const string SaverFileName = "ThimbleBerry.scr";

    public static bool IsSaverRunning()
    {
        if (Mutex.TryOpenExisting(RunningMutexName, out var m))
        {
            m.Dispose();
            return true;
        }
        return false;
    }
}
