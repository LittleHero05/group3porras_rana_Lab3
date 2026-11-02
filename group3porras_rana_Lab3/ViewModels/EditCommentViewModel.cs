using System.ComponentModel.DataAnnotations;

namespace group3porras_rana_Lab3.ViewModels;

public sealed class EditCommentViewModel
{
    public int ResourceId { get; set; }
    [Required]
    public string CommentId { get; set; } = "";
    [Required, StringLength(1000, MinimumLength = 1)]
    [Display(Name = "Comment")]
    public string CommentText { get; set; } = "";
}
