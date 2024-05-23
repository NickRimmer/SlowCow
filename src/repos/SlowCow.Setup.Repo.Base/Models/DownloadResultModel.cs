namespace SlowCow.Setup.Repo.Base.Models;

public record DownloadResultModel : IDisposable
{
    public required Stream PackStream { get; init; }
    public required Dictionary<string, string> Hashes { get; init; }

    public void Dispose() => PackStream.Dispose();
}
