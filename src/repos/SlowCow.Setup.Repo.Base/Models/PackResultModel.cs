namespace SlowCow.Setup.Repo.Base.Models;

public record PackResultModel : IDisposable
{
    public required Packer.PackerResult FullPack { get; init; }
    public Packer.PackerResult? DiffPack { get; init; }

    public void Dispose()
    {
        FullPack.Dispose();
        DiffPack?.Dispose();
    }
}
