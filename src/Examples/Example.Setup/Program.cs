using SlowCow.Setup;
using SlowCow.Setup.Base.Interfaces;
using SlowCow.Setup.Base.Loggers;
using SlowCow.Setup.Base.Models;
using SlowCow.Setup.Repo.GitHub;
using SlowCow.Setup.Windows.Installer;

// define Setup settings
var settings = new RunnerSettingsModel {
    Name = "My Awesome App",
    ApplicationId = Guid.Parse("7B0B8ADB-8F6F-4416-B7DB-9E773FD16DF6"),
    Description = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
    ExecutableFileName = "Example.App.exe",
    Channel = "preview",
};

// in this example we will use GitHub as a repository
// use your own GitHub token with read only access to the Releases
var readonlyGhToken = "github_pat_11AANMJGI0aRETg2gfhG8N_Yx9Qw4rW " +
    "KFC4ND2TDIcHlakJGdwZmChKZeAfGiGV097DQCBKFYCIFkuXRzO"; // otherwise GitHub reject pushes

// that how we can use custom token, for example for writing to the repository
var writerGhToken = args?.FirstOrDefault(x => x.StartsWith("--gh-token="))?.Substring("--gh-token=".Length).Trim();
if (string.IsNullOrWhiteSpace(writerGhToken)) writerGhToken = Environment.GetEnvironmentVariable("GITHUB_PAT");

// create repo instance
var ghToken = !string.IsNullOrWhiteSpace(writerGhToken) ? writerGhToken : readonlyGhToken;
var repo = new GitHubRepo("SlowCow-Project", "Repo.Private", ghToken);

// now we need to create installer for the current operating system
IInstaller? installer = null;
var installerSettings = new InstallerSettingsModel {
    ApplicationId = settings.ApplicationId,
    ApplicationName = settings.Name,
    ExecutableFileName = settings.ExecutableFileName,
};
if (OperatingSystem.IsWindows()) installer = new WindowsInstaller(installerSettings);
if (installer == null) throw new NotSupportedException("Unsupported operating system.");

// everything looks ready for our party (;
await Runner.RunAsync(settings, repo, installer, new TempLogger(settings.ApplicationId));
