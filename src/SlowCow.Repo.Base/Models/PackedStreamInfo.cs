namespace SlowCow.Repo.Base.Models;

/// <summary>
/// Packaged stream.
/// </summary>
public record PackedStreamInfo : IDisposable
{
    /// <inheritdoc cref="PackedStreamInfo"/>
    public PackedStreamInfo(Stream packStream, Dictionary<string, string> hashes, bool isFullPackage)
    {
        if (hashes is not { Count: > 0 }) throw new ArgumentException("At least one hash must be provided.", nameof(hashes));
        if (packStream is not { CanRead: true }) throw new ArgumentException("Stream is null or not readable.", nameof(packStream));

        PackStream = packStream;
        Hashes = hashes;
        IsFullPackage = isFullPackage;
    }

    // for empty instance
    private PackedStreamInfo()
    {
        PackStream = Stream.Null;
        Hashes = new ();
        IsFullPackage = false;
    }

    public Stream PackStream { get; private init; }
    public Dictionary<string, string> Hashes { get; private init; }
    public bool IsFullPackage { get; private init; }

    public void Dispose() => PackStream.Dispose();

    public static readonly PackedStreamInfo Empty = new () {
        PackStream = Stream.Null,
        Hashes = new (),
        IsFullPackage = false,
    };
}
