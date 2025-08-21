using System.Text.Json.Serialization;
using MyPlanner.Data.Entities.Common;

namespace MyPlanner.API.Models.Todo;

public class TodoListResponse
{
    public Guid Id { get; set; } = Guid.Empty;
    // public string Title { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter<PageContentEnum>))]
    public PageContentEnum Type { get; set; }
    public List<TodoTaskInListResponse> Tasks { get; set; } = new List<TodoTaskInListResponse>();
}

public class TodoTaskInListResponse
{
    public Guid Id { get; set; } = Guid.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public Guid ListId { get; set; }
    public long? StartedSessionTimestamp { get; init; }
}