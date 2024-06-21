using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;
using SlowCow.Installers.Base;
using SlowCow.Installers.Base.Interfaces;
using SlowCow.Installers.Base.Models;
using WindowsShortcutFactory;
namespace SlowCow.Installers.Windows;

public class WindowsInstaller : IInstaller
{
    private const string CurrentVersionFolderName = "current";
    private const string UpdatesVersionFolderName = "updates";
    private const string RegisterShortcutMenuKey = "SlowCowStartMenuShortcut";
    private const string RegisterShortcutDesktopKey = "SlowCowDesktopShortcut";
    private const string RegisterAppNameKey = "DisplayName";
    private const string RegisterPublisherKey = "Publisher";
    private const string SetupFileName = $"{Constants.SetupFileNameWithoutExtension}.exe";

    private static readonly JsonSerializerSettings JsonSettings = new () {
        Formatting = Formatting.Indented,
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
    };

    private readonly Lazy<string> _installationFullPath;

    public WindowsInstaller(InstallationSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _installationFullPath = new Lazy<string>(GetInstallationFullPath);
    }

    public WindowsInstaller(string installationFullPath)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("This method is only supported on Windows.");

        _installationFullPath = new Lazy<string>(() => installationFullPath);
        var releaseInfo = GetInstalledReleaseInfoAsync().GetAwaiter().GetResult();

        if (releaseInfo == InstalledReleaseInfo.Empty)
            throw new Exception("Failed to get installed release info. Application is not installed.");

        var applicationName = "Unknown";
        var applicationFolderName = Path.GetFileName(_installationFullPath.Value);
        var publisherName = string.Empty;

        using var parentKey = Registry.CurrentUser.OpenSubKey(UninstallKeyPath, true);
        using var appKey = parentKey?.OpenSubKey(releaseInfo.ApplicationId.ToString());
        if (appKey != null)
        {
            applicationName = appKey.GetValue(RegisterAppNameKey, applicationName).ToString()!;
            publisherName = appKey.GetValue(RegisterPublisherKey, publisherName).ToString()!;
        }

