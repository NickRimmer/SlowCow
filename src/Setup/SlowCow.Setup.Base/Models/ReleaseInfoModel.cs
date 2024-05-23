namespace SlowCow.Setup.Base.Models;

public record ReleaseInfoModel
{
    public required string Channel { get; init; }
    public required string Version { get; init; }
    public required Dictionary<string, string> Hashes { get; init; }
}
