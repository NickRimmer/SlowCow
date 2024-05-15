using System;
namespace SlowCow.Setup.Modules.Setups.Base.Exceptions;

public class InstallerException : Exception
{
    public InstallerException(string message) : base(message)
    {
    }

    public InstallerException(string message, Exception inner) : base(message, inner)
    {
    }
}
