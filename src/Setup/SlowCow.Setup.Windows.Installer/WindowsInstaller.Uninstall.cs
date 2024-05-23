using Microsoft.Extensions.Logging;
using Microsoft.Win32;
namespace SlowCow.Setup.Windows.Installer;

public partial class WindowsInstaller
{
    public Task<bool> UninstallAsync(ILogger logger)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("This method is only supported on Windows.");

        var result =
            DeleteApplicationFiles(logger) &&
            RegisterCleanup(logger) &&
            DeleteSetupFile(logger);

        return Task.FromResult(result);
    }

    private bool DeleteApplicationFiles(ILogger logger)
    {
        var folders = Directory.GetDirectories(InstallationPath);

        // delete all folders
        foreach (var folder in folders)
        {
            try
            {
                logger.LogInformation("Delete folder '{Folder}'", folder);
                Directory.Delete(folder, true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete folder '{Folder}'", folder);
                return false;
            }
        }

        return true;
    }

    // muted because OS already checked before
    #pragma warning disable CA1416
    private bool RegisterCleanup(ILogger logger)
    {
        using var parentKey = Registry.CurrentUser.OpenSubKey(UninstallKeyPath, true);
        if (parentKey == null)
        {
            logger.LogError("Uninstall registry key not found");
            return false;
        }

        using var appKey = parentKey.OpenSubKey(_installerSettings.ApplicationId.ToString());

        var desktopShortcut = appKey?.GetValue(RegisterShortcutDesktopKey) as string;
        var startMenuShortcut = appKey?.GetValue(RegisterShortcutMenuKey) as string;

        if (!string.IsNullOrWhiteSpace(desktopShortcut) && File.Exists(desktopShortcut)) File.Delete(desktopShortcut);
        if (!string.IsNullOrWhiteSpace(startMenuShortcut) && File.Exists(startMenuShortcut)) File.Delete(startMenuShortcut);

        parentKey.DeleteSubKeyTree(_installerSettings.ApplicationId.ToString(), false);
        return true;
    }
    #pragma warning restore CA1416

    private bool DeleteSetupFile(ILogger logger)
    {
        try
        {
            RunPostSetup("uninstall-cleaning", logger);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete itself");
            return false;
        }
    }
}
