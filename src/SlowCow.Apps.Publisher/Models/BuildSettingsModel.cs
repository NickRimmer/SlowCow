using System.Diagnostics.CodeAnalysis;
namespace SlowCow.Apps.Publisher.Models;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")] // used by serialization
public record BuildSettingsModel
{
    public required string BaseInstallerPath { get; init; }
    public string OutputName { get; init; } = "SlowCowInstaller";
    public string OutputFolder { get; init; } = string.Empty;

    public required object InstallerSettings { get; init; }
}
