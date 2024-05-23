namespace SlowCow.Repo.Base.Models;

/// <summary>
/// SlowCow repository data version information.
/// </summary>
public record VersionInfo
{
    /// <summary>
    /// Version name.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Channel name.
    /// </summary>
    public required string Channel { get; init; }
}
