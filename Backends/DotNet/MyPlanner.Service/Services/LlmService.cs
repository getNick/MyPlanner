using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using MyPlanner.Service.Models;
using OpenAI.Chat;
using System.Text.Json;

namespace MyPlanner.Service;

public class LlmService : ILlmService
{
    private readonly LlmSettings _settings;

    public LlmService(IOptions<LlmSettings> llmSettings)
    {
        _settings = llmSettings.Value ?? new LlmSettings();
    }

    public LlmService(HttpClient httpClient, IOptions<LlmSettings> llmSettings)
        : this(llmSettings)
    {
    }

    /// <summary>
    /// Sends a text-only request to the configured LLM provider.
    /// Returns the raw JSON response string (unwrapped from provider-specific wrappers).
    /// </summary>
    public async Task<string> SendTextRequestAsync(string prompt, LlmSchema? responseSchema = null)
    {
        if (string.Equals(_settings.Provider, "Gemini", StringComparison.OrdinalIgnoreCase))
            return await SendToGeminiTextAsync(prompt, responseSchema);

        return await SendToOpenAiTextAsync(prompt, responseSchema);
    }

    /// <summary>
    /// Sends an image + text request to the configured LLM provider.
    /// Returns the raw JSON response string (unwrapped from provider-specific wrappers).
    /// </summary>
    public async Task<string> SendImageRequestAsync(Stream imageStream, string contentType, string prompt, LlmSchema? responseSchema = null)
    {
        var base64 = await ReadBytesAsync(imageStream);

        if (string.Equals(_settings.Provider, "Gemini", StringComparison.OrdinalIgnoreCase))
            return await SendToGeminiImageAsync(base64, GetMimeType(contentType), prompt, responseSchema);

        return await SendToOpenAiImageAsync(base64, GetMimeType(contentType), prompt, responseSchema);
    }

    private async Task<string> ReadBytesAsync(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return Convert.ToBase64String(memoryStream.ToArray());
    }

    private string GetMimeType(string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return "image/jpeg";

        return contentType switch
        {
            "image/png" => "image/png",
            "image/jpeg" or "image/jpg" => "image/jpeg",
            "image/webp" => "image/webp",
            _ => "image/jpeg"
        };
    }

    private string GetGeminiApiKey()
    {
        // Prefer environment variable over configuration
        var envKey = global::System.Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrEmpty(envKey))
            return envKey;

        if (string.IsNullOrEmpty(_settings.ApiKey))
            throw new InvalidOperationException("Google API key is required for Gemini provider.");

