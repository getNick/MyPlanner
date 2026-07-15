using MyPlanner.Service.Exceptions;
using MyPlanner.Service.Helpers.BankExport;
using MyPlanner.Service.Models;

namespace MyPlanner.UnitTests.Helpers;

/*
 * ═══════════════════════════════════════════════════════════
 *  BankExportParserTests — Test Coverage Checklist
 * ═══════════════════════════════════════════════════════════
 *
 * The seam is `BankExportParser.Parse(stream, contentType, bankProvider)`: one statement file in,
 * rows-plus-needs-review out. Nothing here reaches the database; the ledger-side rules are tested
 * at the finance service seam (Services/Finance/BankStatementImportTests.cs).
 *
 * ────────────────────────────────────────────────────────────
 *  Refusals (nothing is guessed)
 * ────────────────────────────────────────────────────────────
 *   [x] No bank provider → refused, names what it can read (no silent Monobank fallback)
 *   [x] Unknown bank provider → refused
 *   [x] Non-CSV upload (a bank PDF) → refused as not a statement, never routed to the image pipeline
 *   [x] Content type with a charset parameter is still read as CSV
 *   [x] Statement missing its card-amount column → refused as "not that bank's statement"
 *
 * ────────────────────────────────────────────────────────────
 *  Rows are never dropped in silence
 * ────────────────────────────────────────────────────────────
 *   [x] Row with a blank timestamp → needs-review (row number, raw cell, description)
 *   [x] Row with an unreadable amount → needs-review
 *   [x] Garbage row → needs-review, and the good rows around it still land
 *
 * ────────────────────────────────────────────────────────────
 *  Money columns
 * ────────────────────────────────────────────────────────────
 *   [x] Amount = original transaction-currency figure; BaseAmount = card-currency figure
 *   [x] UAH rows: both figures are the same number
 *   [x] Foreign row (AliExpress/USD): -13.37 USD with -598.99 UAH beside it
 *   [x] Currency kept as an ISO code, incl. numeric ISO codes and the bank's "EURO" spelling
 *   [x] Unrecognised currency → null for the caller to resolve (never coerced to UAH)
 *
 * ────────────────────────────────────────────────────────────
 *  Reading the file
 * ────────────────────────────────────────────────────────────
 *   [x] Empty stream / header only → nothing read, nothing needing review
 *   [x] Rows newest-first; needs-review in file order
 *   [x] MCC extracted; absent MCC stays null
 *   [x] Balance-after read when printed, null on "—"
 *   [x] Quoted descriptions with commas survive
 *   [x] Real Monobank export (42 dated rows + 1 undated) reads end to end
 */

[TestFixture]
public class BankExportParserTests
{
    /// <summary>The header Mono prints, in Mono's column order.</summary>
    private const string Header =
        "\"Дата i час операції\",\"Деталі операції\",MCC,\"Сума в валюті картки (UAH)\",\"Сума в валюті операції\",Валюта,Курс,\"Сума комісій (UAH)\",\"Сума кешбеку (UAH)\",\"Залишок після операції\"";

    private const string Monobank = "Monobank";

    private static BankParseResult Parse(string body, string provider = Monobank, string contentType = "text/csv")
        => BankExportParser.Parse(StreamOf(body), contentType, provider);

