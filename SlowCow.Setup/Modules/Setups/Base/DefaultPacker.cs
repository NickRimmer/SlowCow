using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
namespace SlowCow.Setup.Modules.Setups.Base;

public static class DefaultPacker
{
    public const string FullPackName = "pack-full";
    public const string DiffPackName = "pack-diff";

    public static async Task PackAsync(string sourceDirectory, string outputDirectory, Dictionary<string, string> previousVersionHashes)
    {
        // get files
        var files = GetFiles(sourceDirectory);
        if (files.Count == 0)
        {
            Log("No files found in the specified path.");
            return;
        }
        Log($"Files found: {files.Count}");

        // check output directory
        Directory.CreateDirectory(outputDirectory);
        Log($"Output directory: {outputDirectory}");

        // pack files to zip
        var fullPackPath = Path.Combine(outputDirectory, $"{FullPackName}.zip");
        var hashes = await PackToZipAllAsync(files, sourceDirectory, fullPackPath);

        if (previousVersionHashes.Count > 0)
        {
            var changesPackPath = Path.Combine(outputDirectory, $"{DiffPackName}.zip");
            var changedFiles = hashes
                .Where(hash => !previousVersionHashes.ContainsKey(hash.Key) || previousVersionHashes[hash.Key] != hash.Value)
                .Select(hash => Path.Combine(sourceDirectory, hash.Key)).ToList();

            if (changedFiles.Count > 0)
                await PackToZipAllAsync(changedFiles, sourceDirectory, changesPackPath);
        }

        // save hashes
        var filesJson = JsonSerializer.Serialize(hashes, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"{FullPackName}.json"), filesJson);
    }

    private static async Task<Dictionary<string, string>> PackToZipAllAsync(IReadOnlyCollection<string> files, string sourceDirectory, string zipPath)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);

        var fileHashes = new Dictionary<string, string>();
        using var zip = new ZipArchive(File.Create(zipPath), ZipArchiveMode.Create);
        using var sha = SHA256.Create();

        // write to zip
        for (var i = 0; i < files.Count; i++)
        {
            var filePath = files.ElementAt(i);
            var entry = zip.CreateEntry(filePath.Replace(sourceDirectory, string.Empty).TrimStart('\\'));
            await using var entryStream = entry.Open();
            await using var fileStream = File.OpenRead(filePath);
            await fileStream.CopyToAsync(entryStream);

            fileStream.Position = 0;
            var hash = await sha.ComputeHashAsync(fileStream);
            var hashString = BitConverter.ToString(hash).Replace("-", string.Empty);
            var hashName = filePath.Substring(sourceDirectory.Length + 1);
            fileHashes.Add(hashName, hashString);

            Log($"Packed {i + 1} / {files.Count}: {hashName} ({hashString})");
        }

        return fileHashes;
    }

    private static IReadOnlyCollection<string> GetFiles(string fullPath)
    {
        var files = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories);
        if (files.Length != 0) return files;

        Log("No files found in the specified path.");
        return [];
    }

    private static void Log(string message) => Console.WriteLine(message);
}
