using System.Diagnostics.CodeAnalysis;
using SlowCow.Installers.Base.Models;
using SlowCow.Repo.GitHub;
namespace SlowCow.Apps.InstallerBase.Models;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")] // used by serialization
public record AppSettingsModel
{
    public required GitHubSettings RepoSettings { get; init; }
    public required InstallationSettings InstallationSettings { get; init; }
    public bool AddDesktopShortcut { get; init; } = true;
    public bool AddStartMenuShortcut { get; init; } = true;
}
