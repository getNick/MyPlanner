using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MyPlanner.API.Mapping;
using MyPlanner.Service;

namespace MyPlanner.API;

[ApiController]
[Authorize]
[Route("api/todo/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITodoTaskService _taskService;

    public TasksController(ITodoTaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask(CreateTaskRequest model)
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
        var task = await _taskService.GetAsync(id);
        if (task != null)
        {
            return Ok(task);
        }
        return NotFound();
    }

    [HttpPut]
    public async Task<IActionResult> UpdateTask(UpdateTaskRequest model)
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
}
