using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using MyPlanner.Data.Entities.Finance;
using MyPlanner.Service.Models;
using MyPlanner.Service.Requests.Finance;
using NUnit.Framework;

namespace MyPlanner.UnitTests.Services.Finance;

/*
 * ═══════════════════════════════════════════════════════════
 *  ReconciliationTests — Test Coverage Checklist
 * ═══════════════════════════════════════════════════════════
 *
 * Legend:
 *   [x] = implemented & passing
 *   [ ] = not yet implemented
 *
 * The seam is the finance service: ConfirmReceiptAsync / ImportBankingFileAsync / UpdateTransactionAsync, with the model (ILlmService) mocked and the statement read through the real CSV
 * pipeline. Assertions are on the ledger afterwards — never on the matcher's internals.
 *
 * ────────────────────────────────────────────────────────────
 *  Matching — one Bill + its one Bank Transaction = one Reconciled row
 * ────────────────────────────────────────────────────────────
 *   [x] Bill uploaded first, statement imported second → one Reconciled row, Line Items preserved
 *   [x] statement imported first, Bill uploaded second → same outcome (upload returns the merged row)
 *   [x] merged row's amount is the bank amount; Money Delta computed, never stored
 *   [x] amount drift (12.99 against 12.50) → no merge, Bill stays Provisional
 *   [x] 2 hours apart → no merge (±1 hour matching window)
 *   [x] two candidate Bank rows → nothing merged; untouched competing candidates count too
 *   [x] Bill never pairs with another Bill, nor with an Income or Transfer row
 *   [x] Owner (Payment Method) inherited from the card that matched
 *   [x] re-import is idempotent; exact bank Timestamp never suppresses a Provisional Bill
 *   [x] import/confirmation/complete saves do not reconcile unrelated historical pairs
 *   [x] complete edits preserve item identity, support empty/omitted detail, validate ownership
 *   [x] temporary partial Bill writes do not trigger Matching
 *   [ ] both source payloads + ParserVersion on the merged row — deferred to ticket 08
 *
 * ────────────────────────────────────────────────────────────
 *  Provisional Bills
 * ────────────────────────────────────────────────────────────
 *   [x] a Bill with no Bank twin stays Provisional, counted in spend
 *   [ ] a Provisional Bill older than 30 days raises a Review Item — derived on the client
 *       (domain/reconciliation.test.ts: staleProvisionalBills), nothing stored backend-side
 *
 * ────────────────────────────────────────────────────────────
 *  Bank facts cannot be overwritten afterwards
 * ────────────────────────────────────────────────────────────
 *   [x] UpdateTransaction on a Reconciled row cannot overwrite the bank amount or detach the card
 *   [x] GetTransactionsAsync reports a truthful Money Delta (guards the missing Include)
 *   [x] ImportBankingFile returns surviving Bills and counts only this file's merges
 *   [x] Reconciled Type changes refused; editing detail widens Money Delta, bank facts survive
 *   [x] EUR from the editor maps to the stored EURO currency
 */

[TestFixture]
public class ReconciliationTests : FinanceServiceTests_Base
{
    private const string StatementHeader =
        "\"Дата i час операції\",\"Деталі операції\",MCC,\"Сума в валюті картки (UAH)\",\"Сума в валюті операції\",Валюта,Курс,\"Сума комісій (UAH)\",\"Сума кешбеку (UAH)\",\"Залишок після операції\"";

