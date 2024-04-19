using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Newtonsoft.Json;
using SlowCow.Setup.Modules.Installers.Base;
using SlowCow.Setup.Modules.Runner;
using SlowCow.Setup.Modules.Setups.Base;
using SlowCow.Setup.Modules.Setups.Base.Exceptions;
using SlowCow.Setup.Modules.Setups.Base.Models;
using WindowsShortcutFactory;
namespace SlowCow.Setup.Modules.Installers;

internal class WindowsInstaller : BaseInstaller
{
    private const string SlowCowStartMenuShortcut = "SlowCowStartMenuShortcut";
    private const string SlowCowDesktopShortcut = "SlowCowDesktopShortcut";

    private readonly ISetup _setup;
    private readonly RunnerModel _runnerSettings;
    public WindowsInstaller(ISetup setup, RunnerModel runnerSettings)
    {
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _runnerSettings = runnerSettings ?? throw new ArgumentNullException(nameof(runnerSettings));
    }

    public override async Task InstallAsync(ManifestModel manifest, bool addDesktop, bool addStartMenu)
    {
        if (string.IsNullOrWhiteSpace(manifest.Version))
            throw new InstallerException("Version cannot be null or whitespace.");

        if (string.IsNullOrWhiteSpace(_runnerSettings.Channel))
            throw new InstallerException("Channel cannot be null or whitespace.");

        // get current version installed
        var channel = _runnerSettings.Channel;
        var loadVersion = manifest.Version;
        var installationInfo = _runnerSettings.HasRepairFlag
            ? null // force to install full version
            : await GetInstallationAsync();

        // download pack file
        var packBytes = await _setup.LoadPackFileAsync(channel, loadVersion, installationInfo?.Version);
        if (packBytes.Length == 0) throw new InstallerException("Failed to load pack file.");

        // rename current folder
        var installationPath = GetInstallationPath();
        var currentDirectory = Path.Combine(installationPath, CurrentAppFolderName);
        var backupDirectory = Path.Combine(installationPath, $"{CurrentAppFolderName}-backup");
        if (Directory.Exists(currentDirectory))
        {
            // check if files from current directory locked by another process
            var lockedFiles = await Task.Run(() => FindLockedFiles(currentDirectory));
            if (lockedFiles.Any())
                throw new InstallerException($"Files are locked by another process:\n{string.Join(
                    "\n",
                    lockedFiles.Select(x => x.Replace(currentDirectory, string.Empty).TrimStart('/', '\\')))}");

            if (Directory.Exists(backupDirectory)) Directory.Delete(backupDirectory, true);
            Directory.Move(currentDirectory, backupDirectory);
        }

