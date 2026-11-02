using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace group3porras_rana_Lab3.ViewModels;

public sealed class ResourceFormViewModel
{
    public int ResourceId { get; set; }

    [Required, StringLength(150)]
    public string Title { get; set; } = "";

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Display(Name = "Resource file")]
    public IFormFile? File { get; set; }
    public string? CurrentFileName { get; set; }
}
