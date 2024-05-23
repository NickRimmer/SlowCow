namespace SlowCow.Repo.Base.Models;

/// <summary>
/// Packed file.
/// </summary>
public record PackedFileInfo : IDisposable
{
    /// <inheritdoc cref="PackedFileInfo"/>
    public PackedFileInfo(string tempPath, Dictionary<string, string> hashes)
    {
        if (hashes is not { Count: > 0 }) throw new ArgumentException("At least one hash must be provided.", nameof(hashes));
        if (string.IsNullOrWhiteSpace(tempPath)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(tempPath));

        TempPath = tempPath;
        Hashes = hashes;
    }

    /// <summary>
    /// List of file names with hashes.
    /// </summary>
    public Dictionary<string, string> Hashes { get; }

    /// <summary>
    /// Path to temporary saved data. Will be removed on dispose.
    /// </summary>
    public string TempPath { get; }

    public void Dispose()
    {
        if (File.Exists(TempPath)) File.Delete(TempPath);
    }
}
