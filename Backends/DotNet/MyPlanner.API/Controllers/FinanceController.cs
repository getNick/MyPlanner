using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyPlanner.API.Models.Finance;
using MyPlanner.Data.Entities.Finance;
using MyPlanner.Service;
using MyPlanner.Service.Exceptions;
using MyPlanner.Service.Interfaces;
using MyPlanner.Service.Models;

namespace MyPlanner.API;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class FinanceController : ControllerBase
{
    private readonly IFinanceService _financeService;
    private readonly ILlmService? _llmService;

    public FinanceController(IFinanceService financeService, ILlmService? llmService = null)
    {
        _financeService = financeService;
        _llmService = llmService;
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

    // ── Receipt Categories (public) ──────────────────────────────────

    [AllowAnonymous]
    [HttpGet("receipts/categories")]
    public IActionResult GetReceiptCategories()
    {
        var result = ReceiptCategories.Categories.Select(c => new { c.Name, Subcategories = c.Subcategories.ToArray() });
        return Ok(result);
    }

    // ── Payment Methods ──────────────────────────────────────────────

    [HttpGet("payment-methods")]
    public async Task<IActionResult> GetPaymentMethods()
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var methods = await _financeService.GetPaymentMethodsAsync(userId);
        return Ok(methods);
    }

    [HttpPost("payment-methods")]
    public async Task<IActionResult> CreatePaymentMethod(PaymentMethodCreateDto dto)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var entity = new PaymentMethod
        {
            UserId = userId,
            Name = dto.Name,
            Type = dto.Type,
            Currency = dto.Currency,
            BankProvider = dto.BankProvider
        };

