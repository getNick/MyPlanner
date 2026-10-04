using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
 *  StatementFileStorageTests — Test Coverage Checklist
 * ═══════════════════════════════════════════════════════════
 *
 * Legend:
 *   [x] = implemented & passing
 *   [ ] = not yet implemented
 *
 * The seam is the finance service: PreviewBankingFileAsync / ImportBankingFileAsync against a real
 * Bucket and the real CSV pipeline. Assertions are on the file on disk and on each Bank Transaction's
 * Raw Transaction Data Envelope — never on parser internals.
 *
 * ────────────────────────────────────────────────────────────
 *  An import writes the statement down once, and says so per row
 * ────────────────────────────────────────────────────────────
 *   [x] the Statement File is stored under its content hash (D4/D7)
 *   [x] every row of that file carries {bank:{statementFileKey,rowNumber,profile}} (D8/Q3)
 *   [x] rowNumber names the line in the file, so re-parsing can find it again
 *   [x] profile names the Bank Profile that was assumed while reading
 *   [x] a second import of the same file stores it once and leaves no partial write (D6/D10)
 *   [x] previewing a statement stores nothing — nothing has been decided yet (Q2)
 *   [x] an over-limit statement is refused before parsing, and nothing is stored (D11/D12)
 */

[TestFixture]
public class StatementFileStorageTests : FinanceServiceTests_Base
{
    /// <summary>sha256 of the CSV below, computed independently (python hashlib, not by this code).</summary>
    private const string CsvHash = "f7a7be096a0c34b2ef7a7bb641e048e6448964bdcddd66dc945f83954094dc8a";

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
        _testContext.PaymentMethods.Add(_card);
        await _testContext.SaveChangesAsync();
    }

    private const string StatementHeader =
        "\"Дата i час операції\",\"Деталі операції\",MCC,\"Сума в валюті картки (UAH)\",\"Сума в валюті операції\",Валюта,Курс,\"Сума комісій (UAH)\",\"Сума кешбеку (UAH)\",\"Залишок після операції\"";

    /// <summary>
    /// Two Monobank rows, ten columns wide. The dates are fixed rather than DateTime.Today because this
    /// fixture never reconciles — no Provisional Bill is ever left to age past the 30-day window — and a
    /// fixed file has a content hash that can be written down and checked against an independent tool.
    /// </summary>
    private static byte[] StatementBytes()
    {
        var csv = string.Join("\n", StatementHeader,
            "\"14.09.2026 10:00:00\",\"SILPO\",5411,-390,-390,UAH,1,—,—,16752.46",
            "\"14.09.2026 14:00:00\",\"ANNA BAKERY\",3541,-180,-180,UAH,1,—,—,16572.46") + "\n";
        return Encoding.UTF8.GetBytes(csv);
    }

    private Task<BankStatementImportResult> ImportAsync(byte[] bytes) =>
        _sut.ImportBankingFileAsync(new ProcessBankingFileRequest
        {
            FileStream = new MemoryStream(bytes),
            ContentType = "text/csv",
            PaymentMethodId = _card.Id,
        }, _testUserId);

    private Transaction BankRow(string description) => GetTransaction(
        _testContext.Transactions.AsNoTracking().Single(t => t.Description == description).Id);

    [Test]
    public async Task ImportingAStatement_StoresTheFileUnderItsContentHash()
    {
        await ImportAsync(StatementBytes());

        Assert.That(FileKeysInBucket(), Is.EquivalentTo(new[] { $"{CsvHash}.csv" }));
        Assert.That(Encoding.UTF8.GetString(await StoredBytesAsync($"{CsvHash}.csv")),
            Does.Contain("ANNA BAKERY"), "the Statement File is the upload verbatim (D3)");
    }

    [Test]
    public async Task EveryRowOfTheFile_NamesTheFileTheLineAndTheProfile()
    {
        await ImportAsync(StatementBytes());

        var silpo = EnvelopeOf(BankRow("SILPO")).Bank!;
        var bakery = EnvelopeOf(BankRow("ANNA BAKERY")).Bank!;

        Assert.Multiple(() =>
        {
            Assert.That(silpo.StatementFileKey, Is.EqualTo($"{CsvHash}.csv"));
            Assert.That(bakery.StatementFileKey, Is.EqualTo($"{CsvHash}.csv"),
                "every row of one file points at the same Statement File (Q3)");
            Assert.That(silpo.RowNumber, Is.EqualTo(2), "the header is line 1, so SILPO is line 2");
            Assert.That(bakery.RowNumber, Is.EqualTo(3));
            Assert.That(silpo.Profile, Is.EqualTo("MonoBank"), "which Bank Profile this file was read as");
            Assert.That(EnvelopeOf(BankRow("SILPO")).Bill, Is.Null, "a Bank row has no Bill side");
        });
    }

    [Test]
    public async Task ImportingTheSameStatementTwice_StoresItOnce()
    {
        var bytes = StatementBytes();
        await ImportAsync(bytes);
        var firstKeys = FileKeysInBucket();

        var second = await ImportAsync(bytes);

        Assert.Multiple(() =>
        {
            Assert.That(second.DuplicateRowCount, Is.EqualTo(2), "the whole file is already known");
            Assert.That(FileKeysInBucket(), Is.EquivalentTo(firstKeys), "a re-import writes nothing new (D6)");
        });
    }

    [Test]
    public async Task PreviewingAStatement_StoresNothing()
    {
        var result = await _sut.PreviewBankingFileAsync(new ProcessBankingFileRequest
        {
            FileStream = new MemoryStream(StatementBytes()),
            ContentType = "text/csv",
            PaymentMethodId = _card.Id,
        }, _testUserId);

        Assert.Multiple(() =>
        {
            Assert.That(result.RowCount, Is.EqualTo(2), "the preview still reads both rows");
            Assert.That(FileKeysInBucket(), Is.Empty, "nothing has been decided yet (Q2)");
            Assert.That(_testContext.Transactions.Local, Is.Empty);
        });
    }

    [Test]
    public async Task AnOverLimitStatement_IsRefusedBeforeParsingAndStoresNothing()
    {
        var tooLarge = new MemoryStream(new byte[UploadLimit.MaxBytes + 1]);

        var ex = Assert.ThrowsAsync<UploadTooLargeException>(() => _sut.ImportBankingFileAsync(
            new ProcessBankingFileRequest
            {
                FileStream = tooLarge, ContentType = "text/csv", PaymentMethodId = _card.Id,
            }, _testUserId));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain(UploadLimit.MaxMegabytes));
            Assert.That(FileKeysInBucket(), Is.Empty);
            Assert.That(_testContext.Transactions.Local, Is.Empty);
        });
    }
}
