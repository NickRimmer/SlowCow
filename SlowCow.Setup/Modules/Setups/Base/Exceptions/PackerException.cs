using System;
namespace SlowCow.Setup.Modules.Setups.Base.Exceptions;

/// <summary>
/// When packer fails to pack the setup.
/// </summary>
public class PackerException : Exception
{
    public PackerException(string message) : base(message)
    {

    }
}
