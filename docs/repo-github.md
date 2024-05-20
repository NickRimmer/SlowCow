# SlowCow.Setup.Repo.GitHub

The `SlowCow.Setup.Repo.GitHub` library provides an implementation of the `IRepo` interface for retrieving resources from GitHub releases. This library builds on the base functionality provided by the `SlowCow.Setup.Repo.Base` library, offering features tailored for accessing and downloading resources directly from GitHub.

## Installation

You can install it via NuGet Package Manager. Use the following command:

```shell
dotnet add package SlowCow.Setup.Repo.GitHub
```

Or via the Package Manager Console:

```shell
Install-Package SlowCow.Setup.Repo.GitHub
```

## Basic Usage

```C#
// in this example we will use GitHub as a repository
// use your own GitHub token with read only access to the Releases
var readonlyGhToken = "YOUR-GITHUB-TOKEN";

// that how we can use custom token, for example for writing to the repository
var writerGhToken = args?.FirstOrDefault(x => x.StartsWith("--gh-token="))?.Substring("--gh-token=".Length).Trim();
if (string.IsNullOrWhiteSpace(writerGhToken)) writerGhToken = Environment.GetEnvironmentVariable("GH_TOKEN");

// create repo instance
var ghToken = !string.IsNullOrWhiteSpace(writerGhToken) ? writerGhToken : readonlyGhToken;
var repo = new GitHubRepo("NickRimmer", "SlowCow.ExamplePrivate", ghToken);
```

Check [examples](https://github.com/SlowCow-Project/SlowCow/tree/main/src/Examples/Example.Setup) for more details.
