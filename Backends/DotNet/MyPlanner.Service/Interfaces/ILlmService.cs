using MyPlanner.Service.Models;

namespace MyPlanner.Service;

public interface ILlmService
{
    /// <summary>
    /// Sends a text-only request to the configured LLM provider.
    /// Returns the raw JSON response string (unwrapped from provider-specific wrappers).
    /// </summary>
    Task<string> SendTextRequestAsync(string prompt, LlmSchema? responseSchema = null);

    /// <summary>
    /// Sends an image + text request to the configured LLM provider.
    /// Returns the raw JSON response string (unwrapped from provider-specific wrappers).
    /// </summary>
    Task<string> SendImageRequestAsync(Stream imageStream, string contentType, string prompt, LlmSchema? responseSchema = null);
}
