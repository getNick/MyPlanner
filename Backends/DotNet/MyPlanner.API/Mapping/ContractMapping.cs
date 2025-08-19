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

}
