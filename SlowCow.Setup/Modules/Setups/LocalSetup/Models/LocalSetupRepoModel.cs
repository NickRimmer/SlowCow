using System.Collections.Generic;
namespace SlowCow.Setup.Modules.Setups.LocalSetup.Models;

public record LocalSetupRepoModel
{
    public Dictionary<string, List<string>> Channels { get; init; } = new ();
}
