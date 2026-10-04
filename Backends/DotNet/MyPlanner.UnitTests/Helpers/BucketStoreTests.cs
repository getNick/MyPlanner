using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MyPlanner.Service;
using MyPlanner.Service.Exceptions;
using MyPlanner.Service.Interfaces;
using MyPlanner.Service.Models;
using NUnit.Framework;

namespace MyPlanner.UnitTests.Helpers;

/*
 * ═══════════════════════════════════════════════════════════
 *  BucketStoreTests — Test Coverage Checklist
 * ═══════════════════════════════════════════════════════════
 *
 * Legend:
 *   [x] = implemented & passing
 *   [ ] = not yet implemented
 *
 * The seam is the Bucket module itself: SaveAsync(stream, contentType) → FileKey and
 * OpenAsync(fileKey) → Stream, over a throwaway folder. Nothing here knows about Bills or
 * Bank Transactions — the Bucket has no notion of file kind (Q9/Q19).
 *
 * ────────────────────────────────────────────────────────────
 *  Round trip
 * ────────────────────────────────────────────────────────────
 *   [x] saved bytes come back identical, under a FileKey that is the sha256 of those bytes
 *   [x] re-saving identical bytes stores one file and yields the same FileKey (dedup, D3)
 *   [x] same bytes declared with two content types keep the first write's extension (D25)
 *   [x] extension comes from the declared content type
 *
 * ────────────────────────────────────────────────────────────
 *  Refusals and atomicity
 * ────────────────────────────────────────────────────────────
 *   [x] a file over the shared upload cap is refused and leaves nothing in the Bucket (D11)
 *   [x] a FileKey that is not a plain filename inside the Bucket is refused
 *   [x] only the final name ever appears in the folder — no temp/partial leftovers
 */

[TestFixture]
public class BucketStoreTests
{
    /// <summary>sha256 of "bill image bytes", computed independently (printf | sha256sum).</summary>
    private const string BillImageHash = "aef71bb11bff1967cc218f7da0910c250ae61474561da4294ccc2d92874916de";

    private string _root = null!;
    private IBucketStore _bucket = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "myplanner-bucket-" + Guid.NewGuid().ToString("N"));
        _bucket = new BucketStore(Options.Create(new BucketOptions { Path = _root }));
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private static Stream Bytes(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    private static string HexOf(Stream stream)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private string[] OnDisk() =>
        Directory.Exists(_root)
            ? Directory.GetFiles(_root).Select(name => Path.GetFileName(name)!).ToArray()
            : Array.Empty<string>();

    [Test]
    public async Task SavedFileIsNamedByItsContentHashAndReadsBackByteForByte()
    {
        var fileKey = await _bucket.SaveAsync(Bytes("bill image bytes"), "image/png");

        Assert.Multiple(() =>
        {
            Assert.That(fileKey, Is.EqualTo($"{BillImageHash}.png"),
                "the FileKey is the whole filename: content hash plus extension (D3)");
            Assert.That(OnDisk(), Is.EquivalentTo(new[] { $"{BillImageHash}.png" }),
                "one file, written atomically under its final name — no temp leftovers");
        });

        await using var opened = await _bucket.OpenAsync(fileKey);
        using var reader = new StreamReader(opened);
        Assert.That(reader.ReadToEnd(), Is.EqualTo("bill image bytes"),
            "the Bucket holds the upload verbatim, never a re-encoded copy");
    }

    [Test]
    public async Task ReSavingIdenticalBytes_StoresOneFileAndYieldsTheSameFileKey()
    {
        var first = await _bucket.SaveAsync(Bytes("statement,bytes\n"), "text/csv");
        var second = await _bucket.SaveAsync(Bytes("statement,bytes\n"), "text/csv");

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.EqualTo(first), "a re-uploaded statement costs nothing extra (D3 dedup)");
            Assert.That(OnDisk(), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task SameBytesDeclaredWithDifferentContentTypes_KeepTheFirstWritesExtension()
    {
        var asPng = await _bucket.SaveAsync(Bytes("bill image bytes"), "image/png");
        var undeclared = await _bucket.SaveAsync(Bytes("bill image bytes"), "application/octet-stream");

        Assert.Multiple(() =>
        {
            Assert.That(undeclared, Is.EqualTo(asPng), "first write wins, so dedup stays deterministic (D25)");
            Assert.That(OnDisk(), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task AFileOverTheUploadCap_IsRefusedAndLeavesNothingInTheBucket()
    {
        var tooLarge = new MemoryStream(new byte[UploadLimit.MaxBytes + 1]);

        var ex = Assert.ThrowsAsync<UploadTooLargeException>(async () => await _bucket.SaveAsync(tooLarge, "image/png"));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain(UploadLimit.MaxMegabytes), "the refusal names the limit");
            Assert.That(OnDisk(), Is.Empty, "a refused upload leaves no orphan behind (D12)");
        });
    }

    [TestCase("../../etc/passwd")]
    [TestCase("subfolder/bill.png")]
    [TestCase("not-a-hash.png")]
    public void OpenAsync_RefusesAnythingThatIsNotAFilenameInsideTheBucket(string fileKey)
    {
        Assert.Throws<ArgumentException>(() => _bucket.OpenAsync(fileKey),
            "a FileKey is one filename, never a path that escapes the Bucket (Q19)");
    }

    [Test]
    public async Task ExtensionComesFromTheDeclaredContentType()
    {
        var photo = await _bucket.SaveAsync(Bytes("a photo"), "image/jpeg");
        var statement = await _bucket.SaveAsync(Bytes("a statement"), "text/csv");

        Assert.Multiple(() =>
        {
            Assert.That(Path.GetExtension(photo), Is.EqualTo(".jpg"));
            Assert.That(Path.GetExtension(statement), Is.EqualTo(".csv"));
            Assert.That(HexOf(Bytes("a photo")), Is.EqualTo(Path.GetFileNameWithoutExtension(photo)),
                "the name is still the hash of the bytes, whatever the extension says");
        });
    }
}
