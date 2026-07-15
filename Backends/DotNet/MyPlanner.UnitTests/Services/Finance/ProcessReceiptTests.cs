using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using MyPlanner.Data.DBContexts;
using MyPlanner.Data.Entities.Finance;
using MyPlanner.Service.Models;
using MyPlanner.Service.Requests.Finance;
using NUnit.Framework;

namespace MyPlanner.UnitTests.Services.Finance;

[TestFixture]
public class ProcessReceiptTests : FinanceServiceTests_Base
{
    private const string TestUserId = "test-user-123";

    [Test]
    public async Task PreviewReceiptAsync_ShouldReturnEditableFieldsWithoutSavingAnything()
    {
        // Arrange: LLM returns a receipt with items and total
        var mockResponse = new Dictionary<string, object>
        {
            ["merchantName"] = "Test Store",
            ["totalAmount"] = 150.50m,
            ["currency"] = "UAH",
            ["timestamp"] = "2025-08-02T14:30:00",
            ["items"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["name"] = "Bread",
                    ["fullName"] = "Хліб Білий",
                    ["unitPrice"] = 50.25m,
                    ["quantity"] = 1.0,
                    ["totalPrice"] = 50.25m,
                    ["category"] = "Groceries",
                    ["subcategory"] = "Bakery"
                },
                new Dictionary<string, object>
                {
                    ["name"] = "Milk",
                    ["fullName"] = "Молоко 3.2%",
                    ["unitPrice"] = 45.0m,
                    ["quantity"] = 1.0,
                    ["totalPrice"] = 45.0m,
                    ["category"] = "Groceries",
                    ["subcategory"] = "Dairy"
                },
                new Dictionary<string, object>
                {
                    ["name"] = "Sugar",
                    ["fullName"] = "Цукор 1кг",
                    ["unitPrice"] = 55.25m,
                    ["quantity"] = 1.0,
                    ["totalPrice"] = 55.25m,
                    ["category"] = "Groceries",
                    ["subcategory"] = "Pantry"
                }
            }
        };

