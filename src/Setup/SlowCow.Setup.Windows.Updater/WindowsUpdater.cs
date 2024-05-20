using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SlowCow.Setup.Base;
using SlowCow.Setup.Base.Exceptions;
using SlowCow.Setup.Base.Interfaces;
using SlowCow.Setup.Base.Loggers;
using SlowCow.Setup.Base.Models;
namespace SlowCow.Setup.Windows.Updater;

public class WindowsUpdater : IUpdater
{
    private const string SetupFileName = "setup.exe";
    private readonly string _executionFolder;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsUpdater"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="executionFolder">Installation path, stay null or empty to find it automatically.</param>
    public WindowsUpdater(ILogger? logger = null, string? executionFolder = null)
    {
        _executionFolder = string.IsNullOrWhiteSpace(executionFolder) ? FindExecutionFolder() : executionFolder;
        _logger = logger ?? new DebugLogger();
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
        var releasePath = Path.Combine(_executionFolder, Constants.ReleaseInfoFileName);
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

    private string GetSetupPath()
    {
        var setupPath = Path.Combine(_executionFolder, SetupFileName); // search in execution folder
        if (!File.Exists(setupPath)) setupPath = Path.Combine(Path.GetDirectoryName(_executionFolder)!, SetupFileName); // search in parent folder

        if (!File.Exists(setupPath)) throw new SlowCowException("Cannot find setup file");
        return setupPath;
    }

    private static string FindExecutionFolder()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath)) throw new SlowCowException("Cannot find executable path");

        var executionFolder = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(executionFolder)) throw new SlowCowException("Cannot find executable path");

        return executionFolder;
    }
}