        try
        {
            if (Directory.Exists(currentDirectory)) Directory.Delete(currentDirectory, true);
            Directory.CreateDirectory(currentDirectory);

            var packStream = new MemoryStream(packBytes);
            ZipFile.ExtractToDirectory(packStream, currentDirectory);

            var manifestJson = JsonConvert.SerializeObject(manifest, Formatting.Indented);
            await File.WriteAllTextAsync(Path.Combine(currentDirectory, ManifestFileName), manifestJson);

            // copy itself to installation folder
            var selfPath = Process.GetCurrentProcess().MainModule!.FileName;
            var selfDestination = Path.Combine(installationPath, "Setup.exe");

            // try to update setup file in installation folder
            try
            {
                if (!File.Exists(selfDestination)) File.Move(selfDestination, Path.Combine(backupDirectory, Path.GetFileName(selfDestination)), true);
                File.Copy(selfPath, selfDestination, true);
            }
            catch
            {
                // ignore
            }

            // get size of installation folder
            var size = Directory.GetFiles(installationPath, "*.*", SearchOption.AllDirectories)
                .Select(x => new FileInfo(x).Length)
                .Sum();

            var executablePath = Path.Combine(currentDirectory, _runnerSettings.ExecutableFileName);
            var desktopShortcut = addDesktop ? AddDesktopShortcut(executablePath, _runnerSettings.Name) : null;
            var startMenuShortcut = addStartMenu ? AddStartMenuShortcut(executablePath, _runnerSettings.Name) : null;

            AddInstalledApplication(channel, loadVersion, selfDestination, size / 1024, desktopShortcut, startMenuShortcut);

            if (Directory.Exists(backupDirectory)) Directory.Delete(backupDirectory, true);
        }
        catch (Exception ex)
        {
            TryRestoreBackup(backupDirectory, currentDirectory);
            throw new InstallerException($"Failed to install the application. {ex.Message}", ex);
        }
    }

    public override async Task UninstallAsync()
    {
        var result = await MessageBoxManager.GetMessageBoxStandard(_runnerSettings.Name, "Are you sure you want to uninstall the application?", ButtonEnum.YesNo, Icon.Question).ShowAsync();
        if (result != ButtonResult.Yes) return;

        var selfPath = Process.GetCurrentProcess().MainModule!.FileName;
        var installationPath = Path.GetDirectoryName(selfPath);
        if (string.IsNullOrWhiteSpace(installationPath))
        {
            await MessageBoxManager.GetMessageBoxStandard("Cannot uninstall application", "Failed to get installation path.", ButtonEnum.Ok, Icon.Error).ShowAsync();
            return;
        }

        var folders = Directory.GetDirectories(installationPath);
        if (folders.Length == 0)
        {
            await MessageBoxManager.GetMessageBoxStandard("Cannot uninstall application", "Failed to get installation folder.", ButtonEnum.Ok, Icon.Error).ShowAsync();
            return;
        }

        // delete all folders
        foreach (var folder in folders)
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception ex)
            {
                await MessageBoxManager.GetMessageBoxStandard("Cannot uninstall application", ex.Message, ButtonEnum.Ok, Icon.Error).ShowAsync();
                return;
            }
        }

        RemoveInstalledApplication();

        // delete itself
        try
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), _runnerSettings.ApplicationId.ToString());
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            Directory.CreateDirectory(tempDirectory);

            var tmpFilePath = Path.Combine(tempDirectory, Path.GetFileName(selfPath));
            File.Copy(selfPath, tmpFilePath);
            Process.Start(new ProcessStartInfo {
                FileName = tmpFilePath,
                Arguments = $"--uninstall-cleaning=\"{Environment.ProcessId};{installationPath}\"",
                WorkingDirectory = tempDirectory,
                UseShellExecute = false,
            });

            await MessageBoxManager.GetMessageBoxStandard("Uninstall application", "Application has been uninstalled successfully.", ButtonEnum.Ok, Icon.Info).ShowAsync();
        }
        catch (Exception ex)
        {
            await MessageBoxManager.GetMessageBoxStandard("Cannot complete uninstalling", ex.Message, ButtonEnum.Ok, Icon.Error).ShowAsync();
        }
    }

    public override string GetInstallationPath()
    {
        var appPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appPath, _runnerSettings.InstallationFolderName);
    }

    private void AddInstalledApplication(string channel, string version, string installerPath, long sizeKb, string? desktopShortcut, string? startMenuShortcut)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("This method is only supported on Windows.");

        using var parentKey = Registry.CurrentUser.OpenSubKey(UninstallKeyPath, true);
        if (parentKey == null)
            throw new Exception("Uninstall registry key not found.");

        using var appKey = parentKey.CreateSubKey(_runnerSettings.ApplicationId.ToString());
        if (appKey == null)
            throw new Exception("Failed to create application registry key.");

        appKey.SetValue("DisplayName", _runnerSettings.Name);
        appKey.SetValue("UninstallString", $"{installerPath} --uninstall");
        appKey.SetValue("DisplayVersion", version);
        appKey.SetValue("Publisher", string.IsNullOrWhiteSpace(_runnerSettings.Publisher) ? _runnerSettings.Name : _runnerSettings.Publisher);
        appKey.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        // appKey.SetValue("DisplayIcon", @"C:\Program Files\YourApp\appicon.ico");
        appKey.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord); // Size in KB
        appKey.SetValue("ModifyPath", $"{installerPath} --repair --channel={channel}");

        if (!string.IsNullOrWhiteSpace(desktopShortcut)) appKey.SetValue(SlowCowDesktopShortcut, desktopShortcut);
        if (!string.IsNullOrWhiteSpace(startMenuShortcut)) appKey.SetValue(SlowCowStartMenuShortcut, startMenuShortcut);
    }

    private string AddDesktopShortcut(string executablePath, string name)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("This method is only supported on Windows.");

        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string shortcutPath = Path.Combine(desktopPath, name + ".lnk");

        using var shortcut = new WindowsShortcut();
        shortcut.Path = executablePath;
        shortcut.Description = name;
        shortcut.WorkingDirectory = Path.GetDirectoryName(executablePath);

        shortcut.Save(shortcutPath);
        return shortcutPath;
    }

    private string AddStartMenuShortcut(string executablePath, string name)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("This method is only supported on Windows.");

        var startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        var shortcutPath = Path.Combine(startMenuPath, "Programs", name + ".lnk");

        using var shortcut = new WindowsShortcut();
        shortcut.Path = executablePath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(executablePath);

        shortcut.Save(shortcutPath);
        return shortcutPath;
    }

    private void RemoveInstalledApplication()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("This method is only supported on Windows.");

        using var parentKey = Registry.CurrentUser.OpenSubKey(UninstallKeyPath, true);
        if (parentKey == null)
            throw new Exception("Uninstall registry key not found.");

        using var appKey = parentKey.OpenSubKey(_runnerSettings.ApplicationId.ToString());

        var desktopShortcut = appKey?.GetValue(SlowCowDesktopShortcut) as string;
        var startMenuShortcut = appKey?.GetValue(SlowCowStartMenuShortcut) as string;

        if (!string.IsNullOrWhiteSpace(desktopShortcut) && File.Exists(desktopShortcut)) File.Delete(desktopShortcut);
        if (!string.IsNullOrWhiteSpace(startMenuShortcut) && File.Exists(startMenuShortcut)) File.Delete(startMenuShortcut);

        parentKey.DeleteSubKeyTree(_runnerSettings.ApplicationId.ToString(), false);
    }

    private static bool TryRestoreBackup(string backupDirectory, string targetDirectory)
    {
        if (!Directory.Exists(backupDirectory)) return false;

        try
        {
            Directory.CreateDirectory(targetDirectory);
            var backedFiles = Directory.GetFiles(backupDirectory, "*.*", SearchOption.AllDirectories);
            foreach (var file in backedFiles)
            {
                var relativePath = file.Replace(backupDirectory, string.Empty).TrimStart(Path.PathSeparator);
                var targetPath = Path.Combine(targetDirectory, relativePath);
                TryCopyFile(file, targetPath);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCopyFile(string source, string target)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyCollection<string> FindLockedFiles(string directory)
    {
        var lockedFiles = new List<string>();
        foreach (var file in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories))
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch
            {
                lockedFiles.Add(file);
            }
        }

        return lockedFiles;
    }

    private static string UninstallKeyPath => Environment.Is64BitOperatingSystem && Environment.Is64BitProcess
        ? @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"
        : @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
}
