namespace MyPlanner.Service.Models;

public class LlmSettings
{
    public string Provider { get; set; } = "Gemini";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
}