        var id = await _financeService.CreatePaymentMethodAsync(userId, entity);
        return CreatedAtAction(nameof(GetPaymentMethods), new { id }, id);
    }

    [HttpPut("payment-methods/{id:guid}")]
    public async Task<IActionResult> UpdatePaymentMethod(Guid id, PaymentMethodUpdateDto dto)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (id != dto.Id) return BadRequest();

        var entity = new PaymentMethod
        {
            Id = dto.Id,
            UserId = userId,
            Name = dto.Name,
            Type = dto.Type,
            Currency = dto.Currency,
            BankProvider = dto.BankProvider
        };

        var updated = await _financeService.UpdatePaymentMethodAsync(userId, entity);
        if (!updated) return NotFound();
        return Ok();
    }

    [HttpDelete("payment-methods/{id:guid}")]
    public async Task<IActionResult> DeletePaymentMethod(Guid id)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _financeService.DeletePaymentMethodAsync(id, userId);

        // A payment method with transactions is refused, not cascaded: the caller has to deal with
        // the money first. The count travels so the UI can say how many transactions are in the way.
        if (result.Status == PaymentMethodDeletionStatus.InUse)
            return Conflict(new { error = result.Explanation, transactionCount = result.TransactionCount });

        if (result.Status == PaymentMethodDeletionStatus.NotFound) return NotFound();
        return NoContent();
    }

    // ── Transactions ─────────────────────────────────────────────────

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var transactions = await _financeService.GetTransactionsAsync(userId, startDate, endDate);
        return Ok(transactions);
    }

    [HttpGet("transactions/{id:guid}")]
    public async Task<IActionResult> GetTransaction(Guid id)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var transaction = await _financeService.GetTransactionAsync(id, userId);
        if (transaction == null) return NotFound();
        return Ok(transaction);
    }

    [HttpPost("transactions")]
    public async Task<IActionResult> CreateTransaction(TransactionCreateDto dto)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        // Convert string Currency to enum (UAH default on null, throws on unknown)
        var currency = FinanceService.ParseCurrencyString(dto.Currency);

        var entity = new Transaction
        {
            UserId = userId,
            Type = dto.Type,
            PaymentMethodId = dto.PaymentMethodId, // null allowed for receipt saves
            ToPaymentMethodId = dto.ToPaymentMethodId,
            Timestamp = dto.Timestamp,
            Amount = dto.Amount,
            Currency = currency,
            Description = dto.Description,
            AdditionalNotes = dto.AdditionalNotes,
            BalanceAfter = dto.BalanceAfter,
            DataOrigin = dto.DataOrigin
        };

        try
        {
            var id = await _financeService.CreateTransactionAsync(userId, entity);
            return CreatedAtAction(nameof(GetTransactions), new { id }, id);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("transactions/{id:guid}")]
    public async Task<IActionResult> UpdateTransaction(Guid id, TransactionUpdateDto dto)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (id != dto.Id) return BadRequest();

        // Convert string Currency to enum (UAH default on null, throws on unknown)
        var currency = FinanceService.ParseCurrencyString(dto.Currency);

        var entity = new Transaction
        {
            Id = dto.Id,
            UserId = userId,
            Type = dto.Type,
            PaymentMethodId = dto.PaymentMethodId,
            ToPaymentMethodId = dto.ToPaymentMethodId,
            Timestamp = dto.Timestamp,
            Amount = dto.Amount,
            Currency = currency,
            Description = dto.Description,
            AdditionalNotes = dto.AdditionalNotes,
            BalanceAfter = dto.BalanceAfter,
            DataOrigin = dto.DataOrigin
        };

        try
        {
            var items = dto.Items?.Select(item => new TransactionItem
            {
                Id = item.Id ?? Guid.Empty,
                TransactionId = id,
                Name = item.Name,
                FullName = item.FullName ?? item.Name,
                Category = item.Category,
                Subcategory = item.Subcategory,
                Quantity = item.Quantity,
                PricePerUnit = item.PricePerUnit,
                TotalPrice = item.TotalPrice
            }).ToArray();
            var updated = await _financeService.UpdateTransactionAsync(userId, entity, items);
            if (updated == null) return NotFound();
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("transactions/{id:guid}")]
    public async Task<IActionResult> DeleteTransaction(Guid id)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var deleted = await _financeService.DeleteTransactionAsync(id, userId);
        if (!deleted) return NotFound();
        return NoContent();
    }

    // ── Receipt Processing ───────────────────────────────────────────

    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/jpg", "image/webp", "image/bmp"
    };

    [HttpPost("receipts/preview")]
    public async Task<IActionResult> PreviewReceipt([FromForm] IFormFile? file)
    {
        if (!TryValidateReceiptFile(file, out var error)) return BadRequest(error);
        if (string.IsNullOrEmpty(GetUserId())) return Unauthorized();

        var request = new MyPlanner.Service.Requests.Finance.ProcessReceiptRequest
        {
            FileStream = file!.OpenReadStream(),
            ContentType = file.ContentType
        };
        return Ok(await _financeService.PreviewReceiptAsync(request));
    }

    [HttpPost("receipts/confirm")]
    public async Task<IActionResult> ConfirmReceipt(
        [FromForm] IFormFile? file,
        [FromForm] string? receipt,
        [FromForm] Guid? paymentMethodId)
    {
        if (!TryValidateReceiptFile(file, out var error)) return BadRequest(error);
        if (string.IsNullOrWhiteSpace(receipt)) return BadRequest("Corrected Bill Draft fields are required.");
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        MyPlanner.Service.Models.ReceiptDto fields;
        try
        {
            fields = System.Text.Json.JsonSerializer.Deserialize<MyPlanner.Service.Models.ReceiptDto>(receipt,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new System.Text.Json.JsonException("Empty Bill Draft.");
        }
        catch (System.Text.Json.JsonException ex)
        {
            return BadRequest(ex.Message);
        }

        try
        {
            var saved = await _financeService.ConfirmReceiptAsync(new MyPlanner.Service.Requests.Finance.ConfirmReceiptRequest
            {
                FileStream = file!.OpenReadStream(),
                ContentType = file.ContentType,
                Receipt = fields,
                PaymentMethodId = paymentMethodId
            }, userId);
            return CreatedAtAction(nameof(GetTransaction), new { id = saved.Id }, saved);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static bool TryValidateReceiptFile(IFormFile? file, out string error)
    {
        if (file == null || file.Length == 0)
        {
            error = "No file provided.";
            return false;
        }
        if (!AllowedImageContentTypes.Contains(file.ContentType))
        {
            error = $"Invalid content type '{file.ContentType}'. Allowed types: {string.Join(", ", AllowedImageContentTypes)}";
            return false;
        }
        error = string.Empty;
        return true;
    }

    // ── Bank Statement Import ────────────────────────────────────────

    /// <summary>
    /// Reads a statement file for the chosen payment method and echoes what it says (row count,
    /// date range, rows needing review) without storing anything. Two stateless calls make the
    /// confirm step: the browser keeps the file between preview and import.
    /// </summary>
    [HttpPost("banking-files/preview")]
    public Task<IActionResult> PreviewBankingFile([FromForm] Guid paymentMethodId, [FromForm] IFormFile? file) =>
        ReadBankStatement(file, paymentMethodId, _financeService.PreviewBankingFileAsync);

    /// <summary>Inserts a confirmed statement. Refusals mirror the preview's.</summary>
    [HttpPost("banking-files")]
    public Task<IActionResult> ImportBankingFile([FromForm] Guid paymentMethodId, [FromForm] IFormFile? file) =>
        ReadBankStatement(file, paymentMethodId, _financeService.ImportBankingFileAsync);

    /// <summary>
    /// Shared shape of both banking-file calls: an uploaded statement read for a chosen method.
    /// Every refusal is an explicit 400 with the reason — a non-CSV file, a bank we cannot read,
    /// a target with no bank set, a method that does not exist — never a 500 and never the
    /// receipt (image) pipeline.
    /// </summary>
    private async Task<IActionResult> ReadBankStatement<T>(
        IFormFile? file,
        Guid paymentMethodId,
        Func<MyPlanner.Service.Requests.Finance.ProcessBankingFileRequest, string, Task<T>> read)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No statement file was uploaded." });

        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var request = new MyPlanner.Service.Requests.Finance.ProcessBankingFileRequest
        {
            FileStream = file.OpenReadStream(),
            ContentType = file.ContentType ?? string.Empty,
            PaymentMethodId = paymentMethodId,
        };

        try
        {
            return Ok(await read(request, userId));
        }
        catch (UnsupportedBankStatementFileException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnsupportedBankProviderException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // The target payment method does not exist for this user.
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Transaction Items ────────────────────────────────────────────

    [HttpGet("transactions/{txId:guid}/items")]
    public async Task<IActionResult> GetTransactionItems(Guid txId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        // Verify transaction exists and is owned by user before returning items
        var transaction = await _financeService.GetTransactionAsync(txId, userId);
        if (transaction == null) return NotFound();

        var items = await _financeService.GetTransactionItemsAsync(txId, userId);
        return Ok(items);
    }

    [HttpPost("transactions/{txId:guid}/items")]
    public async Task<IActionResult> CreateTransactionItem(Guid txId, TransactionItemCreateDto dto)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        // Verify transaction exists and is owned by user before creating item
        var transaction = await _financeService.GetTransactionAsync(txId, userId);
        if (transaction == null) return NotFound();

        var entity = new TransactionItem
        {
            TransactionId = txId,
            Name = dto.Name,
            FullName = dto.FullName ?? dto.Name,
            Category = dto.Category,
            Subcategory = dto.Subcategory,
            Quantity = dto.Quantity,
            PricePerUnit = dto.PricePerUnit,
            TotalPrice = dto.TotalPrice,
            Origin = dto.Origin ?? ItemOrigin.AutoGenerated
        };

        try
        {
            var id = await _financeService.CreateTransactionItemAsync(userId, entity);
            return CreatedAtAction(nameof(GetTransactionItems), new { txId }, id);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("transactions/items/{itemId:guid}")]
    public async Task<IActionResult> UpdateTransactionItem(Guid itemId, TransactionItemUpdateDto dto)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (itemId != dto.Id) return BadRequest();

        var entity = await _financeService.GetTransactionItemAsync(itemId, userId);
        if (entity == null) return NotFound();

        entity.Name = dto.Name;
        entity.FullName = dto.FullName ?? dto.Name;
        entity.Category = dto.Category;
        entity.Subcategory = dto.Subcategory;
        entity.Quantity = dto.Quantity;
        entity.PricePerUnit = dto.PricePerUnit;
        entity.TotalPrice = dto.TotalPrice;
        entity.Origin = dto.Origin;

        try
        {
            var updated = await _financeService.UpdateTransactionItemAsync(userId, entity);
            if (!updated) return NotFound();
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("transactions/items/{itemId:guid}")]
    public async Task<IActionResult> DeleteTransactionItem(Guid itemId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var deleted = await _financeService.DeleteTransactionItemAsync(itemId, userId);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
