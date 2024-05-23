using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Newtonsoft.Json;
using SlowCow.Setup.Base;
using SlowCow.Setup.Base.Exceptions;
using SlowCow.Setup.Base.Models;
using WindowsShortcutFactory;
namespace SlowCow.Setup.Windows.Installer;

public partial class WindowsInstaller
{
    private const string BackupAppFolderName = "backup";
    private const string SetupFileName = "setup.exe";
    private const string RegisterShortcutMenuKey = "SlowCowStartMenuShortcut";
    private const string RegisterShortcutDesktopKey = "SlowCowDesktopShortcut";

    public async Task InstallAsync(Stream packStream, InstallationSettingsModel settings, ILogger logger)
    {
        // check params
        ArgumentNullException.ThrowIfNull(packStream);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        if (!packStream.CanRead)
        {
            logger.LogError("Pack stream cannot be read");
            throw new InstallerException("Failed to load pack file.");
        }

        // let's check if files are locked by another process
        var installationPath = InstallationPath;
        var targetDirectory = Path.Combine(installationPath, Constants.AppFolderName);
        if (FindLockedFiles(targetDirectory, out var lockedFiles))
        {
            logger.LogError("Cannot install package. {Count} files are locked by another process", lockedFiles.Count);
            var lockedFilesString = string.Join("\n", lockedFiles.Select(x => x.Replace(targetDirectory, string.Empty).TrimStart('/', '\\')));
            throw new InstallerException($"Files are locked by another process:\n{lockedFilesString}");
        }

        // backup what we can
        var backupDirectory = Path.Combine(installationPath, BackupAppFolderName);
        if (Directory.Exists(targetDirectory)) BackupTarget(targetDirectory, backupDirectory, logger);

        // and the moment of truth - install
        try
        {
            await InstallInnerAsync(targetDirectory, packStream, settings, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install the application. Restoring backup: {Backup} -> {Target}", backupDirectory, targetDirectory);
            RestoreBackup(targetDirectory, backupDirectory, logger);
            throw new InstallerException($"Failed to install the application. {ex.Message}", ex);
        }
    }

    private async Task InstallInnerAsync(string targetDirectory, Stream packStream, InstallationSettingsModel settings, ILogger logger)
    {
        // extract package
        logger.LogInformation("Extract package to '{TargetDirectory}'", targetDirectory);
        Directory.CreateDirectory(targetDirectory);
        ZipFile.ExtractToDirectory(packStream, targetDirectory);

        // save release details
        await SaveReleaseInfoAsync(settings, logger);

        // try to update updater
        UpdateCurrentlyInstalledSetup(logger);

        // add selected shortcuts (e.g. Desktop, Start Menu)
        var shortcuts = AddShortcuts(settings);

        // register application on windows
        RegisterApplication(settings, shortcuts);

        // cleaning up
        if (Directory.Exists(BackupAppFolderName)) Directory.Delete(BackupAppFolderName, true);
    }

    private void RegisterApplication(InstallationSettingsModel settings, Dictionary<string, string> shortcuts)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("This method is only supported on Windows.");

        var sizeKb = Directory.GetFiles(InstallationPath, "*.*", SearchOption.AllDirectories)
            .Select(x => new FileInfo(x).Length)
            .Sum() / 1024;

        using var parentKey = Registry.CurrentUser.OpenSubKey(UninstallKeyPath, true);
        if (parentKey == null)
            throw new Exception("Uninstall registry key not found.");

        using var appKey = parentKey.CreateSubKey(_installerSettings.ApplicationId.ToString());
        if (appKey == null)
            throw new Exception("Failed to create application registry key.");

        var installerPath = GetInstalledSetupPath();

        appKey.SetValue("DisplayIcon", $"{installerPath},0");
        appKey.SetValue("DisplayName", _installerSettings.ApplicationName);
        appKey.SetValue("UninstallString", $"{installerPath} --uninstall");
        appKey.SetValue("DisplayVersion", settings.Version);
        appKey.SetValue("Publisher", string.IsNullOrWhiteSpace(_installerSettings.PublisherName) ? _installerSettings.ApplicationName : _installerSettings.PublisherName);
        appKey.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        appKey.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord); // Size in KB
        appKey.SetValue("ModifyPath", $"{installerPath} --repair --channel={settings.Channel}");

