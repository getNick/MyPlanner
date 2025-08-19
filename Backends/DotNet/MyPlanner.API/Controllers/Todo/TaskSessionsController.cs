using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyPlanner.API.Mapping;
using MyPlanner.API.Models.Todo.Session;
using MyPlanner.Service;

namespace MyPlanner.API;

[ApiController]
[Authorize]
public class TaskSessionsController : ControllerBase
{
    private readonly ITodoTaskSessionService _taskSessionService;

    public TaskSessionsController(ITodoTaskSessionService taskSessionService)
    {
        _taskSessionService = taskSessionService;
    }

    [HttpGet(ApiEndpoints.Todo.TaskSessions.GetSessions)]
    public async Task<IActionResult> GetSessions(Guid taskId)
    {
        var items = await _taskSessionService.GetAllSessionsAsync(taskId);
        if (items.Any())
        {
            return Ok(items);
        }
        return NoContent();
    }

    [HttpPost(ApiEndpoints.Todo.TaskSessions.CreateSession)]
    public async Task<IActionResult> CreateSession(Guid taskId, CreateSessionRequest request)
    {
        var session = request.MapToSession(taskId);
        bool isCreated = await _taskSessionService.CreateAsync(session);
        if (!isCreated)
        {
            return BadRequest("Failed to create session.");
        }

        return CreatedAtAction(nameof(CreateSession), session.Id);
    }

    [HttpPost(ApiEndpoints.Todo.TaskSessions.StartSession)]
    public async Task<IActionResult> StartSession(Guid taskId, StartSessionRequest request)
    {
        var timestamp = request.Timestamp.HasValue
            ? DateTimeOffset.FromUnixTimeSeconds(request.Timestamp.Value).UtcDateTime
            : DateTime.UtcNow;

        if (timestamp > DateTime.UtcNow)
        {
            return BadRequest("The provided time cannot be in the future.");
        }

        bool isSuccess = await _taskSessionService.StartSessionAsync(taskId, timestamp);
        return isSuccess ? Ok() : NotFound();
    }

    [HttpPost(ApiEndpoints.Todo.TaskSessions.StopSession)]
    public async Task<IActionResult> StopSession(Guid taskId, StopSessionRequest request)
    {
        var timestamp = request.Timestamp.HasValue
            ? DateTimeOffset.FromUnixTimeSeconds(request.Timestamp.Value).UtcDateTime
            : DateTime.UtcNow;

        if (timestamp > DateTime.UtcNow)
        {
            return BadRequest("The provided time cannot be in the future.");
        }
        bool isSuccess = await _taskSessionService.StopSessionAsync(taskId, timestamp);
        return isSuccess ? Ok() : NotFound();
    }

    [HttpDelete(ApiEndpoints.Todo.TaskSessions.DeleteSession)]
    public async Task<IActionResult> DeleteSession(Guid taskId, Guid sessionId)
    {
        bool isRemoved = await _taskSessionService.DeleteSessionAsync(sessionId);
        if (isRemoved == false)
            return NotFound();
        return NoContent();
    }

    [HttpPut(ApiEndpoints.Todo.TaskSessions.UpdateSession)]
    public async Task<IActionResult> UpdateSession(Guid taskId, Guid sessionId, UpdateSessionRequest request)
    {
        var session = request.MapToSession(sessionId);
        bool isUpdated = await _taskSessionService.UpdateAsync(session);
        if (isUpdated == false)
            return NotFound();
        return NoContent();
    }
}
