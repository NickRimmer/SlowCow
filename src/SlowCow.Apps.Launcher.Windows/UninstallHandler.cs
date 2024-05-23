using SlowCow.Installers.Base.Interfaces;
using SlowCow.Installers.Windows;
namespace SlowCow.Examples.Launcher.Windows;

public static class UninstallHandler
{
    public static async Task UninstallAsync(string installationPath)
    {
        IInstaller installer = new WindowsInstaller(installationPath);
        await installer.UninstallAsync();
    }

    public static async Task UninstallCompleteAsync(string installationPath)
    {
        var tries = 20;
        try
        {
            while (tries-- > 0 && Directory.Exists(installationPath))
            {
                try
                {
                    Directory.Delete(installationPath, true);
                    Log.Information("Installation path deleted ({Path})", installationPath);
                    break;
                }
                catch
                {
                    await Task.Delay(3 * 1000); // 3 seconds
                }
            }

            if (Directory.Exists(installationPath))
                Log.Error("Failed to delete installation path ({Path})", installationPath);
        }
        catch
        {
            Log.Error("Failed to delete installation path ({Path})", installationPath);
        }
    }
}
