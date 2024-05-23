using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SlowCow.Setup.Repo.Base.Models;
namespace SlowCow.Setup.Repo.Base;

public class Packer
{
    private readonly ILogger _logger;
    public Packer(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PackResultModel?> PackAsync(string sourceDirectory, Dictionary<string, string> lastFullPackHashes)
    {
        // get files
        var files = GetFiles(sourceDirectory);
        if (files.Count == 0)
        {
            _logger.LogWarning("No files found in the specified path");
            return null;
        }

        var result = new PackResultModel {
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

    private async Task<PackerResult> PackToZipAsync(IReadOnlyCollection<string> files, string sourceDirectory)
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

            _logger.LogDebug("Packed {I} / {FilesCount}: {HashName} ({HashString})", i + 1, files.Count, hashName, hashString);
        }

        return new PackerResult(tempFileName, fileHashes);
    }

    private IReadOnlyCollection<string> GetFiles(string fullPath)
    {
        var files = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories);
        if (files.Length != 0) return files;

        _logger.LogWarning("No files found in the specified path");
        return [];
    }

    public class PackerResult : IDisposable
    {
        public PackerResult(string tempFilePath, Dictionary<string, string> hashes)
        {
            ArgumentNullException.ThrowIfNull(hashes);

            if (hashes.Count == 0) throw new ArgumentException("Value cannot be an empty collection.", nameof(hashes));
            if (string.IsNullOrWhiteSpace(tempFilePath)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(tempFilePath));

            TempFilePath = tempFilePath;
            Hashes = hashes;
        }

        public string TempFilePath { get; }
        public Dictionary<string, string> Hashes { get; }

        public void Dispose()
        {
            if (File.Exists(TempFilePath)) File.Delete(TempFilePath);
        }
    }
}
