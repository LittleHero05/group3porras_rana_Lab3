using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using group3porras_rana_Lab3.Models;

namespace group3porras_rana_Lab3.Services;

public sealed class CommentService
{
    private readonly IAmazonDynamoDB _db;
    private readonly string _table;
    private static AttributeValue N(int value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };
    private static AttributeValue S(string value) => new() { S = value };
    private static Dictionary<string, AttributeValue> Key(int resourceId, string commentId) => new()
    {
        ["ResourceID"] = N(resourceId), ["CommentID"] = S(commentId)
    };

    public CommentService(IAmazonDynamoDB db, IConfiguration config)
    {
        _db = db;
        _table = config["DynamoDB:CommentsTable"] ?? "Comments";
    }

    private static CommentRecord Parse(Dictionary<string, AttributeValue> item)
    {
        var date = DateTime.Parse(item["Timestamp"].S,
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
        return new CommentRecord
        {
            ResourceId = int.Parse(item["ResourceID"].N, CultureInfo.InvariantCulture),
            CommentId = item["CommentID"].S,
            UserId = int.Parse(item["UserID"].N, CultureInfo.InvariantCulture),
            Username = item.TryGetValue("Username", out var username) ? username.S : "User",
            CommentText = item["CommentText"].S,
            TimestampUtc = date,
            TimestampRaw = item["Timestamp"].S
        };
    }

    public async Task AddAsync(int resourceId, int userId, string username, string text, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        string commentId = $"{now:yyyy-MM-ddTHH:mm:ss.fffffffZ}#{Guid.NewGuid():N}";
        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = _table,
            ConditionExpression = "attribute_not_exists(ResourceID)",
            Item = new Dictionary<string, AttributeValue>
            {
                ["ResourceID"] = N(resourceId), ["CommentID"] = S(commentId),
                ["UserID"] = N(userId), ["Username"] = S(username),
                ["CommentText"] = S(text), ["Timestamp"] = S(now.ToString("O", CultureInfo.InvariantCulture))
            }
        }, ct);
    }

    public async Task<List<CommentRecord>> GetForResourceAsync(int resourceId, CancellationToken ct = default)
    {
        var comments = new List<CommentRecord>();
        Dictionary<string, AttributeValue>? pageKey = null;
        do
        {
            var response = await _db.QueryAsync(new QueryRequest
            {
                TableName = _table,
                KeyConditionExpression = "ResourceID = :id",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":id"] = N(resourceId) },
                ScanIndexForward = false,
                ExclusiveStartKey = pageKey
            }, ct);
            foreach (var item in response.Items ?? new List<Dictionary<string, AttributeValue>>())
                comments.Add(Parse(item));
            pageKey = response.LastEvaluatedKey;
        } while (pageKey is { Count: > 0 });
        return comments.OrderByDescending(c => c.TimestampUtc).ToList();
    }

    public async Task<CommentRecord?> GetAsync(int resourceId, string commentId, CancellationToken ct = default)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = _table, Key = Key(resourceId, commentId), ConsistentRead = true
        }, ct);
        return response.Item is { Count: > 0 } ? Parse(response.Item) : null;
    }

    public async Task<bool> UpdateAsync(CommentRecord existing, int currentUserId, string newText, CancellationToken ct = default)
    {
        if (!existing.MayEdit(currentUserId)) return false;
        try
        {
            await _db.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _table, Key = Key(existing.ResourceId, existing.CommentId),
                UpdateExpression = "SET CommentText = :text",
                ConditionExpression = "attribute_exists(CommentID) AND UserID = :owner AND #ts = :original " +
                                      "AND #ts >= :cutoff AND #ts <= :now",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#ts"] = "Timestamp" },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":text"] = S(newText), [":owner"] = N(currentUserId),
                    [":original"] = S(existing.TimestampRaw),
                    [":cutoff"] = S(DateTime.UtcNow.AddHours(-24).ToString("O", CultureInfo.InvariantCulture)),
                    [":now"] = S(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture))
                }
            }, ct);
            return true;
        }
        catch (ConditionalCheckFailedException) { return false; }
    }

    public async Task DeleteAllForResourceAsync(int resourceId, CancellationToken ct = default)
    {
        foreach (var comment in await GetForResourceAsync(resourceId, ct))
            await _db.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = _table, Key = Key(resourceId, comment.CommentId)
            }, ct);
    }
}
