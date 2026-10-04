using System.Linq;
using Microsoft.EntityFrameworkCore;
using MyPlanner.Data.DBContexts;
using MyPlanner.Data.Entities.Finance;
using MyPlanner.Service.Exceptions;
using MyPlanner.Service.Helpers;
using MyPlanner.Service.Helpers.BankExport;
using MyPlanner.Service.Interfaces;
using MyPlanner.Service.Models;
using MyPlanner.Service.Requests.Finance;

namespace MyPlanner.Service;

public partial class FinanceService : IFinanceService
{
    private readonly ApplicationDbContext _context;
    private readonly ILlmService _llmService;
    private readonly IBucketStore _bucket;

    public FinanceService(ApplicationDbContext context, ILlmService llmService, IBucketStore bucket)
    {
        _context = context;
        _llmService = llmService;
        _bucket = bucket;
    }

    // PaymentMethod operations
    public async Task<IReadOnlyList<PaymentMethod>> GetPaymentMethodsAsync(string userId)
    {
        return await _context.PaymentMethods
            .Where(pm => pm.UserId == userId)
            .ToListAsync();
    }

    public async Task<PaymentMethod?> GetPaymentMethodAsync(Guid id, string userId)
    {
        return await _context.PaymentMethods
            .FirstOrDefaultAsync(pm => pm.Id == id && pm.UserId == userId);
    }

    public async Task<Guid> CreatePaymentMethodAsync(string userId, PaymentMethod model)
    {
        // Override UserId to ensure it belongs to the authenticated user
        model.UserId = userId;
        model.BankProvider = NormalizeBankProvider(model.Type, model.BankProvider);

        _context.PaymentMethods.Add(model);
        await _context.SaveChangesAsync();
        return model.Id;
    }

