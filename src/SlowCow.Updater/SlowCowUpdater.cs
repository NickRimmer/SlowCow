using SlowCow.Shared;
using SlowCow.Updater.Common;
using SlowCow.Updater.Updaters;
namespace SlowCow.Updater;

public static class SlowCowUpdater
{
    public static Task<SlowCowVersion?> GetVersionAsync(IUpdater? customUpdater = null)
    {
        var updater = customUpdater ?? GetSystemUpdater();
        return Task.Run(updater.GetVersion);
    }

    private static IUpdater GetSystemUpdater()
    {
        if (CurrentSystem.IsWindows()) return new WindowsUpdater();
        throw new SlowCowException("Unsupported operating system.");
    }
}