        foreach (var (key, value) in shortcuts)
            appKey.SetValue(key, value);
    }

    private Dictionary<string, string> AddShortcuts(InstallationSettingsModel settings)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("This method is only supported on Windows.");

        var result = new Dictionary<string, string>();
        var executablePath = Path.Combine(InstallationPath, Constants.AppFolderName, _installerSettings.ExecutableFileName);

        if (settings.AddDesktop)
            result.Add(RegisterShortcutDesktopKey, AddShortcut(executablePath, Environment.GetFolderPath(Environment.SpecialFolder.Desktop), _installerSettings.ApplicationName));

        if (settings.AddStartMenu)
            result.Add(RegisterShortcutMenuKey, AddShortcut(executablePath, Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), _installerSettings.ApplicationName));

        return result;
    }

    private void UpdateCurrentlyInstalledSetup(ILogger logger)
    {
        var selfPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(selfPath))
        {
            logger.LogWarning("Failed to get self path, will try to copy on post setup operation");
            throw new InstallerException("Failed to get self path");
        }

        var selfDestination = GetInstalledSetupPath();
        logger.LogInformation("Copy self to '{SelfDestination}'", selfDestination);
        try
        {
            File.Copy(selfPath, selfDestination, true);
        }
        catch
        {
            logger.LogWarning("Failed to copy self, will try to copy on post setup operation");
            RunPostSetup("self-update", logger);
        }
    }

    private async Task SaveReleaseInfoAsync(InstallationSettingsModel settings, ILogger logger)
    {
        var releaseInfoPath = GetReleaseInfoPath();
        var releaseInfo = new ReleaseInfoModel {
            Version = settings.Version,
            Channel = settings.Channel,
            Hashes = settings.Hashes,
        };
        var releaseInfoJson = JsonConvert.SerializeObject(releaseInfo, Formatting.Indented);
        logger.LogDebug("Save release info to '{ReleaseInfoPath}'", releaseInfoPath);
        await File.WriteAllTextAsync(releaseInfoPath, releaseInfoJson);
    }

    private string GetInstalledSetupPath() => Path.Combine(InstallationPath, SetupFileName);

    private static string AddShortcut(string executablePath, string targetDirectory, string name)
    {
        var shortcutPath = Path.Combine(targetDirectory, name + ".lnk");

        using var shortcut = new WindowsShortcut();
        shortcut.Path = executablePath;
        shortcut.Description = name;
        shortcut.WorkingDirectory = Path.GetDirectoryName(executablePath);

        shortcut.Save(shortcutPath);
        return shortcutPath;
    }

    private static bool FindLockedFiles(string targetDirectory, out IReadOnlyCollection<string> lockedFiles)
    {
        var result = new List<string>();
        if (!Directory.Exists(targetDirectory))
        {
            lockedFiles = result;
            return false;
        }

        foreach (var file in Directory.GetFiles(targetDirectory, "*.*", SearchOption.AllDirectories))
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch
            {
                result.Add(file);
            }
        }

        lockedFiles = result;
        return result.Count > 0;
    }

    private static void BackupTarget(string targetDirectory, string backupDirectory, ILogger logger)
    {
        try
        {
            if (Directory.Exists(backupDirectory)) Directory.Delete(backupDirectory, true);
            Directory.Move(targetDirectory, backupDirectory);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to backup target directory '{TargetDirectory}'", targetDirectory);
            throw;
        }
    }

    private static void RestoreBackup(string targetDirectory, string backupDirectory, ILogger logger)
    {
        if (!Directory.Exists(backupDirectory))
        {
            logger.LogWarning("Backup directory '{BackupDirectory}' does not exist", backupDirectory);
            return;
        }

        try
        {
            Directory.CreateDirectory(targetDirectory);
            var backedFiles = Directory.GetFiles(backupDirectory, "*.*", SearchOption.AllDirectories);
            foreach (var sourcePath in backedFiles)
            {
                var sourceRelativePath = sourcePath.Replace(backupDirectory, string.Empty).TrimStart(Path.PathSeparator);
                var targetPath = Path.Combine(targetDirectory, sourceRelativePath);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    File.Copy(sourcePath, targetPath, true);
                }
                catch
                {
                    // ignore
                    logger.LogWarning("Failed to restore file '{Source}' to '{Target}'", sourcePath, targetPath);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restore backup");
            // ignore
        }
    }

    private static string UninstallKeyPath => Environment.Is64BitOperatingSystem && Environment.Is64BitProcess
        ? @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"
        : @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
}