    public async Task<bool> UpdatePaymentMethodAsync(string userId, PaymentMethod model)
    {
        var existing = await _context.PaymentMethods
            .FirstOrDefaultAsync(pm => pm.Id == model.Id && pm.UserId == userId);
        
        if (existing == null)
            return false;

        existing.Name = model.Name;
        existing.Type = model.Type;
        existing.Currency = model.Currency;
        existing.BankProvider = NormalizeBankProvider(model.Type, model.BankProvider);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PaymentMethodDeletionResult> DeletePaymentMethodAsync(Guid id, string userId)
    {
        var entity = await _context.PaymentMethods
            .FirstOrDefaultAsync(pm => pm.Id == id && pm.UserId == userId);

        if (entity == null)
            return new PaymentMethodDeletionResult(PaymentMethodDeletionStatus.NotFound, 0);

        // Money never disappears silently: a payment method with transactions on either side of a
        // transfer stays put until the caller has dealt with them. Counting every reference, not
        // just this household's, matches the FK restriction the database itself enforces.
        var transactionCount = await _context.Transactions
            .CountAsync(t => t.PaymentMethodId == id || t.ToPaymentMethodId == id);

        if (transactionCount > 0)
            return new PaymentMethodDeletionResult(PaymentMethodDeletionStatus.InUse, transactionCount);

        _context.PaymentMethods.Remove(entity);
        await _context.SaveChangesAsync();
        return new PaymentMethodDeletionResult(PaymentMethodDeletionStatus.Deleted, 0);
    }

    /// <summary>
    /// Normalises BankProvider on write: blank becomes null so "no provider" has one representation,
    /// and a provider sent with a type that never holds a bank statement (Cash, Other) is dropped
    /// rather than refused — the field does not apply to that shape. A method left without a provider
    /// cannot be an import target; see <c>isImportTarget</c> in Frontends/React/src/domain/paymentMethods.ts.
    /// </summary>
    private static string? NormalizeBankProvider(PaymentMethodType type, string? provider)
    {
        if (type is PaymentMethodType.Cash or PaymentMethodType.Other)
            return null;

        return string.IsNullOrWhiteSpace(provider) ? null : provider.Trim();
    }

    // Transaction operations
    /// <summary>
    /// Reads a statement file for the target payment method and reports what it says — row count,
    /// date span, rows needing review — without storing anything. The confirmation step of a
    /// two-step import; the browser keeps the file between the two calls. Neither the Bucket nor the
    /// ledger is touched: at preview time nothing has been decided yet (Q2).
    /// </summary>
    public async Task<BankStatementSummary> PreviewBankingFileAsync(ProcessBankingFileRequest request, string userId)
    {
        var (paymentMethod, parsed, _) = await ReadBankStatementAsync(request, userId);
        return BuildSummary(paymentMethod, parsed);
    }

    /// <summary>
    /// Inserts a confirmed statement file: rows the ledger has never seen become Bank Transactions,
    /// rows it already has are counted as duplicates and skipped (dedupe stays timestamp + amount,
    /// policy proper is ticket 03). Rows without a readable date were held back by the parser and
    /// are reported, never inserted.
    /// </summary>
    public Task<BankStatementImportResult> ImportBankingFileAsync(ProcessBankingFileRequest request, string userId) =>
        InTransactionAsync(() => ImportBankingFileCoreAsync(request, userId));

    private async Task<BankStatementImportResult> ImportBankingFileCoreAsync(ProcessBankingFileRequest request, string userId)
    {
        var (paymentMethod, parsed, fileBytes) = await ReadBankStatementAsync(request, userId);

        // The Statement File is written before any row is: every Bank Transaction of this import names
        // that one file, so a committed row must never point at a file that failed to save (D9).
        var statementFileKey = await StoreStatementFileAsync(request.ContentType, fileBytes);

        // A Provisional Bill is not bank evidence, even at the exact statement Timestamp.
        // The broader deduplication policy remains timestamp + absolute amount.
        // Stored amounts are absolute (the sign lives in Type), so the statement's signed figure
        // is normalised the same way before comparison — otherwise a re-imported expense never matches.
        var existingKeys = (await _context.Transactions
                .Where(t => t.UserId == userId && t.Timestamp != null && t.DataOrigin != DataOrigin.Receipt)
                .Select(t => new { t.Timestamp, t.Amount })
                .ToListAsync())
            .Select(t => (t.Timestamp!.Value, Math.Abs(t.Amount)))
            .ToHashSet();

        var newTransactions = new List<Transaction>();
        var duplicateRowCount = 0;

        foreach (var dto in parsed.Rows)
        {
            var key = (dto.Timestamp!.Value, Math.Abs(dto.Amount));
            if (!existingKeys.Add(key))
            {
                duplicateRowCount++;
                continue;
            }

            // Amount sign handling: negative → Expense, positive → Income; zero treated as Income.
            // Type is the row's money role — clients read it, they never re-derive it from signs.
            var type = dto.Amount < 0 ? TransactionType.Expense : TransactionType.Income;

            var transaction = new Transaction
            {
                UserId = userId,
                Type = type,
                PaymentMethodId = request.PaymentMethodId,
                Timestamp = dto.Timestamp,
                // Original-currency figure, absolute; the card-currency figure is kept beside it.
                Amount = Math.Abs(dto.Amount),
                Currency = TryParseCurrency(dto.Currency, paymentMethod.Currency),
                BaseAmount = dto.BaseAmount.HasValue ? Math.Abs(dto.BaseAmount.Value) : null,
                Description = dto.Description,
                BalanceAfter = dto.BalanceAfter,
                DataOrigin = DataOrigin.Bank,
                // This row's address inside the Statement File: which file, which line, and which Bank
                // Profile it was read as (D8).
                RawTransactionData = RawTransactionDataEnvelope.Empty
                    .WithBank(new BankRawData(statementFileKey, dto.RowNumber, parsed.ProfileName))
                    .ToJson(),
            };

            ApplyMccCategory(transaction, dto);
            newTransactions.Add(transaction);
        }

        // Bulk insert (EF Core cascades TransactionItems). Saved before the result is built so the
        // returned rows carry their generated ids.
        if (newTransactions.Count > 0)
        {
            _context.Transactions.AddRange(newTransactions);
            await _context.SaveChangesAsync();
        }

        // Matching runs after the file is written down, so both ingestion orders end in the same place.
        var reconciliation = await MatchTransactionsAsync(userId, newTransactions.Select(t => t.Id).ToArray());
        await _context.SaveChangesAsync();

        var survivingIds = newTransactions
            .Select(t => reconciliation.GetValueOrDefault(t.Id, t.Id)).ToArray();
        var stored = await _context.Transactions.AsNoTracking().Include(t => t.Items)
            .Where(t => t.UserId == userId && survivingIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id);

        await PopulateReviewCandidatesAsync(stored.Values.ToArray(), userId);
        return new BankStatementImportResult(
            BuildSummary(paymentMethod, parsed),
            InsertedRowCount: newTransactions.Count,
            DuplicateRowCount: duplicateRowCount,
            ReconciledCount: reconciliation.Count,
            Transactions: survivingIds.Select(id => stored[id]).ToArray());
    }

    /// <summary>
    /// Resolves the import target and reads its file. Both refusals happen here, once:
    /// a method with no bank set (nothing to read the file with), and a bank we have no profile for.
    /// The file's bytes come back alongside the rows so the caller can store them (D9).
    /// </summary>
    private async Task<(PaymentMethod PaymentMethod, BankParseResult Parsed, byte[] FileBytes)> ReadBankStatementAsync(
        ProcessBankingFileRequest request, string userId)
    {
        var paymentMethod = await _context.PaymentMethods
            .FirstOrDefaultAsync(pm => pm.Id == request.PaymentMethodId && pm.UserId == userId);
        if (paymentMethod == null)
            throw new InvalidOperationException(
                $"Payment method with ID {request.PaymentMethodId} not found for user {userId}.");

        if (string.IsNullOrWhiteSpace(paymentMethod.BankProvider))
            throw new UnsupportedBankProviderException(
                $"{paymentMethod.Name} has no bank set, so there is no statement format to read. " +
                "Set the bank on the payment method first.");

        // The upload is read into memory once, here: the CSV reader closes whatever stream it is given,
        // and the same bytes have to still be available to store afterwards. The cap is refused before
        // the parse, which is the expensive work it protects (D11).
        var fileBytes = await ReadUploadAsync(request.FileStream, "A statement file");

        // One entry point decides file kind and column map; there is no default profile to fall
        // back onto — by design.
        using var parseStream = new MemoryStream(fileBytes);
        var parsed = BankExportParser.Parse(parseStream, request.ContentType, paymentMethod.BankProvider);

        return (paymentMethod, parsed, fileBytes);
    }

    /// <summary>
    /// Writes the Statement File into the Bucket verbatim and returns its FileKey. Identical bytes from a
    /// re-import land on the same key, so importing a file twice stores it once (D6).
    /// </summary>
    private async Task<string> StoreStatementFileAsync(string contentType, byte[] fileBytes)
    {
        await using var stream = new MemoryStream(fileBytes);
        return await _bucket.SaveAsync(stream, contentType);
    }

    /// <summary>
    /// Reads an upload into memory, refusing it as soon as it passes the cap (D11) rather than only when
    /// its hash comes out too big — so an over-limit file never reaches a parse or a model call.
    /// </summary>
    private static async Task<byte[]> ReadUploadAsync(Stream fileStream, string what)
    {
        EnsureWithinUploadLimit(fileStream, what);

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        var total = 0L;
        int read;
        while ((read = await fileStream.ReadAsync(chunk)) > 0)
        {
            total += read;
            if (total > UploadLimit.MaxBytes)
                throw new UploadTooLargeException(what);
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static BankStatementSummary BuildSummary(PaymentMethod paymentMethod, BankParseResult parsed)
    {
        DateTime? first = null, last = null;
        foreach (var row in parsed.Rows)
        {
            if (!row.Timestamp.HasValue) continue;
            if (first is null || row.Timestamp < first) first = row.Timestamp;
            if (last is null || row.Timestamp > last) last = row.Timestamp;
        }

        return new BankStatementSummary(
            paymentMethod.Id,
            paymentMethod.Name,
            paymentMethod.BankProvider ?? string.Empty,
            parsed.Rows.Count,
            first,
            last,
            parsed.NeedsReview.Select(BankStatementReviewRow.From).ToList());
    }

    /// <summary>
    /// Converts a string currency code to the Currency enum.
    /// Returns UAH as default when input is null or empty.
    /// Throws InvalidOperationException on unknown currency codes.
    /// </summary>
    public static Currency ParseCurrencyString(string? currencyString)
    {
        if (string.IsNullOrWhiteSpace(currencyString))
            return Currency.UAH;

        var trimmed = currencyString.Trim().ToUpperInvariant();
        if (trimmed == "EUR") return Currency.EURO;
        if (Enum.TryParse<Currency>(trimmed, ignoreCase: true, out var parsed))
            return parsed;

        throw new InvalidOperationException(
            $"Unknown currency code '{currencyString}'. Supported values: {string.Join(", ", Enum.GetNames<Currency>())}");
    }

    /// <summary>
    /// Maps a statement's currency code onto the ledger's enum. "EUR" is the ISO spelling of the
    /// stored <c>EURO</c>; an unknown or missing code falls back to the payment method's own
    /// currency, because a row charged to this card is in something this card holds.
    /// </summary>
    private static Currency TryParseCurrency(string? currencyString, Currency fallback)
    {
        if (!string.IsNullOrWhiteSpace(currencyString))
        {
            var code = currencyString.Trim().ToUpperInvariant();
            if (code == "EUR")
                return Currency.EURO;
            if (Enum.TryParse<Currency>(code, ignoreCase: true, out var parsed))
                return parsed;
        }
        return fallback;
    }

    /// <summary>
    /// Gives a row without Line Items its category: the MCC decides where the money went. A row
    /// that already carries items keeps whatever categorisation they have — this never overwrites
    /// a person's or a receipt's line detail with a merchant-code guess.
    /// </summary>
    private static void ApplyMccCategory(Transaction transaction, TransactionDto dto)
    {
        if (transaction.Items.Count > 0)
            return;

        (string? Category, string? Subcategory)? mccCategory = dto.MCC.HasValue
            ? ReceiptCategories.GetCategoryFromMcc(dto.MCC.Value)
            : null;

        var envelope = RawTransactionDataEnvelope.Read(transaction.RawTransactionData);
        transaction.RawTransactionData = envelope.WithBank((envelope.Bank ?? new BankRawData()) with
        { Category = mccCategory?.Category, Subcategory = mccCategory?.Subcategory }).ToJson();

        // All required members must be set in the object initializer. EF Core will
        // overwrite TransactionId with the correct FK when the parent is added to context.
        transaction.Items.Add(new TransactionItem
        {
            TransactionId = Guid.Empty,
            Name = dto.Description,
            FullName = dto.Description,
            Category = mccCategory?.Category,
            Subcategory = mccCategory?.Subcategory,
            Origin = ItemOrigin.AutoGenerated,
            Quantity = 1,
            PricePerUnit = transaction.Amount,
            TotalPrice = transaction.Amount
        });
    }

    /// <summary>
    /// Runs OCR only. No ledger row is created and no file is stored until the corrected draft is
    /// confirmed (Q2/Q5). The upload cap is enforced here rather than at the door alone: an over-limit
    /// image must never reach a model call, which is the expensive thing being protected (D11).
    /// </summary>
    public async Task<ReceiptDto> PreviewReceiptAsync(ProcessReceiptRequest request)
    {
        EnsureWithinUploadLimit(request.FileStream, "A bill image");
        return await ReceiptParser.ProcessReceiptAsync(_llmService, request.FileStream, request.ContentType);
    }

    /// <summary>
    /// The shared upload cap (D11). A stream that cannot say its length is still caught later, by the
    /// Bucket as it hashes — this check exists to fail before any parsing or model work happens.
    /// </summary>
    private static void EnsureWithinUploadLimit(Stream fileStream, string what)
    {
        if (fileStream.CanSeek && fileStream.Length > UploadLimit.MaxBytes)
            throw new UploadTooLargeException(what);
    }

    /// <summary>Validates and saves the user's corrected Bill Draft.</summary>
    public Task<Transaction> ConfirmReceiptAsync(ConfirmReceiptRequest request, string userId) =>
        InTransactionAsync(() => ConfirmReceiptCoreAsync(request, userId));

    private async Task<Transaction> ConfirmReceiptCoreAsync(ConfirmReceiptRequest request, string userId)
    {
        var receipt = request.Receipt;
        if (receipt.Timestamp is null)
            throw new InvalidOperationException("A confirmed Timestamp is required to save a Bill.");
        if (receipt.Items.Any(i => !double.IsFinite(i.Quantity) || i.Quantity < 0
                                   || i.UnitPrice < 0 || i.TotalPrice < 0))
            throw new InvalidOperationException("Line Item quantity and money fields must be non-negative and valid.");
        if (receipt.TotalAmount is < 0)
            throw new InvalidOperationException("Bill Total must be non-negative.");
        if (request.PaymentMethodId.HasValue && !await _context.PaymentMethods
                .AnyAsync(pm => pm.Id == request.PaymentMethodId && pm.UserId == userId))
            throw new InvalidOperationException("Payment Method not found for this household.");

        // The paper goes to disk before the ledger row exists: a Bill is its Line Items plus this one
        // pointer, and if the database write fails afterwards the orphan file costs nothing (D9/D12).
        var imageFileKey = await _bucket.SaveAsync(request.FileStream, request.ContentType);

        var transaction = new Transaction
        {
            UserId = userId,
            Type = TransactionType.Expense,
            PaymentMethodId = request.PaymentMethodId,
            Timestamp = receipt.Timestamp,
            Amount = receipt.TotalAmount ?? receipt.Items.Sum(i => i.TotalPrice),
            Currency = ParseCurrencyString(receipt.Currency),
            Description = string.IsNullOrWhiteSpace(receipt.MerchantName) ? "Receipt purchase" : receipt.MerchantName.Trim(),
            AdditionalNotes = receipt.AdditionalNotes,
            DataOrigin = DataOrigin.Receipt,
            RawTransactionData = RawTransactionDataEnvelope.Empty.WithBill(new BillRawData(imageFileKey)).ToJson(),
        };

        foreach (var item in receipt.Items)
        {
            transaction.Items.Add(new TransactionItem
            {
                TransactionId = Guid.Empty,
                Name = item.Name,
                FullName = item.FullName,
                Category = item.Category,
                Subcategory = item.Subcategory,
                Quantity = item.Quantity,
                PricePerUnit = item.UnitPrice,
                TotalPrice = item.TotalPrice,
                Origin = ItemOrigin.ReceiptParsed
            });
        }

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        await MatchTransactionsAsync(userId, new[] { transaction.Id });
        await _context.SaveChangesAsync();

        // The re-read returns the merged truth — DataOrigin, bank amount, delta — so the upload
        // step shows the row as it now is, not as it was before Matching ran.
        return (await GetTransactionAsync(transaction.Id, userId))!;
    }

    public async Task<IReadOnlyList<Transaction>> GetTransactionsAsync(string? userId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        // Items travel with the row: MoneyDelta reads them, and a Reconciled row without its
        // Line Items would silently report its whole amount as the delta.
        var query = _context.Transactions.AsNoTracking().Include(t => t.Items).AsQueryable();

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(t => t.UserId == userId);
        }

        if (startDate.HasValue || endDate.HasValue)
        {
            if (startDate.HasValue)
                query = query.Where(t => t.Timestamp.HasValue && t.Timestamp >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(t => t.Timestamp.HasValue && t.Timestamp <= endDate.Value);
        }

        var rows = await query.ToListAsync();
        await PopulateReviewCandidatesAsync(rows, userId);
        return rows;
    }

    public async Task<Transaction?> GetTransactionAsync(Guid id, string userId)
    {
        var row = await _context.Transactions.AsNoTracking().Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (row != null) await PopulateReviewCandidatesAsync(new[] { row }, userId);
        return row;
    }

    public async Task<Guid> CreateTransactionAsync(string userId, Transaction model)
    {
        await ValidateTransactionWriteAsync(userId, model);
        // Override UserId to ensure it belongs to the authenticated user
        model.UserId = userId;
        
        _context.Transactions.Add(model);
        await _context.SaveChangesAsync();
        return model.Id;
    }

    public Task<Transaction?> UpdateTransactionAsync(string userId, Transaction model, IReadOnlyList<TransactionItem>? items = null, string? expectedDetailVersion = null) =>
        InTransactionAsync(() => UpdateTransactionCoreAsync(userId, model, items, expectedDetailVersion));

    private async Task<Transaction?> UpdateTransactionCoreAsync(string userId, Transaction model, IReadOnlyList<TransactionItem>? items, string? expectedDetailVersion)
    {
        var existing = await _context.Transactions.Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == model.Id && t.UserId == userId);

        if (existing == null)
            return null;

        if (expectedDetailVersion != null && expectedDetailVersion != existing.DetailVersion)
            throw new InvalidOperationException("Transaction changed since editing started. Reload before saving.");

        var bankOwned = existing.DataOrigin is DataOrigin.Bank or DataOrigin.Reconciled;
        var allocationEdit = items != null && existing.DataOrigin is DataOrigin.Bank or DataOrigin.Manual;
        if (bankOwned && existing.Type != model.Type)
            throw new InvalidOperationException("Bank-backed Transaction Type cannot be changed by an edit.");
        if (items != null && existing.DataOrigin == DataOrigin.Receipt
                          && (model.DataOrigin != DataOrigin.Receipt || model.Type != TransactionType.Expense))
            throw new InvalidOperationException("A complete Provisional Bill edit must remain a Receipt Expense.");
        if (!bankOwned && !allocationEdit)
            await ValidateTransactionWriteAsync(userId, model);
        if (existing.DataOrigin == DataOrigin.Manual && items == null
            && (model.Amount < 0 || Money(model.Amount) < existing.Items.Where(i => i.Origin != ItemOrigin.AutoGenerated).Sum(i => Money(i.TotalPrice))))
            throw new InvalidOperationException("Paid amount cannot be reduced below saved allocations.");
        if (items != null) ReplaceLineItems(existing, items);

        if (!bankOwned && !allocationEdit) existing.Type = model.Type;

        // The bank's figures are not the household's to retype. On a Reconciled row a Bill edit may
        // change what the paper says (description, notes, Line Items) — that widens Money Delta,
        // nothing else. Without this guard a header PUT would rewrite the bank amount and detach
        // the card.
        if (!bankOwned && !allocationEdit)
        {
            existing.PaymentMethodId = model.PaymentMethodId;
            existing.ToPaymentMethodId = model.ToPaymentMethodId;
            existing.Timestamp = model.Timestamp;
            existing.Amount = existing.DataOrigin == DataOrigin.Receipt && items != null
                ? items.Sum(i => i.TotalPrice) : model.Amount;
            existing.Currency = model.Currency;
            existing.BalanceAfter = model.BalanceAfter;
            existing.DataOrigin = existing.DataOrigin == DataOrigin.Manual ? DataOrigin.Manual : model.DataOrigin;
        }

        if (!allocationEdit)
        {
            existing.Description = model.Description;
            existing.AdditionalNotes = model.AdditionalNotes;
        }

        await _context.SaveChangesAsync();
        if (items != null && existing.DataOrigin == DataOrigin.Receipt)
        {
            await MatchTransactionsAsync(userId, new[] { existing.Id });
            await _context.SaveChangesAsync();
        }
        return await GetTransactionAsync(existing.Id, userId);
    }

    private async Task ValidateTransactionWriteAsync(string userId, Transaction model)
    {
        if (model.DataOrigin == DataOrigin.Receipt)
        {
            if (model.Timestamp is null)
                throw new InvalidOperationException("A confirmed Timestamp is required to save a Bill.");
            if (model.Amount < 0)
                throw new InvalidOperationException("Bill Total must be non-negative.");
            if (model.PaymentMethodId.HasValue && !await _context.PaymentMethods
                    .AnyAsync(pm => pm.Id == model.PaymentMethodId && pm.UserId == userId))
                throw new InvalidOperationException("Payment Method not found for this household.");
        }
    }

    public async Task<bool> DeleteTransactionAsync(Guid id, string userId)
    {
        var entity = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        
        if (entity == null)
            return false;

        _context.Transactions.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    // TransactionItem operations
    public async Task<IReadOnlyList<TransactionItem>> GetTransactionItemsAsync(Guid transactionId, string userId)
    {
        return await _context.TransactionItems
            .Where(ti => ti.TransactionId == transactionId &&
                        _context.Transactions.Any(t => t.Id == transactionId && t.UserId == userId))
            .ToListAsync();
    }

    public async Task<TransactionItem?> GetTransactionItemAsync(Guid id, string userId)
    {
        return await _context.TransactionItems
            .FirstOrDefaultAsync(ti => ti.Id == id &&
                                       _context.Transactions.Any(t => t.Id == ti.TransactionId && t.UserId == userId));
    }

    public Task<Guid> CreateTransactionItemAsync(string userId, TransactionItem model) =>
        InTransactionAsync(() => CreateTransactionItemCoreAsync(userId, model));

    private async Task<Guid> CreateTransactionItemCoreAsync(string userId, TransactionItem model)
    {
        // Verify the parent transaction exists and belongs to this user
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == model.TransactionId && t.UserId == userId);
        if (transaction == null)
            throw new InvalidOperationException(
                $"Transaction with ID {model.TransactionId} not found for user {userId}.");
        if (transaction.DataOrigin is DataOrigin.Bank or DataOrigin.Manual)
        {
            var detail = await _context.TransactionItems.Where(i => i.TransactionId == transaction.Id).ToListAsync();
            var replacement = detail.Where(i => i.Origin != ItemOrigin.AutoGenerated).Select(CopyLineItem).ToList();
            var added = CopyLineItem(model);
            added.Id = Guid.Empty;
            replacement.Add(added);
            var saved = await UpdateTransactionAsync(userId, transaction, replacement);
            return saved!.Items.Single(i => !detail.Any(old => old.Id == i.Id)).Id;
        }
        if (transaction.DataOrigin is DataOrigin.Receipt or DataOrigin.Reconciled)
            ValidateBillLineItem(model);

        model.Origin = ItemOrigin.ManualInput;
        _context.TransactionItems.Add(model);
        await _context.SaveChangesAsync();
        return model.Id;
    }

    public Task<bool> UpdateTransactionItemAsync(string userId, TransactionItem model) =>
        InTransactionAsync(() => UpdateTransactionItemCoreAsync(userId, model));

    private async Task<bool> UpdateTransactionItemCoreAsync(string userId, TransactionItem model)
    {
        var existing = await _context.TransactionItems
            .FirstOrDefaultAsync(ti => ti.Id == model.Id &&
                                       _context.Transactions.Any(t => t.Id == ti.TransactionId && t.UserId == userId));
        if (existing == null)
            return false;
        var parent = await _context.Transactions.FirstAsync(t => t.Id == existing.TransactionId);
        if (parent.DataOrigin is DataOrigin.Bank or DataOrigin.Manual)
        {
            var detail = await _context.TransactionItems.Where(i => i.TransactionId == parent.Id).ToListAsync();
            var replacement = detail.Where(i => i.Id == model.Id || i.Origin != ItemOrigin.AutoGenerated)
                .Select(i => CopyLineItem(i.Id == model.Id ? model : i)).ToArray();
            await UpdateTransactionAsync(userId, parent, replacement);
            return true;
        }
        if (parent.DataOrigin is DataOrigin.Receipt or DataOrigin.Reconciled)
            ValidateBillLineItem(model);

        existing.Name = model.Name;
        existing.FullName = model.FullName;
        existing.Category = model.Category;
        existing.Subcategory = model.Subcategory;
        existing.Quantity = model.Quantity;
        existing.PricePerUnit = model.PricePerUnit;
        existing.TotalPrice = model.TotalPrice;
        existing.Origin = ItemOrigin.ManualInput;

        await _context.SaveChangesAsync();
        return true;
    }

    private static TransactionItem CopyLineItem(TransactionItem item) => new()
    {
        Id = item.Id, TransactionId = item.TransactionId, Name = item.Name, FullName = item.FullName,
        Category = item.Category, Subcategory = item.Subcategory, Quantity = item.Quantity,
        PricePerUnit = item.PricePerUnit, TotalPrice = item.TotalPrice, Origin = item.Origin
    };

    private static void ValidateBillLineItem(TransactionItem item)
    {
        if (!double.IsFinite(item.Quantity) || item.Quantity < 0 || item.PricePerUnit < 0 || item.TotalPrice < 0)
            throw new InvalidOperationException("Line Item quantity and money fields must be non-negative and valid.");
    }

    public Task<bool> DeleteTransactionItemAsync(Guid id, string userId) =>
        InTransactionAsync(() => DeleteTransactionItemCoreAsync(id, userId));

    private async Task<bool> DeleteTransactionItemCoreAsync(Guid id, string userId)
    {
        var entity = await _context.TransactionItems
            .FirstOrDefaultAsync(ti => ti.Id == id &&
                                       _context.Transactions.Any(t => t.Id == ti.TransactionId && t.UserId == userId));
        if (entity == null)
            return false;

        var parent = await _context.Transactions.Include(t => t.Items).FirstAsync(t => t.Id == entity.TransactionId);
        if (parent.DataOrigin is DataOrigin.Bank or DataOrigin.Manual)
        {
            await UpdateTransactionAsync(userId, parent, parent.Items.Where(i => i.Id != id).Select(CopyLineItem).ToArray());
            return true;
        }
        _context.TransactionItems.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}
