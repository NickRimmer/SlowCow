using System.Diagnostics.CodeAnalysis;
namespace SlowCow.Apps.Publisher.Models;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")] // used in serialization
public record PublishSettingsModel
{
    public string? ReleaseNotes { get; init; }
    public string? Version { get; init; }
    public required string SetupExecutableFullPath { get; init; }
    public required string Path { get; init; }
    public required string Channel { get; init; }
}

public record PublishSettingsModel<TRepoSettings> : PublishSettingsModel
{
    public required TRepoSettings RepoSettings { get; init; }
}
