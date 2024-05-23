using System.Text;
using Newtonsoft.Json;
using Serilog;
using SlowCow.Apps.InstallerBuilder.Services;
using SlowCow.Apps.Shared;
using SlowCow.Apps.Shared.Services;

LoggerService.Init();
Log.Information("Build installer");

var argsService = new ArgsService(args);
var settings = await AppSettingsService.LoadAsync(argsService);

// read base installer
if (!File.Exists(settings.BaseInstallerPath))
{
    Log.Error("Installer not found ({FilePath})", settings.BaseInstallerPath);
    return;
}
Log.Information("Installer file using ({FilePath})", settings.BaseInstallerPath);

// build result
var resultInstallerPath = settings.BaseInstallerPath;
if (settings.CreateCopy)
{
    var outputName = string.IsNullOrWhiteSpace(settings.OutputName) ? (Path.GetFileNameWithoutExtension(settings.BaseInstallerPath) + "-packed") : settings.OutputName;
    var outputFolder = string.IsNullOrWhiteSpace(settings.OutputFolder) ? Path.GetDirectoryName(settings.BaseInstallerPath)! : settings.OutputFolder;
    resultInstallerPath = Path.Combine(outputFolder, outputName + Path.GetExtension(settings.BaseInstallerPath));

    Directory.CreateDirectory(outputFolder);
    File.Copy(settings.BaseInstallerPath, resultInstallerPath, true);
}

// write config to installer after delimiter
var configJson = JsonConvert.SerializeObject(settings.InstallerSettings);
var configBytes = Encoding.UTF8.GetBytes(configJson);
var delimiterBytes = Encoding.UTF8.GetBytes(Constants.InstallerInjectionDelimiter);

await using var installerFile = File.Open(resultInstallerPath, FileMode.Append);
await installerFile.WriteAsync(delimiterBytes, 0, delimiterBytes.Length);
await installerFile.WriteAsync(configBytes, 0, configBytes.Length);

Log.Information("Config written to installer ({Path})", resultInstallerPath);
