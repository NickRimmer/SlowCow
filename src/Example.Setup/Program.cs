using SlowCow.Setup;
using SlowCow.Setup.Modules.Runner;
using SlowCow.Setup.Modules.Setups.LocalSetup;

var setup = new LocalSetup("X:\\projects\\SlowCow\\_temp\\repo");
await Runner.RunAsync(new RunnerModel {
    Name = "My Awesome App",
    ApplicationId = Guid.Parse("7B0B8ADB-8F6F-4416-B7DB-9E773FD16DF6"),
    Description = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
    ExecutableFileName = "Example.App.exe",
}, setup);
