using System.Text.Json.Serialization;
using MyPlanner.Data.Entities.Common;

namespace MyPlanner.API.Models.Todo;

public class TodoTaskResponse
{
    public Guid Id { get; set; } = Guid.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public Guid ListId { get; set; }
    public long? StartedSessionTimestamp { get; init; }
    public List<TodoTaskSessionResponse> Sessions { get; set; } = new List<TodoTaskSessionResponse>();
}

public class TodoTaskSessionResponse
{
    public Guid Id { get; set; } = Guid.Empty;
    public long? StartTimestamp { get; init; }
    public long? EndTimestamp { get; init; }
}