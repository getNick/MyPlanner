using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MyPlanner.Service;

namespace MyPlanner.API;

[ApiController]
[Route("api/todo/[controller]")]
public class ListsController : ControllerBase
{
    private readonly ITodoListService _listService;
    private readonly ITodoTaskService _taskService;
    public ListsController(ITodoListService listService, ITodoTaskService taskService)
    {
        _listService = listService;
        _taskService = taskService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateList(CreateListModel model){
        if(string.IsNullOrEmpty(model.Title)){
            return BadRequest();
        }
        Guid id = await _listService.CreateAsync(model);
        return CreatedAtAction(nameof(CreateList), id);
    }

    [HttpGet("noFolder")]
    public async Task<IActionResult> GetListOutsideFolder(){
        var items = await _listService.GetListOutsideOfFolderAsync();
        if(items.Any())
            return Ok(items);
        return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetList(Guid id){
        var folder = await _listService.GetIncludeTasksAsync(id);
        if(folder != null)
            return Ok(folder);
        return NotFound();
    }
    [HttpGet("{id}/tasks")]
    public async Task<IActionResult> GetTasksByList(Guid id){
        var folder = await _taskService.GetAllAsync(id);
        if(folder != null)
            return Ok(folder);
        return NotFound();
    }

    [HttpPut]
    public async Task<IActionResult> UpdateList(UpdateListModel model){
        bool isUpdated = await _listService.UpdateAsync(model);
        if(isUpdated == false)
            return NotFound();
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteList(Guid id){
        bool isRemoved = await _listService.DeleteAsync(id);
        if(isRemoved == false)
            return NotFound();
        return NoContent();
    }
}
