namespace BCKash.Infrastructure.Files;

/// <summary>
/// Content types by file extension. Nothing stores a file's content type, so it's derived from the
/// name at save and download time.
/// </summary>
public static class FileContentTypes
{
    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".webp"] = "image/webp",
        [".heic"] = "image/heic",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".csv"] = "text/csv",
        [".txt"] = "text/plain",
    };

    public const string Default = "application/octet-stream";

    public static string For(string? fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        return !string.IsNullOrEmpty(extension) && ByExtension.TryGetValue(extension, out var type) ? type : Default;
    }

    public static bool IsImage(string? fileName) => For(fileName).StartsWith("image/", StringComparison.Ordinal);
}
