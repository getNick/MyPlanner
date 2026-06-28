using System.Net.Http.Headers;
using System.Text.Json;
using DotNetEnv;
using Microsoft.Extensions.Options;
using Moq;
using MyPlanner.Service;

namespace MyPlanner.UnitTests.Services;

/// <summary>
/// Simple integration test for the Gemini API — sends a text prompt and verifies the response.
/// LLM settings are loaded from .env file (falls back to defaults if not set).
/// </summary>
[TestFixture]
public class LlmServiceTests 
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
    private static readonly Lazy<string> _localBaseUrl = new(() =>
        Environment.GetEnvironmentVariable("LOCAL_LLM_BASE_URL")
            ?? Env.GetString("LOCAL_LLM_BASE_URL")
            ?? "http://localhost:1234/v1");
    private static readonly Lazy<string> _localModel = new(() =>
        Environment.GetEnvironmentVariable("LOCAL_LLM_MODEL")
            ?? Env.GetString("LOCAL_LLM_MODEL")
            ?? "qwopus3.6-35b-a3b-v1");

    private static string GeminiApiKey => _apiKey.Value;
    private static string GeminiModel => _model.Value;
    private static string LocalLlmBaseUrl => _localBaseUrl.Value;
    private static string LocalLlmModel => _localModel.Value;

    // private static string GeminiModel => "gemma-4-31b-it";

    private const string TestImagePath = "Resources/test-bill.png";

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        // Load .env file from the test output directory or search parent directories.
        // System-level env vars (e.g. /etc/environment) are checked first
        // via Environment.GetEnvironmentVariable() in the lazy initializers.
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
    
    [Explicit("Integration test — requires a valid LLM API credentials")] 
    [TestCaseSource(nameof(LlmServiceTestCases))]
    public async Task RequestToLmStudioAsync_ReturnsNonEmptyResponse(Service.Models.LlmSettings llmSettings)
    {
        // ────────────────────────────────────────────────────────────
        // ARRANGE — build LlmService with provided settings
        // ────────────────────────────────────────────────────────────

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5);

        var optionsMock = new Mock<IOptions<Service.Models.LlmSettings>>();
        optionsMock.Setup(o => o.Value).Returns(llmSettings);

        var llmService = new LlmService(httpClient, optionsMock.Object);

        // ────────────────────────────────────────────────────────────
        // ACT — send a text request via LlmService
        // ────────────────────────────────────────────────────────────

        var prompt = "tell me a fun fact about Kyiv";
        var response = await llmService.SendTextRequestAsync(prompt);
        Console.WriteLine($"\n=== {llmSettings.Provider} ===\n{response}\n========================================================================\n");

        Assert.That(response, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Length, Is.GreaterThan(10));
    }

    [Explicit("Integration test — requires a real image file and valid LLM API credentials")] 
    [TestCaseSource(nameof(LlmServiceTestCases))]
    public async Task ProcessReceiptAsync_WithRealImage_ShouldReturnParsedReceipt(Service.Models.LlmSettings llmSettings)
    {
        // ────────────────────────────────────────────────────────────
        // ARRANGE — load the real image file and build LlmService
        // ────────────────────────────────────────────────────────────

        var testImagePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Resources",
            "test-bill.png");

        Assert.That(System.IO.File.Exists(testImagePath), Is.True, $"Test image file not found at {testImagePath}");

        // Use a real HttpClient — no mock handler
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5);

        var optionsMock = new Mock<IOptions<Service.Models.LlmSettings>>();
        optionsMock.Setup(o => o.Value).Returns(llmSettings);

        var llmService = new LlmService(httpClient, optionsMock.Object);

        // ────────────────────────────────────────────────────────────
        // ACT — send the image to provider via LlmService
        // ────────────────────────────────────────────────────────────

        using var imageStream = new FileStream(testImagePath, FileMode.Open, FileAccess.Read);
        var prompt = """
You are a receipt analysis assistant. Extract all items from the provided receipt image and return them in JSON format.
For each item, provide: name (string), unitPrice (number — price per single unit), quantity (integer), totalPrice (number — calculated as unitPrice × quantity).
Also extract: totalAmount (number, nullable), storeName (string, nullable), date (ISO 8601 string, nullable).
Return ONLY valid JSON with no additional text. Use this exact schema:
{
    "items": [{"name": "item name", "unitPrice": 9.99, "quantity": 2, "totalPrice": 19.98}],
    "totalAmount": 19.98,
    "storeName": "Store Name",
    "date": "2024-01-15T10:30:00Z"
}
If any field is not available, omit it from the JSON response.
""";
        var rawJsonResponse = await llmService.SendImageRequestAsync(imageStream, "image/png", prompt);

        Console.WriteLine($"\n=== RAW {llmSettings.Provider} RESPONSE ===\n{rawJsonResponse}\n===========================\n");

        // ────────────────────────────────────────────────────────────
        // ASSERT — compare the unwrapped JSON response against expected value
        // ────────────────────────────────────────────────────────────

        Assert.That(rawJsonResponse, Is.Not.Null.And.Not.Empty,
            "LLM API should return a non-empty JSON response");

        var actualDoc = JsonDocument.Parse(rawJsonResponse);
        var expectedDoc = JsonDocument.Parse(ExpectedJson);
        Assert.That(JsonCompare(actualDoc.RootElement, expectedDoc.RootElement), Is.True,
            $"JSON mismatch.\nExpected:\n{expectedDoc.RootElement.GetRawText()}\nActual:\n{actualDoc.RootElement.GetRawText()}");
    }

    private static object[] LlmServiceTestCases() => new object[] { 
        new Service.Models.LlmSettings { Provider = "LmStudio", BaseUrl = LocalLlmBaseUrl, Model = LocalLlmModel },
        new Service.Models.LlmSettings { Provider = "Gemini", ApiKey = GeminiApiKey, Model = GeminiModel },
    };

    // ──────────────────────────────────────────────────────────────
    // Expected JSON — paste the real Gemini response here after the
    // first test run.  The assertion above will compare against this.
    // Leave empty to skip comparison (first-run mode).
    // ──────────────────────────────────────────────────────────────
    private const string ExpectedJson = """
{
    "items": [
        {
            "name": "Соус Торчин Тартар д/п 200г",
            "unitPrice": 29.99,
            "quantity": 1,
            "totalPrice": 29.99
        },
        {
            "name": "Філе стегна куряче охл. Вл/Вир ваг",
            "unitPrice": 161.62,
            "quantity": 1,
            "totalPrice": 161.62
        },
        {
            "name": "Крило куряче плечова частина Вл/Вир ваг",
            "unitPrice": 147.80,
            "quantity": 1,
            "totalPrice": 147.80
        },
        {
            "name": "Круасан Французький масляний, 55г",
            "unitPrice": 36.99,
            "quantity": 3,
            "totalPrice": 110.97
        },
        {
            "name": "Лаваш вірменський тонкий Кулиничі 200г",
            "unitPrice": 28.99,
            "quantity": 1,
            "totalPrice": 28.99
        },
        {
            "name": "Банан ваг",
            "unitPrice": 41.55,
            "quantity": 1,
            "totalPrice": 41.55
        },
        {
            "name": "Пакет середній 34*55 Novus 7кг",
            "unitPrice": 5.99,
            "quantity": 1,
            "totalPrice": 5.99
        },
        {
            "name": "Мед натуральний різнотрав'я Novus 400г",
            "unitPrice": 99.99,
            "quantity": 1,
            "totalPrice": 99.99
        },
        {
            "name": "Хліб картопляний под ваг",
            "unitPrice": 27.42,
            "quantity": 1,
            "totalPrice": 27.42
        },
        {
            "name": "Йогурт Турецький 8% Яготин 260г ст.",
            "unitPrice": 29.99,
            "quantity": 1,
            "totalPrice": 29.99
        },
        {
            "name": "Пакет майка зелений біо",
            "unitPrice": 1.20,
            "quantity": 2,
            "totalPrice": 2.40
        }
    ],
    "totalAmount": 686.71,
    "date": "2026-04-28T18:33:00Z"
}
""";

    // ──────────────────────────────────────────────────────────────
    // Recursively compare two JsonElements for structural equality,
    // ignoring whitespace / formatting differences.
    // ──────────────────────────────────────────────────────────────
    private static bool JsonCompare(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
            return false;

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                var propsA = a.EnumerateObject().ToList();
                var propsB = b.EnumerateObject().ToList();
                if (propsA.Count != propsB.Count)
                    return false;
                foreach (var propA in propsA)
                {
                    if (!b.TryGetProperty(propA.Name, out var propB))
                        return false;
                    if (!JsonCompare(propA.Value, propB))
                        return false;
                }
                return true;

            case JsonValueKind.Array:
                var arrA = a.EnumerateArray().ToList();
                var arrB = b.EnumerateArray().ToList();
                if (arrA.Count != arrB.Count)
                    return false;
                for (int i = 0; i < arrA.Count; i++)
                {
                    if (!JsonCompare(arrA[i], arrB[i]))
                        return false;
                }
                return true;

            case JsonValueKind.String:
                return a.GetString() == b.GetString();

            case JsonValueKind.Number:
                return a.GetRawText() == b.GetRawText();

            case JsonValueKind.True:
            case JsonValueKind.False:
                return true;

            case JsonValueKind.Null:
                return true;

            default:
                return false;
        }
    }

}
