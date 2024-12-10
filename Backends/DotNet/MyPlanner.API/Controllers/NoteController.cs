using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MyPlanner.Service;

namespace MyPlanner.API;

[ApiController]
[Route("api/[controller]")]
public class NoteController : ControllerBase
{
    private readonly INoteService _noteService;

    public NoteController(INoteService taskService)
    {
        _noteService = taskService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetNote(Guid id)
    {
        var note = await _noteService.GetAsync(id);
        if (note != null)
            return Ok(note);
        return NotFound();
    }

    [HttpPut]
    public async Task<IActionResult> UpdateNote(UpdateNoteModel model)
    {
        bool isUpdated = await _noteService.UpdateAsync(model);
        if (isUpdated == false)
            return NotFound();
        return Ok();
    }
}
