using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MyPlanner.API.Models.Page;
using MyPlanner.Service;

namespace MyPlanner.API;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PagesController : ControllerBase
{
    private readonly IPageService _pageService;
    public PagesController(IPageService pageService)
    {
        _pageService = pageService;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePage(CreatePageRequest request)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(request.Title) || string.IsNullOrEmpty(userId))
        {
            return BadRequest();
        }

        var model = new CreatePageModel(request.Title, userId, request.PageType, request.ParentPageId);
        Guid id = await _pageService.CreateAsync(model);
        return CreatedAtAction(nameof(CreatePage), id);
    }

    [HttpGet]
    public async Task<IActionResult> GetPages()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var items = await _pageService.GetAllAsync(userId);
        if (items.Any())
        {
            var dtos = items.Select(page => new PageModel(page)).ToArray();
            return Ok(dtos);
        }

        return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPage(Guid id)
    {
        var page = await _pageService.GetAsync(id);
        if (page != null)
            return Ok(page);
        return NotFound();
    }

    [HttpGet("{id}/content")]
    public async Task<IActionResult> GetPageContent(Guid id)
    {
        var page = await _pageService.GetPageContentAsync(id);
        if (page != null)
            return Ok(page);
        return NotFound();
    }

    [HttpPut]
    public async Task<IActionResult> UpdateFolder(UpdatePageModel model)
    {
        bool isUpdated = await _pageService.UpdateAsync(model);
        if (isUpdated == false)
            return NotFound();
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFolder(Guid id)
    {
        bool isRemoved = await _pageService.DeleteAsync(id);
        if (isRemoved == false)
            return NotFound();
        return NoContent();
    }

    [HttpPost("share")]
    public async Task<IActionResult> SharePage(SharePageModel model)
    {
        if (string.IsNullOrEmpty(model.UserId))
        {
            return BadRequest();
        }
        bool isCreated = await _pageService.Share(model);
        return CreatedAtAction(nameof(SharePage), model);
    }
}
