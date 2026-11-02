using System.Security.Claims;
using group3porras_rana_Lab3.Models;
using group3porras_rana_Lab3.Services;
using group3porras_rana_Lab3.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace group3porras_rana_Lab3.Controllers;

[Authorize]
public sealed class CommentsController : Controller
{
    private readonly CommentService _comments;
    private readonly LearningPortalContext _context;

    public CommentsController(CommentService comments, LearningPortalContext context)
    {
        _comments = comments; _context = context;
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int resourceId, string? commentText)
    {
        if (!await _context.LearningResources.AnyAsync(r => r.ResourceId == resourceId)) return NotFound();
        if (string.IsNullOrWhiteSpace(commentText) || commentText.Trim().Length > 1000)
        {
            TempData["Warning"] = "Comment is required and must be at most 1000 characters.";
            return RedirectToAction("Details", "Resources", new { id = resourceId });
        }
        await _comments.AddAsync(resourceId, CurrentUserId(), User.Identity!.Name!, commentText.Trim());
        TempData["Success"] = "Comment posted.";
        return RedirectToAction("Details", "Resources", new { id = resourceId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int resourceId, string commentId)
    {
        var comment = await _comments.GetAsync(resourceId, commentId);
        if (comment is null) return NotFound();
        if (!comment.MayEdit(CurrentUserId())) return Forbid();
        return View(new EditCommentViewModel
        {
            ResourceId = comment.ResourceId,
            CommentId = comment.CommentId,
            CommentText = comment.CommentText
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditCommentViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var comment = await _comments.GetAsync(model.ResourceId, model.CommentId);
        if (comment is null) return NotFound();
        if (!comment.MayEdit(CurrentUserId())) return Forbid();
        bool updated = await _comments.UpdateAsync(comment, CurrentUserId(), model.CommentText.Trim());
        if (!updated) return Forbid();
        TempData["Success"] = "Comment updated.";
        return RedirectToAction("Details", "Resources", new { id = model.ResourceId });
    }
}
