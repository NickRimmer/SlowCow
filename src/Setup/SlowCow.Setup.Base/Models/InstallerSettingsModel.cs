namespace SlowCow.Setup.Base.Models;

// ReSharper disable once UnusedAutoPropertyAccessor.Global
public record InstallerSettingsModel
{
    public required string ApplicationName { get; init; }
    public required Guid ApplicationId { get; init; }
    public required string ExecutableFileName { get; init; }
    public string? PublisherName { get; init; }
}
