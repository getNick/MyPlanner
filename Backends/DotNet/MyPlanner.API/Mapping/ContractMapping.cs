using MyPlanner.API.Models.Todo;
using MyPlanner.API.Models.Todo.Session;
using MyPlanner.Data.Entities.Todo;

namespace MyPlanner.API.Mapping;

public static class ContractMapping
{
    public static TodoTaskSession MapToSession(this CreateSessionRequest request, Guid taskId)
    {
        return new TodoTaskSession
        {
            Id = Guid.NewGuid(),
            TodoTaskId = taskId,
            Start = DateTimeOffset.FromUnixTimeSeconds(request.StartTimestamp).UtcDateTime,
            End = DateTimeOffset.FromUnixTimeSeconds(request.EndTimestamp).UtcDateTime,
        };
    }

    public static TodoTaskSession MapToSession(this UpdateSessionRequest request, Guid id)
    {
        return new TodoTaskSession
        {
            Id = id,
            Start = request.StartTimestamp.HasValue ? DateTimeOffset.FromUnixTimeSeconds(request.StartTimestamp.Value).UtcDateTime : null,
            End = request.EndTimestamp.HasValue ? DateTimeOffset.FromUnixTimeSeconds(request.EndTimestamp.Value).UtcDateTime : null,
        };
    }

    public static TodoListResponse MapToResponse(this TodoList list)
    {
        var listResponse = new TodoListResponse()
        {
            Id = list.Id,
            Type = list.Type,
            Tasks = list.Tasks.Select(t => new TodoTaskInListResponse()
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                IsComplete = t.IsComplete,
                ListId = t.ListId,
                StartedSessionTimestamp = ToUnixTimestamp(t.Sessions.FirstOrDefault(s => s.End == null)?.Start)
            }).ToList(),
        };
        return listResponse;
    }

    public static TodoTaskResponse MapToResponse(this TodoTask task)
    {
        var taskResponse = new TodoTaskResponse()
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            IsComplete = task.IsComplete,
            ListId = task.ListId,
            StartedSessionTimestamp = ToUnixTimestamp(task.Sessions.FirstOrDefault(s => s.End == null)?.Start),
            Sessions = task.Sessions.Select(t => new TodoTaskSessionResponse()
            {
                Id = t.Id,
                StartTimestamp = ToUnixTimestamp(t.Start),
                EndTimestamp = ToUnixTimestamp(t.End)
            }).ToList(),
        };
        return taskResponse;
    }

    public static long? ToUnixTimestamp(DateTime? dateTime)
    {
        if (!dateTime.HasValue)
        {
            return null;
        }
        var utcDateTime = DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc);
        return new DateTimeOffset(utcDateTime).ToUnixTimeSeconds();
    }

}
