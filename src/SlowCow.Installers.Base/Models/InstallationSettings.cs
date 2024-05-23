namespace SlowCow.Installers.Base.Models;

/// <summary>
/// Installation settings.
/// </summary>
public record InstallationSettings
{
    /// <summary>
    /// Application display name.
    /// </summary>
    public required string ApplicationName { get; init; }

    /// <summary>
    /// Publisher name.
    /// </summary>
    public string? PublisherName { get; init; }

    /// <summary>
    /// Unique identifier of the application.
    /// </summary>
    public required Guid ApplicationId { get; init; }

    /// <summary>
    /// Name of folder with installed app.
    /// </summary>
    public required string ApplicationFolderName { get; init; }

    /// <summary>
    /// Source channel of the installation.
    /// </summary>
    public required string Channel { get; init; }

    /// <summary>
    /// Path to the executable relative to the installation folder.
    /// </summary>
    public required string ExecutableRelativePath { get; init; }
}
