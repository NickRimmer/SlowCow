namespace SlowCow.Setup.Repo.Base.Models;

public record RepoReleaseModel
{
    public required string Version { get; init; }
    public ReleaseNotesModel ReleaseNotes { get; init; } = new ();
    public required string Channel { get; init; }

    public record ReleaseNotesModel
    {
        public string? Text { get; init; }
        public string? Link { get; init; }
    }
}
