namespace SlowCow.Updater.Common;

// ReSharper disable NotAccessedPositionalProperty.Global
public record SlowCowVersion(string? InstalledVersion, string? AvailableVersion, bool UpdateAvailable, string Channel);
