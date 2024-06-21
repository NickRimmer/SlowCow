using SlowCow.Installers.Base.Models;
namespace SlowCow.Installers.Base.Interfaces;

/// <summary>
/// Installation service.
/// </summary>
public interface IInstaller
{
    /// <summary>
    /// Current installation settings.
    /// </summary>
    InstallationSettings Settings { get; }

    /// <summary>
    /// Get release info of the installed application.
    /// </summary>
    Task<InstalledReleaseInfo> GetInstalledReleaseInfoAsync();

    /// <summary>
    /// Install application.
    /// </summary>
    /// <param name="releaseStream">Package stream.</param>
    /// <param name="hashes">List of files with hashes in package.</param>
    /// <param name="releaseInfo">New release information.</param>
    /// <param name="addDesktopShortcut">Set true to add application shortcut on User Desktop.</param>
    /// <param name="addStartMenuShortcut">Set true to add application shortcut in User Start Menu.</param>
    Task<InstallResult> InstallAsync(Stream releaseStream, Dictionary<string, string> hashes, InstalledReleaseInfo releaseInfo, bool addDesktopShortcut, bool addStartMenuShortcut);

    /// <summary>
    /// Uninstall application.
    /// </summary>
    Task<InstallResult> UninstallAsync();

    /// <summary>
    /// When application uninstalled, this method can be called to clean up if uninstaller restarted with '-uninstall-cleaning' argument.
    /// </summary>
    /// <param name="rawArguments">List of process start arguments.</param>
    /// <param name="parentProcessId">Parent process id. Can be empty if argument wasn't specified on unistalling.</param>
    Task UninstallCleaningAsync(IReadOnlyCollection<string> rawArguments, string parentProcessId);
}
