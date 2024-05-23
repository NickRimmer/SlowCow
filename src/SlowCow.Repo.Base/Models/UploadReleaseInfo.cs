namespace SlowCow.Repo.Base.Models;

/// <summary>
/// Upload packages information.
/// </summary>
public record UploadReleaseInfo
{
    /// <summary>
    /// Version information.
    /// </summary>
    public required VersionInfo Version { get; init; }

    /// <summary>
    /// Full package data.
    /// </summary>
    public required PackedFileInfo FullPackage { get; init; }

    /// <summary>
    /// Diff package data (optional).
    /// </summary>
    public PackedFileInfo? DiffPackage { get; init; }

    /// <summary>
    /// Release notes.
    /// </summary>
    public string? ReleaseNotes { get; init; }
}
