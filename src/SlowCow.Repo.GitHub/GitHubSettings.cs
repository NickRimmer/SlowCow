namespace SlowCow.Repo.GitHub;

/// <summary>
/// SlowCow GitHub repository settings.
/// </summary>
public record GitHubSettings
{
    /// <summary>
    /// Repository owner.
    /// </summary>
    public required string Owner { get; init; }

    /// <summary>
    /// Repository name.
    /// </summary>
    public required string RepositoryName { get; init; }

    /// <summary>
    /// GitHub access token. Not required for public repositories reading. Required for private repositories reading and writing.
    /// </summary>
    public string? AccessToken { get; init; }
}
