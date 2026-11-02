using group3porras_rana_Lab3.Models;

namespace group3porras_rana_Lab3.ViewModels;

public sealed class ResourceDetailsViewModel
{
    public required LearningResource Resource { get; set; }
    public required IReadOnlyList<CommentRecord> Comments { get; set; }
    public int CurrentUserId { get; set; }
}