    private PaymentMethod _card = null!;
    private Guid _cardId;

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
        _cardId = _card.Id;
    }

    /// <summary>A Bill as the household photographed it: Merchant, the paper total, and its Line Items.</summary>
    private sealed record LineItem(string Name, decimal TotalPrice);

    /// <summary>One dated line of the statement, with the card-currency figure the bank prints.</summary>
    private sealed record StatementRow(DateTime Timestamp, string Description, decimal CardAmount, decimal BalanceAfter);

    private List<Transaction> Ledger() => _testContext.Transactions.Include(t => t.Items).ToList();

    /// <summary>
    /// A clock time on <em>today</em>: the calendar day is read from <see cref="DateTime.Today"/>, so a
    /// fixture can never silently age past the 30-day Provisional window the way a hardcoded 2026 date
    /// already has (this suite was written in July; it is now September). The matching window is a
    /// matter of minutes between two rows, which this keeps exact — only the day rides along with the
    /// day the test runs.
    /// </summary>
    private DateTime At(int hour, int minute = 0, int second = 0) =>
        DateTime.Today.AddHours(hour).AddMinutes(minute).AddSeconds(second);

    /// <summary>
    /// Uploads a Bill through the real OCR pipeline, model mocked: the amount read off the paper is
    /// what the Bill carries, and it is deliberately not the sum of its Line Items.
    /// </summary>
    private async Task<Transaction> UploadBillAsync(
        string merchant, decimal paperTotalAmount, DateTime takenAt, params LineItem[] lineItems)
    {
        var modelAnswer = JsonSerializer.Serialize(new
        {
            merchantName = merchant,
            totalAmount = paperTotalAmount,
            currency = "UAH",
            timestamp = takenAt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            items = lineItems.Select(item => new
            {
                name = item.Name,
                fullName = item.Name,
                unitPrice = item.TotalPrice,
                quantity = 1.0,
                totalPrice = item.TotalPrice,
            }),
        });

        _llmServiceMock
            .Setup(x => x.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ReturnsAsync(modelAnswer);

        var parsed = await _sut.PreviewReceiptAsync(new ProcessReceiptRequest
        { FileStream = new MemoryStream(), ContentType = "image/png" });
        return await _sut.ConfirmReceiptAsync(new ConfirmReceiptRequest
        { FileStream = new MemoryStream(), ContentType = "image/png", Receipt = parsed }, _testUserId);
    }

    private Task<BankStatementImportResult> ImportStatementAsync(
        params StatementRow[] rows) => ImportStatementOnCardAsync(_cardId, rows);

    private Task<BankStatementImportResult> ImportStatementOnCardAsync(
        Guid cardId, params StatementRow[] rows)
    {
        // One Monobank statement row, all ten columns in the bank's own order — Date, Description,
        // MCC, Amount in card currency, Amount in transaction currency, Currency, Kurs, Fees,
        // Cashback, Balance After. Kurs (the exchange rate, 1 for a UAH purchase) is mandatory: a
        // row with a gap in the middle shifts every column after it left one place, so Balance After
        // — the only column this fixture exists to prove travels through the merge — reads as null.
        var csv = string.Join("\n",
            new[] { StatementHeader }
                .Concat(rows.Select(row =>
                    $"\"{row.Timestamp:dd.MM.yyyy HH:mm:ss}\",\"{row.Description}\",5411," +
                    $"{row.CardAmount.ToString(CultureInfo.InvariantCulture)},{row.CardAmount.ToString(CultureInfo.InvariantCulture)},UAH,1," +
                    $"—,—,{row.BalanceAfter.ToString(CultureInfo.InvariantCulture)}"))) + "\n";

        return _sut.ImportBankingFileAsync(new ProcessBankingFileRequest
        {
            FileStream = new MemoryStream(Encoding.UTF8.GetBytes(csv)),
            ContentType = "text/csv",
            PaymentMethodId = cardId,
        }, _testUserId);
    }

    /// <summary>
    /// A Bank Transaction written straight to the ledger, bypassing the file seam — used only
    /// where no import can produce the row (a Transfer) or where the import would itself trigger
    /// Matching before the test has placed every candidate.
    /// </summary>
    private Transaction SeedBankRow(
        decimal amount, DateTime timestamp, TransactionType type = TransactionType.Expense,
        Guid? paymentMethodId = null, DataOrigin origin = DataOrigin.Bank)
    {
        var row = new Transaction
        {
            UserId = _testUserId,
            Type = type,
            PaymentMethodId = paymentMethodId ?? _cardId,
            Timestamp = timestamp,
            Amount = Math.Abs(amount),
            Currency = Currency.UAH,
            Description = "Seeded bank row",
            DataOrigin = origin,
        };
        _testContext.Transactions.Add(row);
        _testContext.SaveChanges();
        return row;
    }

    // ── Matching ───────────────────────────────────────────────────────

    [Test]
    public async Task BillAtExactBankTimestamp_ReconcilesAndReimportStaysDuplicate()
    {
        var bill = await UploadBillAsync("SILPO", 390m, At(18), new LineItem("Bread", 90m));
        var row = new StatementRow(At(18), "SILPO", -390m, 900m);

        var imported = await ImportStatementAsync(row);
        var repeated = await ImportStatementAsync(row);
        var ledger = await _sut.GetTransactionsAsync(_testUserId);

        Assert.Multiple(() =>
        {
            Assert.That(imported.ReconciledCount, Is.EqualTo(1));
            Assert.That(ledger, Has.Count.EqualTo(1));
            Assert.That(ledger.Single().Id, Is.EqualTo(bill.Id));
            Assert.That(ledger.Single().DataOrigin, Is.EqualTo(DataOrigin.Reconciled));
            Assert.That(repeated.DuplicateRowCount, Is.EqualTo(1));
            Assert.That(repeated.InsertedRowCount, Is.Zero);
        });
    }

    [Test]
    public async Task AmountDifferenceWithinTenKopecks_Reconciles()
    {
        var bill = await UploadBillAsync("NOVUS", 1377.172m, At(18, 41));

        var imported = await ImportStatementAsync(
            new StatementRow(At(18, 40, 52), "NOVUS", -1377.17m, 1070.94m));
        var ledger = await _sut.GetTransactionsAsync(_testUserId);

        Assert.Multiple(() =>
        {
            Assert.That(imported.ReconciledCount, Is.EqualTo(1));
            Assert.That(ledger, Has.Count.EqualTo(1));
            Assert.That(ledger.Single().Id, Is.EqualTo(bill.Id));
            Assert.That(ledger.Single().DataOrigin, Is.EqualTo(DataOrigin.Reconciled));
            Assert.That(ledger.Single().Amount, Is.EqualTo(1377.17m), "the bank amount remains authoritative");
        });
    }

    [Test]
    public async Task ImportStatement_DoesNotReconcileUnrelatedHistoricalPair()
    {
        var historicalBill = await UploadBillAsync("Old Bill", 50m, At(10));
        var historicalBank = SeedBankRow(50m, At(10, 10));
        await UploadBillAsync("New Bill", 390m, At(18));

        var result = await ImportStatementAsync(new StatementRow(At(18, 30), "New Bill", -390m, 900m));

        var storedBill = await _sut.GetTransactionAsync(historicalBill.Id, _testUserId);
        var storedBank = await _sut.GetTransactionAsync(historicalBank.Id, _testUserId);
        Assert.Multiple(() =>
        {
            Assert.That(result.ReconciledCount, Is.EqualTo(1));
            Assert.That(storedBill!.DataOrigin, Is.EqualTo(DataOrigin.Receipt));
            Assert.That(storedBank, Is.Not.Null);
        });
    }

    [Test]
    public async Task BillUploadedThenStatementImported_EndAsOneReconciledRow_WithLineItemsPreserved()
    {
        await UploadBillAsync("SILPO", 390.00m, At(18, 0, 0),
            new LineItem("Bread", 90.00m),
            new LineItem("Milk", 120.00m));

        await ImportStatementAsync(
            new StatementRow(At(18, 30, 0), "Bank statement label", -390.00m, 16752.46m));

        var ledger = Ledger();
        Assert.Multiple(() =>
        {
            Assert.That(ledger, Has.Count.EqualTo(1), "one purchase, one ledger row — the Bank Transaction was absorbed");
            var merged = ledger.Single();
            Assert.That(merged.DataOrigin, Is.EqualTo(DataOrigin.Reconciled));
            Assert.That(merged.Description, Is.EqualTo("Bank statement label"), "the bank's description survives the merge");
            Assert.That(merged.Items.Select(i => i.Name), Is.EquivalentTo(new[] { "Bread", "Milk" }),
                "the Bill's Line Items survive; the statement row's MCC guess is deleted with it");
            Assert.That(merged.Items.All(i => i.Origin == ItemOrigin.ReceiptParsed), Is.True);
        });
    }

    [Test]
    public async Task StatementImportedThenBillUploaded_EndAsOneReconciledRow_AndUploadReturnsTheMergedRow()
    {
        var takenAt = At(12, 0, 0);
        await ImportStatementAsync(
            new StatementRow(At(12, 20, 0), "SILPO", -390.00m, 16752.46m));

        var uploaded = await UploadBillAsync("SILPO", 390.00m, takenAt,
            new LineItem("Bread", 90.00m),
            new LineItem("Milk", 120.00m));

        var ledger = Ledger();
        Assert.Multiple(() =>
        {
            Assert.That(ledger, Has.Count.EqualTo(1), "the opposite ingestion order lands in the same place");
            Assert.That(uploaded.DataOrigin, Is.EqualTo(DataOrigin.Reconciled),
                "the upload answers with the merged row, not a Provisional Bill that no longer exists");
            Assert.That(uploaded.Items, Has.Count.EqualTo(2));
            Assert.That(ledger.Single().Id, Is.EqualTo(uploaded.Id),
                "the Bill keeps its identity through the merge");
        });
    }

    [Test]
    public async Task MergedRow_AmountIsBankAmount_MoneyDeltaIsBankMinusItems_AndDeltaIsNeverStored()
    {
        await UploadBillAsync("SILPO", 390.00m, At(18, 0, 0),
            new LineItem("Bread", 90.00m),
            new LineItem("Milk", 120.00m));

        await ImportStatementAsync(
            new StatementRow(At(18, 30, 0), "SILPO", -390.00m, 16752.46m));

        var merged = Ledger().Single();
        Assert.Multiple(() =>
        {
            Assert.That(merged.Amount, Is.EqualTo(390.00m), "the amount in every total is what the bank says moved");
            Assert.That(merged.MoneyDelta, Is.EqualTo(180.00m), "390 paid − 210 explained by the paper");
            Assert.That(merged.Timestamp, Is.EqualTo(At(18, 30, 0)),
                "the bank Timestamp travels — otherwise the next import of the same file is no longer a duplicate");
            Assert.That(merged.BalanceAfter, Is.EqualTo(16752.46m));
            Assert.That(merged.RawTransactionData, Does.Contain("SILPO"),
                "the verbatim statement line is kept for ticket 08's un-merge");
            Assert.That(_testContext.Model.FindEntityType(typeof(Transaction))!
                .FindProperty(nameof(Transaction.MoneyDelta)), Is.Null,
                "Money Delta is derived, never a stored fact");
        });
    }

    [Test]
    public async Task AmountDrift_LeavesBillProvisional()
    {
        await UploadBillAsync("SILPO", 12.50m, At(18, 0, 0));
        await ImportStatementAsync(
            new StatementRow(At(18, 10, 0), "SILPO", -12.99m, 900.00m));

        var ledger = Ledger();
        Assert.Multiple(() =>
        {
            Assert.That(ledger, Has.Count.EqualTo(2), "a drift of 49 kopecks is not evidence of the same purchase");
            Assert.That(ledger.Single(t => t.DataOrigin == DataOrigin.Receipt).Amount, Is.EqualTo(12.50m));
            Assert.That(ledger.Single(t => t.DataOrigin == DataOrigin.Bank).Amount, Is.EqualTo(12.99m));
        });
    }

    [Test]
    public async Task OutsideOneHourWindow_LeavesBillProvisional()
    {
        await UploadBillAsync("SILPO", 100.00m, At(18, 0, 0));
        await ImportStatementAsync(
            new StatementRow(At(20, 0, 0), "SILPO", -100.00m, 900.00m));

        var ledger = Ledger();
        Assert.Multiple(() =>
        {
            Assert.That(ledger, Has.Count.EqualTo(2), "two hours apart is not the same purchase, same number or not");
            Assert.That(ledger.Select(t => t.DataOrigin), Is.EquivalentTo(new[] { DataOrigin.Receipt, DataOrigin.Bank }));
        });
    }

    [Test]
    public async Task TwoCandidateBankRows_NoMerge()
    {
        await UploadBillAsync("SILPO", 250.00m, At(18, 0, 0));
        await ImportStatementAsync(
            new StatementRow(At(18, 10, 0), "SILPO A", -250.00m, 900.00m),
            new StatementRow(At(18, 20, 0), "SILPO B", -250.00m, 650.00m));

        var ledger = await _sut.GetTransactionsAsync(_testUserId);

        Assert.Multiple(() =>
        {
            Assert.That(ledger.Any(t => t.DataOrigin == DataOrigin.Reconciled), Is.False, "ambiguity is never guessed away");
            Assert.That(ledger, Has.Count.EqualTo(3));
            Assert.That(ledger.Single(t => t.DataOrigin == DataOrigin.Receipt).Timestamp,
                Is.EqualTo(At(18, 0, 0)), "the Provisional Bill is left exactly as it was");
        });
    }

    [Test]
    public async Task ImportStatement_AssessesAmbiguityAgainstUntouchedBankRows()
    {
        await UploadBillAsync("SILPO", 20m, At(18));
        SeedBankRow(20m, At(18, 10));

        var imported = await ImportStatementAsync(new StatementRow(At(18, 30), "SILPO", -20m, 900m));
        var ledger = await _sut.GetTransactionsAsync(_testUserId);
        Assert.Multiple(() =>
        {
            Assert.That(imported.ReconciledCount, Is.Zero);
            Assert.That(ledger, Has.Count.EqualTo(3));
            Assert.That(ledger.Any(t => t.DataOrigin == DataOrigin.Reconciled), Is.False);
        });
    }

    [Test]
    public async Task Reimport_IsIdempotent()
    {
        await UploadBillAsync("SILPO", 390m, At(18), new LineItem("Bread", 90m));
        var row = new StatementRow(At(18, 30), "SILPO", -390m, 900m);
        var first = await ImportStatementAsync(row);
        var second = await ImportStatementAsync(row);
        var ledger = await _sut.GetTransactionsAsync(_testUserId);

        Assert.Multiple(() =>
        {
            Assert.That(first.ReconciledCount, Is.EqualTo(1));
            Assert.That(second.ReconciledCount, Is.Zero);
            Assert.That(second.DuplicateRowCount, Is.EqualTo(1));
            Assert.That(ledger, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Matching_NeverPairsBillWithBill_NorWithIncomeOrTransfer()
    {
        var t = At(12, 0, 0);

        // Two Bills of the same amount at the same instant: each has one candidate Bank row, but
        // that row is a candidate for both — every pair touching it is dropped.
        await UploadBillAsync("SILPO A", 390.00m, t);
        await UploadBillAsync("SILPO B", 390.00m, t);

        // A Bill and an Income row with the same figure: money arrived, it did not leave.
        await UploadBillAsync("REFUND LOOKALIKE", 250.00m, At(13, 0, 0));
        SeedBankRow(250.00m, At(13, 5, 0), TransactionType.Income);

        // A Bill and a Transfer row: moving money between own accounts is not a purchase.
        await UploadBillAsync("OWN ACCOUNT LOOKALIKE", 190.00m, At(14, 0, 0));
        SeedBankRow(190.00m, At(14, 5, 0), TransactionType.Transfer);

        await ImportStatementAsync(new StatementRow(t.AddMinutes(10), "Lookalike", -390m, 900m));
        var ledger = await _sut.GetTransactionsAsync(_testUserId);

        Assert.Multiple(() =>
        {
            Assert.That(ledger, Has.Count.EqualTo(7), "nothing merges: four Bills, three lookalike bank rows, nothing absorbed");
            Assert.That(ledger.Count(t => t.DataOrigin == DataOrigin.Reconciled), Is.EqualTo(0));
            Assert.That(ledger.Count(t => t.DataOrigin == DataOrigin.Receipt), Is.EqualTo(4));
        });
    }

    [Test]
    public async Task MergedRow_InheritsPaymentMethodFromTheMatchedCard()
    {
        var otherCard = new PaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Name = "Monobank world",
            Type = PaymentMethodType.BankCard,
            Currency = Currency.UAH,
            BankProvider = "Monobank",
        };
        _testContext.PaymentMethods.Add(otherCard);
        await _testContext.SaveChangesAsync();

        await UploadBillAsync("SILPO", 390.00m, At(18, 0, 0));
        await ImportStatementOnCardAsync(otherCard.Id,
            new StatementRow(At(18, 30, 0), "SILPO", -390.00m, 16752.46m));

        var merged = Ledger().Single();
        Assert.Multiple(() =>
        {
            Assert.That(merged.PaymentMethodId, Is.EqualTo(otherCard.Id),
                "Owner is inherited from the card that matched, across cards");
            Assert.That(merged.DataOrigin, Is.EqualTo(DataOrigin.Reconciled));
        });
    }

    [Test]
    public async Task CompleteBillSave_CorrectsAmountAndReconcilesWithAllItems()
    {
        var bill = await UploadBillAsync("SILPO", 12m, At(18), new LineItem("Old line", 12m));
        await ImportStatementAsync(new StatementRow(At(18, 30), "SILPO", -20m, 900m));
        var header = new Transaction
        {
            Id = bill.Id, UserId = _testUserId, Type = TransactionType.Expense,
            DataOrigin = DataOrigin.Receipt, Timestamp = At(18), Amount = 20m,
            Currency = Currency.UAH, Description = "Corrected SILPO"
        };
        var items = new[]
        {
            new TransactionItem
            {
                Id = bill.Items.Single().Id, TransactionId = bill.Id, Name = "Corrected line",
                FullName = "Corrected line", Quantity = 1, PricePerUnit = 15m, TotalPrice = 15m
            },
            new TransactionItem
            {
                TransactionId = bill.Id, Name = "New line", FullName = "New line",
                Quantity = 1, PricePerUnit = 5m, TotalPrice = 5m
            }
        };

        var result = await _sut.UpdateTransactionAsync(_testUserId, header, items);
        var ledger = await _sut.GetTransactionsAsync(_testUserId);

        Assert.Multiple(() =>
        {
            Assert.That(result!.DataOrigin, Is.EqualTo(DataOrigin.Reconciled));
            Assert.That(result.MoneyDelta, Is.Zero);
            Assert.That(result.Items.Select(i => i.Name), Is.EquivalentTo(new[] { "Corrected line", "New line" }));
            Assert.That(result.Items.Single(i => i.Name == "Corrected line").Id, Is.EqualTo(bill.Items.Single().Id));
            Assert.That(result.Items.All(i => i.Id != Guid.Empty), Is.True);
            Assert.That(ledger, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task UpdateTransaction_OnReconciledRow_CannotOverwriteBankAmountOrCard()
    {
        await UploadBillAsync("SILPO", 390.00m, At(18, 0, 0),
            new LineItem("Bread", 90.00m));
        await ImportStatementAsync(
            new StatementRow(At(18, 30, 0), "SILPO", -390.00m, 16752.46m));

        var merged = _testContext.Transactions.AsNoTracking().Single(t => t.Id == Ledger().Single().Id);

        // The header PUT OcrBillsView sends after a Line Item edit: amount and card retyped away.
        merged.Amount = 1.00m;
        merged.PaymentMethodId = null;
        merged.DataOrigin = DataOrigin.Receipt;
        merged.Description = "SILPO (fixed spelling)";

        var updated = await _sut.UpdateTransactionAsync(_testUserId, merged);
        var stored = _testContext.Transactions.AsNoTracking().Single(t => t.Id == merged.Id);

        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.Not.Null);
            Assert.That(stored.Amount, Is.EqualTo(390.00m), "the bank's figure is not the household's to retype");
            Assert.That(stored.PaymentMethodId, Is.EqualTo(_cardId), "the card stays attached");
            Assert.That(stored.DataOrigin, Is.EqualTo(DataOrigin.Reconciled));
            Assert.That(stored.Description, Is.EqualTo("SILPO (fixed spelling)"), "what the paper says is still editable");
        });
    }

    [TestCase(TransactionType.Income)]
    [TestCase(TransactionType.Transfer)]
    public async Task ReconciledBillSave_RejectsTypeChange(TransactionType type)
    {
        var bill = await UploadBillAsync("SILPO", 390m, At(18));
        await ImportStatementAsync(new StatementRow(At(18, 30), "SILPO", -390m, 900m));
        var header = new Transaction
        {
            Id = bill.Id, UserId = _testUserId, Type = type, DataOrigin = DataOrigin.Receipt,
            Timestamp = At(18), Amount = 1m, Currency = Currency.UAH, Description = "Changed"
        };

        Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateTransactionAsync(_testUserId, header));
        var stored = await _sut.GetTransactionAsync(bill.Id, _testUserId);
        Assert.That(stored!.Type, Is.EqualTo(TransactionType.Expense));
    }

    [Test]
    public async Task CompleteBillSave_CannotBypassBillValidationByChangingOrigin()
    {
        var bill = await UploadBillAsync("SILPO", 20m, At(18));
        var header = new Transaction
        {
            Id = bill.Id, UserId = _testUserId, Type = TransactionType.Expense,
            DataOrigin = DataOrigin.Manual, Timestamp = null, Amount = -1m,
            Currency = Currency.UAH, Description = "Invalid"
        };

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.UpdateTransactionAsync(_testUserId, header, Array.Empty<TransactionItem>()));
        var stored = await _sut.GetTransactionAsync(bill.Id, _testUserId);
        Assert.That(stored!.DataOrigin, Is.EqualTo(DataOrigin.Receipt));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task BillSave_OmittedItemsPreserveDetail_EmptyItemsClearIt(bool complete)
    {
        var bill = await UploadBillAsync("SILPO", 20m, At(18), new LineItem("Bread", 20m));
        var bank = SeedBankRow(20m, At(18, 30));
        var result = await _sut.UpdateTransactionAsync(_testUserId, bill,
            complete ? Array.Empty<TransactionItem>() : null);
        var storedBank = await _sut.GetTransactionAsync(bank.Id, _testUserId);

        Assert.Multiple(() =>
        {
            Assert.That(result!.Items.Count, Is.EqualTo(complete ? 0 : 1));
            Assert.That(result.DataOrigin, Is.EqualTo(complete ? DataOrigin.Reconciled : DataOrigin.Receipt));
            Assert.That(storedBank == null, Is.EqualTo(complete));
        });
    }

    [TestCase("other-bill")]
    [TestCase("other-household")]
    [TestCase("duplicate")]
    [TestCase("negative-price")]
    [TestCase("invalid-quantity")]
    public async Task CompleteBillSave_RejectsInvalidDetailWithoutChangingBill(string scenario)
    {
        var bill = await UploadBillAsync("SILPO", 20m, At(18), new LineItem("Bread", 20m));
        var otherBill = await UploadBillAsync("Other", 30m, At(10), new LineItem("Other detail", 30m));
        if (scenario == "other-household")
        {
            var other = await _testContext.Transactions.FindAsync(otherBill.Id);
            other!.UserId = "other-household";
            await _testContext.SaveChangesAsync();
        }
        var item = new TransactionItem
        {
            Id = scenario is "other-bill" or "other-household" ? otherBill.Items.Single().Id : bill.Items.Single().Id,
            TransactionId = bill.Id, Name = "Changed", FullName = "Changed", Quantity = 1,
            PricePerUnit = 20m, TotalPrice = 20m
        };
        if (scenario == "negative-price") item.PricePerUnit = -1;
        if (scenario == "invalid-quantity") item.Quantity = double.NaN;
        var items = scenario == "duplicate" ? new[] { item, item } : new[] { item };
        bill.Description = "Changed header";

        Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateTransactionAsync(_testUserId, bill, items));
        var stored = await _sut.GetTransactionAsync(bill.Id, _testUserId);
        Assert.Multiple(() =>
        {
            Assert.That(stored!.Description, Is.EqualTo("SILPO"));
            Assert.That(stored.Items.Single().Name, Is.EqualTo("Bread"));
            Assert.That(stored.Items.Single().TotalPrice, Is.EqualTo(20m));
        });
    }

    [Test]
    public async Task ReconciledBillCompleteSave_ChangesDetailButPreservesBankFacts()
    {
        var bill = await UploadBillAsync("SILPO", 20m, At(18), new LineItem("Bread", 20m));
        await ImportStatementAsync(new StatementRow(At(18, 30), "SILPO", -20m, 900m));
        bill.Amount = -1;
        bill.Timestamp = null;
        bill.PaymentMethodId = Guid.NewGuid();
        bill.Currency = Currency.USD;
        bill.BalanceAfter = 0;
        bill.Description = "Corrected merchant";
        var item = new TransactionItem
        {
            Id = bill.Items.Single().Id, TransactionId = bill.Id, Name = "Bread", FullName = "Bread",
            Quantity = 1, PricePerUnit = 15m, TotalPrice = 15m
        };

        var result = await _sut.UpdateTransactionAsync(_testUserId, bill, new[] { item });

        Assert.Multiple(() =>
        {
            Assert.That(result!.Amount, Is.EqualTo(20m));
            Assert.That(result.Timestamp, Is.EqualTo(At(18, 30)));
            Assert.That(result.PaymentMethodId, Is.EqualTo(_cardId));
            Assert.That(result.Currency, Is.EqualTo(Currency.UAH));
            Assert.That(result.BaseAmount, Is.EqualTo(20m));
            Assert.That(result.BalanceAfter, Is.EqualTo(900m));
            Assert.That(result.DataOrigin, Is.EqualTo(DataOrigin.Reconciled));
            Assert.That(result.Description, Is.EqualTo("Corrected merchant"));
            Assert.That(result.MoneyDelta, Is.EqualTo(5m));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CompleteBillSave_LeavesAmbiguityAgainstUntouchedCandidates(bool competingBill)
    {
        var bill = await UploadBillAsync("Corrected Bill", 12m, At(18), new LineItem("Bread", 20m));
        if (competingBill) await UploadBillAsync("Other Bill", 20m, At(18, 5));
        SeedBankRow(20m, At(18, 30));
        if (!competingBill) SeedBankRow(20m, At(18, 40));
        bill.Amount = 20m;

        var result = await _sut.UpdateTransactionAsync(_testUserId, bill, bill.Items);
        var ledger = await _sut.GetTransactionsAsync(_testUserId);

        Assert.Multiple(() =>
        {
            Assert.That(result!.DataOrigin, Is.EqualTo(DataOrigin.Receipt));
            Assert.That(ledger.Any(t => t.DataOrigin == DataOrigin.Reconciled), Is.False);
            Assert.That(ledger, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public async Task CompleteBillSave_DoesNotReconcileUnrelatedHistoricalPair()
    {
        var bill = await UploadBillAsync("Corrected Bill", 12m, At(18));
        var historicalBill = await UploadBillAsync("Old Bill", 50m, At(10));
        var historicalBank = SeedBankRow(50m, At(10, 10));
        SeedBankRow(20m, At(18, 30));
        bill.Amount = 20m;

        var result = await _sut.UpdateTransactionAsync(_testUserId, bill, Array.Empty<TransactionItem>());
        var storedBill = await _sut.GetTransactionAsync(historicalBill.Id, _testUserId);
        var storedBank = await _sut.GetTransactionAsync(historicalBank.Id, _testUserId);
        Assert.Multiple(() =>
        {
            Assert.That(result!.DataOrigin, Is.EqualTo(DataOrigin.Reconciled));
            Assert.That(storedBill!.DataOrigin, Is.EqualTo(DataOrigin.Receipt));
            Assert.That(storedBank, Is.Not.Null);
        });
    }

    [Test]
    public async Task BillConfirmation_DoesNotReconcileUnrelatedHistoricalPair()
    {
        var historicalBill = await UploadBillAsync("Old Bill", 50m, At(10));
        var historicalBank = SeedBankRow(50m, At(10, 10));
        var result = await UploadBillAsync("New Bill", 20m, At(18));
        var storedBill = await _sut.GetTransactionAsync(historicalBill.Id, _testUserId);
        var storedBank = await _sut.GetTransactionAsync(historicalBank.Id, _testUserId);

        Assert.Multiple(() =>
        {
            Assert.That(result.DataOrigin, Is.EqualTo(DataOrigin.Receipt));
            Assert.That(storedBill!.DataOrigin, Is.EqualTo(DataOrigin.Receipt));
            Assert.That(storedBank, Is.Not.Null);
        });
    }

    [Test]
    public async Task LegacyLineItemMutation_DoesNotTriggerMatching()
    {
        var bill = await UploadBillAsync("SILPO", 20m, At(18), new LineItem("Bread", 20m));
        var bank = SeedBankRow(20m, At(18, 30));
        var item = bill.Items.Single();
        item.Name = "Changed";
        await _sut.UpdateTransactionItemAsync(_testUserId, item);
        await _sut.CreateTransactionItemAsync(_testUserId, new TransactionItem
        {
            TransactionId = bill.Id, Name = "New", FullName = "New", Quantity = 1
        });
        await _sut.DeleteTransactionItemAsync(item.Id, _testUserId);

        var storedBill = await _sut.GetTransactionAsync(bill.Id, _testUserId);
        var storedBank = await _sut.GetTransactionAsync(bank.Id, _testUserId);
        Assert.Multiple(() =>
        {
            Assert.That(storedBill!.DataOrigin, Is.EqualTo(DataOrigin.Receipt));
            Assert.That(storedBill.Items.Single().Name, Is.EqualTo("New"));
            Assert.That(storedBank, Is.Not.Null);
        });
    }

    [Test]
    public async Task ConfirmBill_AcceptsIsoEurCurrencyFromEditor()
    {
        var bill = await _sut.ConfirmReceiptAsync(new ConfirmReceiptRequest
        {
            FileStream = new MemoryStream(), ContentType = "image/png",
            Receipt = new ReceiptDto
            {
                MerchantName = "European merchant", Timestamp = At(18), Currency = "EUR", TotalAmount = 20m,
                Items = new()
            }
        }, _testUserId);
        Assert.That(bill.Currency, Is.EqualTo(Currency.EURO));
    }

    [Test]
    public async Task GetTransactionsAsync_ExposesTruthfulMoneyDelta()
    {
        await UploadBillAsync("SILPO", 390.00m, At(18, 0, 0),
            new LineItem("Bread", 90.00m),
            new LineItem("Milk", 120.00m));
        await ImportStatementAsync(
            new StatementRow(At(18, 30, 0), "SILPO", -390.00m, 16752.46m));

        var rows = await _sut.GetTransactionsAsync(_testUserId);
        var reconciled = rows.Single(t => t.DataOrigin == DataOrigin.Reconciled);

        Assert.Multiple(() =>
        {
            Assert.That(reconciled.Items, Has.Count.EqualTo(2), "the list query carries Line Items");
            Assert.That(reconciled.MoneyDelta, Is.EqualTo(180.00m),
                "a delta of the whole amount through the list query is a lie about the paper");
        });
    }

    [Test]
    public async Task ImportBankingFile_ReturnsSurvivingBillWithBankFactsAndItems()
    {
        var bill = await UploadBillAsync("SILPO", 390m, At(18), new LineItem("Bread", 90m));
        var result = await ImportStatementAsync(new StatementRow(At(18, 30), "SILPO", -390m, 900m));
        var returned = result.Transactions.Single();
        var stored = await _sut.GetTransactionAsync(returned.Id, _testUserId);

        Assert.Multiple(() =>
        {
            Assert.That(returned.Id, Is.EqualTo(bill.Id));
            Assert.That(returned.DataOrigin, Is.EqualTo(DataOrigin.Reconciled));
            Assert.That(returned.MoneyDelta, Is.EqualTo(300m));
            Assert.That(returned.BalanceAfter, Is.EqualTo(900m));
            Assert.That(stored, Is.Not.Null);
        });
    }

    [Test]
    public async Task ImportBankingFile_ReportsReconciledCount()
    {
        await UploadBillAsync("SILPO", 390.00m, At(18, 0, 0));
        var result = await ImportStatementAsync(
            new StatementRow(At(18, 30, 0), "SILPO", -390.00m, 16752.46m),
            new StatementRow(At(20, 0, 0), "ROCKET", -99.00m, 16653.46m));

        Assert.Multiple(() =>
        {
            Assert.That(result.InsertedRowCount, Is.EqualTo(2));
            Assert.That(result.ReconciledCount, Is.EqualTo(1),
                "the import says how much of what it read was already photographed");
        });
    }
}
