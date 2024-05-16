# SlowCow.Setup.Windows.Installer

The `SlowCow.Setup.Windows.Installer` library provides an implementation of the `IInstaller` interface specifically for Windows environments. This library builds on the base functionality provided by the `SlowCow.Setup.Base` library, offering additional features tailored for creating Windows installers.

## Installation

You can install it via NuGet Package Manager. Use the following command:

```shell
dotnet add package SlowCow.Setup.Windows.Installer
```

Or via the Package Manager Console:

```shell
Install-Package SlowCow.Setup.Windows.Installer
```

## Basic Usage

```C#
IInstaller? installer = null;
var installerSettings = new InstallerSettingsModel {
    ApplicationId = /* your application Id */,
    ApplicationName = /* your application name */,
    ExecutableFileName = /* relative path to executable file */,
};

if (OperatingSystem.IsWindows()) installer = new WindowsInstaller(installerSettings);
```

Check [examples](https://github.com/NickRimmer/SlowCow/tree/main/src/Examples/Example.Setup) for more details.
