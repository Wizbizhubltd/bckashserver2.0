using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using BCKash.Application.Files;
using BCKash.Infrastructure.Aws;
using Microsoft.Extensions.Options;

namespace BCKash.Infrastructure.Files;

/// <summary>
/// Stores every uploaded file in the configured S3 bucket — images under <c>img/</c>, everything
/// else under <c>documents/</c> — named after what the caller suggests (for client files, the
/// client's full name) plus a timestamp and short id so names never collide. Locations look like
/// <c>s3://bucket/img/Ada-Obi-passport-20260928153000-1a2b3c4d.jpg</c>. Files saved before S3 was
/// configured keep their local-disk locations and are still read from there.
/// </summary>
public class S3FileStorageService : IFileStorageService
{
    private const string Scheme = "s3://";
    public const string ImagesFolder = "img";
    public const string DocumentsFolder = "documents";

    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly LocalDiskFileStorageService _legacy;

    public S3FileStorageService(IAmazonS3 s3, IOptions<AwsSettings> settings, LocalDiskFileStorageService legacy)
    {
        _s3 = s3;
        _bucket = settings.Value.S3Bucket!;
        _legacy = legacy;
    }

    public async Task<StoredFile> SaveAsync(Stream content, string suggestedFileName, CancellationToken cancellationToken = default)
    {
        var key = KeyFor(suggestedFileName, DateTime.UtcNow);

        // S3 needs the length up front; uploads come from request streams that may not report one.
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = buffer,
            ContentType = FileContentTypes.For(suggestedFileName),
            AutoCloseStream = false,
        }, cancellationToken);

        return new StoredFile($"{Scheme}{_bucket}/{key}", buffer.Length);
    }

    public async Task<StoredFileContent?> ReadAsync(string location, string originalFileName, CancellationToken cancellationToken = default)
    {
        if (!TryParse(location, out var bucket, out var key))
        {
            return await _legacy.ReadAsync(location, originalFileName, cancellationToken);
        }

        try
        {
            using var response = await _s3.GetObjectAsync(bucket, key, cancellationToken);
            var copy = new MemoryStream();
            await response.ResponseStream.CopyToAsync(copy, cancellationToken);
            copy.Position = 0;
            return new StoredFileContent(copy, FileContentTypes.For(string.IsNullOrWhiteSpace(originalFileName) ? key : originalFileName));
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string location, CancellationToken cancellationToken = default)
    {
        if (!TryParse(location, out var bucket, out var key))
        {
            await _legacy.DeleteAsync(location, cancellationToken);
            return;
        }

        try
        {
            await _s3.DeleteObjectAsync(bucket, key, cancellationToken);
        }
        catch (AmazonS3Exception)
        {
            // Best-effort, like the interface promises — a file that's already gone isn't an error.
        }
    }

    /// <summary>"Ada Obi - passport.jpg" → "img/Ada-Obi-passport-20260928153000-1a2b3c4d.jpg".</summary>
    public static string KeyFor(string suggestedFileName, DateTime nowUtc)
    {
        var extension = Path.GetExtension(suggestedFileName).ToLowerInvariant();
        var folder = FileContentTypes.IsImage(suggestedFileName) ? ImagesFolder : DocumentsFolder;
        var name = SafeName(Path.GetFileNameWithoutExtension(suggestedFileName));
        return $"{folder}/{(name.Length == 0 ? "file" : name)}-{nowUtc:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}{extension}";
    }

    /// <summary>Letters, digits, dots, underscores and single hyphens only — safe as an S3 key and in a URL.</summary>
    private static string SafeName(string value)
    {
        var builder = new StringBuilder();
        foreach (var c in value.Trim())
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '.' or '_')
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var name = builder.ToString().Trim('-', '.');
        return name.Length > 120 ? name[..120].TrimEnd('-') : name;
    }

    private static bool TryParse(string location, out string bucket, out string key)
    {
        bucket = key = string.Empty;
        if (!location.StartsWith(Scheme, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = location[Scheme.Length..];
        var slash = rest.IndexOf('/');
        if (slash <= 0)
        {
            return false;
        }

        (bucket, key) = (rest[..slash], rest[(slash + 1)..]);
        return true;
    }
}
