using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyPlanner.Data.Entities.Finance;
using MyPlanner.Service.Exceptions;
using MyPlanner.Service.Models;
using MyPlanner.Service.Requests.Finance;
using NUnit.Framework;

namespace MyPlanner.UnitTests.Services.Finance;

/*
 * ═══════════════════════════════════════════════════════════
 *  BankStatementImportTests — Test Coverage Checklist
 * ═══════════════════════════════════════════════════════════
 *
 * The seam is the finance service: PreviewBankingFileAsync / ImportBankingFileAsync over the real
 * Monobank fixture CSV, with assertions on what the ledger looks like afterwards. These replace
 * the three old Banking*Tests files that were named after ProcessBankingFileAsync but never
 * called it. Parsing itself is covered at the parser seam (Helpers/BankExportParserTests.cs).
 *
 * ────────────────────────────────────────────────────────────
 *  Preview — says what the file says, stores nothing
 * ────────────────────────────────────────────────────────────
 *   [x] Row count, date range (first/last) echoed from the fixture
 *   [x] Needs-review rows listed with row number and reason
 *   [x] Nothing persisted
 *
 * ────────────────────────────────────────────────────────────
 *  Import — post-insert ledger state
 * ────────────────────────────────────────────────────────────
 *   [x] Every readable row of the fixture lands in the ledger
 *   [x] Amount stored absolute; money role (Expense/Income) derived from the sign, backend-side
 *   [x] Foreign row: original amount + currency kept, UAH base amount beside it
 *   [x] The bank's "EURO" spelling lands as the stored EURO currency, not the card fallback
 *   [x] MCC-derived category on the auto-generated item; absent MCC leaves it null
 *   [x] DataOrigin Bank, BalanceAfter kept
 *   [x] Undated row NOT inserted, reported in the result instead
 *   [x] Target card recorded on every row
 *
 * ────────────────────────────────────────────────────────────
 *  Re-import — duplicates counted, not doubled
 * ────────────────────────────────────────────────────────────
 *   [x] Second import of the same file inserts nothing and reports every row as a duplicate
 *
 * ────────────────────────────────────────────────────────────
 *  Refusals — explicit, before anything is read or written
 * ────────────────────────────────────────────────────────────
 *   [x] Target with no Bank Provider set → refused, ledger untouched (no default profile)
 *   [x] Target whose bank has no profile → refused naming the bank
 *   [x] Non-CSV upload (a PDF) → refused as not a statement, never routed to the image pipeline
 *   [x] Unknown payment method id → refused
 */

