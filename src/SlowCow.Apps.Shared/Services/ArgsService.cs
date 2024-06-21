using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
namespace SlowCow.Apps.Shared.Services;

public class ArgsService
{
    private readonly IConfigurationRoot _config;

    public ArgsService(string[] args)
    {
        RawArguments = args ?? throw new ArgumentNullException(nameof(args));

        var cleanedArgs = CleanArgs(args).ToArray();
        _config = new ConfigurationBuilder()
            .AddCommandLine(cleanedArgs)
            .Build();
    }

    public IReadOnlyCollection<string> RawArguments { get; }

    public IReadOnlyCollection<KeyValuePair<string, string>> RawCommands => _config
        .GetChildren()
        .Select(section => new KeyValuePair<string, string>(section.Key, section.Value ?? string.Empty))
        .ToList();


    public bool TryGetValue(string name, [NotNullWhen(true)] out string? result)
    {
        result = _config[name];
        return !string.IsNullOrWhiteSpace(result);
    }

    public bool ContainsFlag(string name) => RawArguments.Contains($"--{name}", StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> CleanArgs(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            yield return args[i];

            // catch example `--flag --property "value"`
            // and convert it to `--flag "true" --property "value"`

            var isNotLast = i + 1 < args.Length;
            if (isNotLast && args[i].StartsWith("--") && args[i + 1].StartsWith("--"))
                yield return "true";
        }
    }
}
