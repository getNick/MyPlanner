using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using MyPlanner.Data.DBContexts;
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
 *   [x] FinanceService instantiated with context + LLM mock
 *   [x] Cleanup: database deleted, context disposed
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
        _sut = new FinanceService(_testContext, _llmServiceMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        if (!_disposed)
        {
            _testContext?.Database?.EnsureDeleted();
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
