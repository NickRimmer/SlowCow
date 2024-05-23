using System.Diagnostics.CodeAnalysis;
namespace SlowCow.Apps.InstallerBuilder.Models;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")] // used by serialization
public record AppSettingsModel
{
    public required string BaseInstallerPath { get; init; }
    public bool CreateCopy { get; init; } = true;
    public string OutputName { get; init; } = string.Empty;
    public string OutputFolder { get; init; } = string.Empty;

    public required object InstallerSettings { get; init; }
}
