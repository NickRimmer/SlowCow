using System;
namespace SlowCow.Setup.Modules.Setups.Base.Exceptions;

/// <summary>
///     When something goes wrong with the loader.
/// </summary>
public class LoaderException : Exception
{
    /// <inheritdoc cref="LoaderException" />
    public LoaderException(string message) : base(message)
    {
    }

    /// <inheritdoc cref="LoaderException" />
    public LoaderException(string message, Exception inner) : base(message, inner)
    {
    }
}
