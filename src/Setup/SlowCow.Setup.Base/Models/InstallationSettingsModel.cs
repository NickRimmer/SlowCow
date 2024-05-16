namespace SlowCow.Setup.Base.Models;

public record InstallationSettingsModel
{
    public required string Channel { get; init; }
    public required string Version { get; init; }
    public required Dictionary<string, string> Hashes { get; init; }
    public bool AddDesktop { get; init; }
    public bool AddStartMenu { get; init; }
}
