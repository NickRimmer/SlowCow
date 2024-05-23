using System.Diagnostics.CodeAnalysis;
namespace SlowCow.Examples.Publisher;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")] // used in serialization
public record PublishSettings
{
    public string? ReleaseNotes { get; init; }
    public string? Version { get; init; }
    public required string SetupExecutableFullPath { get; init; }
    public required string Path { get; init; }
    public required string Channel { get; init; }
}

public record PublishSettings<TRepoSettings> : PublishSettings
{
    public required TRepoSettings RepoSettings { get; init; }
}
