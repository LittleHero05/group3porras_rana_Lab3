using System.Security.Claims;
using group3porras_rana_Lab3.Models;
using group3porras_rana_Lab3.Services;
using group3porras_rana_Lab3.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace group3porras_rana_Lab3.Controllers;

[Authorize]
public sealed class ResourcesController : Controller
{
    private readonly LearningPortalContext _context;
    private readonly S3Service _s3;
    private readonly CommentService _comments;
    private readonly ILogger<ResourcesController> _logger;

    public ResourcesController(LearningPortalContext context, S3Service s3,
        CommentService comments, ILogger<ResourcesController> logger)
    {
        _context = context; _s3 = s3; _comments = comments; _logger = logger;
    }

    private IQueryable<LearningResource> FullResources() => _context.LearningResources
        .AsNoTracking().Include(r => r.Author).Include(r => r.Category);

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task LoadCategoriesAsync(int selected = 0) =>
        ViewBag.Categories = new SelectList(await _context.Categories.AsNoTracking()
            .OrderBy(c => c.CategoryName).ToListAsync(), "CategoryId", "CategoryName", selected);

    [HttpGet]
    public async Task<IActionResult> Index() =>
        View(await FullResources().OrderByDescending(r => r.CreatedDate).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Popular() =>
        View("Index", await FullResources().OrderByDescending(r => r.ViewCount).Take(5).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Recent() =>
        View("Index", await FullResources().OrderByDescending(r => r.CreatedDate).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Search(string? topic, string? author)
    {
        ViewBag.Topic = topic;
        ViewBag.Author = author;
        var query = FullResources();
        if (!string.IsNullOrWhiteSpace(topic))
        {
            string q = topic.Trim();
            query = query.Where(r => r.Title.Contains(q) ||
                (r.Description != null && r.Description.Contains(q)) ||
                r.Category.CategoryName.Contains(q));
        }
        if (!string.IsNullOrWhiteSpace(author))
        {
            string q = author.Trim();
            query = query.Where(r => r.Author.Username.Contains(q) ||
                r.Author.FirstName.Contains(q) || r.Author.LastName.Contains(q) ||
                (r.Author.FirstName + " " + r.Author.LastName).Contains(q));
        }
        return View(await query.OrderByDescending(r => r.CreatedDate).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        int updated = await _context.LearningResources.Where(r => r.ResourceId == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.ViewCount, r => r.ViewCount + 1));
        if (updated == 0) return NotFound();
        var resource = await FullResources().FirstAsync(r => r.ResourceId == id);
        var comments = await _comments.GetForResourceAsync(id);
        return View(new ResourceDetailsViewModel
        {
            Resource = resource, Comments = comments, CurrentUserId = CurrentUserId()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var resource = await _context.LearningResources.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ResourceId == id);
        if (resource is null) return NotFound();
        return Redirect(_s3.GetDownloadUrl(resource.S3key));
    }

    [Authorize(Roles = "Instructor")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadCategoriesAsync();
        return View(new ResourceFormViewModel());
    }

    [Authorize(Roles = "Instructor")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ResourceFormViewModel model)
    {
        var fileProblem = S3Service.ValidateFile(model.File, required: true);
        if (fileProblem is not null) ModelState.AddModelError(nameof(model.File), fileProblem);
        if (!await _context.Categories.AnyAsync(c => c.CategoryId == model.CategoryId))
            ModelState.AddModelError(nameof(model.CategoryId), "Choose a valid category.");
        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync(model.CategoryId); return View(model);
        }

        string key = await _s3.UploadAsync(model.File!);
        try
        {
            var resource = new LearningResource
            {
                Title = model.Title.Trim(), Description = model.Description?.Trim(),
                CategoryId = model.CategoryId, AuthorId = CurrentUserId(),
                CreatedDate = DateTime.UtcNow, S3key = key,
                FileName = Path.GetFileName(model.File!.FileName),
                ContentType = model.File.ContentType,
                ViewCount = 0
            };
            _context.LearningResources.Add(resource);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Resource created successfully.";
            return RedirectToAction(nameof(Details), new { id = resource.ResourceId });
        }
        catch
        {
            await _s3.DeleteAsync(key);
            throw;
        }
    }

    [Authorize(Roles = "Instructor")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var resource = await _context.LearningResources.FindAsync(id);
        if (resource is null) return NotFound();
        await LoadCategoriesAsync(resource.CategoryId);
        return View(new ResourceFormViewModel
        {
            ResourceId = resource.ResourceId, Title = resource.Title,
            Description = resource.Description, CategoryId = resource.CategoryId,
            CurrentFileName = resource.FileName
        });
    }

    [Authorize(Roles = "Instructor")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ResourceFormViewModel model)
    {
        if (id != model.ResourceId) return BadRequest();
        var resource = await _context.LearningResources.FindAsync(id);
        if (resource is null) return NotFound();
        var fileProblem = S3Service.ValidateFile(model.File, required: false);
        if (fileProblem is not null) ModelState.AddModelError(nameof(model.File), fileProblem);
        if (!await _context.Categories.AnyAsync(c => c.CategoryId == model.CategoryId))
            ModelState.AddModelError(nameof(model.CategoryId), "Choose a valid category.");
        if (!ModelState.IsValid)
        {
            model.CurrentFileName = resource.FileName;
            await LoadCategoriesAsync(model.CategoryId); return View(model);
        }

        string oldKey = resource.S3key;
        string? newKey = null;
        if (model.File is { Length: > 0 }) newKey = await _s3.UploadAsync(model.File);
        resource.Title = model.Title.Trim();
        resource.Description = model.Description?.Trim();
        resource.CategoryId = model.CategoryId;
        if (newKey is not null)
        {
            resource.S3key = newKey;
            resource.FileName = Path.GetFileName(model.File!.FileName);
            resource.ContentType = model.File.ContentType;
        }

        try { await _context.SaveChangesAsync(); }
        catch
        {
            if (newKey is not null) await _s3.DeleteAsync(newKey);
            throw;
        }
        if (newKey is not null)
        {
            try { await _s3.DeleteAsync(oldKey); }
            catch (Exception ex) { _logger.LogWarning(ex, "Old S3 object could not be cleaned up: {Key}", oldKey); }
        }
        TempData["Success"] = "Resource updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Instructor")]
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var resource = await FullResources().FirstOrDefaultAsync(r => r.ResourceId == id);
        return resource is null ? NotFound() : View(resource);
    }

    [Authorize(Roles = "Instructor")]
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var resource = await _context.LearningResources.FindAsync(id);
        if (resource is null) return NotFound();
        // SQL deletes are committed before external cleanup; report incomplete cleanup explicitly.
        _context.LearningResources.Remove(resource);
        await _context.SaveChangesAsync();
        try
        {
            await _s3.DeleteAsync(resource.S3key);
            await _comments.DeleteAllForResourceAsync(id);
            TempData["Success"] = "Resource and cloud objects deleted.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cloud cleanup failed for resource {Id}", id);
            TempData["Warning"] = "Resource deleted from SQL, but some cloud cleanup failed. Review S3 and DynamoDB.";
        }
        return RedirectToAction(nameof(Index));
    }
}
