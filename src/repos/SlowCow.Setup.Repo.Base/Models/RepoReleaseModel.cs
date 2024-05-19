namespace SlowCow.Setup.Repo.Base.Models;

public record RepoReleaseModel
{
    public const string DefaultChannel = "stable";

    public required string Version { get; init; }
    public ReleaseNotesModel ReleaseNotes { get; init; } = new ();
    public string Channel { get; init; } = DefaultChannel;

    public record ReleaseNotesModel
    {
        public string? Text { get; init; }
        public string? Link { get; init; }
    }
}