        Settings = new InstallationSettings {
            Channel = releaseInfo.Channel,
            ApplicationId = releaseInfo.ApplicationId,
            ApplicationName = applicationName,
            ApplicationFolderName = applicationFolderName,
            ExecutableRelativePath = releaseInfo.ExecutableRelativePath,
            PublisherName = publisherName,
        };
    }

    public InstallationSettings Settings { get; private set; }

    public async Task<InstalledReleaseInfo> GetInstalledReleaseInfoAsync()
    {
        var installationPath = _installationFullPath.Value;
        var currentVersionPath = Path.Combine(installationPath, CurrentVersionFolderName);

        // if application is not installed
        if (!Directory.Exists(currentVersionPath))
        {
            Log.Information("No hashes available. Application is not installed");
            return InstalledReleaseInfo.Empty;
        }

        var releaseInfoPath = Path.Combine(currentVersionPath, Constants.ReleaseFileName);
        if (!File.Exists(releaseInfoPath))
        {
            Log.Warning("Release info not found. Cannot validate hashes ({ReleaseFilePath})", releaseInfoPath);
            Debug.Fail("Release info file not found");
            return InstalledReleaseInfo.Empty;
        }

        try
        {
            var releaseInfoJson = await File.ReadAllTextAsync(releaseInfoPath);
            var releaseInfo = JsonConvert.DeserializeObject<InstalledReleaseInfo>(releaseInfoJson);

            return releaseInfo ?? InstalledReleaseInfo.Empty;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to read release info file ({ReleaseFilePath})", releaseInfoPath);
            Debug.Fail("Failed to read release info file");
            return InstalledReleaseInfo.Empty;
        }
    }

    public async Task<InstallResult> InstallAsync(Stream releaseStream, Dictionary<string, string> releaseHashes, InstalledReleaseInfo releaseInfo, bool addDesktopShortcut, bool addStartMenuShortcut)
    {
        Debug.Assert(releaseHashes.Count > 0, "Unexpected empty hashes");

        // check if it is installing of a new version or update
        var installedInfo = await GetInstalledReleaseInfoAsync();
        return installedInfo == InstalledReleaseInfo.Empty
            ? await InstallNewVersionAsync(releaseStream, releaseInfo, addDesktopShortcut, addStartMenuShortcut)
            : await UpdateExistsVersionAsync(releaseStream, releaseHashes, releaseInfo);
    }

    public Task<InstallResult> UninstallAsync()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("This method is only supported on Windows.");

        var appDeleted =
            DeleteApplicationFiles() &&
            RegisterCleanup(Settings.ApplicationId);

        if (appDeleted) InstallationFolderCleanup();
        return Task.FromResult(new InstallResult(appDeleted));
    }

    public Task UninstallCleaningAsync(IReadOnlyCollection<string> rawArguments, string parentProcessId)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("This method is only supported on Windows.");

        if (!string.IsNullOrWhiteSpace(parentProcessId))
            WaitForParentExit(parentProcessId);

        // remove installation folder
        var installationPath = _installationFullPath.Value;
        if (Directory.Exists(installationPath))
        {
            Log.Information("Deleting installation folder '{InstallationPath}'", installationPath);
            Directory.Delete(installationPath, true);
        }

        return Task.CompletedTask;
    }

    private void InstallationFolderCleanup()
    {
        Log.Information("Self cleaning process starting");
        var selfPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(selfPath))
        {
            Log.Error("Failed to get current process path");
            return;
        }

        var selfName = Path.GetFileName(selfPath);
        var tempPath = Path.Combine(Path.GetTempPath(), Settings.ApplicationId.ToString());
        Directory.CreateDirectory(tempPath);

        var tempSelfPath = Path.Combine(tempPath, selfName);

        try
        {
            File.Copy(selfPath, tempSelfPath, true);
            var process = Process.Start(new ProcessStartInfo {
                FileName = tempSelfPath,
                Arguments = $"--{Constants.UninstallCleanupArgName}=\"{_installationFullPath.Value}\" --{Constants.ParentProcessArgName}={Process.GetCurrentProcess().Id}",
                UseShellExecute = false,
            });

            if (process?.HasExited != false)
            {
                Log.Error("Failed to start self-destroying process");
            }
            else
            {
                Log.Information("Self cleaning process started with PID {ProcessId}", process.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start self-destroying process");
        }
    }

    private async Task<InstallResult> InstallNewVersionAsync(Stream releaseStream, InstalledReleaseInfo releaseInfo, bool addDesktopShortcut, bool addStartMenuShortcut)
    {
        // extract
        var currentPath = Path.Combine(_installationFullPath.Value, CurrentVersionFolderName);
        ExtractPackage(releaseStream, currentPath);

        // save release info
        await SaveReleaseInfoAsync(currentPath, releaseInfo);

        // register
        var shortcuts = AddShortcuts(currentPath, addDesktopShortcut, addStartMenuShortcut);
        RegisterInstalledApplication(releaseInfo.Version, shortcuts);

        return true;
    }

    private async Task<InstallResult> UpdateExistsVersionAsync(Stream releaseStream, Dictionary<string, string> releaseHashes, InstalledReleaseInfo releaseInfo)
    {
        // extract
        var updatesPath = Path.Combine(_installationFullPath.Value, UpdatesVersionFolderName);
        ExtractPackage(releaseStream, updatesPath);

        // save release info
        await SaveReleaseInfoAsync(updatesPath, releaseInfo);

        if (LockedFilesFound(releaseHashes.Keys))
        {
            Log.Information("Cannot apply update right now. Locked files found");
            return (false, "Files are locked. Please close the application and try again.");
        }

        // override current files from updates
        var currentPath = Path.Combine(_installationFullPath.Value, CurrentVersionFolderName);
        var updatedFiles = Directory.GetFiles(updatesPath, "*.*", SearchOption.AllDirectories);
        for (var index = 0; index < updatedFiles.Length; index++)
        {
            var sourcePath = updatedFiles[index];
            var sourceRelativePath = sourcePath.Replace(updatesPath, string.Empty).TrimStart(['\\', '/']);
            var targetPath = Path.Combine(currentPath, sourceRelativePath);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(sourcePath, targetPath, true);
                Log.Information("[{Index}/{TotalCount}] {Source} -> {Target}", index + 1, updatedFiles.Length, Path.GetFileName(sourcePath), targetPath);
            }
            catch
            {
                // ignore
                Log.Warning("Failed to copy file from '{Source}' to '{Target}'", sourcePath, targetPath);
            }
        }

        RegisterInstalledApplication(releaseInfo.Version, new ());

        // clean up updates folder
        Directory.Delete(updatesPath, true);

        return true;
    }

    private bool LockedFilesFound(IReadOnlyCollection<string> files)
    {
        var installationPath = _installationFullPath.Value;
        var currentVersionPath = Path.Combine(installationPath, CurrentVersionFolderName);
        var result = new List<string>();

        if (!Directory.Exists(currentVersionPath))
        {
            return false;
        }

        foreach (var file in files)
        {
            var filePath = Path.Combine(currentVersionPath, file);
            if (!File.Exists(filePath)) continue;

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch
            {
                result.Add(file);
            }
        }

        return result.Count > 0;
    }

    private void RegisterInstalledApplication(string version, Dictionary<string, string> shortcuts)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("This method is only supported on Windows.");

        var sizeKb = Directory.GetFiles(_installationFullPath.Value, "*.*", SearchOption.AllDirectories)
            .Select(x => new FileInfo(x).Length)
            .Sum() / 1024;

        using var parentKey = Registry.CurrentUser.OpenSubKey(UninstallKeyPath, true);
        if (parentKey == null)
            throw new Exception("Uninstall registry key not found.");

        using var appKey = parentKey.CreateSubKey(Settings.ApplicationId.ToString());
        if (appKey == null)
            throw new Exception("Failed to create application registry key.");

        var uninstallerFullPath = Path.Combine(_installationFullPath.Value, CurrentVersionFolderName, SetupFileName);
        var executablePath = Path.Combine(_installationFullPath.Value, CurrentVersionFolderName, Settings.ExecutableRelativePath);

        appKey.SetValue("DisplayIcon", $"{executablePath},0");
        appKey.SetValue(RegisterAppNameKey, Settings.ApplicationName);
        appKey.SetValue("UninstallString", $"{uninstallerFullPath} --uninstall");
        appKey.SetValue("DisplayVersion", version);
        appKey.SetValue(RegisterPublisherKey, string.IsNullOrWhiteSpace(Settings.PublisherName) ? Settings.ApplicationName : Settings.PublisherName);
        appKey.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        appKey.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);

        foreach (var (key, value) in shortcuts)
            appKey.SetValue(key, value);
    }

    private static string UninstallKeyPath => Environment.Is64BitOperatingSystem && Environment.Is64BitProcess
        ? @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"
        : @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    private Dictionary<string, string> AddShortcuts(string currentPath, bool addDesktopShortcut, bool addStartMenuShortcut)
    {
        var result = new Dictionary<string, string>();
        var executablePath = Path.Combine(currentPath, Settings.ExecutableRelativePath);
        var iconPath = $"{executablePath}";

        if (addDesktopShortcut)
            result.Add(RegisterShortcutDesktopKey, AddShortcut(executablePath, iconPath, Environment.GetFolderPath(Environment.SpecialFolder.Desktop), Settings.ApplicationName));

        if (addStartMenuShortcut)
            result.Add(RegisterShortcutMenuKey, AddShortcut(executablePath, iconPath, Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Settings.ApplicationName));

        return result;
    }

    private bool DeleteApplicationFiles()
    {
        Log.Information("Deleting application files");
        // var folders = Directory.GetDirectories(Path.Combine(_installationFullPath.Value));

        // get all files
        var files = Directory.GetFiles(_installationFullPath.Value, "*.*", SearchOption.AllDirectories);

        // delete all files
        for (var index = 0; index < files.Length; index++)
        {
            var file = files[index];
            try
            {
                Log.Information("[{Index}/{Total}] deleted '{File}'", index + 1, files.Length, file);
                File.Delete(file);
            }
            catch
            {
                Log.Warning("[{Index}/{Total}] not deleted '{File}'", index + 1, files.Length, file);
            }
        }

        return true;
    }

    private static bool RegisterCleanup(Guid applicationId)
    {
        Log.Information("Registering cleanup for application {ApplicationId}", applicationId);

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("This method is only supported on Windows.");

        using var parentKey = Registry.CurrentUser.OpenSubKey(UninstallKeyPath, true);
        if (parentKey == null)
        {
            Log.Error("Uninstall registry key not found");
            return false;
        }

        using var appKey = parentKey.OpenSubKey(applicationId.ToString());
        if (appKey == null)
        {
            Log.Warning("Application key not found in uninstall registry key. Most probably application is uninstalled already. Cleanup is not required");
            return true;
        }

        var desktopShortcut = appKey.GetValue(RegisterShortcutDesktopKey) as string;
        var startMenuShortcut = appKey.GetValue(RegisterShortcutMenuKey) as string;

        if (!string.IsNullOrWhiteSpace(desktopShortcut) && File.Exists(desktopShortcut)) File.Delete(desktopShortcut);
        if (!string.IsNullOrWhiteSpace(startMenuShortcut) && File.Exists(startMenuShortcut)) File.Delete(startMenuShortcut);

        parentKey.DeleteSubKeyTree(applicationId.ToString(), false);
        return true;
    }

    private static string AddShortcut(string executablePath, string iconPath, string targetDirectory, string name)
    {
        var shortcutPath = Path.Combine(targetDirectory, name + ".lnk");

        using var shortcut = new WindowsShortcut();
        shortcut.Path = executablePath;
        shortcut.Description = name;
        shortcut.WorkingDirectory = Path.GetDirectoryName(executablePath);
        shortcut.IconLocation = iconPath;

        shortcut.Save(shortcutPath);
        return shortcutPath;
    }

    private string GetInstallationFullPath()
    {
        var appDataFullPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var installationPath = Path.Combine(appDataFullPath, Settings.ApplicationFolderName);

        return installationPath;
    }

    private static Task SaveReleaseInfoAsync(string applicationFolderPath, InstalledReleaseInfo releaseInfo)
    {
        var releaseInfoPath = Path.Combine(applicationFolderPath, Constants.ReleaseFileName);
        Log.Information("Saving release info to {ReleaseInfoPath}", releaseInfoPath);

        var releaseInfoJson = JsonConvert.SerializeObject(releaseInfo, JsonSettings);
        return File.WriteAllTextAsync(releaseInfoPath, releaseInfoJson);
    }

    private static void ExtractPackage(Stream stream, string targetFolderPath)
    {
        Log.Information("Extracting package to {TargetFolderPath}", targetFolderPath);
        if (Directory.Exists(targetFolderPath))
        {
            Log.Warning("Target folder already exists. Deleting...");
            Directory.Delete(targetFolderPath, true);
        }

        Directory.CreateDirectory(targetFolderPath);
        ZipFile.ExtractToDirectory(stream, targetFolderPath);
    }

    private static void WaitForParentExit(string parentProcessId)
    {
        if (!int.TryParse(parentProcessId, out var parentProcessIdNumber))
        {
            Log.Warning("Uninstaller cleaning will not wait parent process, because parent process id is not a number");
            return;
        }

        try
        {
            var process = Process.GetProcessById(parentProcessIdNumber);
            if (process == null || process.HasExited)
            {
                Log.Information("Parent process not found or already exited");
                return;
            }

            // wait for parent exit
            var waitUntil = DateTime.Now.AddSeconds(60);
            while (!process.HasExited && DateTime.Now < waitUntil)
            {
                Log.Information("Waiting for parent process to exit");
                Task.Delay(1000).Wait();
            }

            if (!process.HasExited)
            {
                Log.Warning("Parent process did not exit in time. Continuing cleanup");
            }
        }
        catch
        {
            Log.Error("Failed to get parent process by id {ParentProcessId}", parentProcessIdNumber);
        }
    }
}
