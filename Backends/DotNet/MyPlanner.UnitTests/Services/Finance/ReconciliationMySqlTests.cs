using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
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
    private string _bucketPath = null!;
    private IBucketStore _bucket = null!;
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
        _bucketPath = Path.Combine(Path.GetTempPath(), "myplanner-bucket-" + Guid.NewGuid().ToString("N"));
        _bucket = new BucketStore(Options.Create(new BucketOptions { Path = _bucketPath }));
        _context = NewContext(injectFailure: true);
        await _context.Database.EnsureCreatedAsync();
        _sut = Service(_context);
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

    /// <summary>Every service here writes to a real Bucket in a throwaway folder.</summary>
    private IFinanceService Service(ApplicationDbContext context) =>
        new FinanceService(context, Mock.Of<ILlmService>(), _bucket);

    [TearDown]
    public async Task TearDown()
    {
        if (_context == null) return;
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
        _context = null!;
        try { Directory.Delete(_bucketPath, recursive: true); } catch (IOException) { }
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
        var ledger = await Service(fresh).GetTransactionsAsync(User);
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
        var ledger = await Service(fresh).GetTransactionsAsync(User);
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
        var ledger = await Service(fresh).GetTransactionsAsync(User);
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
                PricePerUnit = 5m, TotalPrice = 5m
            }
        });
        // Once Reconciled, real receipt corrections may diverge from the bank amount.
        var correctedItems = result!.Items.Select(item => new TransactionItem
        {
            Id = item.Id, TransactionId = result.Id, Name = item.Name, FullName = item.FullName,
            Quantity = 1, PricePerUnit = item.Name == "New" ? 3m : item.PricePerUnit,
            TotalPrice = item.Name == "New" ? 3m : item.TotalPrice
        }).ToArray();
        result = await _sut.UpdateTransactionAsync(User, result, correctedItems);

        await using var fresh = NewContext();
        var service = Service(fresh);
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

    [Test]
    public async Task BankReplacementFailure_RollsBackDetailAndPreservesBankFactsAndMcc()
    {
        var bank = (await Import()).Transactions.Single();
        var originalItemId = bank.Items.Single().Id;
        _failure.Remaining = 1;
        Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateTransactionAsync(User, bank, new[]
        {
            new TransactionItem { TransactionId = bank.Id, Name = "Typed", FullName = "Typed", Quantity = 1, PricePerUnit = 7m, TotalPrice = 7m }
        }, bank.DetailVersion));
        await using var fresh = NewContext();
        var saved = (await Service(fresh).GetTransactionAsync(bank.Id, User))!;
        Assert.Multiple(() =>
        {
            Assert.That(saved.Amount, Is.EqualTo(20m));
            Assert.That(saved.DataOrigin, Is.EqualTo(DataOrigin.Bank));
            Assert.That(saved.Items.Single().Id, Is.EqualTo(originalItemId));
            Assert.That(saved.Items.Single().Origin, Is.EqualTo(ItemOrigin.AutoGenerated));
            Assert.That(RawTransactionDataEnvelope.Read(saved.RawTransactionData).Bank!.Category, Is.EqualTo("Groceries"));
        });
    }

    [Test]
    public async Task TypedBankDetail_ProtectsMatchAndReviewSurvivesFreshService()
    {
        var bank = (await Import()).Transactions.Single();
        await _sut.UpdateTransactionAsync(User, bank, new[]
        {
            new TransactionItem { TransactionId = bank.Id, Name = "Typed", FullName = "Typed", Quantity = 1, PricePerUnit = 7m, TotalPrice = 7m, Category = "Dining & Takeaway", Subcategory = "Restaurants" }
        }, bank.DetailVersion);
        await ConfirmBill(20m, new()
        {
            new ReceiptItemDto { Name = "Receipt detail", FullName = "Receipt detail", Quantity = 1, UnitPrice = 20m, TotalPrice = 20m }
        });
        var repeated = await Import();
        await using var fresh = NewContext();
        var ledger = await Service(fresh).GetTransactionsAsync(User);
        Assert.Multiple(() =>
        {
            Assert.That(repeated.DuplicateRowCount, Is.EqualTo(1));
            Assert.That(ledger, Has.Count.EqualTo(2));
            Assert.That(ledger.All(row => row.ReviewCandidates.Count == 1), Is.True);
            Assert.That(ledger.Single(row => row.DataOrigin == DataOrigin.Receipt).Items.Single().Origin, Is.EqualTo(ItemOrigin.ReceiptParsed));
            var saved = ledger.Single(row => row.Id == bank.Id);
            Assert.That(saved.Items.Single().Origin, Is.EqualTo(ItemOrigin.ManualInput));
            Assert.That(RawTransactionDataEnvelope.Read(saved.RawTransactionData).Bank!.Category, Is.EqualTo("Groceries"));
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
