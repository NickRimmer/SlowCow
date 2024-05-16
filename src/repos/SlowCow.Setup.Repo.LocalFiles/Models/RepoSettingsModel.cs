namespace SlowCow.Setup.Repo.LocalFiles.Models;

public record RepoSettingsModel
{
    public Dictionary<string, List<string>> Channels { get; init; } = new ();
}
