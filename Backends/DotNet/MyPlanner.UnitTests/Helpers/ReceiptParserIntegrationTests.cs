using System.Net.Http.Headers;
using DotNetEnv;
using Microsoft.Extensions.Options;
using Moq;
using MyPlanner.Service;
using MyPlanner.Service.Models;
using MyPlanner.Service.Helpers;

namespace MyPlanner.UnitTests.Helpers;

/// <summary>
/// Integration test for ReceiptParser — sends a real receipt image to Gemini and verifies the parsed result.
/// LLM settings are loaded from .env file (falls back to defaults if not set).
/// </summary>
[TestFixture]
public class ReceiptParserIntegrationTests 
{
    // ──────────────────────────────────────────────────────────────
    // Settings — read from system env vars first, then .env files,
    // with sensible fallbacks for CI/local dev
    // ──────────────────────────────────────────────────────────────

    private static readonly Lazy<string> _apiKey = new(() =>
        Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? Env.GetString("GEMINI_API_KEY")
            ?? throw new InvalidOperationException(
                "GEMINI_API_KEY not found. Set it in /etc/environment or .env file."));
    private static readonly Lazy<string> _model = new(() =>
        Environment.GetEnvironmentVariable("GEMINI_MODEL")
            ?? Env.GetString("GEMINI_MODEL")
            ?? "gemini-2.5-flash");

    private static string GeminiApiKey => _apiKey.Value;
    private static string GeminiModel => _model.Value;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        // Load .env file from the test output directory or search parent directories.
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var envPath = Path.Combine(baseDir, ".env");
        if (!System.IO.File.Exists(envPath))
        {
            var dir = new DirectoryInfo(baseDir);
            for (int i = 0; i < 4 && dir != null; dir = dir.Parent, i++)
            {
                var candidate = Path.Combine(dir.FullName, ".env");
                if (System.IO.File.Exists(candidate))
                {
                    envPath = candidate;
                    break;
                }
            }
        }

        if (System.IO.File.Exists(envPath))
        {
            Env.Load(envPath);
        }
    }

    [Explicit("Integration test — requires a real image file and valid Gemini API credentials")] 
    [Test]
    public async Task ProcessReceiptAsync_WithRealImage_ShouldReturnParsedReceipt()
    {
        // ────────────────────────────────────────────────────────────
        // ARRANGE — load the real image file, build LlmService for ReceiptParser
        // ────────────────────────────────────────────────────────────

        var testImagePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Resources",
            "test-bill.png");

        Assert.That(System.IO.File.Exists(testImagePath), Is.True, $"Test image file not found at {testImagePath}");

        // Use a real HttpClient — no mock handler
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5);

        var llmOptionsMock = new Mock<IOptions<Service.Models.LlmSettings>>();
        llmOptionsMock.Setup(o => o.Value).Returns(new Service.Models.LlmSettings 
        { 
            Provider = "Gemini", 
            ApiKey = GeminiApiKey, 
            Model = GeminiModel 
        });

        var llmService = new LlmService(httpClient, llmOptionsMock.Object);

        // ────────────────────────────────────────────────────────────
        // ACT — send the image to Gemini via ReceiptParser directly
        // ────────────────────────────────────────────────────────────

        using var imageStream = new FileStream(testImagePath, FileMode.Open, FileAccess.Read);
        var result = await ReceiptParser.ProcessReceiptAsync(llmService, imageStream, "image/png");


        // ────────────────────────────────────────────────────────────
        // ASSERT — compare the parsed ReceiptDto against expected value
        // ────────────────────────────────────────────────────────────
        
        var expected = new ReceiptDto
        {
            Items = new List<ReceiptItemDto>
            {
                new() { Name = "Соус Торчин Тартар", FullName = "Соус Торчин Тартар д/п 200г", UnitPrice = 29.99m, Quantity = 1, TotalPrice = 29.99m },
                new() { Name = "Філе стегна куряче охл.", FullName = "Філе стегна куряче охл. Вл/Вир ваг", UnitPrice = 219.0m, Quantity = 0.738, TotalPrice = 161.62m },
                new() { Name = "Крило куряче плечова частина", FullName = "Крило куряче плечова частина Вл/Вир ваг", UnitPrice = 109.0m, Quantity = 1.356, TotalPrice = 147.8m },
                new() { Name = "Круасан Французький масляний", FullName = "Круасан Французький масляний, 55г", UnitPrice = 36.99m, Quantity = 3, TotalPrice = 110.97m },
                new() { Name = "Лаваш вірменський тонкий Кулиничі", FullName = "Лаваш вірменський тонкий Кулиничі 200г", UnitPrice = 28.99m, Quantity = 1, TotalPrice = 28.99m },
                new() { Name = "Банан", FullName = "Банан ваг", UnitPrice = 71.89m, Quantity = 0.578, TotalPrice = 41.55m },
                new() { Name = "Пакет середній Novus", FullName = "Пакет середній 34*55 Novus 7кг", UnitPrice = 5.99m, Quantity = 1, TotalPrice = 5.99m },
                new() { Name = "Мед натуральний різнотрав'я Novus", FullName = "Мед натуральний різнотрав'я Novus 400г", UnitPrice = 99.99m, Quantity = 1, TotalPrice = 99.99m },
                new() { Name = "Хліб картопляний", FullName = "Хліб картопляний под ваг", UnitPrice = 149.0m, Quantity = 0.184, TotalPrice = 27.42m },
                new() { Name = "Йогурт Турецький Яготин", FullName = "Йогурт Турецький 8% Яготин 260г ст.", UnitPrice = 29.99m, Quantity = 1, TotalPrice = 29.99m },
                new() { Name = "Пакет майка зелений біо", FullName = "Пакет майка зелений біо", UnitPrice = 1.2m, Quantity = 2, TotalPrice = 2.4m }
            },
            TotalAmount = 686.71m,
            MerchantName = "Novus",
            Timestamp = new DateTime(2026, 4, 28, 18, 33, 0),
            Currency = "UAH",
            PaymentMethod = null
        };

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Is.Not.Null.And.Not.Empty,
                "ReceiptParser should return a non-empty list of items");
            Assert.That(result.TotalAmount, Is.Not.Null,
                "ReceiptParser should extract the total amount");

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
        });
    }
}
