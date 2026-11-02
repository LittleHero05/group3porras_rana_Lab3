namespace group3porras_rana_Lab3.Models;

public sealed class CommentRecord
{
    public int ResourceId { get; set; }
    public string CommentId { get; set; } = "";
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string CommentText { get; set; } = "";
    public DateTime TimestampUtc { get; set; }
    public string TimestampRaw { get; set; } = "";

    public bool MayEdit(int currentUserId) => UserId == currentUserId &&
        TimestampUtc <= DateTime.UtcNow &&
        DateTime.UtcNow - TimestampUtc < TimeSpan.FromHours(24);
}
