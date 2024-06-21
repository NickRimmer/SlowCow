using System.IO.Compression;
using System.Security.Cryptography;
using Serilog;
using SlowCow.Repo.Base.Models;
namespace SlowCow.Libs.Publish;

internal static class Packer
{
    public static async Task<PackerResult?> PackAsync(string sourceDirectory, Dictionary<string, string> lastFullPackHashes)
    {
        // get files
        var files = GetFiles(sourceDirectory);
        if (files.Count == 0)
        {
            Log.Warning("No files found in the specified path");
            return null;
        }

        var result = new PackerResult {
            FullPack = await PackToZipAsync(files, sourceDirectory),
        };

        if (lastFullPackHashes.Count > 0)
        {
            var changedFiles = result
                .FullPack
                .Hashes
                .Where(hash => !lastFullPackHashes.ContainsKey(hash.Key) || lastFullPackHashes[hash.Key] != hash.Value)
                .Select(hash => Path.Combine(sourceDirectory, hash.Key)).ToList();

            if (changedFiles.Count > 0)
            {
                result = result with {
                    DiffPack = await PackToZipAsync(changedFiles, sourceDirectory),
                };
            }
        }

        return result;
    }

    private static async Task<PackedFileInfo> PackToZipAsync(IReadOnlyCollection<string> files, string sourceDirectory)
    {
        var fileHashes = new Dictionary<string, string>();
        var tempFileName = Path.GetTempFileName();
        await using var tempFileStream = new FileStream(tempFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        using var zip = new ZipArchive(tempFileStream, ZipArchiveMode.Create);
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

            Log.Debug("Packed {I} / {FilesCount}: {HashName} ({HashString})", i + 1, files.Count, hashName, hashString);
        }

        return new PackedFileInfo(tempFileName, fileHashes);
    }

    private static IReadOnlyCollection<string> GetFiles(string fullPath)
    {
        var files = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories);
        if (files.Length != 0) return files;

        Log.Warning("No files found in the specified path");
        return [];
    }

    internal record PackerResult : IDisposable
    {
        public void Dispose()
        {
            FullPack.Dispose();
            DiffPack?.Dispose();
        }

        public required PackedFileInfo FullPack { get; init; }
        public PackedFileInfo? DiffPack { get; init; }
    }
}