    private static Stream StreamOf(string body) => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));

    private static string Csv(params string[] rows) => string.Join("\n", rows.Concat(new[] { "" }));

    // ── Refusals ───────────────────────────────────────────────────────

    [Test]
    public void Parse_WithoutABankProvider_RefusesRatherThanGuessingAFormat()
    {
        var ex = Assert.Throws<UnsupportedBankProviderException>(
            () => Parse(Csv(Header, "\"11.07.2026 17:30:53\",\"NOVUS\",5814,-80.0,-80.0,UAH,—,—,1.6,16752.46"), provider: ""),
            "A card with no bank set has no column map, so reading its file means inventing one");

        Assert.That(ex!.Message, Does.Contain("Monobank").IgnoreCase);
    }

    [Test]
    public void Parse_UnsupportedBank_RefusesInsteadOfReadingItAsSomeoneElsesStatement()
    {
        var ex = Assert.Throws<UnsupportedBankProviderException>(
            () => Parse(Csv(Header), provider: "PrivatBank"));

        Assert.That(ex!.Message, Does.Contain("PrivatBank"));  // names the bank it refused
    }

    [Test]
    public void Parse_BankPdf_IsRefusedAsNotAStatement()
    {
        var ex = Assert.Throws<UnsupportedBankStatementFileException>(
            () => Parse(Csv(Header), contentType: "application/pdf"));

        Assert.That(ex!.Message, Does.Contain("application/pdf"));
    }

    [Test]
    public void Parse_ContentTypeCarryingACharset_IsStillReadAsCsv()
    {
        var result = Parse(Csv(Header, "\"11.07.2026 17:30:53\",\"NOVUS\",5814,-80.0,-80.0,UAH,—,—,1.6,16752.46"),
                           contentType: "text/csv; charset=utf-8");

        Assert.That(result.Rows, Has.Count.EqualTo(1));
    }

    [Test]
    public void Parse_FileWithoutTheCardAmountColumn_SaysItIsNotThatBanksStatement()
    {
        var foreignHeader = "\"Date\",\"Description\",\"Amount\",Currency";

        var ex = Assert.Throws<UnsupportedBankStatementFileException>(
            () => Parse(Csv(foreignHeader, "\"11.07.2026\",\"NOVUS\",-80.0,UAH")));

        Assert.That(ex!.Message, Does.Contain("MonoBank"));
    }

    // ── Rows are never dropped in silence ──────────────────────────────

    [Test]
    public void Parse_RowWithNoTimestamp_IsReportedForReviewInsteadOfVanishing()
    {
        var result = Parse(Csv(Header,
            "\"11.07.2026 17:30:53\",\"NOVUS\",5814,-80.0,-80.0,UAH,—,—,1.6,16752.46",
            "\"\",\"Переказ готівкою через термінал\",,-100.0,-100.0,UAH,—,—,—,—"));

        Assert.That(result.Rows, Has.Count.EqualTo(1), "the dated row still imports");
        Assert.That(result.NeedsReview, Has.Count.EqualTo(1));

        var review = result.NeedsReview[0];
        Assert.That(review.Issue, Is.EqualTo(BankRowIssue.MissingTimestamp));
        // Line 1 is the header, so the undated row is line 3 of the file.
        Assert.That(review.RowNumber, Is.EqualTo(3));
        Assert.That(review.Description, Is.EqualTo("Переказ готівкою через термінал"));
    }

    [Test]
    public void Parse_RowWithADateTheProfileDoesNotKnow_IsReportedForReview()
    {
        var result = Parse(Csv(Header, "\"2026-07-11\",\"NOVUS\",5814,-80.0,-80.0,UAH,—,—,—,16752.46"));

        Assert.That(result.Rows, Is.Empty);
        Assert.That(result.NeedsReview.Single().Issue, Is.EqualTo(BankRowIssue.MissingTimestamp));
        Assert.That(result.NeedsReview.Single().RawValue, Is.EqualTo("2026-07-11"));
    }

    [Test]
    public void Parse_RowWithAnUnreadableAmount_IsReportedForReview()
    {
        var result = Parse(Csv(Header, "\"11.07.2026 17:30:53\",\"NOVUS\",5814,,,UAH,—,—,—,16752.46"));

        Assert.That(result.Rows, Is.Empty);
        var review = result.NeedsReview.Single();
        Assert.That(review.Issue, Is.EqualTo(BankRowIssue.UnreadableAmount));
        Assert.That(review.RowNumber, Is.EqualTo(2));
    }

    [Test]
    public void Parse_GarbageRow_KeepsTheGoodRowsAndReportsTheBadOne()
    {
        var result = Parse(Csv(Header,
            "\"10.07.2026 10:29:32\",\"NOVUS\",5411,-771.0,-771.0,UAH,—,—,—,18411.02",
            "this is a malformed row",
            "\"11.07.2026 17:30:53\",\"Another Shop\",5814,-50.0,-50.0,UAH,—,—,—,16702.46"));

        Assert.That(result.Rows, Has.Count.EqualTo(2));
        Assert.That(result.NeedsReview, Has.Count.EqualTo(1));
        Assert.That(result.NeedsReview[0].Issue, Is.EqualTo(BankRowIssue.MissingTimestamp));
    }

    // ── Money columns ──────────────────────────────────────────────────

    [Test]
    public void Parse_UahRow_KeepsTheCardFigureBesideTheTransactionFigure()
    {
        var result = Parse(Csv(Header, "\"11.07.2026 17:30:53\",\"NOVUS\",5814,-80.0,-80.0,UAH,—,—,1.6,16752.46"));

        var row = result.Rows.Single();
        Assert.That(row.Amount, Is.EqualTo(-80.0m));
        Assert.That(row.BaseAmount, Is.EqualTo(-80.0m));
        Assert.That(row.Currency, Is.EqualTo("UAH"));
        Assert.That(row.Timestamp, Is.EqualTo(new DateTime(2026, 7, 11, 17, 30, 53)));
        Assert.That(row.BalanceAfter, Is.EqualTo(16752.46m));
    }

    [Test]
    public void Parse_ForeignCurrencyRow_KeepsOriginalAmountAndCurrencyWithTheCardFigureBesideIt()
    {
        var result = Parse(Csv(Header, "\"07.07.2026 18:15:27\",\"AliExpress\",5310,-598.99,-13.37,USD,44.801,—,—,25326.88"));

        var row = result.Rows.Single();
        Assert.That(row.Amount, Is.EqualTo(-13.37m), "the figure the purchase actually cost in dollars");
        Assert.That(row.Currency, Is.EqualTo("USD"));
        Assert.That(row.BaseAmount, Is.EqualTo(-598.99m), "what the card was charged in hryvnia");
    }

    [Test]
    public void Parse_BankSpellsTheCurrencyAsEuro_KeepsItAsIsoEur()
    {
        var result = Parse(Csv(Header, "\"10.07.2026 10:00:00\",\"Euro Shop\",5411,-100.0,-25.0,EURO,4.0,—,—,18411.02"));

        Assert.That(result.Rows.Single().Currency, Is.EqualTo("EUR"));
    }

    [Test]
    public void Parse_NumericIsoCurrencyCode_IsReadAsThatCurrency()
    {
        var result = Parse(Csv(Header, "\"10.07.2026 10:00:00\",\"Zara\",5651,-100.0,-25.0,978,4.0,—,—,18411.02"));

        Assert.That(result.Rows.Single().Currency, Is.EqualTo("EUR"));
    }

    [Test]
    public void Parse_UnknownCurrencyCode_IsLeftNullForTheCallerRatherThanCalledUah()
    {
        var result = Parse(Csv(Header, "\"10.07.2026 10:00:00\",\"Bazaar\",5411,-100.0,-25.0,GOLD,—,—,—,18411.02"));

        Assert.That(result.Rows.Single().Currency, Is.Null);
    }

    // ── Reading the file ───────────────────────────────────────────────

    [Test]
    public void Parse_EmptyStream_ReadsNothingAndNeedsNothing()
    {
        var result = BankExportParser.Parse(new MemoryStream(), "text/csv", Monobank);

        Assert.That(result.Rows, Is.Empty);
        Assert.That(result.NeedsReview, Is.Empty);
    }

    [Test]
    public void Parse_HeaderOnly_ReadsNothingAndNeedsNothing()
    {
        var result = Parse(Csv(Header));

        Assert.That(result.Rows, Is.Empty);
        Assert.That(result.NeedsReview, Is.Empty);
    }

    [Test]
    public void Parse_RowsComeOutNewestFirst_AndNeedsReviewInFileOrder()
    {
        var result = Parse(Csv(Header,
            "\"01.07.2026 09:09:25\",\"NOVUS\",5411,-623.44,-623.44,UAH,—,—,—,33900.72",
            "\"\",\"undated a\",,-1.0,-1.0,UAH,—,—,—,—",
            "\"05.07.2026 12:26:07\",\"NOVUS\",5411,-316.68,-316.68,UAH,—,—,—,26585.34",
            "\"11.07.2026 17:30:53\",\"NOVUS\",5814,-80.0,-80.0,UAH,—,—,1.6,16752.46",
            "\"\",\"undated b\",,-2.0,-2.0,UAH,—,—,—,—"));

        Assert.That(result.Rows.Select(r => r.Timestamp!.Value.Day), Is.EqualTo(new[] { 11, 5, 1 }));
        Assert.That(result.NeedsReview.Select(r => r.RowNumber), Is.EqualTo(new[] { 3, 6 }));
    }

    [Test]
    public void Parse_MccCode_IsExtracted()
    {
        var result = Parse(Csv(Header, "\"10.07.2026 10:29:32\",\"NOVUS\",5411,-771.0,-771.0,UAH,—,—,—,18411.02"));

        Assert.That(result.Rows.Single().MCC, Is.EqualTo(5411));
    }

    [Test]
    public void Parse_WithoutAMcc_IsLeftNull()
    {
        var result = Parse(Csv(Header, "\"10.07.2026 10:29:32\",\"Unknown Merchant\",,-100.0,-100.0,UAH,—,—,—,18411.02"));

        Assert.That(result.Rows.Single().MCC, Is.Null);
    }

    [Test]
    public void Parse_BalanceColumn_KeepsTheBankFigureOrNull()
    {
        var printed = Parse(Csv(Header, "\"11.07.2026 10:39:22\",\"BoHlib\",5462,-230.0,-230.0,UAH,—,—,—,16832.46"));
        var dashed = Parse(Csv(Header, "\"11.07.2026 10:39:22\",\"BoHlib\",5462,-230.0,-230.0,UAH,—,—,—,—"));

        Assert.That(printed.Rows.Single().BalanceAfter, Is.EqualTo(16832.46m));
        Assert.That(dashed.Rows.Single().BalanceAfter, Is.Null);
    }

    [Test]
    public void Parse_DescriptionWithCommas_SurvivesQuoting()
    {
        var result = Parse(Csv(Header, "\"10.07.2026 19:39:10\",\"SILPO, branch #5\",5441,-348.56,-348.56,UAH,—,—,—,17562.46"));

        Assert.That(result.Rows.Single().Description, Is.EqualTo("SILPO, branch #5"));
    }

    [Test]
    public void Parse_ReversalRow_KeepsTheRawDescription()
    {
        var result = Parse(Csv(Header, "\"06.07.2026 01:44:27\",\"Скасування. PUMA\",5651,2451.0,2451.0,UAH,—,—,—,28350.54"));

        Assert.That(result.Rows.Single().Description, Is.EqualTo("Скасування. PUMA"));
    }

    [Test]
    public void Parse_EveryBoundaryMcc_IsReadWithoutAmbiguity()
    {
        var boundaryMccs = new[] { 5410, 5411, 5439, 5440, 5441, 5461, 5462, 5463, 5499, 5810, 5811, 5819, 5998, 5999 };

        foreach (var mcc in boundaryMccs)
        {
            var result = Parse(Csv(Header, $"\"10.07.2026 10:00:00\",\"Test\",{mcc},-100.0,-100.0,UAH,—,—,—,18411.02"));

            Assert.That(result.Rows, Has.Count.EqualTo(1), $"MCC {mcc} should parse without error");
        }
    }

    [Test]
    public void Parse_RealMonobankExport_ReadsEveryRowAndReportsTheUndatedOne()
    {
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "test-banking-file.csv");
        Assert.That(File.Exists(filePath), Is.True, $"Resource file not found at {filePath}");

        using var stream = File.OpenRead(filePath);
        var result = BankExportParser.Parse(stream, "text/csv", Monobank);

        Assert.That(result.NeedsReview, Has.Count.EqualTo(1), "the fixture's undated row is reported, not dropped");
        Assert.That(result.NeedsReview[0].Description, Is.EqualTo("Переказ готівкою через термінал"));

        var expected = new List<TransactionDto>
        {
            new() { Timestamp = new DateTime(2026, 7, 11, 17, 30, 53), Amount = -80.0m, BaseAmount = -80.0m, Description = "NOVUS", Currency = "UAH", MCC = 5814, BalanceAfter = 16752.46m },
            new() { Timestamp = new DateTime(2026, 7, 11, 10, 39, 22), Amount = -230.0m, BaseAmount = -230.0m, Description = "BoHlib", Currency = "UAH", MCC = 5462, BalanceAfter = 16832.46m },
            new() { Timestamp = new DateTime(2026, 7, 11, 10, 27, 19), Amount = -500.0m, BaseAmount = -500.0m, Description = "Pipittodityachaigrovaperu", Currency = "UAH", MCC = 7230, BalanceAfter = 17062.46m },
            new() { Timestamp = new DateTime(2026, 7, 10, 19, 39, 10), Amount = -348.56m, BaseAmount = -348.56m, Description = "SILPO", Currency = "UAH", MCC = 5441, BalanceAfter = 17562.46m },
            new() { Timestamp = new DateTime(2026, 7, 10, 19, 28, 15), Amount = -500.0m, BaseAmount = -500.0m, Description = "Переказ коштів", Currency = "UAH", MCC = 4829, BalanceAfter = 17911.02m },
            new() { Timestamp = new DateTime(2026, 7, 10, 10, 29, 32), Amount = -771.0m, BaseAmount = -771.0m, Description = "NOVUS", Currency = "UAH", MCC = 5411, BalanceAfter = 18411.02m },
            new() { Timestamp = new DateTime(2026, 7, 10, 9, 11, 43), Amount = -405.0m, BaseAmount = -405.0m, Description = "OvochiFrukti", Currency = "UAH", MCC = 5499, BalanceAfter = 19182.02m },
            new() { Timestamp = new DateTime(2026, 7, 9, 22, 13, 45), Amount = -500.0m, BaseAmount = -500.0m, Description = "Переказ коштів", Currency = "UAH", MCC = 4829, BalanceAfter = 19587.02m },
            new() { Timestamp = new DateTime(2026, 7, 9, 11, 3, 20), Amount = -3000.09m, BaseAmount = -3000.09m, Description = "ANSWEAR", Currency = "UAH", MCC = 5651, BalanceAfter = 20087.02m },
            new() { Timestamp = new DateTime(2026, 7, 9, 8, 59, 28), Amount = -120.0m, BaseAmount = -120.0m, Description = "Паркінг", Currency = "UAH", MCC = 7523, BalanceAfter = 23087.11m },
            new() { Timestamp = new DateTime(2026, 7, 8, 19, 47, 37), Amount = -907.22m, BaseAmount = -907.22m, Description = "NOVUS", Currency = "UAH", MCC = 5411, BalanceAfter = 23207.11m },
            new() { Timestamp = new DateTime(2026, 7, 8, 9, 19, 51), Amount = -581.98m, BaseAmount = -581.98m, Description = "NOVUS", Currency = "UAH", MCC = 5411, BalanceAfter = 24114.33m },
            new() { Timestamp = new DateTime(2026, 7, 7, 19, 48, 2), Amount = -80.6m, BaseAmount = -80.6m, Description = "ЕкоВода", Currency = "UAH", MCC = 5499, BalanceAfter = 24696.31m },
            new() { Timestamp = new DateTime(2026, 7, 7, 19, 38, 40), Amount = -80.6m, BaseAmount = -80.6m, Description = "ЕкоВода", Currency = "UAH", MCC = 5499, BalanceAfter = 24776.91m },
            new() { Timestamp = new DateTime(2026, 7, 7, 19, 32, 22), Amount = -469.37m, BaseAmount = -469.37m, Description = "NOVUS", Currency = "UAH", MCC = 5411, BalanceAfter = 24857.51m },
            new() { Timestamp = new DateTime(2026, 7, 7, 18, 15, 27), Amount = -13.37m, BaseAmount = -598.99m, Description = "AliExpress", Currency = "USD", MCC = 5310, BalanceAfter = 25326.88m },
            new() { Timestamp = new DateTime(2026, 7, 7, 14, 35, 30), Amount = -576.0m, BaseAmount = -576.0m, Description = "EVA", Currency = "UAH", MCC = 5977, BalanceAfter = 25925.87m },
            new() { Timestamp = new DateTime(2026, 7, 7, 13, 16, 58), Amount = -500.0m, BaseAmount = -500.0m, Description = "LIQPAY*OPTIKA", Currency = "UAH", MCC = 8043, BalanceAfter = 26501.87m },
            new() { Timestamp = new DateTime(2026, 7, 6, 19, 6, 45), Amount = -210.0m, BaseAmount = -210.0m, Description = "Нова пошта", Currency = "UAH", MCC = 4215, BalanceAfter = 27001.87m },
            new() { Timestamp = new DateTime(2026, 7, 6, 19, 4, 11), Amount = -500.0m, BaseAmount = -500.0m, Description = "Переказ коштів", Currency = "UAH", MCC = 4829, BalanceAfter = 27211.87m },
            new() { Timestamp = new DateTime(2026, 7, 6, 14, 13, 50), Amount = -638.67m, BaseAmount = -638.67m, Description = "NOVUS", Currency = "UAH", MCC = 5411, BalanceAfter = 27711.87m },
            new() { Timestamp = new DateTime(2026, 7, 6, 1, 44, 27), Amount = 2451.0m, BaseAmount = 2451.0m, Description = "Скасування. PUMA", Currency = "UAH", MCC = 5651, BalanceAfter = 28350.54m },
            new() { Timestamp = new DateTime(2026, 7, 5, 19, 16, 42), Amount = -605.8m, BaseAmount = -605.8m, Description = "NOVUS", Currency = "UAH", MCC = 5411, BalanceAfter = 25899.54m },
            new() { Timestamp = new DateTime(2026, 7, 5, 18, 22, 31), Amount = -80.0m, BaseAmount = -80.0m, Description = "Файні Льоди", Currency = "UAH", MCC = 5814, BalanceAfter = 26505.34m },
            new() { Timestamp = new DateTime(2026, 7, 5, 12, 26, 7), Amount = -316.68m, BaseAmount = -316.68m, Description = "NOVUS", Currency = "UAH", MCC = 5411, BalanceAfter = 26585.34m },
            new() { Timestamp = new DateTime(2026, 7, 5, 11, 24, 17), Amount = -50.0m, BaseAmount = -50.0m, Description = "COFFEE360", Currency = "UAH", MCC = 5814, BalanceAfter = 26902.02m },
            new() { Timestamp = new DateTime(2026, 7, 5, 11, 22, 48), Amount = -270.0m, BaseAmount = -270.0m, Description = "COFFEE360", Currency = "UAH", MCC = 5814, BalanceAfter = 26952.02m },
            new() { Timestamp = new DateTime(2026, 7, 4, 19, 21, 23), Amount = -222.43m, BaseAmount = -222.43m, Description = "АНЦ", Currency = "UAH", MCC = 5912, BalanceAfter = 27222.02m },
            new() { Timestamp = new DateTime(2026, 7, 4, 17, 45, 57), Amount = -36.99m, BaseAmount = -36.99m, Description = "NOVUS", Currency = "UAH", MCC = 5411, BalanceAfter = 27444.45m },
            new() { Timestamp = new DateTime(2026, 7, 4, 12, 14, 12), Amount = -31.9m, BaseAmount = -31.9m, Description = "Антошка", Currency = "UAH", MCC = 5641, BalanceAfter = 27481.44m },
            new() { Timestamp = new DateTime(2026, 7, 3, 22, 5, 27), Amount = -4.6m, BaseAmount = -4.6m, Description = "Повернись живим", Currency = "UAH", MCC = 4829, BalanceAfter = 27513.34m },
            new() { Timestamp = new DateTime(2026, 7, 3, 22, 5, 26), Amount = 4.6m, BaseAmount = 4.6m, Description = "Виведення кешбеку 5.98₴", Currency = "UAH", MCC = 4829, BalanceAfter = 27517.94m },
            new() { Timestamp = new DateTime(2026, 7, 3, 18, 48, 44), Amount = -121.0m, BaseAmount = -121.0m, Description = "Нова пошта", Currency = "UAH", MCC = 4215, BalanceAfter = 27513.34m },
            new() { Timestamp = new DateTime(2026, 7, 3, 17, 5, 20), Amount = -784.0m, BaseAmount = -784.0m, Description = "STATION PIZZA", Currency = "UAH", MCC = 5814, BalanceAfter = 27634.34m },
            new() { Timestamp = new DateTime(2026, 7, 3, 11, 52, 32), Amount = -2000.0m, BaseAmount = -2000.0m, Description = "Переказ коштів", Currency = "UAH", MCC = 4829, BalanceAfter = 28418.34m },
            new() { Timestamp = new DateTime(2026, 7, 2, 13, 30, 24), Amount = -399.0m, BaseAmount = -399.0m, Description = "АТБ", Currency = "UAH", MCC = 5499, BalanceAfter = 30418.34m },
            new() { Timestamp = new DateTime(2026, 7, 2, 13, 26, 59), Amount = -134.19m, BaseAmount = -134.19m, Description = "АТБ", Currency = "UAH", MCC = 5499, BalanceAfter = 30817.34m },
            new() { Timestamp = new DateTime(2026, 7, 2, 12, 59, 52), Amount = -723.9m, BaseAmount = -723.9m, Description = "Антошка", Currency = "UAH", MCC = 5641, BalanceAfter = 30951.53m },
            new() { Timestamp = new DateTime(2026, 7, 2, 10, 34, 59), Amount = -1694.6m, BaseAmount = -1694.6m, Description = "PROSTOR", Currency = "UAH", MCC = 5977, BalanceAfter = 31675.43m },
            new() { Timestamp = new DateTime(2026, 7, 1, 19, 24, 50), Amount = -180.69m, BaseAmount = -180.69m, Description = "NOVUS", Currency = "UAH", MCC = 5411, BalanceAfter = 33370.03m },
            new() { Timestamp = new DateTime(2026, 7, 1, 13, 25, 27), Amount = -350.0m, BaseAmount = -350.0m, Description = "Інтернет і ТБ", Currency = "UAH", MCC = 4900, BalanceAfter = 33550.72m },
            new() { Timestamp = new DateTime(2026, 7, 1, 9, 9, 25), Amount = -623.44m, BaseAmount = -623.44m, Description = "NOVUS", Currency = "UAH", MCC = 5411, BalanceAfter = 33900.72m }
        };

        Assert.That(result.Rows, Has.Count.EqualTo(expected.Count), "Row count mismatch");

        for (int i = 0; i < result.Rows.Count; i++)
        {
            var actual = result.Rows[i];
            var exp = expected[i];

            Assert.That(actual.Timestamp, Is.EqualTo(exp.Timestamp), $"[{i}] Timestamp mismatch");
            Assert.That(actual.Amount, Is.EqualTo(exp.Amount), $"[{i}] Amount mismatch");
            Assert.That(actual.BaseAmount, Is.EqualTo(exp.BaseAmount), $"[{i}] BaseAmount mismatch");
            Assert.That(actual.Description, Is.EqualTo(exp.Description), $"[{i}] Description mismatch");
            Assert.That(actual.Currency, Is.EqualTo(exp.Currency), $"[{i}] Currency mismatch");
            Assert.That(actual.MCC, Is.EqualTo(exp.MCC), $"[{i}] MCC mismatch");
            Assert.That(actual.BalanceAfter, Is.EqualTo(exp.BalanceAfter), $"[{i}] BalanceAfter mismatch");
        }
    }
}
