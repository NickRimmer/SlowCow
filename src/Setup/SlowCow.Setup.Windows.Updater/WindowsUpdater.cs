using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SlowCow.Setup.Base.Exceptions;
using SlowCow.Setup.Base.Interfaces;
using SlowCow.Setup.Base.Models;
namespace SlowCow.Setup.Windows.Updater;

public class WindowsUpdater : IUpdater
{
    private readonly ILogger _logger;
    public WindowsUpdater(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SlowCowVersion? GetVersion()
    {
        // run command
        var outputFilePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName() + ".json");
        RunSetup(new Dictionary<string, object> {
            { "get-version", outputFilePath },
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
            throw new SlowCowException("No version information found");
        }
    }

    public bool InstallLatest(bool force)
    {
        var version = GetVersion();
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

    // by default, the setup file is located in the parent directory
    private static string GetSetupPath() =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))!, "setup.exe");
}
