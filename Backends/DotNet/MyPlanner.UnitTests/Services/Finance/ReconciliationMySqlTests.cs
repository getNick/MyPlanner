using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using MyPlanner.Data.DBContexts;
using MyPlanner.Data.Entities.Finance;
using MyPlanner.Service;
using MyPlanner.Service.Interfaces;
using MyPlanner.Service.Models;
using MyPlanner.Service.Requests.Finance;
using MySqlConnector;
using NUnit.Framework;

namespace MyPlanner.UnitTests.Services.Finance;

/*
 * ReconciliationMySqlTests — Test Coverage Checklist
 * [x] Import failure during Matching rolls back bank ingestion and preserves the Bill.
 * [x] Confirmation and complete-edit rollback.
 * [x] Successful merge cascades Bank Line Items and persists retained/new/removed Bill detail.
 * Real MySQL, opt-in via MYPLANNER_TEST_MYSQL. Each test creates/drops its own generated database;
 * the configured database is never used. Fault injection is at the database save boundary.
 */
[TestFixture]
[Category("MySql")]
public class ReconciliationMySqlTests
{
    private string _connectionString = null!;
    private ApplicationDbContext _context = null!;
    private IFinanceService _sut = null!;
    private readonly SaveFailure _failure = new();
    private Guid _cardId;
    private const string User = "test-household";
    private readonly DateTime _timestamp = DateTime.Today.AddHours(12);

    [SetUp]
    public async Task SetUp()
    {
        var configured = Environment.GetEnvironmentVariable("MYPLANNER_TEST_MYSQL");
        if (string.IsNullOrWhiteSpace(configured))
            Assert.Ignore("Set MYPLANNER_TEST_MYSQL to run isolated MySQL reconciliation tests.");
        var builder = new MySqlConnectionStringBuilder(configured)
        {
            Database = "myplanner_test_" + Guid.NewGuid().ToString("N")
        };
        _connectionString = builder.ConnectionString;
        _failure.Remaining = 0;
        _context = NewContext(injectFailure: true);
        await _context.Database.EnsureCreatedAsync();
        _sut = new FinanceService(_context, Mock.Of<ILlmService>());
        _cardId = await _sut.CreatePaymentMethodAsync(User, new PaymentMethod
        {
            UserId = User, Name = "Test card", Currency = Currency.UAH,
            Type = PaymentMethodType.BankCard, BankProvider = "Monobank"
        });
    }

