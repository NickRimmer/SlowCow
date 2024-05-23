namespace SlowCow.Repo.Base.Models;

/// <summary>
/// SlowCow repository data detailed version information.
/// </summary>
public record DetailedVersionInfo : VersionInfo
{
    /// <summary>
    /// Release notes information.
    /// </summary>
    public string ReleaseNotes { get; init; } = string.Empty;

    /// <summary>
    /// Published at date and time.
    /// </summary>
    public DateTimeOffset PublishedAt { get; init; }
}
