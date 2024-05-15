using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
namespace SlowCow.Setup.Modules.Setups.LocalSetup.Models;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public record LocalSetupVersionModel
{
    public IReadOnlyCollection<string> ReleaseNotes { get; init; } = [];
}
