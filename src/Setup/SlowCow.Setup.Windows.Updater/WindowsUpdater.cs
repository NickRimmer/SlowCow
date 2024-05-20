using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SlowCow.Setup.Base;
using SlowCow.Setup.Base.Exceptions;
using SlowCow.Setup.Base.Interfaces;
using SlowCow.Setup.Base.Models;
namespace SlowCow.Setup.Windows.Updater;

public class WindowsUpdater : IUpdater
{
    private readonly string _installationPath;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsUpdater"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="installationPath">Installation path, stay null or empty to find it automatically.</param>
    public WindowsUpdater(ILogger logger, string? installationPath = null)
    {
        if (string.IsNullOrWhiteSpace(installationPath)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(installationPath));

        _installationPath = string.IsNullOrWhiteSpace(installationPath) ? FindInstallationPath() : installationPath;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SlowCowVersion? GetUpdateInfo()
    {
        // run command
        var outputFilePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName() + ".json");
        RunSetup(new Dictionary<string, object> {
            { "get-update", outputFilePath },
        });

        // read output JSON
        try
        {
            var json = File.ReadAllText(outputFilePath);
            return JsonConvert.DeserializeObject<SlowCowVersion>(json);
        }
        catch
        {
            _logger.LogError("Failed to read version information from {OutputFilePath}", outputFilePath);
            return null;
        }
    }

    public bool InstallLatest(bool force)
    {
        var version = GetUpdateInfo();
        if (!force && version?.UpdateAvailable != true)
        {
            _logger.LogInformation("No update available");
            return false;
        }

        if (string.IsNullOrWhiteSpace(version?.AvailableVersion))
        {
            _logger.LogError("No available version found");
            return false;
        }

        var args = new Dictionary<string, object> {
            { "channel", version.Channel },
            { "parent", Environment.ProcessId },
        };

        if (force) args.Add("repair", string.Empty);

        RunSetup(args, false);
        Environment.Exit(0);

        return true; // will never reach this line :P
    }

    public ReleaseInfoModel GetCurrentInfo()
    {
        var releasePath = Path.Combine(_installationPath, Constants.AppFolderName, Constants.ReleaseInfoFileName);
        if (!File.Exists(releasePath))
        {
            _logger.LogError("Cannot find release info file: {ReleasePath}", releasePath);
            throw new SlowCowException("Cannot find release info file");
        }

        var json = File.ReadAllText(releasePath);
        var result = JsonConvert.DeserializeObject<ReleaseInfoModel>(json);

        if (result == null)
        {
            _logger.LogError("Failed to deserialize release info file: {ReleasePath}", releasePath);
            throw new SlowCowException("Failed to deserialize release info file");
        }

        return result;
    }

    private void RunSetup(Dictionary<string, object> args, bool waitForExit = true)
    {
        try
        {
            var setupPath = GetSetupPath();
            if (!File.Exists(setupPath))
            {
                _logger.LogError("The setup file is missing: {SetupPath}", setupPath);
                throw new SlowCowException($"The setup file is missing. ({setupPath})");
            }

            var argsString = string.Join(" ", args.Select(x => string.IsNullOrWhiteSpace(x.Value?.ToString()) ? $"--{x.Key}" : $"--{x.Key}=\"{x.Value}\""));
            var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = setupPath,
                    Arguments = argsString,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(setupPath),
                },
            };

            process.Start();
            if (waitForExit)
            {
                _logger.LogDebug("Waiting for setup process to finish... ({ProcessId})", process.Id);
                process.WaitForExit();
            }

            _logger.LogDebug("Setup process finished ({ProcessId})", process.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run setup");
            throw new SlowCowException($"Failed to run setup. {ex.Message} ({ex.GetType().Name})");
        }
    }

    private string GetSetupPath() => Path.Combine(_installationPath, "setup.exe");

    private static string FindInstallationPath()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath)) throw new SlowCowException("Cannot find executable path");

        var executableFolder = Path.GetDirectoryName(executablePath);
        var installationPath = Path.GetDirectoryName(executableFolder); // as executable file must be in 'current' directory of the installation path

        if (string.IsNullOrWhiteSpace(installationPath)) throw new SlowCowException("Cannot find executable path");

        return installationPath;
    }
}
