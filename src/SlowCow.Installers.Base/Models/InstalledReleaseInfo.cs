namespace SlowCow.Installers.Base.Models;

/// <summary>
/// Contains information about the installed release.
/// </summary>
public record InstalledReleaseInfo
{
    /// <summary>
    /// Channel of the installed release.
    /// </summary>
    public required string Channel { get; init; }

    /// <summary>
    /// Version of the installed release.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// List of hashes of the installed files.
    /// </summary>
    public required Dictionary<string, string> Hashes { get; init; }

    /// <summary>
    /// Relative path to the executable file.
    /// </summary>
    public required string ExecutableRelativePath { get; init; }

    /// <summary>
    /// Unique identifier of the application.
    /// </summary>
    public required Guid ApplicationId { get; init; }

    /// <summary>
    /// Empty instance of <see cref="InstalledReleaseInfo"/>.
    /// </summary>
    public static readonly InstalledReleaseInfo Empty = new () {
        Channel = string.Empty,
        Hashes = new (),
        Version = string.Empty,
        ExecutableRelativePath = string.Empty,
        ApplicationId = Guid.Empty,
    };
}
