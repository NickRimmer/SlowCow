# SlowCow.Setup.Windows.Updater

The `SlowCow.Setup.Windows.Updater` library provides an implementation of the `IUpdater` interface specifically for Windows environments. This library builds on the base functionality provided by the `SlowCow.Setup.Base` library, offering additional features tailored for creating Windows updaters.

## Installation

You can install it via NuGet Package Manager. Use the following command:

```shell
dotnet add package SlowCow.Setup.Windows.Updater
```

Or via the Package Manager Console:

```shell
Install-Package SlowCow.Setup.Windows.Updater
```

## Basic Usage

```C#
var logger = /* Microsoft.Extensions.Logging instance */ ;
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new WindowsUpdater(logger);
```

Check [examples](https://github.com/NickRimmer/SlowCow/tree/main/src/Examples/Example.App) for more details.
