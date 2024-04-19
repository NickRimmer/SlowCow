using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
namespace SlowCow.Setup.Modules.Setups.LocalSetup.Models;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public record LocalSetupPackModel
{
    public IReadOnlyCollection<string> ReleaseNotes { get; init; } = [];
    public string? Version { get; init; }
    public required string Channel { get; init; }
    public required string SourceDirectory { get; init; }
}