    private ApplicationDbContext NewContext(bool injectFailure = false)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(_connectionString, new MySqlServerVersion(new Version(8, 0, 0)));
        if (injectFailure) options.AddInterceptors(_failure);
        return new ApplicationDbContext(options.Options);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_context == null) return;
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
        _context = null!;
    }

    private Task<Transaction> ConfirmBill(decimal amount = 20m, List<ReceiptItemDto>? items = null) => _sut.ConfirmReceiptAsync(
        new ConfirmReceiptRequest
        {
            FileStream = new MemoryStream(), ContentType = "image/png",
            Receipt = new ReceiptDto
            {
                MerchantName = "Test merchant", Timestamp = _timestamp, Currency = "UAH",
                TotalAmount = amount, Items = items ?? new()
            }
        }, User);

    private Task<BankStatementImportResult> Import()
    {
        var csv = "\"Дата i час операції\",\"Деталі операції\",MCC,\"Сума в валюті картки (UAH)\",\"Сума в валюті операції\",Валюта,Курс,\"Сума комісій (UAH)\",\"Сума кешбеку (UAH)\",\"Залишок після операції\"\n" +
                  $"\"{_timestamp.AddMinutes(20):dd.MM.yyyy HH:mm:ss}\",\"Test merchant\",5411,-20,-20,UAH,1,—,—,900\n";
        return _sut.ImportBankingFileAsync(new ProcessBankingFileRequest
        {
            FileStream = new MemoryStream(Encoding.UTF8.GetBytes(csv)), ContentType = "text/csv",
            PaymentMethodId = _cardId
        }, User);
    }

    [Test]
    public async Task ImportFailure_RollsBackIngestionAndMatching()
    {
        var bill = await ConfirmBill();
        _failure.Remaining = 2;

        Assert.ThrowsAsync<InvalidOperationException>(() => Import());

        await using var fresh = NewContext();
        var ledger = await new FinanceService(fresh, Mock.Of<ILlmService>()).GetTransactionsAsync(User);
        Assert.Multiple(() =>
        {
            Assert.That(ledger, Has.Count.EqualTo(1));
            Assert.That(ledger.Single().Id, Is.EqualTo(bill.Id));
            Assert.That(ledger.Single().DataOrigin, Is.EqualTo(DataOrigin.Receipt));
        });
    }

    [Test]
    public async Task ConfirmationFailure_RollsBackBillAndMatching()
    {
        var bank = (await Import()).Transactions.Single();
        _failure.Remaining = 2;

        Assert.ThrowsAsync<InvalidOperationException>(() => ConfirmBill());

        await using var fresh = NewContext();
        var ledger = await new FinanceService(fresh, Mock.Of<ILlmService>()).GetTransactionsAsync(User);
        Assert.That(ledger.Select(t => (t.Id, t.DataOrigin)),
            Is.EqualTo(new[] { (bank.Id, DataOrigin.Bank) }));
    }

    [Test]
    public async Task CompleteEditFailure_RollsBackHeaderItemsAndMatching()
    {
        var bill = await ConfirmBill(12m);
        await Import();
        var header = new Transaction
        {
            Id = bill.Id, UserId = User, Type = TransactionType.Expense, DataOrigin = DataOrigin.Receipt,
            Timestamp = _timestamp, Amount = 20m, Currency = Currency.UAH, Description = "Corrected"
        };
        var items = new[]
        {
            new TransactionItem
            {
                TransactionId = bill.Id, Name = "New detail", FullName = "New detail",
                TotalPrice = 20m, PricePerUnit = 20m, Quantity = 1
            }
        };
        _failure.Remaining = 2;

        Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateTransactionAsync(User, header, items));

        await using var fresh = NewContext();
        var ledger = await new FinanceService(fresh, Mock.Of<ILlmService>()).GetTransactionsAsync(User);
        var storedBill = ledger.Single(t => t.Id == bill.Id);
        Assert.Multiple(() =>
        {
            Assert.That(ledger, Has.Count.EqualTo(2));
            Assert.That(storedBill.Amount, Is.EqualTo(12m));
            Assert.That(storedBill.Description, Is.EqualTo("Test merchant"));
            Assert.That(storedBill.Items, Is.Empty);
            Assert.That(storedBill.DataOrigin, Is.EqualTo(DataOrigin.Receipt));
        });
    }

    [Test]
    public async Task SuccessfulCompleteSave_PersistsSurvivingBillAndReplacementDetail()
    {
        var bill = await ConfirmBill(12m, new()
        {
            new ReceiptItemDto { Name = "Retain", FullName = "Retain", UnitPrice = 7m, TotalPrice = 7m },
            new ReceiptItemDto { Name = "Remove", FullName = "Remove", UnitPrice = 5m, TotalPrice = 5m }
        });
        var bank = (await Import()).Transactions.Single();
        var retainedId = bill.Items.Single(i => i.Name == "Retain").Id;
        var removedId = bill.Items.Single(i => i.Name == "Remove").Id;
        bill.Amount = 20m;
        var result = await _sut.UpdateTransactionAsync(User, bill, new[]
        {
            new TransactionItem
            {
                Id = retainedId, TransactionId = bill.Id, Name = "Retained", FullName = "Retained",
                Quantity = 1, PricePerUnit = 15m, TotalPrice = 15m
            },
            new TransactionItem
            {
                TransactionId = bill.Id, Name = "New", FullName = "New", Quantity = 1,
                PricePerUnit = 3m, TotalPrice = 3m
            }
        });

        await using var fresh = NewContext();
        var service = new FinanceService(fresh, Mock.Of<ILlmService>());
        var ledger = await service.GetTransactionsAsync(User);
        var stored = ledger.Single();
        var deletedBank = await service.GetTransactionAsync(bank.Id, User);
        var deletedBankItems = await service.GetTransactionItemsAsync(bank.Id, User);
        var deletedBillItem = await service.GetTransactionItemAsync(removedId, User);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Id, Is.EqualTo(bill.Id));
            Assert.That(result.DataOrigin, Is.EqualTo(DataOrigin.Reconciled));
            Assert.That(stored.Id, Is.EqualTo(bill.Id));
            Assert.That(stored.MoneyDelta, Is.EqualTo(2m));
            Assert.That(stored.Items.Select(i => i.Name), Is.EquivalentTo(new[] { "Retained", "New" }));
            Assert.That(stored.Items.Single(i => i.Name == "Retained").Id, Is.EqualTo(retainedId));
            Assert.That(deletedBank, Is.Null);
            Assert.That(deletedBankItems, Is.Empty);
            Assert.That(deletedBillItem, Is.Null);
        });
    }

    private sealed class SaveFailure : SaveChangesInterceptor
    {
        public int Remaining { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Remaining > 0 && --Remaining == 0)
                throw new InvalidOperationException("Injected database save failure.");
            return ValueTask.FromResult(result);
        }
    }
}
