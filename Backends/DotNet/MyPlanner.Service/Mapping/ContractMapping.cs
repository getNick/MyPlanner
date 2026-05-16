using MyPlanner.Data.Entities.Todo;
using MyPlanner.Service.Models.Todo.Task;

namespace MyPlanner.Service.Mapping;

public static class ContractMapping
{
    public static TaskDetailsResponse MapToResponse(this TodoTask task)
    {
        var taskResponse = new TaskDetailsResponse()
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            IsComplete = task.IsComplete,
            ListId = task.ListId,
            StartedSessionTimestamp = ToUnixTimestamp(task.Sessions.FirstOrDefault(s => s.End == null)?.Start),
            Sessions = [.. task.Sessions.Select(t => new TaskSessionResponse()
            {
                Id = t.Id,
                StartTimestamp = ToUnixTimestamp(t.Start),
                EndTimestamp = ToUnixTimestamp(t.End)
            })],
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
