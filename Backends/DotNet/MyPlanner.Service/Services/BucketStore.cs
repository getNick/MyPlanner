using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MyPlanner.Service.Exceptions;
using MyPlanner.Service.Interfaces;
using MyPlanner.Service.Models;

namespace MyPlanner.Service;

/// <summary>
/// The Bucket: one flat folder, every upload named by the SHA-256 of its bytes plus an extension taken
/// from the declared content type. Naming by content is what makes dedup free — two Bills photographed
/// from the same paper share one file, and re-importing a statement stores it once (D3).
/// </summary>
public sealed partial class BucketStore : IBucketStore
{
    private readonly string _root;

    public BucketStore(IOptions<BucketOptions> options)
    {
        _root = Path.GetFullPath((options.Value ?? new BucketOptions()).Path ?? BucketOptions.DefaultPath);
    }

    /// <summary>Declared content types mapped to the extension stored on disk. A bank that labels its
    /// CSV export as a spreadsheet still sent text, so the name says text.</summary>
    private static readonly Dictionary<string, string> _extensionByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/webp"] = ".webp",
        ["image/bmp"] = ".bmp",
        ["text/csv"] = ".csv",
        ["application/csv"] = ".csv",
        ["application/vnd.ms-excel"] = ".csv",
        ["text/plain"] = ".txt",
    };

    /// <summary>What an undeclared or unrecognised upload is called. The bytes are kept either way —
    /// the extension only records what the uploader claimed.</summary>
    private const string UnknownExtension = ".bin";

    [GeneratedRegex(@"^[0-9a-f]{64}(\.[a-z0-9]{1,10})?$")]
    private static partial Regex FileKeyPattern();

    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);

        // Written under a temporary name first, then moved into its final name in one step: a
        // half-written upload never appears as a FileKey.
        var temp = Path.Combine(_root, $".{Guid.NewGuid():N}.uploading");
        try
        {
            var hash = Convert.ToHexString(await HashToTempFileAsync(content, temp, cancellationToken)).ToLowerInvariant();
            var declaredExtension = ExtensionFor(contentType);

            // Collision is by content, not by name: the same bytes may arrive again declaring a different
            // type. First write wins — including its extension — so one upload cannot split into two
            // files and dedup stays deterministic (D25).
            var existing = ResolveExistingFileKey(hash);
            if (existing is not null)
                return existing;

            try
            {
                File.Move(temp, Path.Combine(_root, hash + declaredExtension), overwrite: false);
                return hash + declaredExtension;
            }
            catch (IOException)
            {
                // Lost a race with an identical upload; theirs is now the one on disk.
                return ResolveExistingFileKey(hash) ?? hash + declaredExtension;
            }
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public Task<Stream> OpenAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        // A FileKey is one filename inside the Bucket, never a path that escapes it.
        if (!IsFileKey(fileKey))
            throw new ArgumentException(
                $"'{fileKey}' is not a FileKey: a FileKey is a content-hash filename inside the Bucket.",
                nameof(fileKey));

        var path = Path.Combine(_root, fileKey);
        if (!File.Exists(path))
            throw new FileNotFoundException($"No stored upload with FileKey '{fileKey}'.", fileKey);

        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    /// <summary>Whether the declared type is known. Unknown types are stored, just not renamed.</summary>
    private static string ExtensionFor(string contentType)
    {
        var declared = (contentType ?? string.Empty).Split(';')[0].Trim();
        return _extensionByContentType.TryGetValue(declared, out var extension) ? extension : UnknownExtension;
    }

    /// <summary>
    /// Copies the upload to a temp file in the Bucket while hashing it, so the bytes are read once and
    /// the finished file can be moved into its final name in one step — a half-written upload never
    /// appears under a FileKey.
    /// </summary>
    private static async Task<byte[]> HashToTempFileAsync(Stream content, string temp, CancellationToken cancellationToken)
    {
        using var hashing = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using (var write = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            long written = 0;
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                written += read;
                if (written > UploadLimit.MaxBytes)
                    throw new UploadTooLargeException("An uploaded file");

                hashing.AppendData(buffer, 0, read);
                await write.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }

        return hashing.GetHashAndReset();
    }

    /// <summary>Finds the name the same content is already stored under, whatever extension it got.</summary>
    private string? ResolveExistingFileKey(string hash)
    {
        foreach (var path in Directory.EnumerateFiles(_root))
        {
            var name = Path.GetFileName(path);
            if (name.Equals(hash, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(hash + ".", StringComparison.OrdinalIgnoreCase))
                return name;
        }

        return null;
    }


    private static bool IsFileKey(string fileKey) => FileKeyPattern().IsMatch(fileKey ?? string.Empty);
}
