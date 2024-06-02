using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MyPlanner.Service;

namespace MyPlanner.API;

[ApiController]
[Route("api/todo/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITodoTaskService _taskService;

    public TasksController(ITodoTaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask(CreateTaskModel model)
    {
        if (string.IsNullOrEmpty(model.Title))
        {
            return BadRequest();
        }
        Guid id = await _taskService.CreateAsync(model);
        return CreatedAtAction(nameof(CreateTask), id);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTask(Guid id)
    {
        var folder = await _taskService.GetAsync(id);
        if (folder != null)
            return Ok(folder);
        return NotFound();
    }

    [HttpPut]
    public async Task<IActionResult> UpdateTask(UpdateTaskModel model)
    {
        bool isUpdated = await _taskService.UpdateAsync(model);
        if (isUpdated == false)
            return NotFound();
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(Guid id)
    {
        bool isRemoved = await _taskService.DeleteAsync(id);
        if (isRemoved == false)
            return NotFound();
        return NoContent();
    }

    [HttpGet("{taskId}/sessions")]
    public async Task<IActionResult> GetSessions(Guid taskId)
    {
        var items = await _taskService.GetAllSessionsAsync(taskId);
        if (items.Any())
        {
            return Ok(items);
        }
        return NoContent();
    }

    [HttpPost("{taskId}/sessions")]
    public async Task<IActionResult> AddSession(Guid taskId, CreateSessionModel model)
    {
        Guid id = await _taskService.CreateSessionAsync(model);
        return CreatedAtAction(nameof(AddSession), id);
    }

    [HttpPost("{taskId}/sessions/start")]
    public async Task<IActionResult> StartSession(Guid taskId)
    {
        bool isSuccess = await _taskService.StartSessionAsync(taskId);
        return isSuccess ? Ok() : NotFound();
    }

    [HttpPost("{id}/sessions/stop")]
    public async Task<IActionResult> StopSession(Guid taskId)
    {
        bool isSuccess = await _taskService.StopSessionAsync(taskId);
        return isSuccess ? Ok() : NotFound();
    }

    [HttpDelete("{taskId}/sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(Guid taskId, Guid sessionId)
    {
        bool isRemoved = await _taskService.DeleteSessionAsync(sessionId);
        if (isRemoved == false)
            return NotFound();
        return NoContent();
    }

    [HttpPut("{taskId}/sessions/{sessionId}")]
    public async Task<IActionResult> UpdateSession(Guid taskId, UpdateSessionModel model)
    {
        bool isRemoved = await _taskService.UpdateSessionAsync(model);
        if (isRemoved == false)
            return NotFound();
        return NoContent();
    }
}
