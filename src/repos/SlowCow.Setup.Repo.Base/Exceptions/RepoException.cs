namespace SlowCow.Setup.Repo.Base.Exceptions;

/// <summary>
///     When something goes wrong with the loader.
/// </summary>
public class RepoException : Exception
{
    /// <inheritdoc cref="RepoException" />
    public RepoException(string message) : base(message)
    {
    }

    /// <inheritdoc cref="RepoException" />
    public RepoException(string message, Exception inner) : base(message, inner)
    {
    }
}
