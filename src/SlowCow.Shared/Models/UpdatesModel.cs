namespace SlowCow.Shared.Models;

// ReSharper disable NotAccessedPositionalProperty.Global
public record UpdatesModel(string? InstalledVersion, string? AvailableVersion, bool UpdateAvailable, string Channel);

