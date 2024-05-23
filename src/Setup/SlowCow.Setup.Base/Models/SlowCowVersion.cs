namespace SlowCow.Setup.Base.Models;

// ReSharper disable NotAccessedPositionalProperty.Global
public record SlowCowVersion(string? InstalledVersion, string? AvailableVersion, bool UpdateAvailable, string Channel);
