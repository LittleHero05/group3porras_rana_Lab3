using System;
using System.Collections.Generic;

namespace group3porras_rana_Lab3.Models;

public partial class LearningResource
{
    public int ResourceId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public int CategoryId { get; set; }

    public int AuthorId { get; set; }

    public DateTime CreatedDate { get; set; }

    public string S3key { get; set; } = null!;

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public int ViewCount { get; set; }

    public virtual User Author { get; set; } = null!;

    public virtual Category Category { get; set; } = null!;
}
