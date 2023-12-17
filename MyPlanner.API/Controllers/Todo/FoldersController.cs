using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MyPlanner.Service;

namespace MyPlanner.API;

[ApiController]
[Route("api/todo/[controller]")]
public class FoldersController : ControllerBase
{
    private readonly ITodoFolderService _folderService;
    public FoldersController(ITodoFolderService folderService)
    {
        _folderService = folderService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateFolder(CreateFolderModel model){
        if(string.IsNullOrEmpty(model.Title)){
            return BadRequest();
        }
        Guid id = await _folderService.CreateAsync(model);
        return CreatedAtAction(nameof(CreateFolder), id);
    }

    [HttpGet]
    public async Task<IActionResult> GetFolders(){
        var items = await _folderService.GetAllIncludeListsAsync();
        if(items.Any())
            return Ok(items);
        return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetFolder(Guid id){
        var folder = await _folderService.GetAsync(id);
        if(folder != null)
            return Ok(folder);
        return NotFound();
    }

    [HttpPut]
    public async Task<IActionResult> UpdateFolder(UpdateFolderModel model){
        bool isUpdated = await _folderService.UpdateAsync(model);
        if(isUpdated == false)
            return NotFound();
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFolder(Guid id){
        bool isRemoved = await _folderService.DeleteAsync(id);
        if(isRemoved == false)
            return NotFound();
        return NoContent();
    }
}
