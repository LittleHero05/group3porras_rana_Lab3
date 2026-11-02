using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;

namespace group3porras_rana_Lab3.Services;

public sealed class S3Service
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    public const long MaxFileBytes = 20 * 1024 * 1024;
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
    };

    public S3Service(IAmazonS3 s3, IConfiguration configuration)
    {
        _s3 = s3;
        _bucket = configuration["S3:Bucket"] ??
                  throw new InvalidOperationException("Missing S3:Bucket configuration.");
    }

    public static string? ValidateFile(IFormFile? file, bool required)
    {
        if (file is null || file.Length == 0)
            return required ? "Choose a file to upload." : null;
        if (file.Length > MaxFileBytes) return "File must be 20 MB or smaller.";
        if (!AllowedTypes.ContainsKey(Path.GetExtension(file.FileName)))
            return "Only PDF, PNG, JPG and PPTX files are accepted.";
        return null;
    }

    public async Task<string> UploadAsync(IFormFile file, CancellationToken ct = default)
    {
        var problem = ValidateFile(file, required: true);
        if (problem is not null) throw new ArgumentException(problem);
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        string key = $"resources/{Guid.NewGuid():N}{extension}";
        await using var stream = file.OpenReadStream();
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = stream,
            ContentType = AllowedTypes[extension]
        }, ct);
        return key;
    }

    public Task DeleteAsync(string key, CancellationToken ct = default) =>
        _s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = key }, ct);

    public string GetDownloadUrl(string key) => _s3.GetPreSignedURL(new GetPreSignedUrlRequest
    {
        BucketName = _bucket,
        Key = key,
        Verb = HttpVerb.GET,
        Expires = DateTime.UtcNow.AddMinutes(15)
    });
}
