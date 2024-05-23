namespace SlowCow.Installers.Base.Models;

/// <summary>
/// Boolean result of an installation operation
/// </summary>
/// <param name="Success">Success when 'true'.</param>
/// <param name="Message">Details message. Can be empty.</param>
public record InstallResult(bool Success, string Message = "")
{
    public static implicit operator bool(InstallResult result) => result.Success;
    public static implicit operator InstallResult(bool success) => new (success);
    public static implicit operator InstallResult((bool, string) args) => new (args.Item1, args.Item2);
}
