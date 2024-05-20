using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SlowCow.Setup.Base;
using SlowCow.Setup.Base.Exceptions;
using SlowCow.Setup.Base.Interfaces;
using SlowCow.Setup.Base.Models;
namespace SlowCow.Setup.Windows.Installer;

public partial class WindowsInstaller : IInstaller
{
    [GeneratedRegex("[^a-zA-Z0-9\\s]")]
    private static partial Regex GetInstallationFolderNameRegex();

    private readonly Lazy<string> _installationPath;
    private readonly InstallerSettingsModel _installerSettings;

    public WindowsInstaller(InstallerSettingsModel installerSettings)
    {
        _installerSettings = installerSettings ?? throw new ArgumentNullException(nameof(installerSettings));
        _installationPath = new (() =>
        {
            var appPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var installationFolderName = GetInstallationFolderNameRegex().Replace(_installerSettings.ApplicationName, string.Empty);

            return Path.Combine(appPath, installationFolderName);
        });
    }

    public string InstallationPath => _installationPath.Value;

    private string GetReleaseInfoPath() => Path.Combine(InstallationPath, Constants.AppFolderName, Constants.ReleaseInfoFileName);

    private void RunPostSetup(string command, ILogger logger)
    {
        var applicationId = _installerSettings.ApplicationId.ToString();
        var selfPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(selfPath))
        {
            logger.LogWarning("Failed to get self path, will try to copy on post setup operation");
            throw new InstallerException("Failed to get self path");
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), applicationId);
        if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
        Directory.CreateDirectory(tempDirectory);

        var tmpFilePath = Path.Combine(tempDirectory, Path.GetFileName(selfPath));
        logger.LogInformation("Copy self to '{TmpFilePath}'", tmpFilePath);
        File.Copy(selfPath, tmpFilePath);

        var args = $"--parent=\"{Environment.ProcessId}\" --{command}";
        logger.LogInformation("Start process with args '{Args}'", args);
        Process.Start(new ProcessStartInfo {
            FileName = tmpFilePath,
            Arguments = args,
            WorkingDirectory = tempDirectory,
            UseShellExecute = false,
        });
    }
}
