using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using MyPlanner.Data.DBContexts;
using MyPlanner.Data.Entities.Finance;
using MyPlanner.Service;
using MyPlanner.Service.Interfaces;
using MyPlanner.Service.Models;

namespace MyPlanner.UnitTests.Services.Finance;

/*
 * ────────────────────────────────────────────────────────────
 *  Test infrastructure
 * ────────────────────────────────────────────────────────────
 *   [x] In-memory ApplicationDbContext per test (unique DB name)
 *   [x] ILlmService mock returning empty receipt JSON
 *   [x] FinanceService instantiated with context + LLM mock + a real Bucket in a throwaway folder
 *       (the Bucket is never faked: these tests assert what landed on disk)
 *   [x] Cleanup: database deleted, context disposed, Bucket folder deleted
 *
 */

/// <summary>
/// Shared base class for all FinanceService tests.
/// Provides in-memory DB context, LLM mock, and the service under test.
/// </summary>
public abstract class FinanceServiceTests_Base : IDisposable
{
    protected ApplicationDbContext _testContext;
    protected Mock<ILlmService> _llmServiceMock;
    protected IFinanceService _sut;
    protected string _testUserId = "test-user-123";
    private string _bucketPath = null!;
    private IBucketStore _bucket = null!;
    private bool _disposed;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _testContext = new ApplicationDbContext(options);
        _llmServiceMock = new Mock<ILlmService>();
        _llmServiceMock
            .Setup(x => x.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ReturnsAsync("{\"items\":[],\"totalAmount\":0}");

        _bucketPath = Path.Combine(Path.GetTempPath(), "myplanner-bucket-" + Guid.NewGuid().ToString("N"));
        _bucket = new BucketStore(Options.Create(new BucketOptions { Path = _bucketPath }));

        _sut = new FinanceService(_testContext, _llmServiceMock.Object, _bucket);
    }

    /// <summary>FileKeys sitting in the Bucket — what a backup would copy.</summary>
    protected string[] FileKeysInBucket() =>
        Directory.Exists(_bucketPath)
            ? Directory.GetFiles(_bucketPath).Select(name => Path.GetFileName(name)!).ToArray()
            : Array.Empty<string>();

    protected async Task<byte[]> StoredBytesAsync(string fileKey)
    {
        await using var stream = await _bucket.OpenAsync(fileKey);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    /// <summary>Re-reads a ledger row from the database rather than trusting what a call handed back.</summary>
    protected Transaction GetTransaction(Guid id) => _testContext.Transactions.AsNoTracking().Single(t => t.Id == id);

    /// <summary>Reads a row's raw material the only way it can be read.</summary>
    protected static RawTransactionDataEnvelope EnvelopeOf(Transaction transaction) =>
        RawTransactionDataEnvelope.Read(transaction.RawTransactionData);

    [TearDown]
    public void TearDown()
    {
        if (!_disposed)
        {
            _testContext?.Database?.EnsureDeleted();
            try { Directory.Delete(_bucketPath, recursive: true); } catch (IOException) { }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _testContext?.Dispose();
            _disposed = true;
        }
    }
}