        return _settings.ApiKey;
    }

    private async Task<string> SendToGeminiTextAsync(string prompt, LlmSchema? responseSchema)
    {
        var apiKey = GetGeminiApiKey();

        GenerateContentConfig? config = null;
        if (responseSchema != null)
        {
            config = new GenerateContentConfig
            {
                ResponseMimeType = "application/json",
                ResponseSchema = MapToGeminiSchema(responseSchema)
            };
        }

        var client = new Client(apiKey: apiKey);
        var response = await client.Models.GenerateContentAsync(
            model: _settings.Model,
            contents: prompt,
            config: config
        );

        return UnwrapGeminiResponse(response);
    }

    private async Task<string> SendToGeminiImageAsync(string base64Image, string mimeType, string prompt, LlmSchema? responseSchema)
    {
        var apiKey = GetGeminiApiKey();

        GenerateContentConfig? config = null;
        if (responseSchema != null)
        {
            config = new GenerateContentConfig
            {
                ResponseMimeType = "application/json",
                ResponseSchema = MapToGeminiSchema(responseSchema)
            };
        }

        var client = new Client(apiKey: apiKey);
        var bytes = Convert.FromBase64String(base64Image);
        var content = new Content
        {
            Parts = new List<Part>
            {
                new Part { Text = prompt },
                new Part
                {
                    InlineData = new Blob
                    {
                        MimeType = mimeType,
                        Data = bytes
                    }
                }
            }
        };

        var response = await client.Models.GenerateContentAsync(
            model: _settings.Model,
            contents: content,
            config: config
        );

        return UnwrapGeminiResponse(response);
    }

    private string UnwrapGeminiResponse(GenerateContentResponse response)
    {
        if (response.Text is null || string.IsNullOrWhiteSpace(response.Text))
            throw new InvalidOperationException("Unrecognized Gemini response format — no text content");

        var text = response.Text.Trim();
        return CleanJsonString(text);
    }

    private async Task<string> SendToOpenAiTextAsync(string prompt, LlmSchema? responseSchema)
    {
        var client = CreateChatClient();
        var messages = new List<ChatMessage>
        {
            new UserChatMessage(prompt)
        };

        ChatCompletionOptions? options = null;
        if (responseSchema != null)
        {
            options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "llm_schema",
                    jsonSchema: BinaryData.FromString(MapToOpenAiSchemaJson(responseSchema)),
                    jsonSchemaIsStrict: true
                )
            };
        }

        var response = await client.CompleteChatAsync(messages, options);

        return UnwrapOpenAiResponse(response);
    }

    private async Task<string> SendToOpenAiImageAsync(string base64Image, string mimeType, string prompt, LlmSchema? responseSchema)
    {
        var client = CreateChatClient();
        var bytes = Convert.FromBase64String(base64Image);
        var messages = new List<ChatMessage>
        {
            new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(prompt),
                ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(bytes), mimeType)
            )
        };

        ChatCompletionOptions? options = null;
        if (responseSchema != null)
        {
            options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "llm_schema",
                    jsonSchema: BinaryData.FromString(MapToOpenAiSchemaJson(responseSchema)),
                    jsonSchemaIsStrict: true
                )
            };
        }

        var response = await client.CompleteChatAsync(messages, options);

        return UnwrapOpenAiResponse(response);
    }

    private ChatClient CreateChatClient()
    {
        if (!string.IsNullOrEmpty(_settings.BaseUrl))
        {
            var options = new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri(_settings.BaseUrl)
            };
            var key = string.IsNullOrEmpty(_settings.ApiKey) ? "none" : _settings.ApiKey;
            return new ChatClient(_settings.Model, new System.ClientModel.ApiKeyCredential(key), options);
        }

        if (string.IsNullOrEmpty(_settings.ApiKey))
            throw new InvalidOperationException("API key is required for OpenAI-compatible provider.");

        return new ChatClient(_settings.Model, _settings.ApiKey);
    }

    private string UnwrapOpenAiResponse(ChatCompletion response)
    {
        if (response.Content is null || response.Content.Count == 0 || string.IsNullOrWhiteSpace(response.Content[0].Text))
            throw new InvalidOperationException("Unrecognized OpenAI-compatible response format — no content");

        var text = response.Content[0].Text.Trim();
        return CleanJsonString(text);
    }

    private Google.GenAI.Types.Schema MapToGeminiSchema(LlmSchema source)
    {
        var target = new Google.GenAI.Types.Schema
        {
            Type = source.Type switch
            {
                LlmSchemaType.Object => Google.GenAI.Types.Type.Object,
                LlmSchemaType.Array => Google.GenAI.Types.Type.Array,
                LlmSchemaType.String => Google.GenAI.Types.Type.String,
                LlmSchemaType.Number => Google.GenAI.Types.Type.Number,
                LlmSchemaType.Boolean => Google.GenAI.Types.Type.Boolean,
                LlmSchemaType.Integer => Google.GenAI.Types.Type.Integer,
                _ => Google.GenAI.Types.Type.String
            },
            Description = source.Description,
            Nullable = source.Nullable
        };

        if (source.Properties != null)
        {
            target.Properties = new Dictionary<string, Google.GenAI.Types.Schema>();
            foreach (var prop in source.Properties)
            {
                target.Properties.Add(prop.Key, MapToGeminiSchema(prop.Value));
            }
        }

        if (source.Items != null)
        {
            target.Items = MapToGeminiSchema(source.Items);
        }

        if (source.Required != null)
        {
            target.Required = source.Required;
        }

        return target;
    }

    private string MapToOpenAiSchemaJson(LlmSchema source)
    {
        var schemaObject = BuildOpenAiSchemaObject(source);
        return JsonSerializer.Serialize(schemaObject, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    private Dictionary<string, object> BuildOpenAiSchemaObject(LlmSchema source)
    {
        var target = new Dictionary<string, object>();

        if (source.Nullable)
        {
            var typeName = source.Type switch
            {
                LlmSchemaType.Object => "object",
                LlmSchemaType.Array => "array",
                LlmSchemaType.String => "string",
                LlmSchemaType.Number => "number",
                LlmSchemaType.Boolean => "boolean",
                LlmSchemaType.Integer => "integer",
                _ => "string"
            };
            target.Add("type", new[] { typeName, "null" });
        }
        else
        {
            target.Add("type", source.Type.ToString().ToLowerInvariant());
        }

        if (!string.IsNullOrEmpty(source.Description))
        {
            target.Add("description", source.Description);
        }

        if (source.Type == LlmSchemaType.Object)
        {
            target.Add("additionalProperties", false);

            if (source.Properties != null && source.Properties.Count > 0)
            {
                var props = new Dictionary<string, object>();
                foreach (var prop in source.Properties)
                {
                    props.Add(prop.Key, BuildOpenAiSchemaObject(prop.Value));
                }
                target.Add("properties", props);
            }
            else
            {
                target.Add("properties", new Dictionary<string, object>());
            }

            var requiredList = new List<string>();
            if (source.Properties != null)
            {
                requiredList.AddRange(source.Properties.Keys);
            }
            target.Add("required", requiredList);
        }
        else if (source.Type == LlmSchemaType.Array && source.Items != null)
        {
            target.Add("items", BuildOpenAiSchemaObject(source.Items));
        }

        return target;
    }


    private static string CleanJsonString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        input = input.Trim();
        if (input.StartsWith("```"))
        {
            var firstLineEnd = input.IndexOf('\n');
            if (firstLineEnd != -1)
                input = input.Substring(firstLineEnd + 1);
            else
                input = input.Substring(3);

            if (input.EndsWith("```"))
                input = input.Substring(0, input.Length - 3);
            
            input = input.Trim();
        }
        return input;
    }
}
