using System.Threading.Tasks;
using SlowCow.Setup.Modules.Setups.Base.Models;
namespace SlowCow.Setup.Modules.Setups.Base;

public interface ISetup
{
    Task PackAsync(string settingsJson);
    Task<ManifestModel?> LoadManifestAsync(string channel);
    Task<byte[]> LoadPackFileAsync(string channel, string loadVersion, string? currentVersion = null);
}
