using System.Text.Json;
using DotNetEnv;
using Moq;
using MyPlanner.Service;
using MyPlanner.Service.Models;
using MyPlanner.Service.Helpers;

namespace MyPlanner.UnitTests.Helpers;

[TestFixture]
public class ReceiptParserTests
{
    // Read from .env file — falls back to defaults if not set
    private static readonly Lazy<string> _apiKey = new(() =>
        Env.GetString("GEMINI_API_KEY") ?? "test-gemini-api-key");
    private static readonly Lazy<string> _model = new(() =>
        Env.GetString("GEMINI_MODEL") ?? "gemini-2.0-flash-exp-image-generation");

    private static string GeminiApiKey => _apiKey.Value;
    private static string GeminiModel => _model.Value;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        // Load .env file from the test output directory
        var envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
        }
    }

    [Test]
    public async Task ProcessReceiptAsync_FromFile_ShouldProcessAndParseReceipt()
    {
        // ──────────────────────────────────────────────────────────────
        // ARRANGE — read a real image file and mock the LLM response
        // ──────────────────────────────────────────────────────────────

        var imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "test-bill.png");
        Assert.That(File.Exists(imagePath), Is.True, $"Test image not found at {imagePath}");

        // Clean JSON response — LlmService unwraps provider-specific wrappers before returning
        var expectedResponse = @"{
            ""items"": [
                {
                    ""name"": ""Item 1"",
                    ""unitPrice"": 5.99,
                    ""quantity"": 1,
                    ""totalPrice"": 5.99
                }
            ],
            ""merchantName"": ""Test Store"",
            ""date"": ""2024-06-15"",
            ""totalAmount"": 5.99
        }";

        var llmMock = new Mock<ILlmService>();
        llmMock.Setup(s => s.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ReturnsAsync(expectedResponse);

        // ──────────────────────────────────────────────────────────────
        // ACT — read the real image file and process it via ReceiptParser
        // ──────────────────────────────────────────────────────────────

        using var imageStream = File.OpenRead(imagePath);
        var result = await ReceiptParser.ProcessReceiptAsync(llmMock.Object, imageStream, "image/png");

        // ──────────────────────────────────────────────────────────────
        // ASSERT — verify the parsed ReceiptDto
        // ──────────────────────────────────────────────────────────────

        Assert.That(result.Items, Is.Not.Null);
        Assert.That(result.Items.Count, Is.EqualTo(1));
        Assert.That(result.TotalAmount, Is.EqualTo(5.99m));
        Assert.That(result.MerchantName, Is.EqualTo("Test Store"));
    }

    [Test]
    public async Task ProcessReceiptAsync_ShouldReturnParsedReceiptDto()
    {
        // ──────────────────────────────────────────────────────────────
        // ARRANGE — mock the LLM response (clean JSON, no provider wrapper)
        // ──────────────────────────────────────────────────────────────

        var expectedResponse = @"{
            ""items"": [
                {
                    ""name"": ""Milk"",
                    ""unitPrice"": 3.49,
                    ""quantity"": 2,
                    ""totalPrice"": 6.98
                },
                {
                    ""name"": ""Bread"",
                    ""unitPrice"": 2.99,
                    ""quantity"": 1,
                    ""totalPrice"": 2.99
                }
            ],
            ""merchantName"": ""Corner Market"",
            ""date"": ""2024-06-15"",
            ""totalAmount"": 9.97
        }";

        var llmMock = new Mock<ILlmService>();
        llmMock.Setup(s => s.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ReturnsAsync(expectedResponse);

        // ──────────────────────────────────────────────────────────────
        // ACT — send the request with a minimal image stream
        // ──────────────────────────────────────────────────────────────

        using var imageStream = CreateImageStream();
        var result = await ReceiptParser.ProcessReceiptAsync(llmMock.Object, imageStream, "image/jpeg");

        // ──────────────────────────────────────────────────────────────
        // ASSERT — verify the parsed ReceiptDto matches expected data
        // ──────────────────────────────────────────────────────────────

        Assert.That(result.Items, Is.Not.Null);
        Assert.That(result.Items.Count, Is.EqualTo(2));

        var milkItem = result.Items.First(i => i.Name == "Milk");
        Assert.That(milkItem.UnitPrice, Is.EqualTo(3.49m));
        Assert.That(milkItem.Quantity, Is.EqualTo(2));
        Assert.That(milkItem.TotalPrice, Is.EqualTo(6.98m));

        var breadItem = result.Items.First(i => i.Name == "Bread");
        Assert.That(breadItem.UnitPrice, Is.EqualTo(2.99m));
        Assert.That(breadItem.Quantity, Is.EqualTo(1));
        Assert.That(breadItem.TotalPrice, Is.EqualTo(2.99m));

        Assert.That(result.TotalAmount, Is.EqualTo(9.97m));
        Assert.That(result.MerchantName, Is.EqualTo("Corner Market"));
        Assert.That(result.Timestamp, Is.EqualTo(new DateTime(2024, 6, 15)));
        Assert.That(result.Timestamp.Value.Hour, Is.EqualTo(0));
        Assert.That(result.Timestamp.Value.Minute, Is.EqualTo(0));
    }

    private static MemoryStream CreateImageStream()
    {
        // Minimal valid PNG (1x1 transparent pixel) — avoids needing a real image file
        var pngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        return new MemoryStream(pngBytes);
    }

    [Test]
    public async Task ProcessReceiptAsync_ShouldThrowOnApiError()
    {
        // ──────────────────────────────────────────────────────────────
        // ARRANGE — mock an LLM error response
        // ──────────────────────────────────────────────────────────────

        var llmMock = new Mock<ILlmService>();
        llmMock.Setup(s => s.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ThrowsAsync(new HttpRequestException("LLM API error: 429 - Quota exceeded"));

        using var imageStream = CreateImageStream();

        // ──────────────────────────────────────────────────────────────
        // ACT & ASSERT — should throw HttpRequestException
        // ──────────────────────────────────────────────────────────────

        Assert.ThrowsAsync<HttpRequestException>(async () => await ReceiptParser.ProcessReceiptAsync(llmMock.Object, imageStream, "image/jpeg"));
    }


    [Test]
    public async Task ProcessReceiptAsync_FromRealBillJson_ShouldParseAllFieldsCorrectly()
    {
        // ──────────────────────────────────────────────────────────────
        // ARRANGE — read the real test-bill.json and use it as LLM response
        // ──────────────────────────────────────────────────────────────

        var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "test-bill.json");
        Assert.That(File.Exists(jsonPath), Is.True, $"Test JSON not found at {jsonPath}");
        var expectedResponse = File.ReadAllText(jsonPath);

        var llmMock = new Mock<ILlmService>();
        llmMock.Setup(s => s.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ReturnsAsync(expectedResponse);

        // ──────────────────────────────────────────────────────────────
        // ACT — process the receipt with a minimal image stream
        // ──────────────────────────────────────────────────────────────

        using var imageStream = CreateImageStream();
        var result = await ReceiptParser.ProcessReceiptAsync(llmMock.Object, imageStream, "image/png");

        // ──────────────────────────────────────────────────────────────
        // ASSERT — verify all parsed fields match the real bill data
        // ──────────────────────────────────────────────────────────────

        var expected = new ReceiptDto
        {
            Items = new List<ReceiptItemDto>
            {
                new() { Name = "Соус Торчин Тартар", FullName = "Соус Торчин Тартар д/п 200г", UnitPrice = 29.99m, Quantity = 1, TotalPrice = 29.99m },
                new() { Name = "Філе стегна куряче", FullName = "Філе стегна куряче охл. Вл/Вир ваг", UnitPrice = 219.0m, Quantity = 0.738, TotalPrice = 161.62m },
                new() { Name = "Крило куряче плечова частина", FullName = "Крило куряче плечова частина Вл/Вир ваг", UnitPrice = 109.0m, Quantity = 1.356, TotalPrice = 147.8m },
                new() { Name = "Круасан Французький масляний", FullName = "Круасан Французький масляний, 55г", UnitPrice = 36.99m, Quantity = 3, TotalPrice = 110.97m },
                new() { Name = "Лаваш вірменський тонкий", FullName = "Лаваш вірменський тонкий Кулиничі 200г", UnitPrice = 28.99m, Quantity = 1, TotalPrice = 28.99m },
                new() { Name = "Банан", FullName = "Банан ваг", UnitPrice = 71.89m, Quantity = 0.578, TotalPrice = 41.55m },
                new() { Name = "Пакет середній Novus", FullName = "Пакет середній 34*55 Novus 7кг", UnitPrice = 5.99m, Quantity = 1, TotalPrice = 5.99m },
                new() { Name = "Мед натуральний різнотрав'я Novus", FullName = "Мед натуральний різнотрав'я Novus 400г", UnitPrice = 99.99m, Quantity = 1, TotalPrice = 99.99m },
                new() { Name = "Хліб картопляний", FullName = "Хліб картопляний под ваг", UnitPrice = 149.0m, Quantity = 0.184, TotalPrice = 27.42m },
                new() { Name = "Йогурт Турецький", FullName = "Йогурт Турецький 8% Яготин 260г ст.", UnitPrice = 29.99m, Quantity = 1, TotalPrice = 29.99m },
                new() { Name = "Пакет майка зелений біо", FullName = "Пакет майка зелений біо", UnitPrice = 1.2m, Quantity = 2, TotalPrice = 2.4m }
            },
            TotalAmount = 686.71m,
            MerchantName = "Novus",
            Timestamp = new DateTime(2026, 4, 28, 18, 33, 0),
            Currency = "UAH",
            PaymentMethod = null
        };

        Assert.That(result.Items.Count, Is.EqualTo(expected.Items.Count),
            $"Item count mismatch. Expected {expected.Items.Count}, got {result.Items.Count}");

        for (int i = 0; i < result.Items.Count; i++)
        {
            var actual = result.Items[i];
            var exp = expected.Items[i];
            Assert.That(actual.Name, Is.EqualTo(exp.Name), $"Item[{i}] name mismatch: expected '{exp.Name}', got '{actual.Name}'");
            Assert.That(actual.FullName, Is.EqualTo(exp.FullName), $"Item[{i}] fullName mismatch: expected '{exp.FullName}', got '{actual.FullName}'");
            Assert.That(actual.UnitPrice, Is.EqualTo(exp.UnitPrice).Within(0.01m), $"Item[{i}] unitPrice mismatch: expected {exp.UnitPrice}, got {actual.UnitPrice}");
            Assert.That(actual.Quantity, Is.EqualTo(exp.Quantity), $"Item[{i}] quantity mismatch: expected {exp.Quantity}, got {actual.Quantity}");
            Assert.That(actual.TotalPrice, Is.EqualTo(exp.TotalPrice).Within(0.01m), $"Item[{i}] totalPrice mismatch: expected {exp.TotalPrice}, got {actual.TotalPrice}");
        }

        Assert.That(result.TotalAmount!.Value, Is.EqualTo(expected.TotalAmount.Value).Within(0.01m),
            $"Total amount mismatch: expected {expected.TotalAmount}, got {result.TotalAmount}");
        Assert.That(result.MerchantName, Is.EqualTo(expected.MerchantName),
            $"Merchant name mismatch: expected '{expected.MerchantName}', got '{result.MerchantName}'");

        if (expected.Timestamp.HasValue)
        {
            Assert.That(result.Timestamp?.Date, Is.EqualTo(expected.Timestamp.Value.Date),
                $"Timestamp date mismatch: expected {expected.Timestamp}, got {result.Timestamp}");
        }
    }

    [Test]
    public async Task ProcessReceiptAsync_ShouldReturnEmptyDto_WhenMalformedResponse()
    {
        // ──────────────────────────────────────────────────────────────
        // ARRANGE — mock a malformed JSON response from LLM API
        // ──────────────────────────────────────────────────────────────

        var llmMock = new Mock<ILlmService>();
        llmMock.Setup(s => s.SendImageRequestAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LlmSchema>()))
            .ReturnsAsync("{ invalid json content }");

        using var imageStream = CreateImageStream();

        // ──────────────────────────────────────────────────────────────
        // ACT — should not throw; returns empty ReceiptDto gracefully
        // ──────────────────────────────────────────────────────────────

        var result = await ReceiptParser.ProcessReceiptAsync(llmMock.Object, imageStream, "image/jpeg");

        // ──────────────────────────────────────────────────────────────
        // ASSERT — malformed response should return empty ReceiptDto (no crash)
        // ──────────────────────────────────────────────────────────────

        Assert.That(result.Items, Is.Not.Null);
        Assert.That(result.Items.Count, Is.EqualTo(0));
    }

    [Test]
    public void ParseReceiptResponse_ShouldHandleNullJson()
    {
        var result = ReceiptParser.ParseReceiptResponse("null");
        Assert.That(result.Items, Is.Empty);
        Assert.That(result.TotalAmount, Is.Null);
        Assert.That(result.MerchantName, Is.Null);
    }

    [Test]
    public void ParseReceiptResponse_ShouldCalculateTotalPrice_WhenMissing()
    {
        var json = @"{
            ""items"": [{
                ""name"": ""Item"",
                ""unitPrice"": 10.5,
                ""quantity"": 3
            }],
            ""totalAmount"": 31.5
        }";

        var result = ReceiptParser.ParseReceiptResponse(json);
        Assert.That(result.Items[0].TotalPrice, Is.EqualTo(31.5m)); // unitPrice * quantity
    }

    [Test]
    public void ParseReceiptResponse_ShouldUseProvidedTotalPrice_WhenPresent()
    {
        var json = @"{
            ""items"": [{
                ""name"": ""Item"",
                ""unitPrice"": 10.5,
                ""quantity"": 3,
                ""totalPrice"": 29.99
            }],
            ""totalAmount"": 29.99
        }";

        var result = ReceiptParser.ParseReceiptResponse(json);
        Assert.That(result.Items[0].TotalPrice, Is.EqualTo(29.99m)); // uses provided totalPrice
    }

    [Test]
    public void ParseReceiptResponse_ShouldParseTimestampFromDateAndTime()
    {
        var json = @"{
            ""date"": ""2024-12-25"",
            ""time"": ""14:30"",
            ""totalAmount"": 10.0
        }";

        var result = ReceiptParser.ParseReceiptResponse(json);
        Assert.That(result.Timestamp, Is.EqualTo(new DateTime(2024, 12, 25, 14, 30, 0)));
    }

    [Test]
    public void ParseReceiptResponse_ShouldParseTimestampFromIsoString()
    {
        var json = @"{
            ""timestamp"": ""2024-07-04T09:15:00"",
            ""totalAmount"": 10.0
        }";

        var result = ReceiptParser.ParseReceiptResponse(json);
        Assert.That(result.Timestamp, Is.EqualTo(new DateTime(2024, 7, 4, 9, 15, 0)));
    }
}