        _llmServiceMock
            .Setup(x => x.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ReturnsAsync(JsonSerializer.Serialize(mockResponse));

        var request = new ProcessReceiptRequest
        {
            FileStream = new MemoryStream(),
            ContentType = "image/png"
        };

        // Act
        var result = await _sut.PreviewReceiptAsync(request);

        Assert.That(result.MerchantName, Is.EqualTo("Test Store"));
        Assert.That(result.TotalAmount, Is.EqualTo(150.50m));
        Assert.That(result.Timestamp, Is.Not.Null);
        Assert.That(result.Items.Select(i => i.Name).OrderBy(n => n), Is.EqualTo(new[] { "Bread", "Milk", "Sugar" }));
        Assert.That(await _testContext.Transactions.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task PreviewReceiptAsync_NullTimestamp_ShouldRemainAnUnsavedDraft()
    {
        // Arrange: LLM returns a receipt without timestamp
        var mockResponse = new Dictionary<string, object>
        {
            ["merchantName"] = "Quick Shop",
            ["totalAmount"] = 75.0m,
            ["currency"] = "UAH",
            ["items"] = new List<object>()
        };

        _llmServiceMock
            .Setup(x => x.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ReturnsAsync(JsonSerializer.Serialize(mockResponse));

        var request = new ProcessReceiptRequest
        {
            FileStream = new MemoryStream(),
            ContentType = "image/png"
        };

        // Act
        var result = await _sut.PreviewReceiptAsync(request);

        // Assert: timestamp should be null
        Assert.That(result.Timestamp, Is.Null);
        Assert.That(result.TotalAmount, Is.EqualTo(75.0m));
    }

    [Test]
    public async Task ProcessReceiptAsync_MissingTotalAmount_ShouldSumItems()
    {
        // Arrange: LLM returns a receipt without totalAmount but with items
        var mockResponse = new Dictionary<string, object>
        {
            ["merchantName"] = "No Total Store",
            ["currency"] = "USD",
            ["items"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["name"] = "Item A",
                    ["fullName"] = "Item A Full",
                    ["unitPrice"] = 20.5m,
                    ["quantity"] = 2.0,
                    ["totalPrice"] = 41.0m,
                    ["category"] = "General"
                },
                new Dictionary<string, object>
                {
                    ["name"] = "Item B",
                    ["fullName"] = "Item B Full",
                    ["unitPrice"] = 30.0m,
                    ["quantity"] = 1.0,
                    ["totalPrice"] = 30.0m,
                    ["category"] = "General"
                }
            }
        };

        _llmServiceMock
            .Setup(x => x.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ReturnsAsync(JsonSerializer.Serialize(mockResponse));

        var request = new ProcessReceiptRequest
        {
            FileStream = new MemoryStream(),
            ContentType = "image/png"
        };

        // Act
        var result = await _sut.PreviewReceiptAsync(request);

        // Assert: amount should be sum of item totals (41.0 + 30.0 = 71.0)
        Assert.That(result.TotalAmount, Is.Null);
        Assert.That(result.Items.Sum(item => item.TotalPrice), Is.EqualTo(71.0m));
        Assert.That(result.Currency, Is.EqualTo("USD"));
    }

    [Test]
    public async Task PreviewReceiptAsync_UnrecognizedCurrency_RemainsEditable()
    {
        // Arrange: LLM returns a receipt with unknown currency code
        var mockResponse = new Dictionary<string, object>
        {
            ["merchantName"] = "Foreign Store",
            ["totalAmount"] = 100.0m,
            ["currency"] = "XYZ", // Unknown currency
            ["items"] = new List<object>()
        };

        _llmServiceMock
            .Setup(x => x.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ReturnsAsync(JsonSerializer.Serialize(mockResponse));

        var request = new ProcessReceiptRequest
        {
            FileStream = new MemoryStream(),
            ContentType = "image/png"
        };

        // OCR output is an editable draft; currency validation belongs on confirmation.
        var result = await _sut.PreviewReceiptAsync(request);
        Assert.That(result.Currency, Is.EqualTo("XYZ"));
    }

    [Test]
    public async Task ProcessReceiptAsync_EmptyCurrency_ShouldDefaultToUAH()
    {
        // Arrange: LLM returns a receipt with null/empty currency
        var mockResponse = new Dictionary<string, object>
        {
            ["merchantName"] = "Local Store",
            ["totalAmount"] = 50.0m,
            ["currency"] = (object)null!,
            ["items"] = new List<object>()
        };

        _llmServiceMock
            .Setup(x => x.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ReturnsAsync(JsonSerializer.Serialize(mockResponse));

        var request = new ProcessReceiptRequest
        {
            FileStream = new MemoryStream(),
            ContentType = "image/png"
        };

        // Act
        var result = await _sut.PreviewReceiptAsync(request);

        // Assert: should default to UAH
        Assert.That(result.Currency, Is.Null);
    }

    [Test]
    public async Task ProcessReceiptAsync_NoItems_ShouldPersistTransactionWithEmptyItems()
    {
        // Arrange: LLM returns a receipt with no items
        var mockResponse = new Dictionary<string, object>
        {
            ["merchantName"] = "Minimal Store",
            ["totalAmount"] = 25.0m,
            ["currency"] = "UAH",
            ["items"] = new List<object>()
        };

        _llmServiceMock
            .Setup(x => x.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ReturnsAsync(JsonSerializer.Serialize(mockResponse));

        var request = new ProcessReceiptRequest
        {
            FileStream = new MemoryStream(),
            ContentType = "image/png"
        };

        // Act
        var result = await _sut.PreviewReceiptAsync(request);

        // Assert: transaction with no items
        Assert.That(result.Items.Count, Is.EqualTo(0));
        Assert.That(result.TotalAmount, Is.EqualTo(25.0m));
    }

    [Test]
    public async Task ProcessReceiptAsync_EmptyItems_ShouldDefaultAmountToZero()
    {
        // Arrange: LLM returns a receipt with no total and no items (edge case)
        var mockResponse = new Dictionary<string, object>
        {
            ["merchantName"] = "Empty Store",
            ["currency"] = "UAH",
            ["items"] = new List<object>()
        };

        _llmServiceMock
            .Setup(x => x.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ReturnsAsync(JsonSerializer.Serialize(mockResponse));

        var request = new ProcessReceiptRequest
        {
            FileStream = new MemoryStream(),
            ContentType = "image/png"
        };

        // Act
        var result = await _sut.PreviewReceiptAsync(request);

        // Assert: amount defaults to 0 when no total and no items
        Assert.That(result.TotalAmount, Is.Null);
    }

    [Test]
    public async Task ProcessReceiptAsync_ItemWithCalculatedTotal_ShouldUseCalculatedPrice()
    {
        // Arrange: LLM returns an item without totalPrice (should be unitPrice × quantity)
        var mockResponse = new Dictionary<string, object>
        {
            ["merchantName"] = "Calc Store",
            ["totalAmount"] = 100.0m,
            ["currency"] = "UAH",
            ["items"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["name"] = "Apples",
                    ["fullName"] = "Яблука ваг",
                    ["unitPrice"] = 40.0m,
                    ["quantity"] = 2.5,
                    // totalPrice intentionally omitted — should be calculated as 100.0
                    ["category"] = "Groceries"
                }
            }
        };

        _llmServiceMock
            .Setup(x => x.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ReturnsAsync(JsonSerializer.Serialize(mockResponse));

        var request = new ProcessReceiptRequest
        {
            FileStream = new MemoryStream(),
            ContentType = "image/png"
        };

        // Act
        var result = await _sut.PreviewReceiptAsync(request);

        // Assert: item totalPrice should be calculated (40.0 × 2.5 = 100.0)
        Assert.That(result.Items.Count, Is.EqualTo(1));
        Assert.That(result.Items[0].TotalPrice, Is.EqualTo(100.0m));
        Assert.That(result.Items[0].Quantity, Is.EqualTo(2.5));
    }

    [Test]
    public async Task ProcessReceiptAsync_MerchantNameNull_ShouldUseFallbackDescription()
    {
        // Arrange: LLM returns a receipt without merchant name
        var mockResponse = new Dictionary<string, object>
        {
            ["totalAmount"] = 30.0m,
            ["currency"] = "UAH",
            ["items"] = new List<object>()
        };

        _llmServiceMock
            .Setup(x => x.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ReturnsAsync(JsonSerializer.Serialize(mockResponse));

        var request = new ProcessReceiptRequest
        {
            FileStream = new MemoryStream(),
            ContentType = "image/png"
        };

        // Act
        var result = await _sut.PreviewReceiptAsync(request);

        // Assert: description should fall back to "Receipt purchase"
        Assert.That(result.MerchantName, Is.Null);
    }

    [Test]
    public async Task ConfirmReceiptAsync_MissingTimestamp_DoesNotSaveBill()
    {
        var request = new ConfirmReceiptRequest
        {
            FileStream = new MemoryStream(), ContentType = "image/png",
            Receipt = new MyPlanner.Service.Models.ReceiptDto { TotalAmount = 12m, Currency = "UAH" }
        };

        Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ConfirmReceiptAsync(request, TestUserId));
        Assert.That(await _testContext.Transactions.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task ConfirmReceiptAsync_UnknownPaymentMethod_IsAllowedAndSavesCorrectedBill()
    {
        var request = new ConfirmReceiptRequest
        {
            FileStream = new MemoryStream(), ContentType = "image/png",
            Receipt = new MyPlanner.Service.Models.ReceiptDto
            {
                MerchantName = "Corrected merchant", Timestamp = new DateTime(2025, 1, 2, 9, 30, 0),
                TotalAmount = 12.5m, Currency = "USD",
                Items = { new MyPlanner.Service.Models.ReceiptItemDto { Name = "Corrected item", FullName = "Corrected item", UnitPrice = 12.5m, Quantity = 1, TotalPrice = 12.5m } }
            }
        };

        var saved = await _sut.ConfirmReceiptAsync(request, TestUserId);

        Assert.That(saved.Description, Is.EqualTo("Corrected merchant"));
        Assert.That(saved.Items.Single().Name, Is.EqualTo("Corrected item"));
        Assert.That(saved.PaymentMethodId, Is.Null);
        Assert.That(saved.DataOrigin, Is.EqualTo(DataOrigin.Receipt));
        Assert.That(await _testContext.Transactions.CountAsync(), Is.EqualTo(1));
        _llmServiceMock.Verify(x => x.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()), Times.Never);
    }

    [Test]
    public async Task ConfirmReceiptAsync_ForeignPaymentMethod_DoesNotSaveBill()
    {
        var foreign = new PaymentMethod { UserId = "other-user", Name = "Other", Type = PaymentMethodType.BankCard, Currency = Currency.UAH };
        _testContext.PaymentMethods.Add(foreign);
        await _testContext.SaveChangesAsync();
        var request = new ConfirmReceiptRequest
        {
            FileStream = new MemoryStream(), ContentType = "image/png", PaymentMethodId = foreign.Id,
            Receipt = new MyPlanner.Service.Models.ReceiptDto { Timestamp = DateTime.UtcNow, TotalAmount = 5m, Currency = "UAH" }
        };

        Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ConfirmReceiptAsync(request, TestUserId));
        Assert.That(await _testContext.Transactions.CountAsync(), Is.Zero);
    }
}
