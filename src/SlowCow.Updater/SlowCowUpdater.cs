using SlowCow.Shared;
using SlowCow.Updater.Common;
using SlowCow.Updater.Updaters;
namespace SlowCow.Updater;

public static class SlowCowUpdater
{
    public static SlowCowVersion? GetVersion(IUpdater? customUpdater = null)
    {
        var updater = customUpdater ?? GetSystemUpdater();
        return updater.GetVersion();
    }

    public static bool InstallLatestVersion(IUpdater? customUpdater = null)
    {
        var updater = customUpdater ?? GetSystemUpdater();
        return updater.InstallLatest();
    }

    private static IUpdater GetSystemUpdater()
    {
        if (CurrentSystem.IsWindows()) return new WindowsUpdater();
        throw new SlowCowException("Unsupported operating system.");
    }
}