[TestFixture]
public class BankStatementImportTests : FinanceServiceTests_Base
{
    private static string FixturePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "test-banking-file.csv");

    private Guid _cardId;
    private PaymentMethod _card = null!;

    [SetUp]
    public async Task SeedCard()
    {
        _card = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Name = "Monobank black",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.UAH,
            BankProvider = "Monobank",
        };
        _cardId = _card.Id;
        _testContext.PaymentMethods.Add(_card);
        await _testContext.SaveChangesAsync();
    }

    private static ProcessBankingFileRequest FixtureRequest(Guid paymentMethodId) => new()
    {
        FileStream = File.OpenRead(FixturePath),
        ContentType = "text/csv",
        PaymentMethodId = paymentMethodId,
    };

    private Task<BankStatementImportResult> ImportFixtureAsync(Guid? paymentMethodId = null) =>
        _sut.ImportBankingFileAsync(FixtureRequest(paymentMethodId ?? _cardId), _testUserId);

    private Task<BankStatementSummary> PreviewFixtureAsync() =>
        _sut.PreviewBankingFileAsync(FixtureRequest(_cardId), _testUserId);

    private List<Transaction> Ledger() => _testContext.Transactions
        .Include(t => t.Items)
        .OrderBy(t => t.Timestamp)
        .ToList();

    // ── Preview ────────────────────────────────────────────────────────

    [Test]
    public async Task Preview_EchoesTheFixtureRowCountAndDateRange_AndStoresNothing()
    {
        var summary = await PreviewFixtureAsync();

        Assert.Multiple(() =>
        {
            Assert.That(summary.RowCount, Is.EqualTo(42));
            Assert.That(summary.FirstTimestamp, Is.EqualTo(new DateTime(2026, 7, 1, 9, 9, 25)));
            Assert.That(summary.LastTimestamp, Is.EqualTo(new DateTime(2026, 7, 11, 17, 30, 53)));
            Assert.That(summary.PaymentMethodId, Is.EqualTo(_cardId));
            Assert.That(summary.PaymentMethodName, Is.EqualTo("Monobank black"));
            Assert.That(summary.BankProvider, Is.EqualTo("Monobank"));
            Assert.That(Ledger(), Is.Empty, "a preview is not an import");
        });
    }

    [Test]
    public async Task Preview_ListsTheUndatedFixtureRowAsNeedingReview_WithRowNumberAndReason()
    {
        var summary = await PreviewFixtureAsync();

        var review = summary.NeedsReview.Single();
        Assert.That(review.RowNumber, Is.EqualTo(44), "header is line 1; the undated row is the last of 44 lines");
        Assert.That(review.Description, Is.EqualTo("Переказ готівкою через термінал"));
        Assert.That(review.Reason, Does.Contain("date"));
    }

    // ── Import — post-insert ledger state ──────────────────────────────

    [Test]
    public async Task Import_InsertsEveryReadableRowOfTheFixture_AndKeepsTheUndatedOneOut()
    {
        var result = await ImportFixtureAsync();

        var ledger = Ledger();
        Assert.Multiple(() =>
        {
            Assert.That(result.InsertedRowCount, Is.EqualTo(42));
            Assert.That(result.DuplicateRowCount, Is.EqualTo(0));
            Assert.That(ledger, Has.Count.EqualTo(42));
            Assert.That(ledger.Any(t => t.Description == "Переказ готівкою через термінал"), Is.False,
                "the undated row is reported for review, never inserted");
            Assert.That(result.Summary.NeedsReview, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Import_StoresAmountAbsoluteWithTheMoneyRoleDerivedFromTheSign()
    {
        await ImportFixtureAsync();

        var ledger = Ledger();
        var spend = ledger.Single(t => t.Description == "NOVUS" && t.Timestamp!.Value.Day == 11);
        var reversal = ledger.Single(t => t.Description == "Скасування. PUMA");

        Assert.Multiple(() =>
        {
            Assert.That(spend.Amount, Is.EqualTo(80.0m));
            Assert.That(spend.Type, Is.EqualTo(TransactionType.Expense), "the backend derives the money role; clients read it");
            Assert.That(reversal.Amount, Is.EqualTo(2451.0m));
            Assert.That(reversal.Type, Is.EqualTo(TransactionType.Income));
        });
    }

    [Test]
    public async Task Import_KeepsOriginalAmountAndCurrencyWithTheUahBaseAmountBesideIt()
    {
        await ImportFixtureAsync();

        var aliexpress = Ledger().Single(t => t.Description == "AliExpress");

        Assert.Multiple(() =>
        {
            Assert.That(aliexpress.Amount, Is.EqualTo(13.37m), "what the purchase cost in dollars");
            Assert.That(aliexpress.Currency, Is.EqualTo(Currency.USD));
            Assert.That(aliexpress.BaseAmount, Is.EqualTo(598.99m), "the card-currency figure from the statement");
            Assert.That(aliexpress.Type, Is.EqualTo(TransactionType.Expense));
        });
    }

    [Test]
    public async Task Import_StoresTheUahBaseAmountOnDomesticRowsToo()
    {
        await ImportFixtureAsync();

        var novus = Ledger().Single(t => t.Description == "NOVUS" && t.Timestamp!.Value.Day == 11);

        Assert.That(novus.BaseAmount, Is.EqualTo(80.0m));
    }

    [Test]
    public async Task Import_KeepsTheBankEuroSpellingAsStoredEuro_NotTheCardFallback()
    {
        var pm = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Name = "Revna euro card",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.UAH,
            BankProvider = "Monobank",
        };
        _testContext.PaymentMethods.Add(pm);
        await _testContext.SaveChangesAsync();

        var csv = string.Join("\n",
            "\"Дата i час операції\",\"Деталі операції\",MCC,\"Сума в валюті картки (UAH)\",\"Сума в валюті операції\",Валюта,Курс,\"Сума комісій (UAH)\",\"Сума кешбеку (UAH)\",\"Залишок після операції\"",
            "\"10.07.2026 10:00:00\",\"Euro Shop\",5411,-100.0,-25.0,EURO,4.0,—,—,18411.02",
            "");

        var result = await _sut.ImportBankingFileAsync(new ProcessBankingFileRequest
        {
            FileStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv)),
            ContentType = "text/csv",
            PaymentMethodId = pm.Id,
        }, _testUserId);

        var stored = _testContext.Transactions.Single(t => t.Description == "Euro Shop");
        Assert.Multiple(() =>
        {
            Assert.That(stored.Currency, Is.EqualTo(Currency.EURO));
            Assert.That(stored.Amount, Is.EqualTo(25.0m), "original-currency figure, absolute");
            Assert.That(stored.BaseAmount, Is.EqualTo(100.0m));
            Assert.That(result.InsertedRowCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Import_AppliesMccDerivedCategoryOnlyWhereTheRowHasNoLineItems()
    {
        await ImportFixtureAsync();

        var novus = Ledger().Single(t => t.Description == "NOVUS" && t.Timestamp!.Value.Day == 10 && t.Timestamp!.Value.Hour == 10);
        var expected = ReceiptCategories.GetCategoryFromMcc(5411);

        Assert.That(novus.Items, Has.Count.EqualTo(1), "a bank row carries exactly its auto-generated item");
        var item = novus.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.Category, Is.EqualTo(expected?.Category));
            Assert.That(item.Subcategory, Is.EqualTo(expected?.Subcategory));
            Assert.That(item.Origin, Is.EqualTo(ItemOrigin.AutoGenerated));
            Assert.That(item.Name, Is.EqualTo("NOVUS"));
        });

        // A transfer code maps however the table says — including to nothing; the import neither
        // invents a category nor drops one that the table has.
        var transfer = Ledger().First(t => t.Description == "Переказ коштів");
        Assert.That(transfer.Items.Single().Category,
            Is.EqualTo(ReceiptCategories.GetCategoryFromMcc(4829)?.Category));
    }

    [Test]
    public async Task Import_RecordsOriginTargetAndBalanceAfterOnEveryRow()
    {
        await ImportFixtureAsync();

        var ledger = Ledger();
        Assert.Multiple(() =>
        {
            Assert.That(ledger.All(t => t.DataOrigin == DataOrigin.Bank), Is.True);
            Assert.That(ledger.All(t => t.PaymentMethodId == _cardId), Is.True);
            Assert.That(ledger.All(t => t.BalanceAfter.HasValue), Is.True, "the fixture prints a balance on every dated row");
        });
    }

    // ── Re-import ──────────────────────────────────────────────────────

    [Test]
    public async Task Reimport_CountsEveryRowAsDuplicate_AndDoublesNothing()
    {
        await ImportFixtureAsync();

        var second = await ImportFixtureAsync();

        Assert.Multiple(() =>
        {
            Assert.That(second.InsertedRowCount, Is.EqualTo(0));
            Assert.That(second.DuplicateRowCount, Is.EqualTo(42), "stored amounts are absolute, so a re-imported expense matches");
            Assert.That(Ledger(), Has.Count.EqualTo(42));
        });
    }

    // ── Refusals ───────────────────────────────────────────────────────

    [Test]
    public void Import_IntoAMethodWithNoBankSet_IsRefusedAndTheLedgerStaysEmpty()
    {
        var noBank = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Name = "PUMB card",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.UAH,
            BankProvider = null,
        };
        _testContext.PaymentMethods.Add(noBank);
        _testContext.SaveChangesAsync().GetAwaiter().GetResult();

        var ex = Assert.ThrowsAsync<UnsupportedBankProviderException>(
            () => ImportFixtureAsync(noBank.Id));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("PUMB card"));
            Assert.That(ex.Message, Does.Contain("no bank set"));
            Assert.That(Ledger(), Is.Empty);
        });
    }

    [Test]
    public void Import_FromABankWithNoProfile_IsRefusedByName()
    {
        var privat = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Name = "Privat card",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.UAH,
            BankProvider = "PrivatBank",
        };
        _testContext.PaymentMethods.Add(privat);
        _testContext.SaveChangesAsync().GetAwaiter().GetResult();

        var ex = Assert.ThrowsAsync<UnsupportedBankProviderException>(
            () => ImportFixtureAsync(privat.Id));

        Assert.That(ex!.Message, Does.Contain("PrivatBank"));
    }

    [Test]
    public void Import_ABankPdf_IsRefusedAsNotAStatement()
    {
        var request = new ProcessBankingFileRequest
        {
            FileStream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 fake statement")),
            ContentType = "application/pdf",
            PaymentMethodId = _cardId,
        };

        var ex = Assert.ThrowsAsync<UnsupportedBankStatementFileException>(
            () => _sut.ImportBankingFileAsync(request, _testUserId));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("application/pdf"));
            Assert.That(Ledger(), Is.Empty);
        });
    }

    [Test]
    public void Import_IntoAnUnknownPaymentMethod_IsRefused()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => ImportFixtureAsync(Guid.NewGuid()));
    }
}
