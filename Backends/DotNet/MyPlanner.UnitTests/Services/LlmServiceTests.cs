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
For each item, provide: name (string), price (number), quantity (integer).
Also extract: totalAmount (number, nullable), storeName (string, nullable), date (ISO 8601 string, nullable).
Return ONLY valid JSON with no additional text. Use this exact schema:
{
    "items": [{"name": "item name", "price": 9.99, "quantity": 2}],
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

        // If ExpectedJson is set (after first real call), compare directly.
        if (!string.IsNullOrEmpty(ExpectedJson))
        {
            Assert.That(rawJsonResponse, Is.EqualTo(ExpectedJson),
                "Unwrapped JSON response should match expected value from previous API call");
        }
        else
        {
            // First run — verify the LLM response structure is valid (clean JSON, no wrapper)
            var parsed = System.Text.Json.JsonDocument.Parse(rawJsonResponse);
            Assert.That(parsed.RootElement.TryGetProperty("items", out _), Is.True,
                "LLM response should contain 'items' property (unwrapped from provider format)");

            Console.WriteLine($"\n=== First run — unwrapped LLM response (paste into ExpectedJson below) ===\n{rawJsonResponse}\n========================================================================\n");
        }
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
    private const string ExpectedJson = "";

}
